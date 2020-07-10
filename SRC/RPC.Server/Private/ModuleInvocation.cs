﻿/********************************************************************************
* ModuleInvocation.cs                                                           *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Internals
{
    using DI.Interfaces;
    using Properties;

    /// <summary>
    /// Executes module methods by context.
    /// </summary>
    /// <param name="injector">The <see cref="IInjector"/> in which the module was registered.</param>
    /// <param name="context">The context which describes the invocation.</param>
    public delegate Task<object?> ModuleInvocation(IInjector injector, IRequestContext context);

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

        private static IEnumerable<MethodInfo> GetAllInterfaceMethods(Type iface) =>
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

        [SuppressMessage("Reliability", "CA2008:Do not create tasks without passing a TaskScheduler")]
        private Expression<ModuleInvocation> BuildExpression(IEnumerable<Type> interfaces) 
        {
            ParameterExpression
                injector  = Expression.Parameter(typeof(IInjector), nameof(injector)),
                context   = Expression.Parameter(typeof(IRequestContext), nameof(context)),
                argsArray = Expression.Variable(typeof(object?[]), nameof(argsArray));

            Expression
                ifaceId  = GetFromContext(nameof(IRequestContext.Module)),
                methodId = GetFromContext(nameof(IRequestContext.Method));

            return Expression.Lambda<ModuleInvocation>
            (
                Expression.Block
                (
                    variables: new[] { argsArray },
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
                        defaultBody: Throw<ServiceNotFoundException>(new[] { typeof(string) }, ifaceId)
                    )
                ),
                injector,
                context
            );

            Expression InvokeModule(Type iface, MethodInfo method)
            {
                Expression 
                    //
                    // argsArray = deserializer(context.Args);
                    //

                    assignArgs = Expression.Assign
                    (
                        argsArray,
                        Expression.Invoke
                        (
                            Expression.Constant(GetDeserializerFor(method)),
                            GetFromContext(nameof(IRequestContext.Args))
                        )
                    ),

                    //
                    // ((TInterface) injector.Get(typeof(TInterface), null)).Method((T0) argsArray[0], ..., (TN) argsArray[N])
                    //

                    callModule = Expression.Call
                    (
                        //
                        // (TInterface) injector.Get(typeof(TInterface), null)
                        //

                        instance: Expression.Convert
                        (
                            Expression.Call
                            (
                                injector,
                                InjectorGet,
                                Expression.Constant(iface),
                                Expression.Constant(null, typeof(string))
                            ),
                            iface
                        ),

                        //
                        // .Method((T0) argsArray[0], ..., (TN) argsArray[N])
                        //

                        method,
                        arguments: method.GetParameters().Select
                        (
                            (para, i) => Expression.Convert
                            (
                                Expression.ArrayAccess(argsArray, Expression.Constant(i)),
                                para.ParameterType
                            )
                        )
                    );

                List<Expression> block = new List<Expression> 
                { 
                    assignArgs
                };

                if (method.ReturnType != typeof(void))
                    //
                    // return (object) ...;
                    //

                    block.Add
                    (
                        Expression.Convert(callModule, typeof(object))
                    );
                else
                {
                    //
                    // ...;
                    // return null;
                    //

                    block.Add(callModule);
                    block.Add(Expression.Default(typeof(object)));
                }

                //
                // new Task<object?>(() => ..., TaskCreationOptions.xXx)
                //

                return Expression.Invoke
                (
                    Expression.Constant((Func<Func<object?>, TaskCreationOptions, Task<object?>>) CreateTask),
                    Expression.Lambda<Func<object?>>
                    (
                       Expression.Block(block)
                    ),                
                    Expression.Constant(IsLongRunning(method) ? TaskCreationOptions.LongRunning : TaskCreationOptions.None)
                );
            }

            MemberExpression GetFromContext(string name) => Expression.Property
            (
                context,
                typeof(IRequestContext).GetProperty(name) ?? throw new MissingMemberException(nameof(IRequestContext), nameof(IRequestContext.Args))
            );

            static Task<object?> CreateTask(Func<object?> invocation, TaskCreationOptions opts) => Task.Factory.StartNew(invocation, opts);
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

            return member.GetCustomAttribute<AliasAttribute>(inherit: false)?.Name ?? member.Name;
        }

        /// <summary>
        /// Gets the deserializer for the given method.
        /// </summary>
        protected virtual Func<string, object[]> GetDeserializerFor(MethodInfo ifaceMethod) 
        {
            if (ifaceMethod == null)
                throw new ArgumentNullException(nameof(ifaceMethod));

            Type[] argTypes = ifaceMethod
                .GetParameters()
                .Select(param => param.ParameterType)
                .ToArray();

            return jsonString => MultiTypeArraySerializer.Deserialize(jsonString, argTypes);
        }

        /// <summary>
        /// Returns true if the given method may run long.
        /// </summary>
        protected virtual bool IsLongRunning(MethodInfo ifaceMethod)
        {
            if (ifaceMethod == null)
                throw new ArgumentNullException(nameof(ifaceMethod));

            return ifaceMethod.GetCustomAttribute<MayRunLongAttribute>() != null;
        }
        #endregion

        #region Public
        /// <summary>
        /// Adds a module to this builder.
        /// </summary>
        public void AddModule<TInterface>() where TInterface : class 
        {
            if (!typeof(TInterface).IsInterface)
                throw new ArgumentException(Resources.NOT_AN_INTERFACE);

            FModules.Add(typeof(TInterface));
        }

        /// <summary>
        /// Returns the registered modules.
        /// </summary>
        public IReadOnlyCollection<Type> Modules => FModules;

        /// <summary>
        /// Builds a <see cref="ModuleInvocation"/> instance.
        /// </summary>
        public virtual ModuleInvocation Build() => BuildExpression(FModules).Compile();
        #endregion
    }
}