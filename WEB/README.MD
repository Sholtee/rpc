# RpcDotNet-Connector
> This package provides the functionality for invoking [RPC.NET](https://github.com/Sholtee/rpc ) services.

**This documentation refers the version 3.X of the library**
## How to use it
1. Reference this library via *jsdelivr*: `<script src="https://cdn.jsdelivr.net/npm/rpcdotnet-connector@3.0.0-preview1/dist/apiconnector.js"></script>` or install as an *NPM* package: `npm install --save-prod rpcdotnet-connector@3.0.0-preview1 `
2. Create an API connection:
   ```js
   const { ApiConnection } = window.apiconnector;  // or "import { ApiConnection } from 'rpcdotnet-connector'" 
   ...
   const conn = new ApiConnection('http://127.0.0.1:1986/api');
   // Setting the following properties is not required
   conn.sessionId = 'xXx';
   conn.timeout = 5000; // ms
   ```
   Optionally you can hook into the invocation process for instance, to bind your [loader](https://loading.io/css/ ):
   ```js
   conn.onFetch(async function(...args) {
     _yourLoaderService.activated++;
     try {
       return await this(...args);
     } finally {
       _yourLoaderService.activated--;
     }
   });
   ```
3. Invoke the remote module:
   ```js
   // every method invocations are async
   const result = await conn.invoke('ICalculator' /*module*/, 'Add' /*method*/, [1, 2] /*args*/);
   ```
## Schema API
In practice the `invoke()` method is considered internal. So instead of it, we may use the schema API that creates a "well defined" client against the schema provided by the remote server:
```js
const calculator = await conn.createAPI('ICalculator');
...
await calculator.add(1, 2); // by default "camelCase" naming convention is used
```
FYI: The schema itself looks like:
```json
{
  "ICalculator": {
    "Methods": {
      "Add": {...},
      "ParseInt": {...}
    },
    "Properties": {
        "PI": {...}
    }
  }
}
```
## Version history
Can be found [here](https://github.com/Sholtee/rpc/tree/master/WEB/history.md ).