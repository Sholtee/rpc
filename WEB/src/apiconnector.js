/********************************************************************************
*  apiconnector.js                                                              *
*  Author: Denes Solti                                                          *
********************************************************************************/
'use strict';

(function(window) {
const RESPONSE_NOT_VALID = 'Server response could not be processed';

// class
function ApiConnectionFactory(urlBase, /*can be mocked*/ xhrFactory = () => new window.XMLHttpRequest()) {
  Object.assign(this, {
    sessionId: null,
    headers: {},
    timeout: 0,
    invoke: function(module, method, args) {
      let url = `${urlBase}?module=${module}&method=${method}`;
      if (this.sessionId) url += `&sessionid=${this.sessionId}`;

      return post(url, args, this.headers, this.timeout);
    },
    createConnection: function(module) {
      const owner = this;

      return Object.assign(function ApiConnection() {}, {
        registerMethod: function(name, alias) {
          this.prototype[alias || name] = (...args) => owner.invoke(module, name, args);

          //
          // A hivasok lancba fuzhetoek legyenek
          //

          return this;
        },
        registerProperty: function(name, alias) {
          Object.defineProperty(this.prototype, alias || name, {
            enumerable: true,
            get: () =>  owner.invoke(module, `get_${name}`, []),
            set: val => owner.invoke(module, `set_${name}`, [val])
          });
          return this;
        },
        module
      });
    }
  });

  /* eslint-disable no-invalid-this */
  function onError(reject) {
    reject(this.statusText);
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
          //
          // Ne hasOwnProperty()-vel vizsgaljuk mert lehet jelen van, csak NULL
          //

          if (parsed.Exception) {
            reject(parsed.Exception.Message);
            break;
          }

          //
          // Viszont itt mar NULL is jo ertek -> hasOwnProperty()
          //

          if (parsed.hasOwnProperty('Result')) {
            resolve(parsed.Result);
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
  }
  /* eslint-enable no-invalid-this */

  function post(url, args, headers, timeout) {
    return new Promise((resolve, reject) => {
      const xhr = xhrFactory();

      xhr.open('POST', url, true);
      xhr.timeout = timeout;

      headers = Object.assign({
        'Content-Type': 'application/json'
      }, headers);

      Object
        .keys(headers)
        .forEach(key => xhr.setRequestHeader(key, headers[key].toString()));

      xhr.onload = onResponse.bind(xhr, resolve, reject);

      xhr.onerror = xhr.ontimeout = onError.bind(xhr, reject);

      xhr.send(JSON.stringify(args));
    });
  }
}

//
// Exports
//

Object.assign(window, {
    RESPONSE_NOT_VALID,
    ApiConnectionFactory
});
})(window);