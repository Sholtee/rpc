/********************************************************************************
*  apiconnector.spec.js                                                         *
*  Author: Denes Solti                                                          *
********************************************************************************/

/*
for (let file in window.__karma__.files) {
    console.log(file);
}
*/

describe('ApiConnection', () => {
    const {
        apiconnector: { ApiConnection , RESPONSE_NOT_VALID, REQUEST_TIMED_OUT, SCHEMA_NOT_FOUND },

        //
        // WHATWGFetch uses XHR under the hood. It's required since SinonJS is able to fake XHR only.
        //

        WHATWGFetch: { fetch }
    } = window;

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
            server.autoRespond = true;
        });

        afterEach(() => server.restore());

        it('should return a Promise', () => {
            server.respondWith('POST', api, [200, { 'Content-Type': 'application/json' }, '{"Exception": null, "Result": 2}']);
            expect(conn.invoke('ICalculator', 'Add', [1, 1])).toBeInstanceOf(Promise);
        });

        it('should deserialize the result', async() => {
            server.respondWith('POST', api, [200, { 'Content-Type': 'application/json' }, '{"Exception": null, "Result": 2}']);
            expect(await conn.invoke('ICalculator', 'Add', [1, 1])).toBe(2);
        });

        it('should deserialize complex result', async() => {
            server.respondWith('POST', api, [200, { 'Content-Type': 'application/json' }, '{"Exception": null, "Result": {"Cica": 1986}}']);
            expect(await conn.invoke('ICalculator', 'Add', [1, 1])).toEqual({cica: 1986});
        });

        it('should throw on malformed result', async() => {
            server.respondWith('POST', api, [200, { 'Content-Type': 'application/json' }, '1986']);
            try {
                await conn.invoke('ICalculator', 'Add', [1, 1]);
            } catch (e) {
                expect(e).toEqual(RESPONSE_NOT_VALID);
            }
        });

        it('should throw on invalid content type', async() => {
            server.respondWith('POST', api, [200, { 'Content-Type': 'cica' }, '{"Exception": null, "Result": 2}']);
            try {
                await conn.invoke('ICalculator', 'Add', api, [1, 1]);
            } catch (e) {
                expect(e).toEqual(RESPONSE_NOT_VALID);
            }
        });

        it('should handle text result', async() => {
            server.respondWith('POST', api, [500, { 'Content-Type': 'text/html' }, 'akarmi']);
            try {
                await conn.invoke('ICalculator', 'Add', [1, 1]);
            } catch (e) {
                expect(e).toEqual('akarmi');
            }
        });

        it('may send the session ID', async() => {
            server.respondWith(
                'POST',
                /http:\/\/localhost:1986\/api\?module=IGetMySessionIdBack&method=GetBack/,
                xhr => xhr.respond(200, { 'Content-Type': 'application/json' }, `{"Exception": null, "Result": "${new URL(xhr.url).searchParams.get('sessionId')}"}`)
            );
            conn.sessionId = 'cica';
            expect(await conn.invoke('IGetMySessionIdBack', 'GetBack')).toBe('cica');
        });

        it('should set the content type', async() => {
            let headers = {};

            server.respondWith('POST', api, xhr => {
                headers = xhr.requestHeaders;
            });

            try {
                await conn.invoke('ICalculator', 'Add', [1, 1]);
            // eslint-disable-next-line no-empty
            } catch {}

            expect(headers['Content-Type']).toBe('application/json;charset=utf-8');
        });

        it('may send custom headers', async() => {
            conn.onFetch(function(url, opts) {
                opts.headers['my-header'] = 'value';
                // eslint-disable-next-line no-invalid-this
                return this(url, opts);
            });

            let headers = {};

            server.respondWith('POST', api, xhr => {
                headers = xhr.requestHeaders;
            });

            try {
                await conn.invoke('ICalculator', 'Add', [1, 1]);
            // eslint-disable-next-line no-empty
            } catch {}

            expect(headers['my-header']).toBe('value');
        });

        it('may be decorated', async() => {
            let decoratorCalled = 0;

            conn.onInvoke(function(...args) {
                decoratorCalled++;
                // eslint-disable-next-line no-invalid-this
                return this(...args);
            });

            server.respondWith('POST', api, [200, { 'Content-Type': 'application/json' }, '{"Exception": null, "Result": 2}']);
            const result = await conn.invoke('ICalculator', 'Add', [1, 1]);

            expect(result).toBe(2);
            expect(decoratorCalled).toBe(1);
        });

        it('may be decorated more than once', async() => {
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
            const result = await conn.invoke('ICalculator', 'Add', [1, 1]);

            expect(result).toBe(2);
            expect(firstDecoratorCalled).toBe(1);
            expect(secondDecoratorCalled).toBe(1);
        });

        it('should handle remote exceptions', async() => {
            server.respondWith('POST', api, [200, { 'Content-Type': 'application/json' }, '{"Exception": {"Message": "error"}, "Result": null}']);

            try {
                await conn.invoke('ICalculator', 'Add', [1, 1]);
            } catch (e) {
                expect(e.message).toEqual('error');
            }
        });

        it('should timeout', async() => {
            server.autoRespondAfter = 1000;
            conn.timeout = 20;
            try {
                await conn.invoke('ICalculator', 'Add', api, [1, 1]);
            } catch (e) {
                expect(e).toEqual(REQUEST_TIMED_OUT);
            }
        });

        it('may receive streams', async() => {
            server.respondWith('POST', 'http://localhost:1986/api?module=IStreamProvider&method=GetStream', [200, { 'Content-Type': 'application/octet-stream' }, 'cica']);
            const result = await conn.invoke('IStreamProvider', 'GetStream', []);

            // eslint-disable-next-line no-undef
            expect(result instanceof Blob).toBeTrue();
        });
    });

    describe('createAPI', () => {
        const schema = 'http://localhost:1986/api?module=ICalculator';

        let server;

        beforeEach(function() {
            server = sinon.createFakeServer();
            server.autoRespond = true;
        });

        afterEach(() => server.restore());

        it('should parse the schema', async() => {
            server.respondWith('GET', schema, [200, { 'Content-Type': 'application/json' }, '{"ICalculator": {"Methods": {"Add": {}}, "Properties": {"PI": {"HasGetter": true, "HasSetter": false}}}}']);
            const api = await conn.createAPI('ICalculator');
            expect('add' in api).toBeTrue();
            expect('PI' in api).toBeTrue();
        });

        it('should throw on invalid content type', async() => {
            server.respondWith('GET', schema, [200, { 'Content-Type': 'cica' }, '{}']);
            try {
                await conn.createAPI('ICalculator');
            } catch (e) {
                expect(e).toEqual(RESPONSE_NOT_VALID);
            }
        });

        it('should throw if the schema cannot be found', async() => {
            server.respondWith('GET', schema, [200, { 'Content-Type': 'application/json' }, '{}']);
            try {
                await conn.createAPI('ICalculator');
            } catch (e) {
                expect(e).toBe(SCHEMA_NOT_FOUND);
            }
        });

        it('should support methods', async() => {
            server.respondWith('GET', schema, [200, { 'Content-Type': 'application/json' }, '{"ICalculator": {"Methods": {"Add": {}}, "Properties": {}}}']);
            const api = await conn.createAPI('ICalculator');

            server.respondWith('POST', 'http://localhost:1986/api?module=ICalculator&method=Add', [200, { 'Content-Type': 'application/json' }, '{"Exception": null, "Result": 2}']);
            expect(await api.add(1, 1)).toBe(2);
        });

        it('should support properties', async() => {
            server.respondWith('GET', schema, [200, { 'Content-Type': 'application/json' }, '{"ICalculator": {"Methods": {}, "Properties": {"PI": {"HasGetter": true}}}}']);
            const api = await conn.createAPI('ICalculator');

            server.respondWith('POST', 'http://localhost:1986/api?module=ICalculator&method=get_PI', [200, { 'Content-Type': 'application/json' }, '{"Exception": null, "Result": 3.14}']);
            expect(await api.PI).toEqual(3.14);
        });

        it('should skip setters', async() => {
            server.respondWith('GET', schema, [200, { 'Content-Type': 'application/json' }, '{"ICalculator": {"Methods": {}, "Properties": {"PI": {"HasGetter": true, "HasSetter": true}}}}']);
            const
                api = await conn.createAPI('ICalculator'),
                descr = Object.getOwnPropertyDescriptor(Object.getPrototypeOf(api), 'PI');

            expect(descr.get).toBeDefined();
            expect(descr.set).toBeUndefined();
        });

        it('should skip setters (2)', async() => {
            server.respondWith('GET', schema, [200, { 'Content-Type': 'application/json' }, '{"ICalculator": {"Methods": {}, "Properties": {"PI": {"HasSetter": true}}}}']);
            const
                api = await conn.createAPI('ICalculator'),
                descr = Object.getOwnPropertyDescriptor(Object.getPrototypeOf(api), 'PI');
            expect(descr).toBeUndefined();
        });
    });
});