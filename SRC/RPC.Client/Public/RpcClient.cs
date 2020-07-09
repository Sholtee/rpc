﻿/********************************************************************************
* RpcClient.cs                                                                  *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.WebUtilities;

[assembly: InternalsVisibleTo("Solti.Utils.Rpc.RpcClient<Solti.Utils.Rpc.Tests.RpcTests.IModule>.MethodCallForwarder_Solti.Utils.Rpc.Tests.RpcTests.IModule_Proxy")]

namespace Solti.Utils.Rpc
{   
    using Primitives;
    using Primitives.Patterns;
    using Primitives.Threading;
    using Properties;
    using Proxy;
    using Proxy.Generators;
    
    /// <summary>
    /// Contains the logic related to RPC service invocation
    /// </summary>
    public class RpcClient<TInterface>: Disposable where TInterface: class
    {
        #region Private
        private readonly HttpClient FHttpClient;

        static RpcClient() 
        {
            string cacheDir = Path.Combine(Path.GetTempPath(), $".rpcclient", typeof(RpcClient<TInterface>).Assembly.GetName().Version.ToString());
            Directory.CreateDirectory(cacheDir);

            ProxyGenerator<TInterface, MethodCallForwarder>.CacheDirectory = cacheDir;
        }

        [SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes")]
        internal class MethodCallForwarder : InterfaceInterceptor<TInterface>
        {
            public RpcClient<TInterface> Owner { get; }

            public MethodCallForwarder(RpcClient<TInterface> owner) : base(null) => Owner = owner;

            public override object? Invoke(MethodInfo method, object[] args, MemberInfo extra) => Owner.InvokeService(method, args);
        }

        private TInterface CreateProxy() => (TInterface) ProxyGenerator<TInterface, MethodCallForwarder>
            .GeneratedType
            .GetConstructor(new Type[] { typeof(RpcClient<TInterface>) })
            .ToStaticDelegate()
            .Invoke(new object[] { this });

        private static Type GenerateTypeResponseTo(MethodInfo method) 
        {
            Type returnType = method.ReturnType;

            if (returnType == typeof(void) || returnType == typeof(Task))
                returnType = typeof(object);
            else if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
                returnType = returnType.GetGenericArguments().Single();

            return typeof(TypedRpcResponse<>).MakeGenericType(returnType);
        }
        #endregion

        #region Protected
        /// <summary>
        /// Gets the ID of a member.
        /// </summary>
        protected virtual string GetMemberId(MemberInfo member)
        {
            if (member == null)
                throw new ArgumentNullException(nameof(member));

            return member.GetCustomAttribute<AliasAttribute>(inherit: false)?.Name ?? member.Name;
        }


        /// <summary>
        /// Does the actual remote module invocation.
        /// </summary>
        [SuppressMessage("Globalization", "CA1304:Specify CultureInfo")]
        protected virtual async Task<object?> InvokeServiceAsync(MethodInfo method, object[] args)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            if (args == null)
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
            // Parmeter az API hivashoz.
            //

            var paramz = new Dictionary<string, string>
            {
                { "module", GetMemberId(method.ReflectedType) },
                { "method", GetMemberId(method)}
            };
            if (SessionId != null) paramz.Add("sessionid", SessionId);

            //
            // POST
            //

            HttpResponseMessage response;

            using (var data = new StringContent(JsonSerializer.Serialize(args), Encoding.UTF8, "application/json"))
            {
                response = await FHttpClient.PostAsync(QueryHelpers.AddQueryString(Host, paramz), data);
            }

            //
            // Eredmeny feldolgozas.
            //

            switch (response.Content.Headers.ContentType.MediaType) 
            {
                case "application/json":
                    IRpcResonse result = (IRpcResonse) JsonSerializer.Deserialize
                    (
                        await response.Content.ReadAsStringAsync(),
                        GenerateTypeResponseTo(method)
                    );
                    if (result.Exception != null) ProcessRemoteError(result.Exception);
                    return result.Result;
                case "application/octet-stream":
                    return await response.Content.ReadAsStreamAsync();
                default: 
                    throw new NotSupportedException(Resources.CONTENT_TYPE_NOT_SUPPORTED);
            }
        }

        /// <summary>
        /// Does the actual remote module invocation.
        /// </summary>
        protected virtual object? InvokeService(MethodInfo method, object[] args)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            Task<object?> getResult = InvokeServiceAsync(method, args);

            if (!typeof(Task).IsAssignableFrom(method.ReturnType))
                //
                // A "GetAwaiter().GetResult()" miatt kivetel eseten nem AggregateException-t kapunk vissza,
                // hanem az eredeti kivetelt.
                //

                return getResult.GetAwaiter().GetResult();
            else
            {
                if (method.ReturnType == typeof(Task)) return getResult;

                return getResult.Cast
                (
                    method.ReturnType.GetGenericArguments().Single()
                );
            }
        }

        /// <summary>
        /// Processes the <see cref="ExceptionInfo"/> returned by the remote host.
        /// </summary>
        protected virtual void ProcessRemoteError(ExceptionInfo exception) 
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            Type? remoteException = Type.GetType(exception.TypeName, throwOnError: false);
            
            if (remoteException != null && typeof(Exception).IsAssignableFrom(remoteException))
            {
                Func<object?[], object>? ctor = remoteException
                    .GetConstructor(new[] { typeof(string) })
                    ?.ToStaticDelegate();

                if (ctor != null) 
                    throw new RpcException(Resources.RPC_FAILED, (Exception) ctor(new object?[] { exception.Message }));
            }

            throw new RpcException(Resources.RPC_FAILED, new Exception(exception.Message));
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
        /// The session ID related to this instance.
        /// </summary>
        public string? SessionId { get; }

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
        /// Custom headers
        /// </summary>
        public IDictionary<string, string> CustomHeaders { get; } = new Dictionary<string, string>();

        /// <summary>
        /// Creates a new <see cref="RpcClient{TInterface}"/> instance.
        /// </summary>
        public RpcClient(string host, string? sessionId = null)
        {
            Host        = host ?? throw new ArgumentNullException(nameof(host));
            SessionId   = sessionId;
            FHttpClient = new HttpClient();
            Timeout     = TimeSpan.FromSeconds(10);
            Proxy       = CreateProxy();
        }

        /// <summary>
        /// The generated proxy instance related to this client.
        /// </summary>
        public TInterface Proxy { get; }
        #endregion
    }
}
