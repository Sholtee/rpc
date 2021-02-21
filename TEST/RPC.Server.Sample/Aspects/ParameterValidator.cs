/********************************************************************************
* ParameterValidator.cs                                                         *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Linq;
using System.Reflection;

namespace Solti.Utils.Rpc.Server.Sample
{
    using Proxy;

    public class ParameterValidator<TInterface> : InterfaceInterceptor<TInterface> where TInterface : class
    {
        public ParameterValidator(TInterface target) : base(target) { }

        public override object? Invoke(MethodInfo method, object?[] args, MemberInfo extra)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            if (args == null)
                throw new ArgumentNullException(nameof(args));

            foreach (var ctx in method.GetParameters().Select(
                (p, i) => new
                {
                    Parameter = p,
                    Value = args[i],
                    Validators = p.GetCustomAttributes().OfType<IParameterValidator>()
                }))
            {
                foreach (IParameterValidator validator in ctx.Validators)
                {
                    validator.Validate(ctx.Parameter, ctx.Value);
                }
            }

            return base.Invoke(method, args, extra);
        }
    }
}
