/********************************************************************************
*  apiconnector.js                                                              *
*  Author: Denes Solti                                                          *
********************************************************************************/
export const
    RESPONSE_NOT_VALID = 'Server response could not be processed',
    REQUEST_TIMED_OUT = 'Request timed out';

// class
export function ApiConnectionFactory(urlBase, /*can be mocked*/ xhrFactory = () => new XMLHttpRequest()) {
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
    return new Promise((resolve, reject) => {
      const url = new URL(urlBase);
      url.searchParams.append('module', module);
      url.searchParams.append('method', method);

      if (this.sessionId)
        url.searchParams.append('sessionId', this.sessionId);

      const xhr = xhrFactory();

      xhr.open('POST', url.toString(), true);
      xhr.timeout = this.timeout;

      for (const [key, value] of Object.entries({...this.headers, 'Content-Type': 'application/json'})) {
        xhr.setRequestHeader(key, value.toString());
      }

      xhr.onload = onResponse.bind(xhr, resolve, reject);
      xhr.onerror = onError.bind(xhr, reject);
      xhr.ontimeout = onTimeout.bind(xhr, reject);

      xhr.send(JSON.stringify(args));
    });

    function onError(reject) {
      reject(this.statusText);
    }

    function onTimeout(reject) {
      reject(REQUEST_TIMED_OUT);
    }

    function onResponse(resolve, reject) {
      const response = this.response || this.responseText;

      if (this.status !== 200) {
        reject(`Status inappropriate: ${this.status} (${this.statusText})`);
        return;
      }

      switch (this.getResponseHeader('Content-Type')) {
        case 'text/html': {
          reject(response);
          break;
        }
        case 'application/json': {
          const parsed = JSON.parse(response);

          if (typeof parsed === 'object') {
            const exception = getProp(parsed, 'Exception');

            if (exception) {
              reject(exception);
              break;
            }

            //
            // NULL is jo ertek
            //

            const result = getProp(parsed, 'Result');

            if (typeof result !== 'undefined') {
              resolve(result);
              break;
            }
          }

          //
          // Nem kell break, tudatos
          //
        }
        // eslint-disable-next-line no-fallthrough
        default: {
          reject(RESPONSE_NOT_VALID);
          break;
        }
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