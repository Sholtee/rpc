/********************************************************************************
* IParameterValidator.cs                                                        *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System.Reflection;

namespace Solti.Utils.Rpc.Server.Sample
{
    public interface IParameterValidator
    {
        void Validate(ParameterInfo param, object? value);
    }
}
