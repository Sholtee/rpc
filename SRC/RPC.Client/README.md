# RPC.NET Client 
> Strongly typed client to invoke [RPC.NET servers](https://github.com/Sholtee/rpc/tree/master/SRC/RPC.Server )

**This documentation refers the version 6.X of the library**

|Name|Package|
|:--:|:--:|
|**RPC.NET.Interfaces**|[![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/rpc.net.interfaces)](https://www.nuget.org/packages/rpc.net.interfaces )|
|**RPC.NET.Client**|[![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/rpc.net.client)](https://www.nuget.org/packages/rpc.net.client )|
## How it works
1. The server exposes [modules](https://github.com/Sholtee/rpc/tree/master/SRC/RPC.Server#modules ) having well defined layout (service interface). These interfaces are placed in a separate project (for instance [this](https://github.com/Sholtee/rpc/blob/master/TEST/RPC.Server.Sample.Interfaces/ICalculator.cs )) so the client app may grab it.
2. The client [generates](https://github.com/Sholtee/proxygen ) a [proxy](https://en.wikipedia.org/wiki/Proxy_pattern ) against the module interface.
3. When a proxy method is called, the underlying implementation invokes the backend in the way described [here](https://github.com/Sholtee/rpc/blob/master/SRC/RPC.Server/README.md#how-it-works ).
## Client example
This example invokes the [server](https://github.com/Sholtee/rpc/blob/master/TEST/RPC.Server.Sample.Interfaces/ICalculator.cs ) used in tests.
```csharp
using Solti.Utils.Rpc;
using Solti.Utils.Rpc.Server.Sample.Interfaces;
...
using RpcClientFactory factory = new("http://127.0.0.1:1986/api/");
ICalculator calculator = await factory.CreateClient<ICalculator>();
try
{
  int result = await calculator.AddAsync(1, 2);
}
catch(RpcException ex)
{
  // ex.InnerException will contain the original exception
}
```
## Additional resources
[API docs](https://sholtee.github.io/rpc )

[Version history](https://github.com/Sholtee/rpc/tree/master/history.md )