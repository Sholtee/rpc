# RpcDotNet-Connector
> This package provides the functionality for invoking [RPC.NET](https://github.com/Sholtee/rpc ) services.

## How to use it
1. Create a connection factory:
   ```js
   let factory = new ApiConnectionFactory('http://127.0.0.1:1986/api/');
   ```
2. Set up the API connection:
   ```js
   const Calculator = factory.createConnection('ICalculator');
   Calculator.registerMethod('Add' /*remote method name*/, 'add' /*optional local alias*/);
   ```
3. Invoke the API:
   ```js
   const calculator = new Calculator();
   // every method invocations are async
   calculator.add(1, 1).then(result => {...});  // or "let result = await calculator.add(1, 1);"
   ```