﻿/********************************************************************************
* RpcClientFactory.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.AspNetCore.WebUtilities;

#pragma warning disable CA2201 // Do not raise reserved exception types

namespace Solti.Utils.Rpc
{
    using Interfaces;
    using Internals;
    using Primitives;
    using Primitives.Patterns;
    using Primitives.Threading;
    using Properties;
    using Proxy;
    using Proxy.Generators;
    
    /// <summary>
    /// Creates remote API connections.
    /// </summary>
    /// <remarks>This class and also the created proxy objects are not thread safe.</remarks>
    public class RpcClientFactory: Disposable
    {
        #region Private
        private readonly HttpClient FHttpClient;

        /// <summary>
        /// Represents the underlying proxy.
        /// </summary>
        /// <remarks>This is an internal class, you should never use it.</remarks>
        [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "ProxyGen.NET requires types to be visible.")]
        public class MethodCallForwarder<TInterface> : InterfaceInterceptor<TInterface> where TInterface : class
        {
            /// <summary>
            /// The owner of this entity.
            /// </summary>
            public RpcClientFactory Owner { get; }

            /// <summary>
            /// Creates a new <see cref="MethodCallForwarder{TInterface}"/> instance.
            /// </summary>
            public MethodCallForwarder(RpcClientFactory owner) : base(null) => Owner = owner ?? throw new ArgumentNullException(nameof(owner));

            /// <summary>
            /// Forwards the intercepted method calls to the <see cref="Owner"/>.
            /// </summary>
            public override object? Invoke(InvocationContext context)
            {
                if (context is null)
                    throw new ArgumentNullException(nameof(context));

                return Owner.InvokeService(context.Method, context.Args);
            }
        }

        private static Type GenerateTypedResponseTo(Type returnType) => Cache.GetOrAdd(returnType, () =>
        {
            if (returnType == typeof(void) || returnType == typeof(Task))
                returnType = typeof(object);
            else if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
                returnType = returnType.GetGenericArguments().Single();

            Type responseType = returnType.IsValueType
                ? typeof(ValueRpcResponse<>)
                : typeof(ReferenceRpcResponse<>);

            return responseType.MakeGenericType(returnType);
        });

        private async Task<Version> GetServiceVersion() => await (await CreateClient<IServiceDescriptor>()).Version;
        #endregion

        #region Protected
        /// <summary>
        /// Gets the ID of a member.
        /// </summary>
        protected virtual string GetMemberId(MemberInfo member)
        {
            if (member is null)
                throw new ArgumentNullException(nameof(member));

            return member.GetId();
        }

        /// <summary>
        /// Gets the request parameters.
        /// </summary>
        protected virtual IDictionary<string, string> GetRequestParameters(MethodInfo method) 
        {
            if (method is null)
                throw new ArgumentNullException(nameof(method));

            var paramz = new Dictionary<string, string>
            {
                { "module", GetMemberId(method.ReflectedType) },
                { "method", GetMemberId(method)}
            };

            if (SessionId != null) paramz.Add("sessionid", SessionId);

            return paramz;
        }

        /// <summary>
        /// Does the actual remote module invocation.
        /// </summary>
        [SuppressMessage("Globalization", "CA1304:Specify CultureInfo")]
        protected virtual async Task<object?> InvokeServiceAsync(MethodInfo method, object?[] args)
        {
            if (method is null)
                throw new ArgumentNullException(nameof(method));

            if (args is null)
                throw new ArgumentNullException(nameof(args));

            //
            // Egyedi fejlecek felvetele
            //

            FHttpClient.DefaultRequestHeaders.Clear();

            foreach (KeyValuePair<string, string> header in CustomHeaders)
            {
                FHttpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
            }

            //
            // POST
            //

            HttpResponseMessage response;

            using (var stm = new MemoryStream())
            {
                await JsonSerializer.SerializeAsync(stm, args, SerializerOptions);
                stm.Seek(0, SeekOrigin.Begin);

                using var data = new StreamContent(stm);
                data.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                data.Headers.ContentEncoding.Add("utf-8");

                response = await FHttpClient.PostAsync
                (
                    QueryHelpers.AddQueryString(Host, GetRequestParameters(method)),
                    data
                );
            }

            //
            // Status megfelelo?
            //

            response.EnsureSuccessStatusCode();

            //
            // Eredmeny feldolgozas.
            //

            switch (response.Content.Headers.ContentType?.MediaType) 
            {
                case "application/json":
                    using (Stream stm = await response.Content.ReadAsStreamAsync())
                    {
                        IRpcResonse? result =  (IRpcResonse?) await JsonSerializer.DeserializeAsync
                        (
                            stm,
                            GenerateTypedResponseTo(method.ReturnType),
                            SerializerOptions
                        );

                        if (result is null)
                            throw new RpcException(Resources.MALFORMED_CONTENT);

                        if (result.Exception is not null)
                            ProcessRemoteError(result.Exception);

                        return result.Result;
                    }
                case "application/octet-stream":
                    return await response.Content.ReadAsStreamAsync();
                default:
                    throw new RpcException(Resources.CONTENT_TYPE_NOT_SUPPORTED);
            }
        }

        /// <summary>
        /// Does the actual remote module invocation.
        /// </summary>
        protected virtual object? InvokeService(MethodInfo method, object?[] args)
        {
            if (method is null)
                throw new ArgumentNullException(nameof(method));

            Task<object?> getResult = InvokeServiceAsync(method, args);

            if (!typeof(Task).IsAssignableFrom(method.ReturnType))
                //
                // Ha a metodus visszaterese nem Task akkor meg kell varni az eredmenyt.
                //
                // A "GetAwaiter().GetResult()" miatt kivetel eseten nem AggregateException-t kapunk vissza.
                //

                return getResult.GetAwaiter().GetResult();
            else
            {
                //
                // Ha a metodus visszaterese Task akkor nincs dolgunk (Task<object?> maga is Task).
                //

                if (!method.ReturnType.IsGenericType) return getResult;

                //
                // Kulonben a konvertaljuk a metodus visszateresenek megfelelo formara: Task<object?> -> Task<int> pl
                //

                return getResult.Cast
                (
                    method.ReturnType.GetGenericArguments().Single()
                );
            }
        }

        /// <summary>
        /// Processes the <see cref="ExceptionInfo"/> returned by the remote host.
        /// </summary>
        protected virtual void ProcessRemoteError(ExceptionInfo info) 
        {
            if (info is null)
                throw new ArgumentNullException(nameof(info));

            Type? remoteException = Type.GetType(info.TypeName, throwOnError: false);

            Exception? ex = null;

            if (remoteException is not null && typeof(Exception).IsAssignableFrom(remoteException))
            {
                Func<object?[], object>? ctor = remoteException
                    .GetConstructor(new[] { typeof(string) })
                    ?.ToStaticDelegate();

                if (ctor is not null)
                    ex = (Exception) ctor(new object?[] { info.Message });
            }

            ex ??= new Exception(info.Message);

            foreach (KeyValuePair<string, string> kvp in info.Data)
            {
                ex.Data[kvp.Key] = kvp.Value;
            }

            throw new RpcException(Resources.RPC_FAILED, ex);
        }

        /// <summary>
        /// See <see cref="IDisposable.Dispose"/>.
        /// </summary>
        protected override void Dispose(bool disposeManaged)
        {
            if (disposeManaged) FHttpClient.Dispose();

            base.Dispose(disposeManaged);
        }
        #endregion

        #region Public
        /// <summary>
        /// The (optional) session ID related to this instance.
        /// </summary>
        public string? SessionId { get; set; }

        /// <summary>
        /// The address of the remote host (e.g.: "www.mysite.com:1986/api").
        /// </summary>
        public string Host { get; }

        /// <summary>
        /// Represents the request timeout.
        /// </summary>
        public TimeSpan Timeout 
        {
            get => FHttpClient.Timeout;
            set => FHttpClient.Timeout = value;
        }

        /// <summary>
        /// Contains the options for the underlying <see cref="JsonSerializer"/>.
        /// </summary>
        /// <remarks>These options will be applied to serialization and deserialization as well.</remarks>
        public JsonSerializerOptions SerializerOptions { get; internal set; /*tesztekhez*/ } = new JsonSerializerOptions();

        /// <summary>
        /// The <see cref="Version"/> of the remote service we want to invoke.
        /// </summary>
        public Task<Version> ServiceVersion => GetServiceVersion();

        /// <summary>
        /// Headers sent along with each request.
        /// </summary>
        /// <remarks>You should not set "content-type", it is done by the system automatically.</remarks>
        public IDictionary<string, string> CustomHeaders { get; } = new Dictionary<string, string>();

        /// <summary>
        /// Creates a new <see cref="RpcClientFactory"/> instance.
        /// </summary>
        public RpcClientFactory(string host)
        {
            Host        = host ?? throw new ArgumentNullException(nameof(host));
            FHttpClient = new HttpClient();
            Timeout     = TimeSpan.FromSeconds(10);
        }

        /// <summary>
        /// Creates a new RPC client against the given service <typeparamref name="TInterface"/>.
        /// </summary>
        public async Task<TInterface> CreateClient<TInterface>() where TInterface : class => (TInterface) (await ProxyGenerator<TInterface, MethodCallForwarder<TInterface>>.GetGeneratedTypeAsync())
            .GetConstructor(new Type[] { typeof(RpcClientFactory) })
            .ToStaticDelegate()
            .Invoke(new object[] { this });
        #endregion
    }
}
