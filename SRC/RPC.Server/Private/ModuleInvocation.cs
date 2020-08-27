/********************************************************************************
* ModuleInvocation.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
    /// Executes module methods by context.
    /// </summary>
    /// <param name="injector">The <see cref="IInjector"/> in which the module was registered.</param>
    /// <param name="context">The context which describes the invocation.</param>
    public delegate Task<object?> ModuleInvocation(IInjector injector, IRequestContext context);

    /// <summary>
    /// Defines some extensions to the <see cref="ModuleInvocation"/> delegate.
    /// </summary>
    public static class ModuleInvocationExtensions // "delegate"-bol nem szarmazhatunk ezert ez a megoldas
    {
        internal static IDictionary<ModuleInvocation, IReadOnlyList<Type>> RelatedModules { get; } = new ConcurrentDictionary<ModuleInvocation, IReadOnlyList<Type>>();

        /// <summary>
        /// Gets the registered modules related to this <see cref="ModuleInvocation"/> instance.
        /// </summary>
        public static IReadOnlyList<Type> GetRelatedModules(this ModuleInvocation src) => RelatedModules[src ?? throw new ArgumentNullException(nameof(src))];
    }

    /// <summary>
    /// Builds <see cref="ModuleInvocation"/> instances.
    /// </summary>
    public class ModuleInvocationBuilder
    {
        #region Private
        private static readonly MethodInfo InjectorGet = ((MethodCallExpression) ((Expression<Action<IInjector>>) (i => i.Get(null!, null))).Body).Method;

        private readonly HashSet<Type> FModules = new HashSet<Type>();

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

        private Expression CreateSwitch(Expression value, IEnumerable<(MemberInfo Member, Expression Body)> cases, Expression defaultBody) => Expression.Switch
        (
            switchValue: value,
            defaultBody,
            comparison: null, // default
            cases: cases.Select
            (
                @case => Expression.SwitchCase
                (
                    @case.Body,
                    Expression.Constant(GetMemberId(@case.Member))
                )
            )
        );

        private static async Task<object?> DoInvoke(Task<object?[]> getArgs, Func<object?[], object> invocation)
        {
            object result = invocation(await getArgs);

            if (result is Task task)
            {
                await task;

                Type taskType = task.GetType();

                return taskType.IsGenericType
                    ? taskType.GetProperty(nameof(Task<object?>.Result)).ToGetter().Invoke(task)
                    : null;
            }

            return result;
        }

        internal static IEnumerable<MethodInfo> GetAllInterfaceMethods(Type iface) =>
            //
            // A "BindingFlags.FlattenHierarchy" interface-ekre nem mukodik
            //

            iface
                .GetMethods(BindingFlags.Instance | BindingFlags.Public /* | BindingFlags.FlattenHierarchy*/)
                .Concat
                (
                    iface.GetInterfaces().SelectMany(GetAllInterfaceMethods)
                )

                //
                // IIface: IA, IB ahol IA: IC es IB: IC -> Distinct()
                //

                .Distinct();

        private Expression<ModuleInvocation> BuildExpression(IEnumerable<Type> interfaces) 
        {
            ParameterExpression
                injector  = Expression.Parameter(typeof(IInjector), nameof(injector)),
                context   = Expression.Parameter(typeof(IRequestContext), nameof(context));

            Expression
                ifaceId  = GetFromContext(context, context => context.Module),
                methodId = GetFromContext(context, context => context.Method);

            return Expression.Lambda<ModuleInvocation>
            (
                Expression.Block
                (
                    CreateSwitch
                    (
                        value: ifaceId,
                        cases: interfaces.Select
                        (
                            iface =>
                            (
                                (MemberInfo) iface,
                                (Expression) CreateSwitch
                                (
                                    value: methodId, 
                                    cases: GetAllInterfaceMethods(iface).Where(method => method.GetCustomAttribute<IgnoreAttribute>() == null).Select
                                    (
                                        method => 
                                        (
                                            (MemberInfo) method, 
                                            (Expression) InvokeModule(iface, method)
                                        )
                                    ), 
                                    defaultBody: Throw<MissingMethodException>(new[] { typeof(string), typeof(string) }, ifaceId, methodId)
                                )
                            )
                        ),
                        defaultBody: Throw<MissingModuleException>(new[] { typeof(string) }, ifaceId)
                    )
                ),
                injector,
                context
            );

            Expression InvokeModule(Type module, MethodInfo ifaceMethod)
            {
                ParameterExpression
                    //
                    // A localXxX valtozok workaround-ok mivel (gozom nincs miert) ha switch esetek szama eler egy szamot
                    // akkor a lambda fordito elvesziti a scope-ot es elszall InvalidOperationException-el. NE modositsd!
                    //

                    localInjector = Expression.Variable(typeof(IInjector), nameof(localInjector)),
                    localContext  = Expression.Variable(typeof(IRequestContext), nameof(localContext)),
                    args          = Expression.Parameter(typeof(object?[]), nameof(args));

                Expression
                    assignLocalInjector = Expression.Assign(localInjector, injector),
                    assignLocalContext  = Expression.Assign(localContext, context),

                    //
                    // deserializer(context.Payload, context.Cancellation);
                    //

                    getArgs = Expression.Invoke
                    (
                        Expression.Constant(GetDeserializerFor(ifaceMethod)),
                        GetFromContext(localContext, localContext => localContext.Payload),
                        GetFromContext(localContext, localContext => localContext.Cancellation)
                    ),

                    //
                    // args => ((TInterface) injector.Get(typeof(TInterface), null)).Method((T0) args[0], ..., (TN) args[N])
                    //

                    invokeModule = Expression.Call
                    (
                        //
                        // (TInterface) injector.Get(typeof(TInterface), null)
                        //

                        instance: Expression.Convert
                        (
                            Expression.Call
                            (
                                localInjector,
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

                List<Expression> invocationBlock = new List<Expression>();

                if (ifaceMethod.ReturnType != typeof(void))
                    //
                    // return (object) ...;
                    //

                    invocationBlock.Add
                    (
                        Expression.Convert(invokeModule, typeof(object))
                    );
                else
                {
                    //
                    // ...;
                    // return null;
                    //

                    invocationBlock.Add(invokeModule);
                    invocationBlock.Add(Expression.Default(typeof(object)));
                }

                return Expression.Block
                (
                    //
                    // Parameterek lokalizalasa egy workaround (lasd metodus teteje), NE modositsd!
                    //

                    new[] { localInjector, localContext },

                    assignLocalInjector,
                    assignLocalContext,

                    //
                    // DoInvoke(deserializer(context.Payload, context.Cancellation), args => {...})
                    //

                    Expression.Invoke
                    (
                        Expression.Constant((Func<Task<object?[]>, Func<object?[], object>, Task<object?>>) DoInvoke),
                        getArgs,
                        Expression.Lambda<Func<object?[], object?>>
                        (
                           Expression.Block(invocationBlock),
                           args
                        )
                    )
                );
            }

            static MemberExpression GetFromContext<T>(ParameterExpression context, Expression<Func<IRequestContext, T>> ctx) => Expression.Property
            (
                context,
                typeof(IRequestContext).GetProperty(((MemberExpression) ctx.Body).Member.Name)
            );
        }
        #endregion

        #region Protected
        /// <summary>
        /// Gets the member name to be used in the execution process.
        /// </summary>
        protected virtual string GetMemberId(MemberInfo member)
        {
            if (member == null)
                throw new ArgumentNullException(nameof(member));

            return member.GetId();
        }

        /// <summary>
        /// Gets the deserializer for the given method.
        /// </summary>
        protected virtual Func<Stream, CancellationToken, Task<object?[]>> GetDeserializerFor(MethodInfo ifaceMethod)
        {
            if (ifaceMethod == null)
                throw new ArgumentNullException(nameof(ifaceMethod));

            Type[] argTypes = ifaceMethod
                .GetParameters()
                .Select(param => param.ParameterType)
                .ToArray();

            return (json, cancellation) =>
            {
                try
                {
                    return MultiTypeArraySerializer.Deserialize(json, cancellation, argTypes);
                }
                finally
                {
                    //
                    // Forrast alaphelyzetbe allitjuk ha lehet.
                    //

                    if (json.CanSeek) 
                        json.Seek(0, SeekOrigin.Begin);
                }
            };
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
            if (iface == null)
                throw new ArgumentNullException(nameof(iface));

            if (!iface.IsInterface)
                throw new ArgumentException(Errors.NOT_AN_INTERFACE, nameof(iface));

            if (iface.IsGenericTypeDefinition)
                throw new ArgumentException(Errors.GENERIC_IFACE, nameof(iface));

            MethodInfo[] methodsHavingByRefParam = GetAllInterfaceMethods(iface).Where
            (
                method => method.ReturnType.IsByRef || method.GetParameters().Any(para => para.ParameterType.IsByRef)
            ).ToArray();

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
            ModuleInvocation result = BuildExpression(FModules).Compile();

            //
            // A this.Modules bejegyzesek ertekei valtozhatnak, ezert a ToArray() hivas.
            //

            ModuleInvocationExtensions.RelatedModules.Add(result, FModules.ToArray());
            return result;
        }
        #endregion
    }
}