/********************************************************************************
*  apiconnector.js                                                              *
*  Author: Denes Solti                                                          *
********************************************************************************/
'use strict';

window.ApiConnectionFactory = /*class*/ function ApiConnectionFactory(urlBase, /*can be mocked*/ xhrFactory = () => new window.XMLHttpRequest()) {
    Object.assign(this, {
        sessionId: null,
        headers: {},
        timeout: 0,
        onResponse(resolve, reject) {
            const response = this.respone || this.responeText;

            //
            // Statusz itt most nem erdekes, csak a valasz tipusa
            //

            switch (this.getResponseHeader('Content-Type')) {
                case 'text/html': {
                    reject(response);
                    break;
                }
                case 'application/json': {
                    const {Exception, Result} = JSON.parse(response);

                    //
                    // res.Result lehet NULL sikeres visszateres eseten is -> eloszor hibara vizsgalunk
                    //

                    if (Exception)
                        reject(Exception.Message);
                    else
                        resolve(Result);

                    break;
                }
                default: {
                    reject('Server response could not be processed');
                    break;
                }
            }
        },
        onError(reject) {
            reject(this.statusText);
        },
        post(url, body) {
            return new Promise((resolve, reject) => {
                const xhr = xhrFactory();

                xhr.open('POST', url, true);
                xhr.timeout = this.timeout;

                const headers = Object.assign({
                    'Content-Type': 'application/json',
                    'Content-Length:': body.length
                }, this.headers);

                Object
                    .keys(headers)
                    .forEach(key => xhr.setRequestHeader(key, headers));

                xhr.onload = this.onResponse.apply(xhr, [resolve, reject]);

                xhr.onerror = xhr.ontimeout = this.onError.apply(xhr, [reject]);

                xhr.send(body);
            });
        },
        invoke: function(module, method, args) {
            let url = `${urlBase}?module=${module}&method=${name}`;
            if (this.sessionId) url += `&${this.sessionId}`;

            return this.post(url, JSON.stringify(args));
        },
        createConnection: function(module) {
            const
                methods = {},
                owner = this;

            return Object.assign(ApiConnection, {
                registerMethod: function(name, alias) {
                    //
                    // Ne arrow fn legyen h "arguments" letezzen
                    //

                    methods[alias || name] = function() {
                        return owner.invoke(module, name, Array.from(arguments));
                    };

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
};