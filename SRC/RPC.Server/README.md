# RPC.NET Server [![Build status](https://ci.appveyor.com/api/projects/status/sqgld5a86pha51wf/branch/master?svg=true)](https://ci.appveyor.com/project/Sholtee/rpc/branch/master) ![AppVeyor tests](https://img.shields.io/appveyor/tests/sholtee/rpc/master) [![Coverage Status](https://coveralls.io/repos/github/Sholtee/rpc/badge.svg?branch=master)](https://coveralls.io/github/Sholtee/rpc?branch=master) ![GitHub last commit (branch)](https://img.shields.io/github/last-commit/sholtee/rpc/master)
> Simple, lightweight RPC server implementation for .NET

**This documentation refers the version 6.X of the library**

|Name|Package|
|:--:|:--:|
|**RPC.NET.Interfaces**|[![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/rpc.net.interfaces)](https://www.nuget.org/packages/rpc.net.interfaces )|
|**RPC.NET.Server**|[![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/rpc.net.server)](https://www.nuget.org/packages/rpc.net.server )|
## How it works
1. The client sends a *HTTP POST* to the server where 
   - The request *URI*
     - Must use *HTTP* or *HTTPS* scheme
     - Identifies the remote *module* and *method* (in the query component)
     - May contain the *sessionid* and/or custom data (in the query component)
   
     For example: `http://www.example.org:1986/api?module=IMyModule&method=ModuleMethod&sessionid=xXx`. 
   - The *content-type* is `application/json`
   - The *request body* is an (UTF-8) *JSON* stringified array that contains the method arguments. For example: `["cica", 10]`.
2. The type of response depends on the kind of the result:
   - If the remote method has a non `Stream` return value then the *content-type* is `application/json` and the *response body* contains the (UTF-8) *JSON* stringified result. The result is a wrapped object that contains the actual outcome of the method or the error description:
     ```js
     {
       "Result": 12,
	   "Exception": null
     }
     ```
	 or
     ```js
     {
       "Result": null,
	   "Exception": {
         "TypeName": "System.Exception, System.Private.CoreLib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e",
         "Message": "Exception of type 'System.Exception' was thrown.",
         "Data": {}
       }
     }
     ```	 
   - If the remote method has a `Stream` return value (and the invocation was successful) then the *content-type* is `application/octet-stream` and the *response body* contains the raw data.
## Architecture
1. layer: The *app-host* 
   - It's responsible for configuring, installing and running the underlying `WebService`. These logics are placed in separate methods defined by [AppHostBase](https://sholtee.github.io/rpc/doc/Solti.Utils.Rpc.Hosting.AppHostBase.html ) class.
   ```csharp
   using Solti.Utils.DI.Interfaces;

   using Solti.Utils.Rpc;
   using Solti.Utils.Rpc.Hosting;
   using Solti.Utils.Rpc.Interfaces;
   using Solti.Utils.Rpc.Pipeline;
   using Solti.Utils.Rpc.Servers;

   public class AppHost : AppHostBase
   {
     public AppHost(string[] args) : base(args) { }

     public override void OnConfigureWin32Service(Win32ServiceDescriptor descriptor)
     {
       base.OnConfigureWin32Service(descriptor);
       descriptor.Name = "My Service";
     }

     public override void OnConfigure(WebServiceBuilder webServiceBuilder) => webServiceBuilder
       // it's required to set a HTTP server implementation
       .ConfigureBackend(_ => new HttpListenerBackend("http://localhost:1986/api/") { ReserveUrl = true })
       // shortcut to use the default RPC pipeline
       .ConfigureRpcService(conf =>
       {
         // confifure the pipeline
         switch (conf) 
         {
           case Modules modules: 
             modules.Register<ICalculator, Calculator>(); // remotely visible modules
             break;
           case RpcAccessControl ac:
             ac.AllowedOrigins.Add("http://localhost:1987");
             break;
          }
        })
        // services defined here are WebService exclusives
        .ConfigureServices(services => ...);

      // services defined here are visible for the WebService and also for the installation logic (OnInstall, OnUninstall, etc)
      public override OnConfigureServices(services) => services => services
          .Service<IMyService, MyService>(Lifetime.Scoped)
          .Factory<ICache>(i => ..., Lifetime.Singleton));
  
      public override void OnInstall(IInjector scope) { /*Initialize DB schema, etc...*/ }
    }

    static class Program
    {
        static int Main(string[] args) => new AppHost(args).Run();
    }
   ```
   - It defines an extensible *command line interface* to let you control your app. The command structure is `verb -arg1 value1 -arg2 value2` (e.g.: `install -root "cica@mica.hu -pwVariable PW_VARIABLE"`). Defaults verbs are  `[service ]install`, `[service ]uninstall` (if you don't wish to install your app as a Win32 Service you can ommit the "service " prefix'). Note that you can define your own verbs as you can see [here](https://github.com/Sholtee/rpc/blob/master/SRC/RPC.Server/Public/Hosting/AppHostBase.Install.cs ).
2. layer: The [WebService](https://sholtee.github.io/rpc/doc/Solti.Utils.Rpc.WebService.html ), which has 3 responsibilities:
   - controls the underlying `IHttpServer` implementation
   - maintains the worker threads
   - when a new request is available, it creates a new scope and processes the request by invoking the request pipeline. The default pipeline (defined by `ConfigureRpcService()`) is:
     ```csharp
     webServiceBuilder
       .ConfigurePipeline(pipeline => pipeline
         .Use<Modules>(conf => conf
           .Register<ICalculator, Calculator>()) // exposed (remotely visible) module
         .Use<RequestTimeout>(conf => {...})
         .Use<RpcAccessControl>(conf => {...})
         .Use<RequestLimiter>(conf => {...})
         .Use<ExceptionCatcher>(conf => {...}));
     ```
     As you can see every pipeline item (request handler) has its own configuration. For more information check [this](https://sholtee.github.io/rpc/doc/Solti.Utils.Rpc.Pipeline.html ) out.
3. layer: The exposed modules:
   - They are defined in [Modules](https://sholtee.github.io/rpc/doc/Solti.Utils.Rpc.Pipeline.Modules.html ) pipeline item
   - They may be decorated by [aspects](https://github.com/Sholtee/injector#aspects )
   - Each session has its own module instance
## Modules
- are also services
- are accessible remotely (see [how it works](#How-it-works ) section)
- may have [dependencies](https://github.com/Sholtee/injector#registering-services ) (defined in the `ConfigureServices()` method)
- are registered in a `Scoped` lifetime

Note that the `ConfigureRpcService()` (see above) method implicitly registeres the [IServiceDescriptor](https://sholtee.github.io/rpc/doc/Solti.Utils.Rpc.Interfaces.IServiceDescriptor.html ) module which is intended to describe the RPC service itself and exposes only the name and version of your app.

A basic module looks like this:
```csharp
using Solti.Utils.Rpc.Interfaces;

public interface ICalculator // it may be worth to put this interface to a separate assembly
{
  [Alias("AddInt")] // method will be exposed with this name
  int Add(int a, int b); // regular method
  double PI { get; } // property
  Task<int> AddAsync(int a, int b); // async methods also supported
  [Ignore] // this cannot be called remotely
  void Ignored();
}
...
public class Calculator : ICalculator 
{
  private readonly IRequestContext FContext;
  // You can access the request context as a regular dependency
  public Calculator(IRequestContext context) => FContext = context ?? throw new ArgumentNullException(nameof(context));
  public int Add(int a, int b) => a + b;
  public Task<int> AddAsync(int a, int b)
  {
    FContext.Cancellation.ThrowIfCancellationRequested();
    return Task.FromResult(a + b);
  }
  public double PI => Math.PI;
}
```
## Aspects
[Aspects](https://github.com/Sholtee/injector#aspects ) are intended to separate common functionality from the actual behavior (in case of modules this means the business logic). In practice aspects are implemented with [interceptors](https://sholtee.github.io/proxygen/doc/Solti.Utils.Proxy.InterfaceInterceptor-1.html ) and [attributes](https://sholtee.github.io/injector/doc/Solti.Utils.DI.Interfaces.AspectAttribute.html ):
```csharp
using Solti.Utils.DI.Interfaces;

[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
public sealed class ParameterValidatorAspect : AspectAttribute
{
  public override Type GetInterceptor(Type iface)
  {
    // to ensure "separation of concerns" the attribute itself never references the actual interceptor
    Type interceptor = Type.GetType("Solti.Utils.Rpc.Aspects.ParameterValidator`1, Solti.Utils.Rpc, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null", throwOnError: true);
    return interceptor.MakeGenericType(iface);
  }
}
// Base class of all the validator attributes
public abstract class ParameterValidatorAttribute: Attribute
{
  public abstract void Validate(ParameterInfo param, object value);
}

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public class NotNullAttribute : ParameterValidatorAttribute
{
  public override void Validate(ParameterInfo param, object value)
  {
    if (value is null)
      throw new ArgumentNullException(param.Name);
  }
}
...
using Solti.Utils.Proxy;

public class ParameterValidatorProxy<TInterface> : InterfaceInterceptor<TInterface> where TInterface : class
{
  public ParameterValidatorProxy(TInterface target) : base(target) { }

  public override object Invoke(InvocationContext context)
  {
    foreach(var descr in context.Method.GetParameters().Select(
      (p, i) => new 
      { 
        Parameter = p, 
        Value = context.Args[i], 
        Validators = p.GetCustomAttributes<ParameterValidatorAttribute>() // ParamterValidator is the base class of all the validators
      }))
    {
      foreach (var validator in descr.Validators) 
      {
        validator.Validate(descr.Parameter, descr.Value);
      }
    }
    return base.Invoke(context);
  }
}
```
Interceptors are applied automatically during service/module registration so you just have to deal with the aspect attributes:
```csharp
[ParameterValidatorAspect]
public interface IModule
{
  void DoSomething([NotNull] string arg1);
}
...
webServiceBuilder
  .ConfigureRpcService(conf => (conf as Modules)?.Register<IModule, Module>());
```
### Parameter validation aspect
`ParameterValidatorAspect` aims that its name suggests:
```csharp
using Solti.Utils.Rpc.Interfaces;

[ParameterValidatorAspect]
public interface IModule
{
  void DoSomething([NotNull, Match("cica$", ParameterValidationErrorMessage = "Parameter must be ended by 'cica'")] string arg1, [NotNull] object arg2);
  void DoSomethingElse([NotNull, ValidateProperties] ComplexData arg);
  void ConditionallyValidated([NotNull(ParameterValidationErrorMessage = "Customized error message")] string arg);
}
```
If a validation fails the server throws a `ValidationException` which uses the `Data["TargetName"]` to identify the target parameter or property. It allows the client to handle the error more precisely (e.g. it may highlight the input containing the wrong data)

Built-in validators are the follows:
- `NotNullAttribute`: Ensures that a parameter or property is not null
- `NotEmptyAttribute`: Ensures that a parameter or property (implementing the `IEnumerable` interface) is not empty
- `LengthBetweenAttribute`: Ensures that the length of a collection is between the two provided values
- `MatchAttribute`: Executes a regular expression against a parameter or property
- `MustAttribute`: Executes a predicate (`IPredicate` implementation) against a parameter or property:
   ```csharp
   void DoSomethingElse([Must(typeof(CustomValidationLogic))] ComplexData arg);
   ```
- `ValidatePropertiesAttribute`: Instructs the system to executes validators placed on properties.
   ```csharp
   public class ComplexData
   {
      [NotNull]
      public string Prop {get; set;}
      [NotNull, ValidateProperties]
      public InnerData InnerComplex {get; set;}
   }
   void DoSomethingElse([ValidateProperties] ComplexData arg);
   ```

Of course, you can define your own validator by implementing the `IParameterValidator` and  `IPropertyValidator` interfaces:
```csharp
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property, AllowMultiple = false)]
public sealed class MyValidatorAttribute : Attribute, IParameterValidator, IPropertyValidator
{
}
```
### Authorization aspect
By default the system uses "role based" authorization. It means that every user (even an anonymous) has its own set of assigned roles (for e.g.: `AuthenticatedUser`, `Admin`) and every module method must provide a role list required the calling user to have:
```csharp
using Solti.Utils.Rpc.Interfaces;

[Flags]
public enum MyRoles
{
  Anonymous,
  AuthenticatedUser,
  MayPrint,
  Admin
}

[RoleValidatorAspect]
public interface IModule
{
  // Admins or any authenticated user having print right are able to print 
  [RequiredRoles(MyRoles.AuthenticatedUser | MyRoles.MayPrint, MyRoles.Admin)]
  void Print();
  [RequiredRoles(MyRoles.AuthenticatedUser | MyRoles.MayPrint, MyRoles.Admin)]
  Task<string> PrintAsync();
  [RequiredRoles(MyRoles.Anonymous)]
  string Login(string user, string pw); // returns the new session id
  [RequiredRoles(MyRoles.AuthenticatedUser)]
  void Logout();
  // will throw on every invocation since there is no RequiredRoles attribute
  void MissingRequiredRoleAttribute();
}

public class Module: IModule {...}

// In order to use the RoleValidatorAspect we have to implement the IRoleManager interface
public class RoleManagerService: IRoleManager
{
  public Enum GetAssignedRoles(string? sessionId)
  {
    if (session is null)
      return MyRoles.Anonymous;

    // acquire the roles assigned to the user
    MyRoles assignedRoles = ...;
    return assignedRoles | MyRoles.AuthenticatedUser;
  }
  public Task<Enum> GetAssignedRolesAsync(string? sessionId, CancellationToken cancellation) => Task.FromResult(GetAssignedRoles(sessionId));
}
...
webServiceBuilder
  .ConfigureRpcService(conf => (conf as Modules)?.Register<IModule, Module>())
  .ConfigureServices(services => services.Service<IRoleManager, RoleManagerService>(Lifetime.Scoped));
```
### Transaction manager aspect
To ensure database consistency we can define [transactions](https://docs.oracle.com/cd/B19306_01/server.102/b14220/transact.htm#i1666 ) using aspects:
```csharp
using System.Data;
using System.Threading.Tasks;
using Solti.Urils.Rpc.Interfaces;

[TransactionAspect]
public interface IModule
{
  void NonTransactional();
  [Transactional]
  void DoSomething(object arg);
  [Transactional(IsolationLevel = IsolationLevel.Serializable)]
  Task<int> DoSomethingAsync();
}
```
`TransactionAspect` requires the `IDbConnection` service to be installed:
```csharp
using System.Data;

webServiceBuilder
  .ConfigureRpcService(conf => (conf as Modules)?.Register<IModule, Module>())
  .ConfigureServices(services => services.Factory<IDbConnection>(injector => /*create a new db connection*/, Lifetime.Scoped));
```
## How to listen on HTTPS (Windows only)
Requires [this](https://raw.githubusercontent.com/Sholtee/rpc.boilerplate/master/BUILD/cert.ps1 ) script to be loaded (`.(".\cert.ps1")`)
1. If you don't have your own, create a self-signed certificate
   ```ps
   Create-SelfSignedCertificate -OutDir ".\Cert" -Password "cica"
   ```
2. Register the certificate
   ```ps
   Bind-Certificate -P12Cert ".Cert\certificate.p12" -Password "cica" -IpPort "127.0.0.1:1986"
   ```
## Resources
[API docs](https://sholtee.github.io/rpc )

[Version history](https://github.com/Sholtee/rpc/tree/master/history.md )

[Server boilerplate](https://github.com/Sholtee/rpc.boilerplate ) (comprehensive)

[Sample server](https://github.com/Sholtee/rpc/tree/master/TEST/RPC.Server.Sample ) (used in tests)

[Benchmark results](https://sholtee.github.io/rpc/perf/ )