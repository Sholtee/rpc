/********************************************************************************
* PropertyValidator.cs                                                          *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

using Moq;
using NUnit.Framework;

namespace Solti.Utils.Rpc.Aspects.Tests
{
    using DI.Interfaces;
    using Interfaces;
    using Interfaces.Properties;
    using Proxy.Generators;

    [TestFixture]
    public class PropertyValidatorTests
    {
        public class MyParameter1
        {
            [NotNull(Condition = typeof(IfNoSession))]
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

        public class IfNoSession : IConditionalValidatior
        {
            public bool ShouldRun(MethodInfo containingMethod, IInjector currentScope) =>
                currentScope.TryGet<IRequestContext>() is null;
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
            mockInjector
                .Setup(i => i.TryGet(typeof(IRequestContext), null))
                .Returns<Type, string>((iface, name) => null);

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
            Assert.That(ex.TargetName, Is.EqualTo(nameof(MyParameter2.Value2)));

            ex = Assert.Throws<ValidationException>(() => module.DoSomething(new MyParameter2
            {
                Value2 = new MyParameter1 
                {
                    Value1 = null
                },
                Value3 = new object()
            }));
            Assert.That(ex.TargetName, Is.EqualTo(nameof(MyParameter1.Value1)));

            ex = Assert.Throws<ValidationException>(() => module.DoSomething(new MyParameter2
            {
                Value2 = new MyParameter1
                {
                    Value1 = new object()
                },
                Value3 = null
            }));
            Assert.That(ex.TargetName, Is.EqualTo(nameof(MyParameter2.Value3)));
        }

        [Test]
        public void AggregatedPropertyValidationTest()
        {
            var mockModule = new Mock<IModule>(MockBehavior.Loose);

            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);
            mockInjector
                .Setup(i => i.TryGet(typeof(IRequestContext), null))
                .Returns<Type, string>((iface, name) => null);

            Type proxyType = ProxyGenerator<IModule, ParameterValidator<IModule>>.GetGeneratedType();

            IModule module = (IModule) Activator.CreateInstance(proxyType, mockModule.Object, mockInjector.Object, true)!;

            Assert.DoesNotThrow(() => module.DoSomethingElse(new MyParameter2 { Value2 = new MyParameter1 { Value1 = new object() }, Value3 = new object() }));
            AggregateException ex = Assert.Throws<AggregateException>(() => module.DoSomethingElse(new MyParameter2()));
            Assert.That(ex.InnerExceptions.Count, Is.EqualTo(2));
            Assert.That(ex.InnerExceptions[0], Is.InstanceOf<ValidationException>());
            Assert.That(((ValidationException) ex.InnerExceptions[0]).TargetName, Is.EqualTo(nameof(MyParameter2.Value2)));
            Assert.That(ex.InnerExceptions[1], Is.InstanceOf<ValidationException>());
            Assert.That(((ValidationException) ex.InnerExceptions[1]).TargetName, Is.EqualTo(nameof(MyParameter2.Value3)));
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

        [Test]
        public void NestedPropertyValidator_MayBeConditional()
        {
            var mockModule = new Mock<IModule>(MockBehavior.Loose);

            IRequestContext context = null;

            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);
            mockInjector
                .Setup(i => i.TryGet(typeof(IRequestContext), null))
                .Returns<Type, string>((iface, name) => context);

            Type proxyType = ProxyGenerator<IModule, ParameterValidator<IModule>>.GetGeneratedType();

            IModule module = (IModule) Activator.CreateInstance(proxyType, mockModule.Object, mockInjector.Object, false);

            Assert.Throws<ValidationException>(() => module.DoSomething(new MyParameter2 { Value3 = new object(), Value2 = new MyParameter1 { Value1 = null } }));

            context = new Mock<IRequestContext>(MockBehavior.Strict).Object;

            Assert.DoesNotThrow(() => module.DoSomething(new MyParameter2 { Value3 = new object(), Value2 = new MyParameter1 { Value1 = null } }));
        }

        [Test]
        public void MatchAttribute_ShouldThrowIfThereIsNoMatch()
        {
            var attr = new MatchAttribute("cica");

            PropertyInfo prop = GetType().GetProperty(nameof(EmptyValues), BindingFlags.Public | BindingFlags.Static);
            Assert.Throws<ValidationException>(() => ((IPropertyValidator)attr).Validate(prop, "mica", null!), Errors.PROPERTY_NOT_MATCHES);
        }

        public static IEnumerable<object> EmptyValues
        {
            get
            {
                yield return string.Empty;
                yield return Array.Empty<int>();
                yield return new List<IDisposable>(0);
            }
        }

        [Test]
        public void NotEmptyAttribute_ShouldThrowOnEmptyValue([ValueSource(nameof(EmptyValues))] object value)
        {
            IPropertyValidator validator = new NotEmptyAttribute();

            PropertyInfo prop = GetType().GetProperty(nameof(EmptyValues), BindingFlags.Public | BindingFlags.Static);
            Assert.Throws<ValidationException>(() => validator.Validate(prop, value, null!), Errors.EMPTY_PROPERTY);
        }

        [Test]
        public void LengthBetween_ShouldThrowIfTheLengthIsNotBetweenTheGivenValues()
        {
            PropertyInfo prop = GetType().GetProperty(nameof(EmptyValues), BindingFlags.Public | BindingFlags.Static);

            IPropertyValidator validator = new LengthBetweenAttribute(min: 1, max: 3);

            Assert.DoesNotThrow(() => validator.Validate(prop, new int[1], null));
            Assert.DoesNotThrow(() => validator.Validate(prop, new int[3], null));

            Assert.Throws<ValidationException>(() => validator.Validate(prop, new int[0], null), Errors.INVALID_PROPERTY_LENGTH);
            Assert.Throws<ValidationException>(() => validator.Validate(prop, new int[4], null), Errors.INVALID_PROPERTY_LENGTH);
        }

        private class NotNull : IAsyncPredicate
        {
            public bool Execute(object value, IInjector currentScope) => value != null;

            public Task<bool> ExecuteAsync(object value, IInjector currentScope) => Task.FromResult(value != null);
        }

        [Test]
        public void MustAttribute_ShouldThrowIfThePredicateReturnsFalse()
        {
            IAsyncPropertyValidator validator = new MustAttribute(typeof(NotNull));

            Assert.DoesNotThrow(() => validator.Validate(GetDummyPropInfo(), new object(), null));
            Assert.DoesNotThrowAsync(() => validator.ValidateAsync(GetDummyPropInfo(), new object(), null));

            Assert.Throws<ValidationException>(() => validator.Validate(GetDummyPropInfo(), null, null), Errors.VALIDATION_FAILED);
            Assert.ThrowsAsync<ValidationException>(() => validator.ValidateAsync(GetDummyPropInfo(), null, null), Errors.VALIDATION_FAILED);

            PropertyInfo GetDummyPropInfo() => typeof(ValidatorAttributeBase).GetProperties()[0];
        }

        public class AsyncNotNullAttribute : Attribute, IAsyncPropertyValidator
        {
            public string PropertyValidationErrorMessage { get; set; }

            public bool SupportsNull => true;

            public bool SupportsAsync => true;

            public void Validate(PropertyInfo prop, object value, IInjector currentScope) => throw new NotImplementedException();

            public Task ValidateAsync(PropertyInfo prop, object value, IInjector currentScope) => value is null
                ? Task.FromException<ValidationException>(new ValidationException(PropertyValidationErrorMessage))
                : Task.CompletedTask;
        }

        public class MyArg 
        {
            [AsyncNotNull]
            public string Value { get; set; }
        }

        public interface IMyAsyncModule
        {
            Task Foo([ValidateProperties] MyArg para);

            Task Bar([ValidateProperties(aggregate: true)] MyArg para);
        }

        [Test]
        public void AsyncPropertyValidationTest()
        {
            var mockModule = new Mock<IMyAsyncModule>(MockBehavior.Loose);

            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);
            mockInjector
                .Setup(i => i.TryGet(typeof(IRequestContext), null))
                .Returns<Type, string>((iface, name) => null);

            Type proxyType = ProxyGenerator<IMyAsyncModule, ParameterValidator<IMyAsyncModule>>.GetGeneratedType();

            IMyAsyncModule module = (IMyAsyncModule) Activator.CreateInstance(proxyType, mockModule.Object, mockInjector.Object)!;

            Assert.DoesNotThrowAsync(() => module.Foo(new MyArg 
            {
                Value = "cica"
            }));

            Assert.ThrowsAsync<ValidationException>(() => module.Foo(new MyArg()));
        }

        [Test]
        public void AsyncAggregatedPropertyValidationTest()
        {
            var mockModule = new Mock<IMyAsyncModule>(MockBehavior.Loose);

            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);
            mockInjector
                .Setup(i => i.TryGet(typeof(IRequestContext), null))
                .Returns<Type, string>((iface, name) => null);

            Type proxyType = ProxyGenerator<IMyAsyncModule, ParameterValidator<IMyAsyncModule>>.GetGeneratedType();

            IMyAsyncModule module = (IMyAsyncModule) Activator.CreateInstance(proxyType, mockModule.Object, mockInjector.Object, true)!;

            Assert.DoesNotThrowAsync(() => module.Foo(new MyArg
            {
                Value = "cica"
            }));

            var ex = Assert.ThrowsAsync<AggregateException>(() => module.Foo(new MyArg()));
            Assert.That(ex.InnerExceptions.Count, Is.EqualTo(1));
            Assert.That(ex.InnerExceptions[0], Is.InstanceOf<ValidationException>());
        }
    }
}
