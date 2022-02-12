/********************************************************************************
*  apiconnector.js                                                              *
*  Author: Denes Solti                                                          *
********************************************************************************/
export const
  RESPONSE_NOT_VALID = 'Server response could not be processed',
  REQUEST_TIMED_OUT = 'Request timed out',
  SCHEMA_NOT_FOUND = 'Schema could not be found';

export class ApiConnection {
  #urlBase;
  #resultProp;
  #exceptionProp;
  #propFmt;

  sessionId = null;
  headers = {};
  timeout = 0;

  constructor(urlBase, propFmt = ApiConnection.#startWithLowerCase) {
    this.#urlBase = urlBase;
    this.#resultProp = propFmt('Result');
    this.#exceptionProp = propFmt('Exception');
    this.#propFmt = propFmt;
  }

  async invoke(module, method, args = []) {
    const url = new URL(this.#urlBase);

    url.searchParams.append('module', module);
    url.searchParams.append('method', method);

    if (this.sessionId != null)
      url.searchParams.append('sessionId', this.sessionId);

    const response = await this.#fetch(url.toString(), {
      method: 'POST',
      headers: {...this.headers, 'Content-Type': 'application/json'},
      body: JSON.stringify(args)
    });

    //
    // Babel would generate enormous amount of helper code just because of this simple switch.
    //

    /*
    switch (response.headers.get('Content-Type').toLowerCase()) {
      case 'application/json': {
        const data = ApiConnection.#loadJSON(await response.text(), this.#propFmt);
        if (typeof data !== 'object')
          break;

        const exception = data[this.#exceptionProp];
        if (exception)
          throw exception;

        return data[this.#resultProp];
      }
      case 'application/octet-stream': {
        return response.body;
      }
    }
    throw RESPONSE_NOT_VALID;
    */

    const contentType = response.headers.get('Content-Type').toLowerCase();
    if (contentType === 'application/json') {
      const data = ApiConnection.#loadJSON(await response.text(), this.#propFmt);
      if (typeof data === 'object') {
        const exception = data[this.#exceptionProp];
        if (exception)
          throw exception;

        return data[this.#resultProp];
      }
    } else if (contentType === 'application/octet-stream')
      return response.body;

    throw RESPONSE_NOT_VALID;
  }

  decorateInvoke(newFn) { // TODO: fetch()-re
    const baseFn = this.invoke;
    this.invoke = Object.defineProperty(decorator, 'name', {value: baseFn.name}); // "decorator.name = ..." won't work

    return this;

    function decorator(...args) {
      /* eslint-disable no-invalid-this */
      return newFn.apply(this, [
        ...args,
        (...baseArgs) => baseFn.apply(this, baseArgs)
      ]);
      /* eslint-enable no-invalid-this */
    }
  }

  async createAPI(module) {
    const url = new URL(this.#urlBase);

    url.searchParams.append('module', module);

    const response = await this.#fetch(url.toString(), { method: 'GET' });
    if (response.headers.get('Content-Type').toLowerCase() !== 'application/json')
      throw RESPONSE_NOT_VALID;

    //
    // Don't use prop_fmt so the method and property names remain untouched
    //

    const schema = ApiConnection.#loadJSON(await response.text())[module];
    if (!schema)
      throw SCHEMA_NOT_FOUND;

    for (const [method] of Object.entries(schema.Methods)) {
      API.prototype[method] = function(...args) {
        return this.$connection.invoke(module, method, args);
      };
    }

    for (const [property, descriptor] of Object.entries(schema.Properties)) {
      //
      // Setters are not supported since setting a property value cannot return a Promise.
      //

      if (!descriptor.HasGetter)
        continue;

      Object.defineProperty(API.prototype, property, {
        configurable: false,
        enumerable: true,
        get: function() {
          return this.$connection.invoke(module, `get_${property}`, []);
        }
      });
    }

    return new API(this);

    function API(connection) {
      this.$connection = connection;
    }
  }

  static #loadJSON(str, fmt) {
    return JSON.parse(str, !fmt ? undefined : function(key, value) {
      /* eslint-disable no-invalid-this */
      if (typeof this === 'object' && !Array.isArray(this) && key) {
        this[fmt(key)] = value;

        //
        // By returning undefined, the original property will be removed:
        // https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/JSON/parse#using_the_reviver_parameter
        //

        return;
      }
      /* eslint-enable no-invalid-this */
      return value;
    });
  }

  async #fetch(url, options) {
    const response = await this.#addTimeout(fetch(url, options));

    if (!response.ok)
      throw (await response.text()) || response.statusText;

    return response;
  }

  //
  // This method should not be inlined otherwise, a malformed code will be created.
  //

  async #addTimeout(promise) {
    if (!this.timeout)
      return promise; // It's fine to return the promise itself

    let
      handle,
      rto = new Promise((_, reject) => {
        handle = setTimeout(() => reject(REQUEST_TIMED_OUT), this.timeout);
      });

    console.assert(handle);

    try {
      return await Promise.race([promise, rto]);
    } finally {
      //
      // Promise.prototype.finally() is available since Chrome 63 so let Babel do its work
      //

      clearTimeout(handle);
    }
  }

  static #startWithLowerCase(str) {
    if (str.length >= 2 && isUpper(str[0]) && !isUpper(str[1])) {
      str[0] = str[0].toLowerCase();
    }
    return str;

    function isUpper(chr) { return /[A-Z]/.test(chr); }
  }
}