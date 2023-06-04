/********************************************************************************
* TransactionManager.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Data;
using System.Reflection;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Internals
{
    using DI.Interfaces;
    using Interfaces;

    internal sealed class TransactionManager: AspectInterceptor
    {
        private readonly ILazy<IDbConnection> FConnection;

        /// <summary>
        /// The underlying DB connection
        /// </summary>
        public IDbConnection Connection => FConnection.Value;

        public TransactionManager(ILazy<IDbConnection> dbConn) =>
            FConnection = dbConn ?? throw new ArgumentNullException(nameof(dbConn));

        /// <inheritdoc/>
        protected override object? Decorator(IInvocationContext context, Next<IInvocationContext, object?> callNext)
        {
            TransactionalAttribute? ta = context.TargetMethod.GetCustomAttribute<TransactionalAttribute>();
            if (ta is null)
                return callNext(context);

            using IDbTransaction transaction = Connection.BeginTransaction(ta.IsolationLevel);

            object? result;

            try
            {
                result = callNext(context);
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
        protected override async Task<object?> DecoratorAsync(IInvocationContext context, Next<IInvocationContext, Task<object?>> callNext)
        {
            TransactionalAttribute? ta = context.TargetMethod.GetCustomAttribute<TransactionalAttribute>();
            if (ta is null)
                return await callNext(context);

            using IDbTransaction transaction = Connection.BeginTransaction(ta.IsolationLevel);

            object? result;

            try
            {
                result = await callNext(context);
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
