# RPC.NET [![Build status](https://ci.appveyor.com/api/projects/status/sqgld5a86pha51wf/branch/master?svg=true)](https://ci.appveyor.com/project/Sholtee/rpc/branch/master) ![AppVeyor tests](https://img.shields.io/appveyor/tests/sholtee/rpc) [![Coverage Status](https://coveralls.io/repos/github/Sholtee/rpc/badge.svg?branch=master)](https://coveralls.io/github/Sholtee/rpc?branch=master)
> Simple, lightweight RPC implementation for .NET

|Name|Package|
|:--:|:--:|
|**RPC.NET.Attributes**|[![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/rpc.net.attributes)](https://www.nuget.org/packages/rpc.net.attributes )|
|**RPC.NET.Client**|[![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/rpc.net.client)](https://www.nuget.org/packages/rpc.net.client )|
|**RPC.NET.Server**|[![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/rpc.net.server)](https://www.nuget.org/packages/rpc.net.server )|
|**RPC.NET-Connector**|[![npm version](https://badge.fury.io/js/rpcdotnet-connector.svg)](https://badge.fury.io/js/rpcdotnet-connector)|
## How it works
1. The client sends a *HTTP POST* to the server where 
   - The request *URI*
     - Must use *HTTP* or *HTTPS* scheme
     - Identifies the remote *module* and *method* (in the query component)
     - May contain the *sessionid* (in the query component)
   
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
   }
   ...
   public class Calculator : ICalculator 
   {
     public int Add(int a, int b) => a + b;
     public Task<int> AddAsync(int a, int b) => Task.FromResult(a + b);
   }
   ```
   There are some control attributes that can be applied on (module) interface methods:
   - `AliasAttribute`: Specifies the alias of the method. Useful if your module has overloaded methods.
   - `IgnoreAttribute`: Marks the method "remotely invisible".
   - `MayRunLongAttribute`: Marks a method as long running. Using this attribute makes sense only if the method has non `Task` return value.
   
   These attributes are provided by the [RPC.NET.Attributes](https://www.nuget.org/packages/rpc.net.attributes ) package.
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
2. Reference the assembly that contains the interface of the module you want to use.
3. Create the client:
   ```csharp
   using Solti.Utils.Rpc;
   ...
   using var client = new RpcClient<ICalculator>("http://127.0.0.1:1986/api/");
   try
   {
     int result = await client.Proxy.AddAsync(1, 2));
   } catch(RpcException ex) {
     // ex.InnerException will contain the original exception
   }
   ```
## JS client example
1. Reference the [rpcdotnet-connector]( )  package: `<script src="https://cdn.jsdelivr.net/npm/rpcdotnet-connector@1.x.x/dist/rpcdotnet-connector-1.x.x.min.js"></script>`
2. Create a connection factory:
   ```js
   let factory = new ApiConnectionFactory('http://127.0.0.1:1986/api/');
   ```
3. Set up the API connection:
   ```js
   const Calculator = factory.createConnection('ICalculator');
   Calculator.registerMethod('Add' /*remote method name*/, 'add' /*optional local alias*/);
   ```
4. Invoke the API:
   ```js
   const calculator = new Calculator();
   // every method invocations are async
   calculator.add(1, 1).then(result => {...});  // or "let result = await calculator.add(1, 1);"
   ```
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
   class Program
   {
     static void Main()
     {
       using CalculatorHost appHost = new CalculatorHost();
       appHost.Runner.Start(); // blocks until your app terminates
     }
   }
   ```
3. The compiled executable can be used in several ways:
   - You can simply run it to debug your app (Ctrl-C terminates the server)
   - You can invoke it with `-install` to install your app as a local service (`-uninstall` does the opposite)
   - It can run as a local service (started by [SCM](https://docs.microsoft.com/en-us/windows/win32/services/service-control-manager )) - if it was installed previously
## Resources
[API docs](https://sholtee.github.io/rpc )

[Examples](https://github.com/Sholtee/rpc/blob/master/TEST/RPC.Tests/Rpc.cs )

[Sample server](https://github.com/Sholtee/rpc/tree/master/TEST/RPC.Server.Sample )

[Benchmark results](https://sholtee.github.io/rpc/perf/ )