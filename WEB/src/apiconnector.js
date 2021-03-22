/********************************************************************************
*  apiconnector.js                                                              *
*  Author: Denes Solti                                                          *
********************************************************************************/
export const
    RESPONSE_NOT_VALID = 'Server response could not be processed',
    REQUEST_TIMED_OUT = 'Request timed out';

// class
export function ApiConnectionFactory(urlBase, /*can be mocked*/ fetch = window.fetch) {
  Object.assign(this, {
    sessionId: null,
    headers: {},
    timeout: 0,
    invoke: overridable(this, invoke),
    createConnection: overridable(this, createConnection)
  });

  Object.defineProperty(this, 'serviceVersion', {
    enumerable: false,
    get() {
      return this.invoke('IServiceDescriptor', 'get_Version');
    }
  });

  /* eslint-disable no-invalid-this */
  function invoke(module, method, args = []) {
    const url = new URL(urlBase);

    url.searchParams.append('module', module);
    url.searchParams.append('method', method);

    if (this.sessionId)
      url.searchParams.append('sessionId', this.sessionId);

    const post = fetch(url.toString(), {
      method: 'POST',
      headers: {...this.headers, 'Content-Type': 'application/json'},
      body: JSON.stringify(args)
    });

    return (this.timeout <= 0 ? post : addTimeout(post, this.timeout)).then(response => {
      if (!response.ok)
        //
        // Biztonsagos a then() agban kivetelt dobni: https://javascript.info/promise-error-handling
        //

        // eslint-disable-next-line no-throw-literal
        throw `Status inappropriate: ${response.status} (${response.statusText})`;

      switch (response.headers.get('Content-Type').toLowerCase()) {
        case 'text/html':
          //
          // Nem gond h Promise-t adunk vissza: https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Promise/then
          //

          return response.text().then(txt => {
            throw txt;
          });
        case 'application/json':
          return response.json().then(json => {
            if (typeof json === 'object') {
              const exception = getProp(json, 'Exception');
              if (exception)
                throw  exception;

              //
              // NULL is jo ertek
              //

              const result = getProp(json, 'Result');
              if (typeof result !== 'undefined')
                return result;
            }
            throw RESPONSE_NOT_VALID;
          });
        default:
          throw RESPONSE_NOT_VALID;
      }
    });

    function addTimeout(promise, timeout) {
      let handle;
      return Promise
        .race([
          promise,
          new Promise((_, reject) => {
            handle = setTimeout(() => reject(REQUEST_TIMED_OUT), timeout);
          })
        ])
        .finally(() => clearTimeout(handle));
    }

    function getProp(obj, prop) { // nem kis-nagy betu erzekeny
      let key;
      for (key in obj) {
        if (key.toLowerCase() === prop.toLowerCase()) {
          return obj[key];
        }
      }
    }
  }

  function createConnection(module) {
    const owner = this;

    return Object.assign(function ApiConnection() {}, {
      registerMethod(name, alias) {
        this.prototype[alias || name] = (...args) => owner.invoke(module, name, args);

        //
        // A hivasok lancba fuzhetoek legyenek
        //

        return this;
      },
      registerProperty(name, alias) {
        Object.defineProperty(this.prototype, alias || name, {
          enumerable: true,
          get: () =>  owner.invoke(module, `get_${name}`)

          //
          // Direkt nincs setter, mivel azon nem tudnank await-elni
          //
        });
        return this;
      },
      module
    });
  }

  function overridable(obj, fn) {
    return Object.assign(fn, {
      decorate: decorate.bind(obj, fn.name)
    });

    function decorate(fn, newFn) {
      const oldFn = this[fn];

      Object.defineProperty(decorator, 'name', {value: fn}); // "decorator.name = fn" nem mukodik
      overridable(this, this[fn] = decorator);

      function decorator(...args) {
        this.$base = oldFn;
        try {
          return newFn.apply(this, args);
        } finally {
          delete this.$base;
        }
      }
    }
  }
  /* eslint-enable no-invalid-this */
}