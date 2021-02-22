/********************************************************************************
* TransactionManager.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Aspects
{
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
        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope")]
        public override object? Invoke(MethodInfo method, object?[] args, MemberInfo extra)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            TransactionalAttribute? ta = method.GetCustomAttribute<TransactionalAttribute>();
            if (ta is null)
                return base.Invoke(method, args, extra);

            return typeof(Task).IsAssignableFrom(method.ReturnType)
                ? InvokeAsync()
                : Invoke();

            Task InvokeAsync()
            {
                IDbTransaction transaction = Connection.BeginTransaction(ta.IsolationLevel);

                //
                // Nem tudjuk h van e es ha igen milyen tipusu a visszateres ezert a ContinueWith()-es varazslas
                //

                Task task = (Task) base.Invoke(method, args, extra)!;
                task.ContinueWith(t => 
                {
                    if (!t.IsCompleted) return; 
         
                    switch (t.Status)
                    {
                        case TaskStatus.RanToCompletion:
                            transaction.Commit();
                            break;
                        case TaskStatus.Canceled:
                        case TaskStatus.Faulted:
                            transaction.Rollback();
                            break;
                        default:
                            Debug.Fail($"Unexpected state {t.Status}");
                            break;
                    }
   
                    transaction.Dispose();
                }, default, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

                return task;
            }

            object? Invoke()
            {
                using IDbTransaction transaction = Connection.BeginTransaction(ta.IsolationLevel);

                try
                {
                    object? result = base.Invoke(method, args, extra);
                    transaction.Commit();
                    return result;
                }
                catch 
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }
    }
}
