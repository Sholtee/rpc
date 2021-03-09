/********************************************************************************
* ParameterValidatorTests.cs                                                    *
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
    using DI;
    using DI.Interfaces;
    using Interfaces;
    using Interfaces.Properties;
    using Proxy.Generators;

    [TestFixture]
    public class ParameterValidatorTests
    {
        [ParameterValidatorAspect]
        public interface IModule
        {
            void DoSomething([NotNull, Match("cica", ParameterValidationErrorMessage = "ooops")] string arg1, [NotNull] object arg2);
            void DoSomethingElse();
            void ConditionallyValidated([NotNull(Condition = typeof(IfLoggedIn))] string arg);
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

        [Test]
        public void ParameterValidationTest()
        {
            var mockModule = new Mock<IModule>(MockBehavior.Loose);
            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);

            Type proxyType = ProxyGenerator<IModule, ParameterValidator<IModule>>.GetGeneratedType();

            IModule module = (IModule) Activator.CreateInstance(proxyType, mockModule.Object, mockInjector.Object, false)!;

            Assert.DoesNotThrow(() => module.DoSomething("cica", 1));
            var ex = Assert.Throws<ValidationException>(() => module.DoSomething(null, null));
            Assert.That(ex.Name, Is.EqualTo("arg1"));
            Assert.That(ex.Message, Is.EqualTo(Errors.NULL_PARAM));
        }

        [Test]
        public void AggregatedParameterValidationTest()
        {
            var mockModule = new Mock<IModule>(MockBehavior.Loose);
            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);

            Type proxyType = ProxyGenerator<IModule, ParameterValidator<IModule>>.GetGeneratedType();

            IModule module = (IModule) Activator.CreateInstance(proxyType, mockModule.Object, mockInjector.Object, true)!;

            Assert.DoesNotThrow(() => module.DoSomething("cica", 1));
            AggregateException ex = Assert.Throws<AggregateException>(() => module.DoSomething("kutya", null));
            Assert.That(ex.InnerExceptions.Count, Is.EqualTo(2));
            Assert.That(ex.InnerExceptions[0], Is.InstanceOf<ValidationException>());
            Assert.That(((ValidationException) ex.InnerExceptions[0]).Name, Is.EqualTo("arg1"));
            Assert.That(((ValidationException) ex.InnerExceptions[0]).Message, Is.EqualTo("ooops"));
            Assert.That(ex.InnerExceptions[1], Is.InstanceOf<ValidationException>());
            Assert.That(((ValidationException) ex.InnerExceptions[1]).Name, Is.EqualTo("arg2"));
        }

        [Test]
        public void ParameterValidatorAspect_ShouldSpecializeTheParameterValidator()
        {
            var mockModule = new Mock<IModule>(MockBehavior.Strict);

            using (IServiceContainer container = new ServiceContainer())
            {
                container.Factory(injector => mockModule.Object, Lifetime.Scoped);

                IInjector injector = container.CreateInjector();
                IModule module = injector.Get<IModule>();

                Assert.That(module, Is.InstanceOf<ParameterValidator<IModule>>());
            }
        }

        [Test]
        public void Validator_MayBeConditional()
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

            IModule module = (IModule)Activator.CreateInstance(proxyType, mockModule.Object, mockInjector.Object, false);

            Assert.DoesNotThrow(() => module.ConditionallyValidated(null));

            role = MyEnum.LoggedInUser;

            Assert.Throws<ValidationException>(() => module.ConditionallyValidated(null));
        }

        [Test]
        public void MatchAttribute_ShouldThrowIfThereIsNoMatch()
        {
            var attr = new MatchAttribute("cica");

            Assert.Throws<ValidationException>(() => ((IParameterValidator) attr).Validate(GetDummyParamInfo(), "mica", null!), Errors.PARAM_NOT_MATCHES);

            ParameterInfo GetDummyParamInfo() => ((MethodInfo)MethodBase.GetCurrentMethod()).ReturnParameter;
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
            var attr = new NotEmptyAttribute();

            ParameterInfo param = MethodBase.GetCurrentMethod().GetParameters()[0];
            Assert.Throws<ValidationException>(() => ((IParameterValidator) attr).Validate(param, value, null!), Errors.EMPTY_PARAM);
        }

        private class NotNull : IAsyncPredicate
        {
            public bool Execute(object value, IInjector currentScope) => value != null;

            public Task<bool> ExecuteAsync(object value, IInjector currentScope) => Task.FromResult(value != null);
        }

        [Test]
        public void MustAttribute_ShouldThrowIfThePredicateReturnsFalse()
        {
            IAsyncParameterValidator validator = new MustAttribute(typeof(NotNull));

            Assert.DoesNotThrow(() => validator.Validate(GetDummyParamInfo(), new object(), null));
            Assert.DoesNotThrowAsync(() => validator.ValidateAsync(GetDummyParamInfo(), new object(), null));

            Assert.Throws<ValidationException>(() => validator.Validate(GetDummyParamInfo(), null, null), Errors.VALIDATION_FAILED);
            Assert.ThrowsAsync<ValidationException>(() => validator.ValidateAsync(GetDummyParamInfo(), null, null), Errors.VALIDATION_FAILED);

            
        }

        [Test]
        public void LengthBetween_ShouldThrowIfTheLengthIsNotBetweenTheGivenValues() 
        {
            IParameterValidator validator = new LengthBetweenAttribute(min: 1, max: 3);

            Assert.DoesNotThrow(() => validator.Validate(GetDummyParamInfo(), new int[1], null));
            Assert.DoesNotThrow(() => validator.Validate(GetDummyParamInfo(), new int[3], null));

            Assert.Throws<ValidationException>(() => validator.Validate(GetDummyParamInfo(), new int[0], null), Errors.INVALID_PARAM_LENGTH);
            Assert.Throws<ValidationException>(() => validator.Validate(GetDummyParamInfo(), new int[4], null), Errors.INVALID_PARAM_LENGTH);
        }

        private static ParameterInfo GetDummyParamInfo() => ((MethodInfo) MethodBase.GetCurrentMethod()).ReturnParameter;

        public class AsyncNotNullAttribute : Attribute, IAsyncParameterValidator
        {
            public string ParameterValidationErrorMessage { get; set; }

            public bool SupportsNull => true;

            public bool SupportsAsync => true;

            public void Validate(ParameterInfo param, object value, IInjector currentScope) => throw new NotImplementedException();

            public Task ValidateAsync(ParameterInfo param, object value, IInjector currentScope) => value is null
                ? Task.FromException<ValidationException>(new ValidationException(ParameterValidationErrorMessage))
                : Task.CompletedTask;
        }

        public interface IMyAsyncModule 
        {
            Task Foo([AsyncNotNull(ParameterValidationErrorMessage = "ooops")] string para);
        }

        [Test]
        public void AsyncParameterValidationTest()
        {
            var mockModule = new Mock<IMyAsyncModule>(MockBehavior.Strict);
            mockModule
                .Setup(m => m.Foo(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);

            Type proxyType = ProxyGenerator<IMyAsyncModule, ParameterValidator<IMyAsyncModule>>.GetGeneratedType();

            IMyAsyncModule module = (IMyAsyncModule) Activator.CreateInstance(proxyType, mockModule.Object, mockInjector.Object, false)!;

            Assert.DoesNotThrowAsync(() => module.Foo("cica"));
            var ex = Assert.ThrowsAsync<ValidationException>(() => module.Foo(null));
            Assert.That(ex.Message, Is.EqualTo("ooops"));
        }

        [Test]
        public void AsyncAggregatedParameterValidationTest()
        {
            var mockModule = new Mock<IMyAsyncModule>(MockBehavior.Strict);
            mockModule
                .Setup(m => m.Foo(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var mockInjector = new Mock<IInjector>(MockBehavior.Strict);

            Type proxyType = ProxyGenerator<IMyAsyncModule, ParameterValidator<IMyAsyncModule>>.GetGeneratedType();

            IMyAsyncModule module = (IMyAsyncModule) Activator.CreateInstance(proxyType, mockModule.Object, mockInjector.Object, true)!;

            Assert.DoesNotThrowAsync(() => module.Foo("cica"));
            AggregateException ex = Assert.ThrowsAsync<AggregateException>(() => module.Foo(null));
            Assert.That(ex.InnerExceptions.Count, Is.EqualTo(1));
            Assert.That(ex.InnerExceptions[0], Is.InstanceOf<ValidationException>());
            Assert.That(((ValidationException)ex.InnerExceptions[0]).Message, Is.EqualTo("ooops"));
        }
    }
}
