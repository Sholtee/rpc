/********************************************************************************
*  apiconnector.js                                                              *
*  Author: Denes Solti                                                          *
********************************************************************************/
const RESPONSE_NOT_VALID = 'Server response could not be processed';

// class
function ApiConnectionFactory(urlBase, /*can be mocked*/ xhrFactory = () => new XMLHttpRequest()) {
  Object.assign(this, {
    sessionId: null,
    headers: {},
    timeout: 0,
    invoke(module, method, args = []) {
      let url = `${urlBase}?module=${module}&method=${method}`;
      if (this.sessionId) url += `&sessionid=${this.sessionId}`;

      return new Promise((resolve, reject) => {
        const xhr = xhrFactory();

        xhr.open('POST', url, true);
        xhr.timeout = this.timeout;

        const headers = {
          ...this.headers,
          'Content-Type': 'application/json'
        };

        Object
          .keys(headers)
          .forEach(key => xhr.setRequestHeader(key, headers[key].toString()));

        xhr.onload = onResponse.bind(xhr, resolve, reject);

        xhr.onerror = xhr.ontimeout = onError.bind(xhr, reject);

        xhr.send(JSON.stringify(args));
      });
    },
    createConnection(module) {
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
            get: () =>  owner.invoke(module, `get_${name}`),
            set: val => owner.invoke(module, `set_${name}`, [val])
          });
          return this;
        },
        module
      });
    }
  });

  Object.defineProperty(this, 'serviceVersion', {
    enumerable: false,
    get() {
      return this.invoke('IServiceDescriptor', 'get_Version');
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
          // Ne "in" operatorral vizsgaljuk mert lehet jelen van, csak NULL
          //

          if (parsed.Exception) {
            reject(parsed.Exception);
            break;
          }

          //
          // Viszont itt mar NULL is jo ertek
          //

          if ('Result' in parsed) {
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

}

export {ApiConnectionFactory, RESPONSE_NOT_VALID};