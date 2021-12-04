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
        public override object? Invoke(InvocationContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            TransactionalAttribute? ta = context.Method.GetCustomAttribute<TransactionalAttribute>();
            if (ta is null)
                return base.Invoke(context);

            if (typeof(Task).IsAssignableFrom(context.Method.ReturnType)) // Task | Task<>
            {
                Task? task = null;

                return AsyncExtensions.Before(() => task!, context.Method.ReturnType, InvokeInTransactionAsync);

                async Task InvokeInTransactionAsync()
                {
                    using IDbTransaction transaction = Connection.BeginTransaction(ta.IsolationLevel);

                    try
                    {
                        task = (Task) base.Invoke(context)!;

                        await task;
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }

                    transaction.Commit();
                }
            }

            return InvokeInTransaction();

            object? InvokeInTransaction()
            {
                using IDbTransaction transaction = Connection.BeginTransaction(ta.IsolationLevel);

                object? result;

                try
                {
                    result = base.Invoke(context);
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
