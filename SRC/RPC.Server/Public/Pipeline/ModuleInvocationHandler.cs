﻿/********************************************************************************
* ModuleInvocationHandler.cs                                                    *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CA1031

namespace Solti.Utils.Rpc.Pipeline
{
    using DI.Interfaces;
    using Interfaces;
    using Internals;
    using Properties;

    /// <summary>
    /// Invokes a module method, described by the <see cref="IRpcRequestContext"/> and writes the result into the <see cref="HttpListenerResponse"/>.
    /// </summary>
    public class ModuleInvocationHandler : IRequestHandler
    {
        private sealed record RpcRequestContext
        (
            string? SessionId,
            string Module,
            string Method,
            HttpListenerRequest OriginalRequest,
            CancellationToken Cancellation
        ) : IRpcRequestContext
        {
            public Stream Payload => OriginalRequest.InputStream;
        };

        [ThreadStatic]
        internal static IRpcRequestContext? RpcContext;

        /// <summary>
        /// Creates the HTTP response.
        /// </summary>
        /// <remarks>This operation cannot be cancelled.</remarks>
        protected virtual async Task CreateResponse(object? result, HttpListenerResponse response)
        {
            if (response is null)
                throw new ArgumentNullException(nameof(response));

            switch (result)
            {
                case Stream stream:
                    response.ContentType = "application/octet-stream";
                    response.ContentEncoding = null;
                    if (stream.CanSeek)
                        stream.Seek(0, SeekOrigin.Begin);
                    await stream.CopyToAsync(response.OutputStream);
#if NETSTANDARD2_1_OR_GREATER
                    await stream.DisposeAsync();
#else
                    stream.Dispose();
#endif
                    break;
                case Exception ex:
                    response.ContentType = "application/json";
                    response.ContentEncoding = Encoding.UTF8;
                    await SafeSerializer.SerializeAsync(response.OutputStream, new RpcResponse
                    {
                        Exception = new ExceptionInfo
                        {
                            TypeName = ex.GetType().AssemblyQualifiedName,
                            Message  = ex.Message,
                            Data     = ex.Data
                        }
                    }, Parent.SerializerOptions);
                    break;
                default:
                    response.ContentType = "application/json";
                    response.ContentEncoding = Encoding.UTF8;
                    await SafeSerializer.SerializeAsync(response.OutputStream, new RpcResponse { Result = result }, Parent.SerializerOptions);
                    break;
            }
        }

        /// <summary>
        /// Parses the <paramref name="context"/> to RPC context.
        /// </summary>
        protected virtual IRpcRequestContext CreateContext(RequestContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            HttpListenerRequest request = context.Request;

            //
            // Metodus validalasa (POST eseten a keresnek kene legyen torzse).
            //

            if (request.HttpMethod.ToUpperInvariant() is not "POST")
                throw new HttpException(Errors.HTTP_METHOD_NOT_SUPPORTED) { Status = HttpStatusCode.MethodNotAllowed };

            //
            // Tartalom validalasa. ContentType property bar nem nullable de lehet NULL.
            //

            if (request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) is not true)
                throw new HttpException(Errors.HTTP_CONTENT_NOT_SUPPORTED) { Status = HttpStatusCode.BadRequest };

            if (request.ContentEncoding?.WebName.Equals("utf-8", StringComparison.OrdinalIgnoreCase) is not true)
                throw new HttpException(Errors.HTTP_ENCODING_NOT_SUPPORTED) { Status = HttpStatusCode.BadRequest };

            NameValueCollection paramz = request.QueryString;

            //
            // Szukseges parameterek lekerdezese (nem kis-nagy betu erzekeny).
            //

            return new RpcRequestContext
            (
                paramz[nameof(RpcRequestContext.SessionId)],
                paramz[nameof(RpcRequestContext.Module)] ?? throw new HttpException(Errors.NO_MODULE) { Status = HttpStatusCode.BadRequest },
                paramz[nameof(RpcRequestContext.Method)] ?? throw new HttpException(Errors.NO_METHOD) { Status = HttpStatusCode.BadRequest },
                request,
                context.Cancellation           
            );
        }

        /// <inheritdoc/>
        public IRequestHandler Next { get; }

        /// <summary>
        /// The parent instance.
        /// </summary>
        public Modules Parent { get; }

        /// <summary>
        /// Creates a new <see cref="ModuleInvocationHandler"/> instance.
        /// </summary>
        /// <remarks>This handler requires a <paramref name="next"/> value to be supplied.</remarks>
        public ModuleInvocationHandler(IRequestHandler next, Modules parent)
        {
            Parent = parent ?? throw new ArgumentNullException(nameof(parent));
            Next   = next   ?? throw new ArgumentNullException(nameof(next));
        }

        /// <inheritdoc/>
        public async Task HandleAsync(RequestContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            RpcContext = CreateContext(context);

            object? result;

            try
            {
                result = await Parent.ModuleInvocation!(context.Scope, RpcContext, Parent.SerializerOptions);
            }

            //
            // Egyedi HTTP hibakod is megadhato, azt nem szerializaljuk.
            //

            catch (HttpException) { throw; }

            //
            // Kulonben valid valasz fogja tartalmazni a hibat.
            //

            catch (Exception ex) { result = ex; }

            //
            // 
            finally { RpcContext = null; }

            //
            // A valasz kiirasat mar nem lehet megszakitani.
            //

            await CreateResponse(result, context.Response);
            await Next.HandleAsync(context);
        }
    }

    /// <summary>
    /// Invokes a module method described by the <see cref="IRpcRequestContext"/> and writes the result into the <see cref="HttpListenerResponse"/>.
    /// </summary>
    public class Modules : RequestHandlerFactory, IModuleRegistry
    {
        private ModuleInvocationBuilder ModuleInvocationBuilder { get; } = new();

        /// <inheritdoc/>
        protected internal override void FinishConfiguration()
        {
            ModuleInvocation = ModuleInvocationBuilder.Build();
            WebServiceBuilder.ConfigureServices(svcs => svcs.Factory<IRpcRequestContext>(_ => ModuleInvocationHandler.RpcContext ?? throw new InvalidOperationException(), Lifetime.Scoped));
            base.FinishConfiguration();
        }

        /// <inheritdoc/>
        protected override IRequestHandler Create(IRequestHandler next) => new ModuleInvocationHandler(next, this);

        /// <summary>
        /// The built <see cref="Internals.ModuleInvocation"/> delegate.
        /// </summary>
        /// <remarks>This property is set once the <see cref="FinishConfiguration()"/> method is called.</remarks>
        public ModuleInvocation? ModuleInvocation { get; private set; }

        /// <summary>
        /// The serializer options.
        /// </summary>
        public JsonSerializerOptions SerializerOptions { get; set; } = new JsonSerializerOptions();

        /// <inheritdoc/>
        public IModuleRegistry Register<TInterface, TImplementation>() where TInterface : class where TImplementation : TInterface
        {
            ModuleInvocationBuilder.AddModule<TInterface>();
            WebServiceBuilder.ConfigureServices(svcs => svcs.Service<TInterface, TImplementation>(Lifetime.Scoped));
            return this;
        }

        /// <inheritdoc/>
        public IModuleRegistry Register<TInterface>(Func<IInjector, TInterface> factory) where TInterface : class
        {
            ModuleInvocationBuilder.AddModule<TInterface>();
            WebServiceBuilder.ConfigureServices(svcs => svcs.Factory<TInterface>(factory, Lifetime.Scoped));
            return this;
        }
    }
}