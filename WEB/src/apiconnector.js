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
  #fetchImpl = (...args) => this.#fetch(...args);
  #invokeImpl = (...args) => this.#invoke(...args);

  sessionId = null;
  timeout = 0;

  constructor(urlBase, propFmt = ApiConnection.#startWithLowerCase) {
    this.#urlBase = urlBase;
    this.#resultProp = propFmt('Result');
    this.#exceptionProp = propFmt('Exception');
    this.#propFmt = propFmt;
  }

  invoke(module, method, args = []) {
    return this.#invokeImpl(module, method, args);
  }

  async createAPI(module) {
    const url = new URL(this.#urlBase);

    url.searchParams.append('module', module);

    const response = await this.#fetchImpl(url.toString(), { method: 'GET' });
    if (response.headers.get('Content-Type').toLowerCase() !== 'application/json')
      throw RESPONSE_NOT_VALID;

    //
    // Don't use prop_fmt so the method and property names remain untouched
    //

    const schema = ApiConnection.#loadJSON(await response.text())[module];
    if (!schema)
      throw SCHEMA_NOT_FOUND;

    for (const [method] of Object.entries(schema.Methods)) {
      API.prototype[this.#propFmt(method)] = function(...args) {
        return this.$connection.invoke(module, method, args);
      };
    }

    for (const [property, descriptor] of Object.entries(schema.Properties)) {
      //
      // Setters are not supported since setting a property value cannot return a Promise.
      //

      if (!descriptor.HasGetter)
        continue;

      Object.defineProperty(API.prototype, this.#propFmt(property), {
        configurable: false,
        enumerable: true,
        get: function() {
          return this.$connection.invoke(module, `get_${property}`, []);
        }
      });
    }

    //
    // "API.name = ..." won't work
    //

    const Class = Object.defineProperty(API, 'name', {value: `${module}_API`});

    return new Class(this);

    function API(connection) {
      this.$connection = connection;
    }
  }

  onFetch(fn) {
    this.#fetchImpl = ApiConnection.#chain(this.#fetchImpl, fn);
  }

  onInvoke(fn) {
    this.#invokeImpl = ApiConnection.#chain(this.#invokeImpl, fn);
  }

  async #invoke(module, method, args) {
    const url = new URL(this.#urlBase);

    url.searchParams.append('module', module);
    url.searchParams.append('method', method);

    if (this.sessionId != null)
      url.searchParams.append('sessionId', this.sessionId);

    const response = await this.#fetchImpl(url.toString(), {
      method: 'POST',
      headers: {'Content-Type': 'application/json'},
      body: JSON.stringify(args)
    });

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

      //
      // Without this line, Babel would create erroneous output
      //

      default:
        break;
    }
    throw RESPONSE_NOT_VALID;
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

  static #chain(oldFn, newFn) {
    return (...args) => newFn.apply(oldFn, args);
  }

  static #startWithLowerCase(str) {
    //
    // "str[x] = ..." throws in strict mode
    //

    if (str.length >= 2) {
      const [first, second, ...rest] = str;
      if (isUpper(first) && !isUpper(second)) {
        return [first.toLowerCase(), second, ...rest].join('');
      }
    }

    return str;

    function isUpper(chr) { return /[A-Z]/.test(chr); }
  }
}