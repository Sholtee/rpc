# RpcDotNet-Connector
> This package provides the functionality for invoking [RPC.NET](https://github.com/Sholtee/rpc ) services.

## How to use it
1. Reference this package via `jsdelivr`: `<script src="https://cdn.jsdelivr.net/npm/rpcdotnet-connector@1.x.x/dist/rpcdotnet-connector.min.js"></script>`
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