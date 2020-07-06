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
using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.WebUtilities;

namespace Solti.Utils.Rpc
{   
    using Primitives;
    using Properties;
    using Proxy;
    using Proxy.Generators;
    
    /// <summary>
    /// Contains the logic related to RPC service invocation
    /// </summary>
    public class RpcClient<TInterface> where TInterface: class
    {
        #region Private
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

            public override object? Invoke(MethodInfo method, object[] args, MemberInfo extra) => Owner.InvokeServicy(method, args);
        }

        [SuppressMessage("Reliability", "CA2008:Do not create tasks without passing a TaskScheduler")]
        private static Task<T> Cast<T>(Task<object?> task) => task.ContinueWith(t => Task.FromResult((T) t.Result!)).Unwrap();

        private static Task Cast(Task<object?> task, Type returnType) => (Task) Cache
            .GetOrAdd(returnType, () =>
            {
                MethodInfo cast = ((MethodCallExpression)((Expression<Action>)(() => Cast<object>(null!))).Body)
                    .Method
                    .GetGenericMethodDefinition();

                return cast.MakeGenericMethod(returnType).ToStaticDelegate();
            })
            .Invoke(new object[] { task });

        private TInterface CreateProxy() => (TInterface) ProxyGenerator<TInterface, MethodCallForwarder>
            .GeneratedType
            .GetConstructor(new Type[] { typeof(RpcClient<TInterface>) })
            .ToStaticDelegate()
            .Invoke(new object[] { this });
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
        protected virtual async Task<object?> InvokeServiceAsync(MethodInfo method, object[] args)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            if (args == null)
                throw new ArgumentNullException(nameof(args));

            using var data = new StringContent(JsonSerializer.Serialize(args), Encoding.UTF8, "application/json");

            using var client = new HttpClient();

            var paramz = new Dictionary<string, string>
            {
                { "module", GetMemberId(method.ReflectedType) },
                { "method", GetMemberId(method)}
            };
            if (SessionId != null) paramz.Add("sessionid", SessionId);

            HttpResponseMessage response = await client.PostAsync(QueryHelpers.AddQueryString(Host, paramz), data);
            response.EnsureSuccessStatusCode();

            RpcResponse result = JsonSerializer.Deserialize<RpcResponse>(await response.Content.ReadAsStringAsync());

            if (result.Exception != null) ProcessRemoteError(result.Exception);

            return result.Result;
        }

        /// <summary>
        /// Does the actual remote module invocation.
        /// </summary>
        protected virtual object? InvokeServicy(MethodInfo method, object[] args)
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

                return Cast(getResult, method.ReturnType.GetGenericArguments().Single());
            }
        }

        /// <summary>
        /// Processes the <see cref="ExceptionInfo"/> returned by the remote host.
        /// </summary>
        protected virtual void ProcessRemoteError(ExceptionInfo exception) 
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            Func<object?[], object>? getRemoteException = Type
                .GetType(exception.TypeName, throwOnError: false)
                ?.GetConstructor(new[] { typeof(string) })
                ?.ToStaticDelegate();

            //
            // Ha nem sikerult a tavoli kivetelt betolteni akkor sima Exception-t dobunk
            //

            Exception instance = (Exception?) getRemoteException?.Invoke(new object?[] { exception.Message }) ?? new Exception(exception.Message);

            throw new RpcException(Resources.RPC_FAILED, instance);
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
        /// Creates a new <see cref="RpcClient{TInterface}"/> instance.
        /// </summary>
        public RpcClient(string host, string? sessionId)
        {
            Host = host ?? throw new ArgumentNullException(nameof(host));
            SessionId = sessionId;
            Proxy = CreateProxy();
        }

        /// <summary>
        /// The generated proxy instance related to this client.
        /// </summary>
        public TInterface Proxy { get; }
        #endregion
    }
}
