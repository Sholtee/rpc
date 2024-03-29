﻿/********************************************************************************
* TransactionManager.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

namespace Solti.Utils.Rpc.Aspects.Tests
{
    using DI;
    using DI.Interfaces;
    using Interfaces;
    using Proxy.Generators;

    [TestFixture]
    public class TransactionManagerTests
    {
        [TransactionAspect]
        public interface IModule
        {
            void NonTransactional();

            [Transactional]
            void DoSomething(object arg);

            [Transactional]
            void DoSomethingFaulty();

            [Transactional]
            Task<int> DoSomethingAsync();
        }

        [Test]
        public void TransactionManager_ShouldCallCommitOnSuccessfulInvocation()
        {
            var mockTransaction = new Mock<IDbTransaction>(MockBehavior.Strict);
            mockTransaction.Setup(tr => tr.Commit());
            mockTransaction.Setup(tr => tr.Dispose());

            var mockDbConnection = new Mock<IDbConnection>(MockBehavior.Strict);
            mockDbConnection
                .Setup(conn => conn.BeginTransaction(IsolationLevel.Unspecified))
                .Returns(mockTransaction.Object);

            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule.Setup(m => m.DoSomething(It.IsAny<object>()));

            Type proxyType = ProxyGenerator<IModule, TransactionManager<IModule>>.GetGeneratedType();

            IModule module = (IModule) Activator.CreateInstance(proxyType, mockModule.Object, new Lazy<IDbConnection>(() => mockDbConnection.Object))!;

            module.DoSomething(new object());

            mockDbConnection.Verify(conn => conn.BeginTransaction(IsolationLevel.Unspecified), Times.Once);
            mockTransaction.Verify(tr => tr.Commit(), Times.Once);
        }

        [Test]
        public async Task TransactionManager_ShouldCallCommitOnSuccessfulAsyncInvocation()
        {
            var mockTransaction = new Mock<IDbTransaction>(MockBehavior.Strict);
            mockTransaction.Setup(tr => tr.Commit());
            mockTransaction.Setup(tr => tr.Dispose());

            var mockDbConnection = new Mock<IDbConnection>(MockBehavior.Strict);
            mockDbConnection
                .Setup(conn => conn.BeginTransaction(IsolationLevel.Unspecified))
                .Returns(mockTransaction.Object);

            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(m => m.DoSomethingAsync())
                .Returns(Task.FromResult(1));

            Type proxyType = ProxyGenerator<IModule, TransactionManager<IModule>>.GetGeneratedType();

            IModule module = (IModule) Activator.CreateInstance(proxyType, mockModule.Object, new Lazy<IDbConnection>(() => mockDbConnection.Object))!;

            int result = await module.DoSomethingAsync();

            Assert.That(result, Is.EqualTo(1));

            mockDbConnection.Verify(conn => conn.BeginTransaction(IsolationLevel.Unspecified), Times.Once);
            mockTransaction.Verify(tr => tr.Commit(), Times.Once);
        }

        [Test]
        public void TransactionManager_ShouldNotCallCommitOnFaultyInvocation()
        {
            var mockTransaction = new Mock<IDbTransaction>(MockBehavior.Strict);
            mockTransaction.Setup(tr => tr.Dispose());
            mockTransaction.Setup(tr => tr.Rollback());

            var mockDbConnection = new Mock<IDbConnection>(MockBehavior.Strict);
            mockDbConnection
                .Setup(conn => conn.BeginTransaction(IsolationLevel.Unspecified))
                .Returns(mockTransaction.Object);

            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(m => m.DoSomethingFaulty())
                .Callback(() => throw new Exception());

            Type proxyType = ProxyGenerator<IModule, TransactionManager<IModule>>.GetGeneratedType();

            IModule module = (IModule) Activator.CreateInstance(proxyType, mockModule.Object, new Lazy<IDbConnection>(() => mockDbConnection.Object))!;

            Assert.Throws<Exception>(module.DoSomethingFaulty);

            mockDbConnection.Verify(conn => conn.BeginTransaction(IsolationLevel.Unspecified), Times.Once);
            mockTransaction.Verify(tr => tr.Commit(), Times.Never);
            mockTransaction.Verify(tr => tr.Rollback(), Times.Once);
        }

        [Test]
        public void TransactionManager_ShouldNotCallCommitOnFaultyAsyncInvocation1()
        {
            var mockTransaction = new Mock<IDbTransaction>(MockBehavior.Strict);
            mockTransaction.Setup(tr => tr.Dispose());
            mockTransaction.Setup(tr => tr.Rollback());

            var mockDbConnection = new Mock<IDbConnection>(MockBehavior.Strict);
            mockDbConnection
                .Setup(conn => conn.BeginTransaction(IsolationLevel.Unspecified))
                .Returns(mockTransaction.Object);

            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(m => m.DoSomethingAsync())
                .ThrowsAsync(new Exception("cica"));

            Type proxyType = ProxyGenerator<IModule, TransactionManager<IModule>>.GetGeneratedType();

            IModule module = (IModule) Activator.CreateInstance(proxyType, mockModule.Object, new Lazy<IDbConnection>(() => mockDbConnection.Object))!;

            Assert.ThrowsAsync<Exception>(module.DoSomethingAsync);

            mockDbConnection.Verify(conn => conn.BeginTransaction(IsolationLevel.Unspecified), Times.Once);
            mockTransaction.Verify(tr => tr.Commit(), Times.Never);
            mockTransaction.Verify(tr => tr.Rollback(), Times.Once);
        }

        [Test]
        public void TransactionManager_ShouldNotCallCommitOnFaultyAsyncInvocation2()
        {
            var mockTransaction = new Mock<IDbTransaction>(MockBehavior.Strict);
            mockTransaction.Setup(tr => tr.Dispose());
            mockTransaction.Setup(tr => tr.Rollback());

            var mockDbConnection = new Mock<IDbConnection>(MockBehavior.Strict);
            mockDbConnection
                .Setup(conn => conn.BeginTransaction(IsolationLevel.Unspecified))
                .Returns(mockTransaction.Object);

            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(m => m.DoSomethingAsync())
                .Throws(new Exception("cica"));

            Type proxyType = ProxyGenerator<IModule, TransactionManager<IModule>>.GetGeneratedType();

            IModule module = (IModule)Activator.CreateInstance(proxyType, mockModule.Object, new Lazy<IDbConnection>(() => mockDbConnection.Object))!;

            Assert.ThrowsAsync<Exception>(module.DoSomethingAsync);

            mockDbConnection.Verify(conn => conn.BeginTransaction(IsolationLevel.Unspecified), Times.Once);
            mockTransaction.Verify(tr => tr.Commit(), Times.Never);
            mockTransaction.Verify(tr => tr.Rollback(), Times.Once);
        }

        [Test]
        public void TransactionManager_ShouldIgnoreMethodsWithoutTransactionalAttribute()
        {
            var mockTransaction = new Mock<IDbTransaction>(MockBehavior.Strict);
            var mockDbConnection = new Mock<IDbConnection>(MockBehavior.Strict);

            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule.Setup(m => m.NonTransactional());

            Type proxyType = ProxyGenerator<IModule, TransactionManager<IModule>>.GetGeneratedType();

            IModule module = (IModule) Activator.CreateInstance(proxyType, mockModule.Object, new Lazy<IDbConnection>(() => mockDbConnection.Object))!;

            module.NonTransactional(); ;

            mockDbConnection.Verify(conn => conn.BeginTransaction(It.IsAny<IsolationLevel>()), Times.Never);
            mockTransaction.Verify(tr => tr.Commit(), Times.Never);
            mockTransaction.Verify(tr => tr.Rollback(), Times.Never);
        }

        [Test]
        public void TransactionAspect_ShouldSpecializeTheTransactionManager()
        {
            var mockModule = new Mock<IModule>(MockBehavior.Strict);

            using IScopeFactory scopeFactory = ScopeFactory.Create(svcs => svcs.Factory(injector => mockModule.Object, Lifetime.Scoped));
            using IInjector injector = scopeFactory.CreateScope();

            IModule module = injector.Get<IModule>();

            Assert.That(module, Is.InstanceOf<TransactionManager<IModule>>());
        }

        [Test]
        public void TransactionManager_ShouldNotCompleteUntilTheTransactionIsDone() 
        {
            bool inTransaction = false;

            var mockTransaction = new Mock<IDbTransaction>(MockBehavior.Strict);
            mockTransaction
                .Setup(tr => tr.Commit())
                .Callback(() => Thread.Sleep(10));
            mockTransaction
                .Setup(tr => tr.Dispose())
                .Callback(() => inTransaction = false);

            var mockDbConnection = new Mock<IDbConnection>(MockBehavior.Strict);
            mockDbConnection
                .Setup(conn => conn.BeginTransaction(IsolationLevel.Unspecified))
                .Returns(() =>
                {
                    inTransaction = true;
                    return mockTransaction.Object;
                });

            var mockModule = new Mock<IModule>(MockBehavior.Strict);
            mockModule
                .Setup(m => m.DoSomethingAsync())
                .Returns(async () => 
                {
                    await Task.Delay(10);
                    return 0;
                });

            Type proxyType = ProxyGenerator<IModule, TransactionManager<IModule>>.GetGeneratedType();

            IModule module = (IModule) Activator.CreateInstance(proxyType, mockModule.Object, new Lazy<IDbConnection>(() => mockDbConnection.Object))!;

            for (int i = 0; i < 100; i++)
            {
                Assert.DoesNotThrowAsync(async () =>
                {
                    await module.DoSomethingAsync();
                    Assert.False(inTransaction);
                });
            }
        }
    }
}
