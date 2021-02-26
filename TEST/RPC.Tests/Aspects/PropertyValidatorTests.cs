/********************************************************************************
* PropertyValidatorTests.cs                                                     *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Reflection;

using Moq;
using NUnit.Framework;

namespace Solti.Utils.Rpc.Aspects.Tests
{
    using DI.Interfaces;
    using Interfaces;
    using Proxy.Generators;

    [TestFixture]
    public class PropertyValidatorTests
    {
        public class MyParameter1
        {
            [NotNull]
            public object Value1 { get; set; }
        }

        public class MyParameter2 
        {
            [NotNull, ValidateProperties]
            public MyParameter1 Value2 { get; set; }

            [NotNull]
            public object Value3 { get; set; }
        }

        public enum MyEnum
        {
            Anonymous = 0,
            LoggedInUser = 1
        }

        public class IfLoggedIn : IConditionalValidatior
        {
            public bool ShouldRun(MethodInfo containingMethod, IInjector currentScope) =>
                currentScope.Get<IRoleManager>().GetAssignedRoles(null).Equals(MyEnum.LoggedInUser);
        }

        public interface IModule
        {
            void DoSomething([ValidateProperties] MyParameter2 arg);
            void DoSomethingElse([ValidateProperties(aggregate: true)] MyParameter2 arg);
            void ConditionallyValidated([ValidateProperties(Condition = typeof(IfLoggedIn))] MyParameter2 arg);
        }

        [Test]
        public void PropertyValidationTest()
        {
            var mockModule = new Mock<IModule>(MockBehavior.Loose);
            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);

            Type proxyType = ProxyGenerator<IModule, ParameterValidator<IModule>>.GetGeneratedType();

            IModule module = (IModule) Activator.CreateInstance(proxyType, mockModule.Object, mockInjector.Object)!;

            Assert.DoesNotThrow(() => module.DoSomething(new MyParameter2 
            {
                Value2 = new MyParameter1 
                {
                    Value1 = new object()
                },
                Value3 = new object()
            }));

            var ex = Assert.Throws<ValidationException>(() => module.DoSomething(new MyParameter2 
            {
                Value2 = null
            }));
            Assert.That(ex.Name, Is.EqualTo(nameof(MyParameter2.Value2)));

            ex = Assert.Throws<ValidationException>(() => module.DoSomething(new MyParameter2
            {
                Value2 = new MyParameter1 
                {
                    Value1 = null
                },
                Value3 = new object()
            }));
            Assert.That(ex.Name, Is.EqualTo(nameof(MyParameter1.Value1)));

            ex = Assert.Throws<ValidationException>(() => module.DoSomething(new MyParameter2
            {
                Value2 = new MyParameter1
                {
                    Value1 = new object()
                },
                Value3 = null
            }));
            Assert.That(ex.Name, Is.EqualTo(nameof(MyParameter2.Value3)));
        }

        [Test]
        public void AggregatedPropertyValidationTest()
        {
            var mockModule = new Mock<IModule>(MockBehavior.Loose);
            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);

            Type proxyType = ProxyGenerator<IModule, ParameterValidator<IModule>>.GetGeneratedType();

            IModule module = (IModule) Activator.CreateInstance(proxyType, mockModule.Object, mockInjector.Object, true)!;

            Assert.DoesNotThrow(() => module.DoSomethingElse(new MyParameter2 { Value2 = new MyParameter1 { Value1 = new object() }, Value3 = new object() }));
            AggregateException ex = Assert.Throws<AggregateException>(() => module.DoSomethingElse(new MyParameter2()));
            Assert.That(ex.InnerExceptions.Count, Is.EqualTo(2));
            Assert.That(ex.InnerExceptions[0], Is.InstanceOf<ValidationException>());
            Assert.That(((ValidationException) ex.InnerExceptions[0]).Name, Is.EqualTo(nameof(MyParameter2.Value2)));
            Assert.That(ex.InnerExceptions[1], Is.InstanceOf<ValidationException>());
            Assert.That(((ValidationException) ex.InnerExceptions[1]).Name, Is.EqualTo(nameof(MyParameter2.Value3)));
        }

        [Test]
        public void PropertyValidator_ShouldHandleNulls()
        {
            var mockModule = new Mock<IModule>(MockBehavior.Loose);
            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);

            Type proxyType = ProxyGenerator<IModule, ParameterValidator<IModule>>.GetGeneratedType();

            IModule module = (IModule) Activator.CreateInstance(proxyType, mockModule.Object, mockInjector.Object)!;

            Assert.DoesNotThrow(() => module.DoSomething(null));
        }

        [Test]
        public void PropertyValidator_MayBeConditional() 
        {
            var mockModule = new Mock<IModule>(MockBehavior.Loose);

            var role = MyEnum.Anonymous;

            var mockRoleManager = new Mock<IRoleManager>(MockBehavior.Strict);
            mockRoleManager
                .Setup(rm => rm.GetAssignedRoles(null))
                .Returns<string>(sessionId => role);

            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);
            mockInjector
                .Setup(i => i.Get(typeof(IRoleManager), null))
                .Returns(mockRoleManager.Object);

            Type proxyType = ProxyGenerator<IModule, ParameterValidator<IModule>>.GetGeneratedType();

            IModule module = (IModule) Activator.CreateInstance(proxyType, mockModule.Object, mockInjector.Object, false);

            Assert.DoesNotThrow(() => module.ConditionallyValidated(new MyParameter2()));

            role = MyEnum.LoggedInUser;

            Assert.Throws<ValidationException>(() => module.ConditionallyValidated(new MyParameter2()));
        }
    }
}
