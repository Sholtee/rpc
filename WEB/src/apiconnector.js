/********************************************************************************
*  apiconnector.js                                                              *
*  Author: Denes Solti                                                          *
********************************************************************************/
export const
  STATUS_NOT_VALID = 'Inappropriate status: {0} ({1})',
  RESPONSE_NOT_VALID = 'Server response could not be processed',
  REQUEST_TIMED_OUT = 'Request timed out',
  SIGNATURE_NOT_MATCH = 'The elements of the parameters array do not match the signature of the method';

//
// Ezeket a prototipuson definialjuk h dekoralhatoak legyenek
//

Object.assign(ApiConnectionFactory.prototype, {
  invoke(module, method, args = []) {
    const url = new this.$backend.URL(this.$urlBase);

    url.searchParams.append('module', module);
    url.searchParams.append('method', method);

    if (this.sessionId != null)
      url.searchParams.append('sessionId', this.sessionId);

    const post = this.$backend.fetch(url.toString(), {
      method: 'POST',
      headers: {...this.headers, 'Content-Type': 'application/json'},
      body: JSON.stringify(args)
    });

    return (this.timeout <= 0 ? post : addTimeout(post, this.timeout)).then(response => {
      if (!response.ok)
        //
        // Biztonsagos a then() agban kivetelt dobni: https://javascript.info/promise-error-handling
        //

        throw format(STATUS_NOT_VALID, response.status, response.statusText);

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
                throw exception;

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
  },
  createConnection(module) {
    const owner = this;

    return Object.assign(function ApiConnection() {}, {
      registerMethod(name, alias, layout) {
        const fnName = alias || name;

        this.prototype[fnName] = Object.defineProperty(invoke, 'name', {value: fnName});
        if (layout)
          this.decorate(fnName, validateLayout);

        return this; // A hivasok lancba fuzhetoek legyenek

        function invoke(...args) {
          return owner.invoke(module, name, args);
        }

        function validateLayout(...args) {
          return args.length !== layout.length || args.some(invalid)
            ? Promise.reject(SIGNATURE_NOT_MATCH)
            // eslint-disable-next-line no-invalid-this
            : this.$base(...args);

          function invalid(arg, i) {
            const type = typeof layout[i] === 'function' ? layout[i] : window[layout[i]];
            if (!type) return true;

            //
            // - NULL-t mindig valid erteknek tekintjuk
            // - "instanceof" nem mukodik String es Number ertekekre (ha csak az nem new String(...)-el vt letrehozva)
            //

            return arg != null && !(arg instanceof type) && typeof arg !== type.name.toLowerCase();
          }
        }
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
      decorate,
      module
    });
  }
});

Object.assign(ApiConnectionFactory, {
  fromSchema(config, {fetch = window.fetch, URL = window.URL}) {
    return typeof config === 'string'
      ? fetch(config).then(response => response.json()).then(createApi)
      : createApi(config);

    function createApi({urlBase, modules = {}}) {
      const
        factory = new ApiConnectionFactory(urlBase, {fetch, URL}),
        api = {$factory: factory};

      Object.entries(modules).forEach(([module, descriptor]) => {
        const ModuleType = factory.createConnection(module);

        if (descriptor.methods)
          Object.entries(descriptor.methods).forEach(([method, {alias}]) =>
            ModuleType.registerMethod(method, alias));

        if (descriptor.properties)
          Object.entries(descriptor.properties).forEach(([property, {alias}]) =>
            ModuleType.registerProperty(property, alias));

        api[descriptor.alias || module] = new ModuleType();
      });

      return api;
    }
  },
  decorate
});

// class
export function ApiConnectionFactory(urlBase, /*can be mocked*/ {fetch = window.fetch, URL = window.URL}) {
  Object.assign(this, {
    sessionId: null,
    headers: {},
    timeout: 0,
    $urlBase: urlBase,
    $backend: {fetch, URL}
  });

  Object.defineProperty(this, 'serviceVersion', {
    enumerable: false,
    get() {
      return this.invoke('IServiceDescriptor', 'get_Version');
    }
  });
}

/* eslint-disable no-invalid-this */
function decorate(fn, newFn) {
  const oldFn = this.prototype[fn];
  this.prototype[fn] = Object.defineProperty(decorator, 'name', {value: oldFn.name}); // "decorator.name = ..." nem mukodik;

  return this;

  function decorator(...args) {
    this.$base = oldFn;
    try {
      return newFn.apply(this, args);
    } finally {
      delete this.$base;
    }
  }
}
/* eslint-enable no-invalid-this */

function format(str, ...args) {
  return str.replace(/{(\d+)}/g, (match, number) => typeof args[number] !== 'undefined'
    ? args[number]
    : match);
}