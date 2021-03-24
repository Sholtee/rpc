# RpcDotNet-Connector
> This package provides the functionality for invoking [RPC.NET](https://github.com/Sholtee/rpc ) services.

**This documentation refers the version 2.X of the library**
## How to use it
1. Reference this library via *jsdelivr*: `<script src="https://cdn.jsdelivr.net/npm/rpcdotnet-connector@2.x.x/dist/apiconnector.js"></script>` or install as an *NPM* package: `npm install --save-prod rpcdotnet-connector`
2. Create a connection factory:
   ```js
   const {ApiConnectionFactory} = window.apiconnector;  // or "import {ApiConnectionFactory} from 'rpcdotnet-connector'" 
   ...
   const factory = new ApiConnectionFactory('http://127.0.0.1:1986/api');
   ```
   Optionally you can hook into the invocation process for instance, to bind your [loader](https://loading.io/css/ ):
   ```js
   ApiConnectionFactory.decorate('invoke', async function(...args) {
     _yourLoaderService.activated++;
     try {
       return await this.$base(...args);
     } finally {
       _yourLoaderService.activated--;
     }
   });
   ```
3. Set up the API connection:
   ```js
   const Calculator = factory.createConnection('ICalculator');
   Calculator
     .registerMethod('Add' /*remote method name*/, 'add' /*optional local alias*/, [Number, Number] /*optional layout*/)
     .registerProperty('PI');
   ```
   Optionally you may decorate the registered methods:
   ```js
   Calculator.decorate('add', function(...args) {
     // do some cool stuff here
     return this.$base(...args);
   });
   ```
4. Invoke the API:
   ```js
   const calculator = new Calculator();
   // every method invocations are async
   calculator.add(1, 1).then(result => {...});  // or "let result = await calculator.add(1, 1);"
   const PI = await calculator.PI;
   ```
## Schema API
A schema describes the layout of the remote API. It can be a JavaScript object, or a JSON file:
```json
{
  "urlBase": "http://localhost:1986/api",
  "modules": {
    "ICalculator": {
      "alias": "calculator",
      "methods": {
        "Add": {
          "alias": "add",
          "layout": ["Number", "Number"]
        }
      },
      "properties": {
         "PI": true
      }
    }
  }
}
```
Loading the schema is just a simple line:
```js
const api = ApiConnectionFactory.fromSchema(schemaObject); // NOT async
// or
const api = await ApiConnectionFactory.fromSchema('path/to/schema.json');
...
let result = await api.calculator.add(1, 5);
```
### Version history
Can be found [here](https://github.com/Sholtee/rpc/tree/master/WEB/history.md ).