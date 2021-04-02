/********************************************************************************
* TransactionManager.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Aspects
{
    using Interfaces;
    using Primitives;
    using Proxy;

    /// <summary>
    /// Manages database transactions.
    /// </summary>
    public class TransactionManager<TInterface> : InterfaceInterceptor<TInterface> where TInterface : class
    {
        private readonly Lazy<IDbConnection> FConnection;

        /// <summary>
        /// The underlying DB connection
        /// </summary>
        public IDbConnection Connection => FConnection.Value;

        /// <summary>
        /// Creates a new <see cref="TransactionManager{TInterface}"/> instance.
        /// </summary>
        public TransactionManager(TInterface target, Lazy<IDbConnection> dbConn) : base(target ?? throw new ArgumentNullException(nameof(target))) =>
            FConnection = dbConn ?? throw new ArgumentNullException(nameof(dbConn));

        /// <inheritdoc/>
        public override object? Invoke(MethodInfo method, object?[] args, MemberInfo extra)
        {
            if (method is null)
                throw new ArgumentNullException(nameof(method));

            TransactionalAttribute? ta = method.GetCustomAttribute<TransactionalAttribute>();
            if (ta is null)
                return base.Invoke(method, args, extra);

            if (typeof(Task) == method.ReturnType)
            {
                return InvokeAsync();

                async Task InvokeAsync()
                {
                    using IDbTransaction transaction = Connection.BeginTransaction(ta.IsolationLevel);

                    try
                    {
                        await (Task) base.Invoke(method, args, extra)!;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }

                    transaction.Commit();
                }
            }

            if (typeof(Task).IsAssignableFrom(method.ReturnType)) // Task<>
            {
                Func<Task<object>> invokeAsync = InvokeAsync<object>;

                return (Task) invokeAsync
                    .Method
                    .GetGenericMethodDefinition()
                    .MakeGenericMethod(method.ReturnType.GetGenericArguments().Single())
                    .ToInstanceDelegate()
                    .Invoke(invokeAsync.Target, Array.Empty<object?>());

                async Task<T> InvokeAsync<T>()
                {
                    using IDbTransaction transaction = Connection.BeginTransaction(ta.IsolationLevel);

                    T result;

                    try
                    {
                        result = await (Task<T>) base.Invoke(method, args, extra)!;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }

                    transaction.Commit();
                    return result;
                }
            }

            return Invoke();

            object? Invoke()
            {
                using IDbTransaction transaction = Connection.BeginTransaction(ta.IsolationLevel);

                object? result;

                try
                {
                    result = base.Invoke(method, args, extra);
                }
                catch 
                {
                    transaction.Rollback();
                    throw;
                }

                transaction.Commit();
                return result;
            }
        }
    }
}
