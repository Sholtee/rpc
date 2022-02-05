/********************************************************************************
* ModuleInvocation.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Internals
{
    using DI.Interfaces;

    using Interfaces;
    using Primitives;
    using Properties;

    /// <summary>
    /// Executes module methods.
    /// </summary>
    public delegate Task<object?> ModuleInvocation(IInjector scope, IRpcRequestContext context); // TBD: context scope-bol?

    /// <summary>
    /// Builds <see cref="ModuleInvocation"/> instances.
    /// </summary>
    public class ModuleInvocationBuilder: IBuilder<ModuleInvocation>
    {
        #region Private
        private static readonly MethodInfo InjectorGet = ((MethodCallExpression) ((Expression<Action<IInjector>>) (i => i.Get(null!, null))).Body).Method;

        private readonly HashSet<Type> FModules = new();

        static MemberExpression GetFromContext<T>(ParameterExpression context, Expression<Func<IRpcRequestContext, T>> ctx) => Expression.Property
        (
            context,
            typeof(IRpcRequestContext).GetProperty(((MemberExpression) ctx.Body).Member.Name)
        );

        //
        // {throw new Exception(...); return null;}
        //

        private static Expression Throw<TException>(Type[] argTypes, params Expression[] args) where TException: Exception => Expression.Block
        (
            typeof(Task<object?>),
            Expression.Throw
            (
                Expression.New
                (
                    typeof(TException).GetConstructor(argTypes) ?? throw new MissingMethodException(typeof(TException).Name, "Ctor"),
                    args
                )
            ),
            Expression.Default(typeof(Task<object?>))
        );

        private Expression CreateSwitch(Expression value, IEnumerable<(MemberInfo Member, Expression Body)> cases, Expression defaultBody)
        {
            return Expression.Switch
            (
                switchValue: value,
                defaultBody,
                comparison: ((Func<string, string, bool>) CompareStr).Method,
                cases: cases.Select
                (
                    @case => Expression.SwitchCase
                    (
                        @case.Body,
                        Expression.Constant(GetMemberId(@case.Member))
                    )
                )


            );

            //
            // Comparer-nek statikusnak kell lennie
            //

            static bool CompareStr(string a, string b) => StringComparer.OrdinalIgnoreCase.Equals(a, b);
        }

        private static async Task<object?> DoInvoke(Task<object?[]> getArgs, Func<object?[], object> invocation, Func<Task, object?> getResult)
        {
            object result = invocation(await getArgs);
            if (result is not Task task)
                return result;

            await task;

            return getResult(task);
        }

        //
        // (scope, ctx) =>
        // {
        //   switch (ctx.Module)
        //   {
        //     case "moduleA":
        //     {
        //       switch (ctx.Method)
        //       {
        //         case "Method_1":
        //            return DoInvoke
        //            (
        //              deserializer(scope, context.Payload, context.Cancellation),
        //              args => ((IModuleA) scope.Get(typeof(IModuleA), null)).Method_1((T0) arg0, (T1) arg1, ...),
        //              task => (object) ((Task<TResult>) task).Result | null
        //            );
        //         ...
        //         default:
        //           throw new MissingMethodException(ctx.Method);
        //       }
        //     }
        //     ...
        //     default:
        //       throw new MissingModuleException(ctx.Module);
        //   }
        // }

        private Expression<ModuleInvocation> BuildExpression(IEnumerable<Type> interfaces) 
        {
            ParameterExpression
                scope   = Expression.Parameter(typeof(IInjector), nameof(scope)),
                context = Expression.Parameter(typeof(IRpcRequestContext), nameof(context));

            MemberExpression
                module = GetFromContext(context, ctx => ctx.Module),
                method = GetFromContext(context, ctx => ctx.Method);

            return Expression.Lambda<ModuleInvocation>
            (
                Expression.Block
                (
                    CreateSwitch
                    (
                        value: module,
                        cases: interfaces.Select
                        (
                            moduleType =>
                            (
                                (MemberInfo) moduleType,
                                (Expression) CreateSwitch
                                (
                                    value: method, 
                                    cases: moduleType
                                        .GetAllInterfaceMethods()
                                        .Where(method => method.GetCustomAttribute<IgnoreAttribute>() is null)
                                        .Select
                                        (
                                            methodType => 
                                            (
                                                (MemberInfo) methodType,
                                                (Expression) InvokeModule(moduleType, methodType)
                                            )
                                        ), 
                                    defaultBody: Throw<MissingMethodException>(new[] { typeof(string), typeof(string) }, module, method)
                                )
                            )
                        ),
                        defaultBody: Throw<MissingModuleException>(new[] { typeof(string) }, module)
                    )
                ),
                scope,
                context
            );

            Expression InvokeModule(Type module, MethodInfo ifaceMethod)
            {
                //
                // A "localScope" valtozo egy workaround mivel (gozom nincs miert) ha switch esetek szama eler egy szamot
                // akkor a ModuleInvocationDelegate osszeallitasakor a fordito elszall InvalidOperationException-el.
                //
                // A "context"-re nem kell ilyen valtozo mivel ot nem akarjuk egy belso lambda-ban is hasznalni.
                //

                ParameterExpression localScope = Expression.Parameter(typeof(IInjector), nameof(localScope));

                //
                // DoInvoke(deserializer(scope, context.Payload, context.Cancellation), args => {...invokeModule...}, task => {...getResult...})
                //

                return Expression.Block
                (
                    new[] { localScope },
                    Expression.Assign(localScope, scope),
                    Expression.Invoke
                    (
                        Expression.Constant((Func<Task<object?[]>, Func<object?[], object>, Func<Task, object?>, Task<object?>>) DoInvoke),
                        BuildDeserializerInvocation(),
                        BuildModuleInvocationDelegate(module, ifaceMethod),
                        BuildGetResultDelegate(ifaceMethod.ReturnType)
                    )
                );

                Expression BuildDeserializerInvocation() => Expression.Invoke
                (
                    Expression.Constant(GetDeserializerFor(ifaceMethod)),
                    localScope,
                    GetFromContext(context, ctx => ctx.Payload),
                    GetFromContext(context, ctx => ctx.Cancellation)
                );

                Expression<Func<object?[], object?>> BuildModuleInvocationDelegate(Type module, MethodInfo ifaceMethod)
                {
                    ParameterExpression args = Expression.Parameter(typeof(object?[]), nameof(args));

                    //
                    // args => ((TInterface) scope.Get(typeof(TInterface), null)).Method((T0) args[0], ..., (TN) args[N])
                    //

                    Expression invokeModule = Expression.Call
                    (
                        //
                        // (TInterface) scope.Get(typeof(TInterface), null)
                        //

                        instance: Expression.Convert
                        (
                            Expression.Call
                            (
                                localScope,
                                InjectorGet,
                                Expression.Constant(module),
                                Expression.Constant(null, typeof(string))
                            ),
                            module
                        ),

                        //
                        // .Method((T0) args[0], ..., (TN) args[N])
                        //

                        ifaceMethod,
                        arguments: ifaceMethod.GetParameters().Select
                        (
                            (para, i) => Expression.Convert
                            (
                                Expression.ArrayAccess(args, Expression.Constant(i)),
                                para.ParameterType
                            )
                        )
                    );

                    if (ifaceMethod.ReturnType != typeof(void))
                        //
                        // return (object) ...;
                        //

                        invokeModule = Expression.Convert(invokeModule, typeof(object));
                    else
                    {
                        //
                        // ...;
                        // return null;
                        //

                        List<Expression> invocationBlock = new();

                        invocationBlock.Add(invokeModule);
                        invocationBlock.Add(Expression.Default(typeof(object)));

                        invokeModule = Expression.Block(invocationBlock);
                    }

                    return Expression.Lambda<Func<object?[], object?>>
                    (
                        Expression.Block(invokeModule),
                        args
                    );
                }

                static LambdaExpression BuildGetResultDelegate(Type returnType) 
                {
                    ParameterExpression task = Expression.Parameter(typeof(Task), nameof(task));

                    BlockExpression block = !typeof(Task).IsAssignableFrom(returnType) || returnType == typeof(Task)
                        //
                        // task => null;
                        //

                        ? Expression.Block(Expression.Default(typeof(object)))

                        //
                        // task => (object) ((Task<TResult>) task).Result;
                        //

                        : Expression.Block
                        (
                            Expression.Convert
                            (
                                Expression.Property
                                (
                                    Expression.Convert(task, returnType),
                                    returnType.GetProperty(nameof(Task<object>.Result))
                                ),
                                typeof(object)
                            )
                        );

                    return Expression.Lambda<Func<Task, object>>(block, task);
                }
            }
        }
        #endregion

        #region Protected
        /// <summary>
        /// Gets the member name to be used in the execution process.
        /// </summary>
        protected virtual string GetMemberId(MemberInfo member)
        {
            if (member is null)
                throw new ArgumentNullException(nameof(member));

            return member.GetId();
        }

        /// <summary>
        /// Gets the deserializer for the given method.
        /// </summary>
        protected virtual Func<IInjector, Stream, CancellationToken, Task<object?[]>> GetDeserializerFor(MethodInfo ifaceMethod)
        {
            if (ifaceMethod is null)
                throw new ArgumentNullException(nameof(ifaceMethod));

            //
            // Idoigenyes ezert NE a visszaadott fuggvenyben (ami tobbszor meghivasra kerul) kerdezzuk le
            //

            Type[] elementTypes = ifaceMethod
                .GetParameters()
                .Select(param => param.ParameterType)
                .ToArray();

            return (injector, stm, cancellation) => injector
                .Get<IJsonSerializer>()
                .DeserializeMultiTypeArrayAsync(elementTypes, stm, cancellation);
        }
        #endregion

        #region Public
        /// <summary>
        /// Adds a module to this builder.
        /// </summary>
        public void AddModule<TInterface>() where TInterface : class => AddModule(typeof(TInterface));

        /// <summary>
        /// Adds a module to this builder.
        /// </summary>
        public void AddModule(Type iface)
        {
            if (iface is null)
                throw new ArgumentNullException(nameof(iface));

            if (!iface.IsInterface)
                throw new ArgumentException(Errors.NOT_AN_INTERFACE, nameof(iface));

            MethodInfo[] methods = iface.GetAllInterfaceMethods().ToArray();

            if (iface.IsGenericTypeDefinition || methods.Any(m => m.IsGenericMethodDefinition))
                throw new ArgumentException(Errors.GENERIC_IFACE, nameof(iface));

            //
            // Modul neve ne "IModule`1" es tarsai legyen
            //

            if (iface.IsGenericType && iface.GetCustomAttribute<AliasAttribute>() is null)
                throw new ArgumentException(Errors.ALIAS_REQUIRED, nameof(iface));

            MethodInfo[] methodsHavingByRefParam = methods
                .Where
                (
                    method => method.ReturnType.IsByRef || method.GetParameters().Any(para => para.ParameterType.IsByRef)
                )
                .ToArray();

            if (methodsHavingByRefParam.Any())
            {
                var ex = new ArgumentException(Errors.BYREF_PARAMETER, nameof(iface));
                ex.Data["methods"] = methodsHavingByRefParam;
                throw ex;
            }

            FModules.Add(iface);
        }

        /// <summary>
        /// Returns the registered modules.
        /// </summary>
        public IReadOnlyCollection<Type> Modules => FModules;

        /// <summary>
        /// Builds a <see cref="ModuleInvocation"/> instance.
        /// </summary>
        public ModuleInvocation Build()
        {
            Expression<ModuleInvocation> moduleInvocation = BuildExpression(FModules);
            Debug.WriteLine($"ModuleInvocation built:{Environment.NewLine}{moduleInvocation.GetDebugView()}");

            ModuleInvocation result = moduleInvocation.Compile();

            //
            // A this.Modules bejegyzesek ertekei valtozhatnak, ezert a ToArray() hivas.
            //

            ModuleInvocationExtensions.RelatedModules.Add(result, FModules.ToArray());
            return result;
        }

        /// <summary>
        /// Empty <see cref="ModuleInvocation"/> delegate.
        /// </summary>
        public static ModuleInvocation EmptyDelegate { get; } = new ModuleInvocationBuilder().Build(); // Nem ide kene de delegate-bol nem lehet leszarmazni
        #endregion
    }
}