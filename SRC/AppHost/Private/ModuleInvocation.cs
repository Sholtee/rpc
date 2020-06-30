/********************************************************************************
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

namespace Solti.Utils.AppHost.Internals
{
    using DI.Interfaces;
    using Properties;

    /// <summary>
    /// Executes module methods by name.
    /// </summary>
    /// <param name="injector">The <see cref="IInjector"/> in which the module was registered.</param>
    /// <param name="ifaceId">The short name of the service interface (e.g.: IMyService).</param>
    /// <param name="methodId">The name of the interface emthod to be invoked.</param>
    /// <param name="args">The serialized method arguments.</param>
    internal delegate object ModuleInvocation(IInjector injector, string ifaceId, string methodId, string args);

    internal class ModuleInvocationBuilder
    {
        #region Private
        private static MethodInfo InjectorGet = ((MethodCallExpression) ((Expression<Action<IInjector>>) (i => i.Get(null!, null))).Body).Method;

        private readonly HashSet<Type> FModules = new HashSet<Type>();

        //
        // {throw new Exception(...); return null;}
        //

        private static Expression Throw<TException>(Type[] argTypes, params Expression[] args) where TException: Exception => Expression.Block
        (
            typeof(object),
            Expression.Throw
            (
                Expression.New
                (
                    typeof(TException).GetConstructor(argTypes) ?? throw new MissingMethodException(typeof(TException).Name, "Ctor"),
                    args
                )
            ),
            Expression.Default(typeof(object))
        );

        private Expression CreateSwitch(ParameterExpression parameter, IEnumerable<(MemberInfo Member, Expression Body)> cases, Expression defaultBody) => Expression.Switch
        (
            switchValue: parameter,
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

        private Expression<ModuleInvocation> BuildExpression(IEnumerable<Type> interfaces) 
        {
            ParameterExpression
                injector = Expression.Parameter(typeof(IInjector), nameof(injector)),
                ifaceId  = Expression.Parameter(typeof(string),    nameof(ifaceId)),
                methodId = Expression.Parameter(typeof(string),    nameof(methodId)),
                args     = Expression.Parameter(typeof(string),    nameof(args));

            return Expression.Lambda<ModuleInvocation>
            (
                CreateSwitch
                (
                    parameter: ifaceId,
                    cases: interfaces.Select
                    (
                        iface =>
                        (
                            (MemberInfo) iface,
                            (Expression) CreateSwitch
                            (
                                parameter: methodId, 
                                cases: GetAllInterfaceMethods(iface).Select
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
                ),
                injector,
                ifaceId,
                methodId,
                args
            );


            Expression InvokeModule(Type iface, MethodInfo method)
            {
                //
                // object[] argsArray = deserializer(argsString);
                //

                ParameterExpression argsArray = Expression.Variable(typeof(object[]), nameof(argsArray));

                Expression getArgs = Expression.Assign
                (
                    argsArray,
                    Expression.Invoke
                    (
                        Expression.Constant(GetDeserializerFor(method)),
                        args
                    )
                );

                //
                // ((TInterface) injector.Get(typeof(TInterface), null)).Method((T0) argsArray[0], ..., (TN) argsArray[N])
                //

                Expression call = Expression.Call
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

                List<Expression> block = new List<Expression> { getArgs };

                if (method.ReturnType != typeof(void))
                    //
                    // return (object) ...;
                    //

                    block.Add
                    (
                        Expression.Convert(call, typeof(object))
                    );
                else
                {
                    //
                    // ...;
                    // return null;
                    //

                    block.Add(call);
                    block.Add(Expression.Default(typeof(object)));
                }

                return Expression.Block
                (
                    variables: new[] { argsArray },
                    block
                );
            }
        }
        #endregion

        #region Protected
        /// <summary>
        /// Gets the member name to be used in the execution process.
        /// </summary>
        [SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "'member' is never null")]
        protected virtual string GetMemberId(MemberInfo member) => member.GetCustomAttribute<AliasAttribute>(inherit: false)?.Name ?? member.Name;

        /// <summary>
        /// Gets the deserializer for the given method.
        /// </summary>
        [SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "'ifaceMethod' is never null")]
        protected virtual Func<string, object[]> GetDeserializerFor(MethodInfo ifaceMethod) 
        {
            Type[] argTypes = ifaceMethod
                .GetParameters()
                .Select(param => param.ParameterType)
                .ToArray();

            return jsonString => MultiTypeArraySerializer.Deserialize(jsonString, argTypes);
        }
        #endregion

        #region Public
        public void AddModule<TInterface>() where TInterface : class 
        {
            if (!typeof(TInterface).IsInterface)
                throw new ArgumentException(Resources.NOT_AN_INTERFACE);

            FModules.Add(typeof(TInterface));
        }

        /// <summary>
        /// Builds a <see cref="ModuleInvocation"/> instance.
        /// </summary>
        public ModuleInvocation Build() => BuildExpression(FModules).Compile();
        #endregion
    }
}