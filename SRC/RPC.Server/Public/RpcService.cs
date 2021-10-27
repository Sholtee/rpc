/********************************************************************************
* RpcService.cs                                                                 *
*                                                                               *
* Author: Denes Solti                                                           *
********************************************************************************/
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Solti.Utils.Rpc
{
    using DI.Interfaces;

    using Interfaces;
    using Internals;

    /// <summary>
    /// Implements the core RPC service functionality.
    /// </summary>
    public class RpcService : WebService
    {
        private readonly ModuleInvocation FModuleInvocation;
        private readonly JsonSerializerOptions FSerializerOptions;

        #region Protected
        /// <summary>
        /// Creates a new <see cref="RpcService"/> instance.
        /// </summary>
        protected internal RpcService(WebServiceDescriptor descriptor, IScopeFactory scopeFactory, ModuleInvocation moduleInvocation, JsonSerializerOptions serializerOptions) : base(descriptor, scopeFactory)
        {
            FModuleInvocation = moduleInvocation ?? throw new ArgumentNullException(nameof(moduleInvocation));
            FSerializerOptions = serializerOptions ?? throw new ArgumentNullException(nameof(serializerOptions));
        }

        /// <inheritdoc/>
        protected override void SetAcHeaders(HttpListenerContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            base.SetAcHeaders(context);

            context.Response.Headers["Access-Control-Allow-Methods"] = "POST";
            context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type, Content-Length";
        }

        /// <inheritdoc/>
        protected override async Task Process(HttpListenerContext context, IInjector injector, CancellationToken cancellation)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            cancellation.ThrowIfCancellationRequested();

            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            //
            // Eloellenorzesek HTTP hibat generalnak -> NE a try-catch blokkban legyenek
            //

            if (request.HttpMethod.ToUpperInvariant() is not "POST")
            {
                throw new HttpException
                {
                    Status = HttpStatusCode.MethodNotAllowed
                };           
            }

            //
            // Content-Type lehet NULL a kodolas viszont nem
            //

            if (request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) is not true || request.ContentEncoding.WebName is not "utf-8")
            {
                throw new HttpException
                {
                    Status = HttpStatusCode.BadRequest
                };
            }

            object? result;

            try
            {
                result = await InvokeModule(new RequestContext(request, cancellation), injector);
            }

            catch (OperationCanceledException ex) // ez megeszi a TaskCanceledException-t is
            {
                //
                // Mivel itt mar "cancellation.IsCancellationRequested" jo esellyel igaz ezert h a CreateResponse() ne 
                // dobjon egybol TaskCanceledException-t (ami HTTP 500-at generalna) ne az eredeti "cancellation"-t 
                // adjuk at.
                //

                cancellation = default;
                result = ex;
            }

            catch (HttpException)
            {
                //
                // Egyedi HTTP hibakod is megadhato, ekkor nem szerializalunk.
                //

                throw;
            }

            #pragma warning disable CA1031 // We have to catch all kind of exceptions here
            catch (Exception ex)
            #pragma warning restore CA1031
            {
                //
                // Kulomben valid valasz fogja tartalmazni a hibat.
                //

                result = ex;
            }

            await CreateResponse(result, response, cancellation);
        }

        /// <summary>
        /// Invokes a module method described by the <paramref name="context"/>.
        /// </summary>
        protected async virtual Task<object?> InvokeModule(IRequestContext context, IInjector injector)
        {
            if (context is null) 
                throw new ArgumentNullException(nameof(context));

            if (injector is null)
                throw new ArgumentNullException(nameof(injector));

            //
            // A kontextust elerhetik a modulok fuggosegkent.
            //

            injector.Meta(RpcServiceBuilder.META_REQUEST, context);

            return await FModuleInvocation(injector, context);
        }

        /// <summary>
        /// Sets the HTTP response.
        /// </summary>
        protected virtual async Task CreateResponse(object? result, HttpListenerResponse response, CancellationToken cancellation)
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
                    await stream.CopyToAsync
                    (
                        response.OutputStream
#if NETSTANDARD2_1_OR_GREATER
                        , cancellation
#endif
                    );      
                    stream.Dispose();
                    break;
                case Exception ex:
                    response.ContentType = "application/json";
                    response.ContentEncoding = Encoding.UTF8;
                    await JsonSerializer.SerializeAsync(response.OutputStream, new RpcResponse
                    {
                        Exception = new ExceptionInfo
                        {
                            TypeName = ex.GetType().AssemblyQualifiedName,
                            Message = ex.Message,
                            Data = ex.Data
                        }
                    }, FSerializerOptions, cancellation);
                    break;
                default:
                    response.ContentType = "application/json";
                    response.ContentEncoding = Encoding.UTF8;
                    await JsonSerializer.SerializeAsync(response.OutputStream, new RpcResponse
                    {
                        Result = result
                    }, FSerializerOptions, cancellation);
                    break;
            }

            //
            // Statuszt es hosszt nem kell beallitani.
            //

            response.Close();
        }
        #endregion
    }
}
