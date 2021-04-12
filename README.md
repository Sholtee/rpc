# RPC.NET [![Build status](https://ci.appveyor.com/api/projects/status/sqgld5a86pha51wf/branch/master?svg=true)](https://ci.appveyor.com/project/Sholtee/rpc/branch/master) ![AppVeyor tests](https://img.shields.io/appveyor/tests/sholtee/rpc/master) [![Coverage Status](https://coveralls.io/repos/github/Sholtee/rpc/badge.svg?branch=master)](https://coveralls.io/github/Sholtee/rpc?branch=master) ![GitHub last commit (branch)](https://img.shields.io/github/last-commit/sholtee/rpc/master)
> Simple, lightweight RPC implementation for .NET

**This documentation refers the version 3.X of the library**

|Name|Package|
|:--:|:--:|
|**RPC.NET.Interfaces**|[![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/rpc.net.interfaces)](https://www.nuget.org/packages/rpc.net.interfaces )|
|**RPC.NET.Client**|[![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/rpc.net.client)](https://www.nuget.org/packages/rpc.net.client )|
|**RPC.NET.Server**|[![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/rpc.net.server)](https://www.nuget.org/packages/rpc.net.server )|
|**RPC.NET-Connector**|[![npm version](https://badge.fury.io/js/rpcdotnet-connector.svg)](https://badge.fury.io/js/rpcdotnet-connector)|
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
## Server example
1. Install the [RPC.NET.Server](https://www.nuget.org/packages/rpc.net.server ) package. Since modules are stored in a `IServiceContainer` you may need to install the [Injector.NET](https://www.nuget.org/packages/injector.net/ ) package as well.
2. Define an interface and implementation for your module:
   ```csharp
   public interface ICalculator // Since clients may want to use it as well, it may be worth to put this interface into a common assembly
   {
     int Add(int a, int b);
     Task<int> AddAsync(int a, int b); // async methods also supported
     double PI { get; }
   }
   ...
   public class Calculator : ICalculator 
   {
     private readonly IRequestContext FContext;
     // You can access the request context as a dependency
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
   There are some control attributes that can be applied on (module) interface methods:
   - `AliasAttribute`: Specifies the alias of the method. Useful if your module has overloaded methods.
   - `IgnoreAttribute`: Marks the method "remotely invisible".
   - [Aspects](https://github.com/Sholtee/injector#aspects ) are also supported. The built-in aspects are the followings:
     - `ParameterValidatorAspectAttribute`:
	 ```csharp
     [ParameterValidatorAspect]
     public interface IModule
     {
	   void DoSomething([NotNull, Match("cica", ParameterValidationErrorMessage = "ooops")] string arg1, [NotNull] object arg2);
       void DoSomethingElse();
       void ConditionallyValidated([NotNull(Condition = typeof(IfLoggedIn))] string arg);
     }

     public enum MyRoles
     {
       Anonymous = 0,
       LoggedInUser = 1
     }

     public class IfLoggedIn : IConditionalValidatior
     {
       public bool ShouldRun(MethodInfo containingMethod, IInjector currentScope) =>
         currentScope.Get<IRoleManager>().GetAssignedRoles(null).Equals(MyRoles.LoggedInUser);
     }
	 ```
	 The complete list of available parameter/property validators are [here](https://github.com/Sholtee/rpc/tree/master/SRC/RPC.Interfaces/Attributes/Aspects/ParameterValidation )
	 
     - `TransactionAspectAttribute`:
     ```csharp
     [TransactionAspect]
     public interface IModule
     {
       void NonTransactional();
       [Transactional]
       void DoSomething(object arg);
       [Transactional]
       void DoSomethingFaulty();
       [Transactional(IsolationLevel = IsolationLevel.Serializable)]
       Task<int> DoSomethingAsync();
     }
	 ```
	 - `RoleValidatorAspectAttribute`:
	 ```csharp
     [Flags]
     public enum MyRoles
     {
       Anonymous = 0,
       User = 1,
       MayPrint = 2,
       Admin = 4
     }

     [RoleValidatorAspect] // to usse this aspect you have to implement and register the IRoleManager interface
     public interface IModule
     {
       [RequiredRoles(MyRoles.User | MyRoles.MayPrint, MyRoles.Admin)]
       void Print();
       [RequiredRoles(MyRoles.User | MyRoles.MayPrint, MyRoles.Admin)]
       Task<string> PrintAsync();
       [RequiredRoles(MyRoles.Anonymous)]
       void Login();
       void MissingRequiredRoleAttribute();
     }	 
	 ```
	 
     Note that these aspects are [naked](https://github.com/Sholtee/injector#naked-aspects )
   
   These attributes are provided by the [RPC.NET.Interfaces](https://www.nuget.org/packages/rpc.net.interfaces ) package.
3. Create the service `exe`:
   ```csharp
   using System;
   using Solti.Utils.DI;
   using Solti.Utils.Rpc;
   
   class Program
   {
     static void Main(string[] args)
     {
       using var container = new ServiceContainer();
       using var service = new RpcService(container);
	   
       service.Register<ICalculator, Calculator>();
       service.Start("http://127.0.0.1:1986/api/");
	   
       Console.WriteLine("Press any key to terminate the server... ");
	   Console.ReadLine();
     }
   }
   ```
## Client example
1. Install the [RPC.NET.Client](https://www.nuget.org/packages/rpc.net.client) package.
2. Reference the assembly that contains the module interface you want to use.
3. Create the client:
   ```csharp
   using Solti.Utils.Rpc;
   ...
   using var factory = new RpcClientFactory("http://127.0.0.1:1986/api/");
   ICalculator calculator = await factory.CreateClient<ICalculator>();
   try
   {
     int result = await calculator.AddAsync(1, 2);
   } catch(RpcException ex) {
     // ex.InnerException will contain the original exception
   }
   ```
## JS client example
See [here](https://github.com/Sholtee/rpc/blob/master/WEB/README.MD )
## Hosting the server
1. Create a console project that will host your app:
   ```csharp
   using Solti.Utils.Rpc;
   using Solti.Utils.Rpc.Hosting;
   using Solti.Utils.DI.Interfaces;
   ...
   public class CalculatorHost : AppHostBase
   {
     // You may need to set more properties (see documentation). These two are mandatory:
	 
     public override string Name => "Calculator";

     public override string Url => "http://127.0.0.1:1986/api/";

     public override void OnRegisterModules(IModuleRegistry registry)
     {
       base.OnRegisterModules(registry);
       registry.Register<ICalculator, Calculator>();
     }
	 
     public override void OnRegisterServices(IServiceCOntainer container)
     {
       base.OnRegisterServices(container);
	   // Register app dependencies here. For more information about DI see: https://github.com/Sholtee/injector
     }
   }
   ```
2. Into `Program.cs` simply put the followings:
   ```csharp
   using Solti.Utils.Rpc.Hosting;
   ...
   class Program
   {
     static void Main() => HostRunner.Run<CalculatorHost>();
   }
   ```
3. The compiled executable can be used in several ways:
   - You can simply run it to debug your app (Ctrl-C terminates the server)
   - You can invoke it with `-install` to install your app as a local service (`-uninstall` does the opposite)
   - It can run as a local service (started by [SCM](https://docs.microsoft.com/en-us/windows/win32/services/service-control-manager )) - if it was installed previously
## How to listen on HTTPS (Windows only)
Requires [this](https://github.com/Sholtee/rpc.boilerplate/blob/master/cert.ps1 ) script to be loaded (`.(".\cert.ps1")`)
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

[Tests](https://github.com/Sholtee/rpc/blob/master/TEST/RPC.Tests/Rpc.cs ) (remote module invocation related)

[Benchmark results](https://sholtee.github.io/rpc/perf/ )