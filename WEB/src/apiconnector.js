/********************************************************************************
*  apiconnector.js                                                              *
*  Author: Denes Solti                                                          *
********************************************************************************/
'use strict';

(function(window) {
const RESPONSE_NOT_VALID = 'Server response could not be processed';

/*class*/ function ApiConnectionFactory(urlBase, /*can be mocked*/ xhrFactory = () => new window.XMLHttpRequest()) {
    Object.assign(this, {
        sessionId: null,
        headers: {},
        timeout: 0,
        onResponse(resolve, reject) {
            const response = this.response || this.responseText;

            //
            // Statusz itt most nem erdekes, csak a valasz tipusa
            //

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
        },
        onError(reject) {
            reject(this.statusText);
        },
        post(url, args) {
            return new Promise((resolve, reject) => {
                const xhr = xhrFactory();

                xhr.open('POST', url, true);
                xhr.timeout = this.timeout;

                const headers = Object.assign({
                    'Content-Type': 'application/json'
                }, this.headers);

                Object
                    .keys(headers)
                    .forEach(key => xhr.setRequestHeader(key, headers[key].toString()));

                xhr.onload = this.onResponse.bind(xhr, resolve, reject);

                xhr.onerror = xhr.ontimeout = this.onError.bind(xhr, reject);

                xhr.send(JSON.stringify(args));
            });
        },
        invoke: function(module, method, args) {
            let url = `${urlBase}?module=${module}&method=${name}`;
            if (this.sessionId) url += `&${this.sessionId}`;

            return this.post(url, args);
        },
        createConnection: function(module) {
            const
                methods = {},
                owner = this;

            return Object.assign(ApiConnection, {
                registerMethod: function(name, alias) {
                    methods[alias || name] = (...args) => owner.invoke(module, name, args);

                    //
                    // A hivasok lancba fuzhetoek legyenek
                    //

                    return this;
                },
                module
            });

            // class
            function ApiConnection() {
                Object.keys(methods).forEach(name => {
                    //
                    // "arrow" fv-ben nincs this u h jo helyre hivatkozunk
                    //

                    this[name] = methods[name];
                });
            }
        }
    });
}

//
// Exports
//

Object.assign(window, {
    RESPONSE_NOT_VALID,
    ApiConnectionFactory
});
})(window);