/********************************************************************************
*  apiconnector.spec.js                                                         *
*  Author: Denes Solti                                                          *
********************************************************************************/

/*
for (let file in window.__karma__.files) {
    console.log(file);
}
*/

const
    {ApiConnection, RESPONSE_NOT_VALID, REQUEST_TIMED_OUT, SCHEMA_NOT_FOUND} = window.apiconnector,

    //
    // WHATWGFetch uses XHR under the hood. It's required since SinonJS is able to fake XHR only.
    //

    {fetch} = window.WHATWGFetch;

describe('ApiConnection', () => {
    const noop = function() {};

    jasmine.DEFAULT_TIMEOUT_INTERVAL = 5000;

    let conn, oldFetch;

    beforeAll(() => {
        oldFetch = window.fetch;
        window.fetch = fetch;
    });

    afterAll(() => {
        window.fetch = oldFetch;
    });

    beforeEach(() => {
        conn = new ApiConnection('http://localhost:1986/api');
    });

    describe('invoke()', () => {
        const api = 'http://localhost:1986/api?module=ICalculator&method=Add';

        let server;

        beforeEach(function() {
            server = sinon.createFakeServer();
            server.autoRespond = false;
        });

        afterEach(() => server.restore());

        it('should return a Promise', () => expect(conn.invoke('ICalculator', 'Add', [1, 1])).toBeInstanceOf(Promise));

        it('should deserialize the result', done => {
            server.respondWith('POST', api, [200, { 'Content-Type': 'application/json' }, '{"Exception": null, "Result": 2}']);
            conn.invoke('ICalculator', 'Add', [1, 1]).then(result => {
                expect(result).toBe(2);
                done();
            });
            server.respond();
        });

        it('should deserialize complex result', done => {
            server.respondWith('POST', api, [200, { 'Content-Type': 'application/json' }, '{"Exception": null, "Result": {"Cica": 1986}}']);
            conn.invoke('ICalculator', 'Add', [1, 1]).then(result => {
                expect(result).toEqual({cica: 1986});
                done();
            });
            server.respond();
        });

        it('should throw on malformed result', done => {
            server.respondWith('POST', api, [200, { 'Content-Type': 'application/json' }, '1986']);
            conn.invoke('ICalculator', 'Add', [1, 1]).catch(e => {
                expect(e).toEqual(RESPONSE_NOT_VALID);
                done();
            });
            server.respond();
        });

        it('should throw on invalid content type',  done => {
            server.respondWith('POST', api, [200, { 'Content-Type': 'cica' }, '{"Exception": null, "Result": 2}']);
            conn.invoke('ICalculator', 'Add', api, [1, 1]).catch(e => {
                expect(e).toEqual(RESPONSE_NOT_VALID);
                done();
            });
            server.respond();
        });

        it('should handle text result',  done => {
            server.respondWith('POST', api, [500, { 'Content-Type': 'text/html' }, 'akarmi']);
            conn.invoke('ICalculator', 'Add', [1, 1]).catch(e => {
                expect(e).toEqual('akarmi');
                done();
            });
            server.respond();
        });

        it('may send the session ID', done => {
            server.respondWith('POST', /http:\/\/localhost:1986\/api\?module=IGetMySessionIdBack&method=GetBack/, xhr =>
                xhr.respond(200, { 'Content-Type': 'application/json' }, `{"Exception": null, "Result": "${new URL(xhr.url).searchParams.get('sessionId')}"}`));
            conn.sessionId = 'cica';
            conn.invoke('IGetMySessionIdBack', 'GetBack').then(result => {
                expect(result).toBe('cica');
                done();
            });
            server.respond();
        });

        it('should set the content type', () => {
            let headers = {};

            server.respondWith('POST', api, xhr => {
                headers = xhr.requestHeaders;
            });

            conn.invoke('ICalculator', 'Add', [1, 1]).catch(noop);
            server.respond();

            expect(headers['Content-Type']).toBe('application/json;charset=utf-8');
        });

        it('may send custom headers', () => {
            conn.onFetch(function(url, opts) {
                opts.headers['my-header'] = 'value';
                // eslint-disable-next-line no-invalid-this
                return this(url, opts);
            });

            let headers = {};

            server.respondWith('POST', api, xhr => {
                headers = xhr.requestHeaders;
            });

            conn.invoke('ICalculator', 'Add', [1, 1]).catch(noop);
            server.respond();

            expect(headers['my-header']).toBe('value');
        });

        it('may be decorated', done => {
            let decoratorCalled = 0;

            conn.onInvoke(function(...args) {
                decoratorCalled++;
                // eslint-disable-next-line no-invalid-this
                return this(...args);
            });

            server.respondWith('POST', api, [200, { 'Content-Type': 'application/json' }, '{"Exception": null, "Result": 2}']);
            conn.invoke('ICalculator', 'Add', [1, 1]).then(result => {
                expect(result).toBe(2);
                expect(decoratorCalled).toBe(1);
                done();
            });
            server.respond();
        });

        it('may be decorated more than once', done => {
            let
                firstDecoratorCalled = 0,
                secondDecoratorCalled = 0;

            conn.onInvoke(function(...args) {
                firstDecoratorCalled++;
                // eslint-disable-next-line no-invalid-this
                return this(...args);
            });
            conn.onInvoke(function(...args) {
                secondDecoratorCalled++;
                // eslint-disable-next-line no-invalid-this
                return this(...args);
            });

            server.respondWith('POST', api, [200, { 'Content-Type': 'application/json' }, '{"Exception": null, "Result": 2}']);
            conn.invoke('ICalculator', 'Add', [1, 1]).then(result => {
                expect(result).toBe(2);
                expect(firstDecoratorCalled).toBe(1);
                expect(secondDecoratorCalled).toBe(1);
                done();
            });
            server.respond();
        });

        it('should handle remote exceptions', done => {
            server.respondWith('POST', api, [200, { 'Content-Type': 'application/json' }, '{"Exception": {"Message": "error"}, "Result": null}']);
            conn.invoke('ICalculator', 'Add', [1, 1]).catch(e => {
                expect(e.message).toEqual('error');
                done();
            });
            server.respond();
        });

        it('should timeout', done => {
            server.autoRespond = true;
            server.autoRespondAfter = 1000;
            conn.timeout = 20;
            conn.invoke('ICalculator', 'Add', api, [1, 1]).catch(e => {
                expect(e).toEqual(REQUEST_TIMED_OUT);
                done();
            });
        });

        it('may receive streams', done => {
            server.respondWith('POST', 'http://localhost:1986/api?module=IStreamProvider&method=GetStream', [200, { 'Content-Type': 'application/octet-stream' }, 'cica']);
            conn.invoke('IStreamProvider', 'GetStream', []).then(result => {
                // eslint-disable-next-line no-undef
                expect(result instanceof Blob).toBeTrue();
                done();
            });
            server.respond();
        });
    });

    describe('createAPI', () => {
        const schema = 'http://localhost:1986/api?module=ICalculator';

        let server;

        beforeEach(function() {
            server = sinon.createFakeServer();
            server.autoRespond = false;
        });

        afterEach(() => server.restore());

        it('should parse the schema', done => {
            server.respondWith('GET', schema, [200, { 'Content-Type': 'application/json' }, '{"ICalculator": {"Methods": {"Add": {}}, "Properties": {"PI": {"HasGetter": true, "HasSetter": false}}}}']);
            conn.createAPI('ICalculator').then(api => {
                expect('add' in api).toBeTrue();
                expect('PI' in api).toBeTrue();
                done();
            });
            server.respond();
        });

        it('should throw on invalid content type',  done => {
            server.respondWith('GET', schema, [200, { 'Content-Type': 'cica' }, '{}']);
            conn.createAPI('ICalculator').catch(e => {
                expect(e).toEqual(RESPONSE_NOT_VALID);
                done();
            });
            server.respond();
        });

        it('should throw if the schema cannot be found', done => {
            server.respondWith('GET', schema, [200, { 'Content-Type': 'application/json' }, '{}']);
            conn.createAPI('ICalculator').catch(e => {
                expect(e).toBe(SCHEMA_NOT_FOUND);
                done();
            });
            server.respond();
        });

        it('should support methods', done => {
            server.respondWith('GET', schema, [200, { 'Content-Type': 'application/json' }, '{"ICalculator": {"Methods": {"Add": {}}, "Properties": {}}}']);
            conn.createAPI('ICalculator').then(api => {
                server.respondWith('POST', 'http://localhost:1986/api?module=ICalculator&method=Add', [200, { 'Content-Type': 'application/json' }, '{"Exception": null, "Result": 2}']);
                api.add(1, 1).then(result => {
                    expect(result).toBe(2);
                    done();
                });
                server.respond();
            });
            server.respond();
        });

        it('should support properties', done => {
            server.respondWith('GET', schema, [200, { 'Content-Type': 'application/json' }, '{"ICalculator": {"Methods": {}, "Properties": {"PI": {"HasGetter": true}}}}']);
            conn.createAPI('ICalculator').then(api => {
                server.respondWith('POST', 'http://localhost:1986/api?module=ICalculator&method=get_PI', [200, { 'Content-Type': 'application/json' }, '{"Exception": null, "Result": 3.14}']);
                api.PI.then(result => {
                    expect(result).toEqual(3.14);
                    done();
                });
                server.respond();
            });
            server.respond();
        });

        it('should skip setters', done => {
            server.respondWith('GET', schema, [200, { 'Content-Type': 'application/json' }, '{"ICalculator": {"Methods": {}, "Properties": {"PI": {"HasGetter": true, "HasSetter": true}}}}']);
            conn.createAPI('ICalculator').then(api => {
                const descr = Object.getOwnPropertyDescriptor(Object.getPrototypeOf(api), 'PI');
                expect(descr.get).toBeDefined();
                expect(descr.set).toBeUndefined();
                done();
            });
            server.respond();
        });

        it('should skip setters (2)', done => {
            server.respondWith('GET', schema, [200, { 'Content-Type': 'application/json' }, '{"ICalculator": {"Methods": {}, "Properties": {"PI": {"HasSetter": true}}}}']);
            conn.createAPI('ICalculator').then(api => {
                expect('PI' in api).toBeFalse();
                done();
            });
            server.respond();
        });
    });
});