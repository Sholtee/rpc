﻿/********************************************************************************
* SchemaProviderHandler.cs                                                      *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc.Pipeline
{
    using DI.Interfaces;
    using Interfaces;
    using Internals;
    using Properties;

    /// <summary>
    /// Specifies the <see cref="SchemaProviderHandler"/> configuration.
    /// </summary>
    public interface ISchemaProviderHandlerConfig
    {
        /// <summary>
        /// The actual schema
        /// </summary>
        public IReadOnlyDictionary<string, object> Schema { get; }
    }

    /// <summary>
    /// Adds schema support to the request pipeline.
    /// </summary>
    public class SchemaProviderHandler : RequestHandlerBase<ISchemaProviderHandlerConfig>
    {
        /// <summary>
        /// Creates a new <see cref="SchemaProviderHandler"/> instance.
        /// </summary>
        /// <remarks>This handler requires a <paramref name="next"/> value to be provided.</remarks>
        public SchemaProviderHandler(IRequestHandler next, ISchemaProviderHandlerConfig config) : base(next, config)
        {
        }

        /// <inheritdoc/>
        public override async Task HandleAsync(IInjector scope, IHttpSession context, CancellationToken cancellation)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            IHttpRequest request = context.Request;

            //
            // Ha nem GET van akkor tovabb engedjuk a kerest. ModuleInvocationHandler ugy is validal.
            //

            if (!request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                await Next.HandleAsync(scope, context, cancellation);
                return;
            }

            if (request.QueryParameters.Count is not 1 || !request.QueryParameters.TryGetValue(nameof(IRpcRequestContext.Module), out string module))
                throw new HttpException(Errors.NO_MODULE) { Status = HttpStatusCode.BadRequest };

            IHttpResponse response = context.Response;

            //
            // Elmeletben a kesobbiekben egynel tobb modul adatait is visszaadhajtuk majd
            //

            IDictionary<string, object> descriptor = CopyEntries(Config.Schema, new Dictionary<string, object>(), module);

            //
            // Tartalom tipusat meg a Payload beallitasa elott kell megadni.
            //

            response.Headers["Content-Type"] = "application/json";

            await scope.Get<IJsonSerializer>().SerializeAsync
            (
                descriptor.GetType(),
                descriptor,
                response.Payload,

                //
                // Ezt itt mar ne szakitsuk meg h ne maradjon felig kiirt adat a valaszban
                //

                CancellationToken.None
            );

            await response.Close();

            static IDictionary<string, object> CopyEntries(IReadOnlyDictionary<string, object> src, IDictionary<string, object> dst, params string[] keys)
            {
                foreach (string key in keys)
                {
                    if (src.TryGetValue(key, out object val))
                        dst.Add(key, val);
                }
                return dst;
            }
        }
    }

    /// <summary>
    /// Configures the schama provider.
    /// </summary>
    /// <remarks>If you are using the <see cref="WebServiceBuilderExtensions.ConfigureRpcService(WebServiceBuilder, Action{RequestHandlerBuilder}, bool)"/> method, there is no need to configure the provider directly. Instead, use the <see cref="PublishSchemaAttribute"/>.</remarks>
    public class SchemaProvider : RequestHandlerBuilder, ISchemaProviderHandlerConfig
    {
        private readonly Dictionary<string, object> FSchema = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the unique ID of a member.
        /// </summary>
        protected virtual string GetMemberId(MemberInfo member)
        {
            if (member is null)
                throw new ArgumentNullException(nameof(member));

            return member.GetId();
        }

        /// <summary>
        /// Creates a new <see cref="SchemaProvider"/> instance.
        /// </summary>
        public SchemaProvider(WebServiceBuilder webServiceBuilder, RequestHandlerBuilder? parent) : base(webServiceBuilder, parent)
        {
            //
            // Itt nem kell regisztraljuk az IJsonSerializer szervizt, mivel az a Modules osztalyban ugy is
            // regisztralva lesz.
            //
        }

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, object> Schema => FSchema;

        /// <summary>
        /// Registers a module interface in the schema "database".
        /// </summary>
        public SchemaProvider Register(Type iface)
        {
            if (iface is null)
                throw new ArgumentNullException(nameof(iface));

            if (!iface.IsInterface)
                throw new ArgumentException(Errors.NOT_AN_INTERFACE, nameof(iface));

            if (iface.IsGenericTypeDefinition)
                throw new ArgumentException(Errors.GENERIC_IFACE, nameof(iface));

            try
            {
                FSchema[GetMemberId(iface)] = new
                {
                    Methods = iface
                        .GetAllInterfaceMethods()
                        .Where(m => !m.IsSpecialName)
                        .ToDictionary(GetMemberId, m => new { Layout = "TODO" }),
                    Properties = iface
                        .GetAllInterfaceProperties()
                        .ToDictionary(GetMemberId, p => new { Layout = "TODO", HasGetter = p.CanRead, HasSetter = p.CanWrite })
                };
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException(Errors.DUPLICATE_MEMBER_ID, ex);
            }

            return this;
        }

        /// <summary>
        /// Registers a module interface in the schema "database".
        /// </summary>
        public SchemaProvider Register<TInterface>() where TInterface: class => Register(typeof(TInterface));

        /// <inheritdoc/>
        public override IRequestHandler Build(IRequestHandler next) => new SchemaProviderHandler(next, this);
    }
}
