# RPC.NET [![Build status](https://ci.appveyor.com/api/projects/status/sqgld5a86pha51wf/branch/master?svg=true)](https://ci.appveyor.com/project/Sholtee/rpc/branch/master) ![AppVeyor tests](https://img.shields.io/appveyor/tests/sholtee/rpc) [![Coverage Status](https://coveralls.io/repos/github/Sholtee/rpc/badge.svg?branch=master)](https://coveralls.io/github/Sholtee/rpc?branch=master)
> W.I.P.

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
1. Install the [RPC.NET.Server]() package. Since modules are stored in a `IServiceContainer` you may need to install the [Injector.NET](https://www.nuget.org/packages/injector.net/ ) package as well.
2. Define an interface and implementation for your module:
   ```csharp
   public interface ICalculator 
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
3. Create the service `exe`:
   ```csharp
   using System;
   using Solti.Utils.DI;
   using Solti.Utils.RPC;
   
   class Program
   {
     static void Main(string[] args)
     {
       using var service = new RpcService(new ServiceContainer());
       service.Register<ICalculator, Calculator>();
       service.Start("http://127.0.0.1:1986/api/");
	   
       Console.WriteLine("Press any key to terminate the server... ");
	   Console.ReadLine();
     }
   }
   ```


## Resources
[API docs](https://sholtee.github.io/rpc )

[Benchmark results](https://sholtee.github.io/rpc/perf/ )