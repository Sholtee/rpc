/********************************************************************************
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
    using Proxy;
    using Proxy.Generators;
    
    /// <summary>
    /// Contains the logic related to RPC service invocation
    /// </summary>
    public class RpcClient<TInterface> : InterfaceInterceptor<TInterface> where TInterface : class
    {
        static RpcClient() 
        {
            string cacheDir = Path.Combine(Path.GetTempPath(), ".rpc", typeof(RpcClient<TInterface>).Assembly.GetName().Version.ToString());
            Directory.CreateDirectory(cacheDir);

            ProxyGenerator<TInterface, RpcClient<TInterface>>.CacheDirectory = cacheDir;
        }

        private static string GetMemberId(MemberInfo member) => member.GetCustomAttribute<AliasAttribute>(inherit: false)?.Name ?? member.Name;

        private async Task<object?> InvokeService(MethodInfo method, object[] args)
        {
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

        [SuppressMessage("Reliability", "CA2008:Do not create tasks without passing a TaskScheduler")]
        private static Task<T> Cast<T>(Task<object?> task) => task.ContinueWith(t => Task.FromResult((T) t.Result!)).Unwrap();

        private static Task Cast(Task<object?> task, Type returnType) => (Task) Cache
            .GetOrAdd(returnType, () =>
            {
                MethodInfo cast = ((MethodCallExpression) ((Expression<Action>) (() => Cast<object>(null!))).Body)
                    .Method
                    .GetGenericMethodDefinition();

                return cast.MakeGenericMethod(returnType).ToStaticDelegate();
            })
            .Invoke(new object[] { task } );

        private static void ProcessRemoteError(ExceptionInfo exception) 
        {
            Type exceptionType = Type.GetType(exception.TypeName, throwOnError: false);

            Exception? instance = null;

            if (exceptionType != null)
            {
                Func<object?[], object>? ctor = exceptionType.GetConstructor(new[] { typeof(string) })?.ToStaticDelegate();

                //
                // Ha van string parameteru konstruktora akkor azt hasznaljuk
                //

                if (ctor != null) instance = (Exception) ctor.Invoke(new object?[] { exception.Message });
            }

            if (instance == null) instance = new RpcException(exception.Message);

            throw instance;
        }

        /// <summary>
        /// See <see cref="InterfaceInterceptor{TInterface}.Invoke(MethodInfo, object[], MemberInfo)"/>
        /// </summary>
        public override object? Invoke(MethodInfo method, object[] args, MemberInfo extra)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            Task<object?> getResult = InvokeService(method, args);

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
        /// The session ID related to this instance.
        /// </summary>
        public string? SessionId { get; }

        /// <summary>
        /// The address of the remote host (e.g.: "www.mysite:1986/api").
        /// </summary>
        public string Host { get; }

        internal RpcClient(string host, string? sessionId): base(null)
        {
            Host = host ?? throw new ArgumentNullException(nameof(host));
            SessionId = sessionId;
        }

        /// <summary>
        /// Creates a new <see cref="RpcClient{TInterface}"/> instance.
        /// </summary>
        [SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "<Pending>")]
        public static TInterface Create(string host, string? sessionId) => (TInterface) ProxyGenerator<TInterface, RpcClient<TInterface>>
            .GeneratedType
            .GetConstructor(new Type[] { typeof(string), typeof(string) })
            .ToStaticDelegate()
            .Invoke(new object?[] { host, sessionId });
    }
}
