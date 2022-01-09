/********************************************************************************
* TransactionManager.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Data;
using System.Reflection;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Aspects
{
    using Interfaces;
    using Internals;
    using Proxy;

    /// <summary>
    /// Manages database transactions.
    /// </summary>
    public class TransactionManager<TInterface> : AspectInterceptor<TInterface> where TInterface : class
    {
        private readonly Lazy<IDbConnection> FConnection;

        /// <summary>
        /// The underlying DB connection
        /// </summary>
        public IDbConnection Connection => FConnection.Value;

        /// <summary>
        /// Creates a new <see cref="TransactionManager{TInterface}"/> instance.
        /// </summary>
        public TransactionManager(TInterface target, Lazy<IDbConnection> dbConn) : base(target) =>
            FConnection = dbConn ?? throw new ArgumentNullException(nameof(dbConn));

        /// <inheritdoc/>
        protected override object? Decorator(InvocationContext context, Func<object?> callNext)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            if (callNext is null)
                throw new ArgumentNullException(nameof(callNext));

            TransactionalAttribute? ta = context.Method.GetCustomAttribute<TransactionalAttribute>();
            if (ta is null)
                return callNext();

            using IDbTransaction transaction = Connection.BeginTransaction(ta.IsolationLevel);

            object? result;

            try
            {
                result = callNext();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }

            transaction.Commit();
            return result;
        }

        /// <inheritdoc/>
        protected override async Task<Task> DecoratorAsync(InvocationContext context, Func<Task> callNext)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            if (callNext is null)
                throw new ArgumentNullException(nameof(callNext));

            TransactionalAttribute? ta = context.Method.GetCustomAttribute<TransactionalAttribute>();
            if (ta is null)
                return callNext();

            using IDbTransaction transaction = Connection.BeginTransaction(ta.IsolationLevel);

            Task task;

            try
            {
                task = callNext();
                await task;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }

            transaction.Commit();
            return task;
        }
    }
}
