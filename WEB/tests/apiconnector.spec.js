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
    {ApiConnectionFactory, RESPONSE_NOT_VALID, REQUEST_TIMED_OUT, SIGNATURE_NOT_MATCH} = window.apiconnector,
    {fetch} = window.WHATWGFetch; // SinonJS csak XHR-t tud fake-elni

describe('ApiConnectionFactory', () => {
    const noop = function() {};

    //
    // Proxy generalas az elso megszolitaskor sokaig tarthat (ha a generalt asm-ek gyorsitotarazasa nincs beallitva)
    //

    jasmine.DEFAULT_TIMEOUT_INTERVAL = 5000;

    let factory;

    beforeEach(() => {
        factory = new ApiConnectionFactory('http://localhost:1986/api', {fetch});
    });

    describe('invoke()', () => {
        const api = 'http://localhost:1986/api?module=ICalculator&method=Add';

        let server;

        beforeEach(function() {
            server = sinon.createFakeServer();
            server.autoRespond = false;
        });

        afterEach(() => server.restore());

        it('should return a Promise', () => expect(factory.invoke('ICalculator', 'Add', [1, 1])).toBeInstanceOf(Promise));

        it('should deserialize the result', done => {
            server.respondWith('POST', api, [200, { 'Content-Type': 'application/json' }, '{"Exception": null, "Result": 2}']);
            factory.invoke('ICalculator', 'Add', [1, 1]).then(result => {
                expect(result).toBe(2);
                done();
            });
            server.respond();
        });

        it('should handle malformed result', done => {
            server.respondWith('POST', api, [200, { 'Content-Type': 'application/json' }, '1986']);
            factory.invoke('ICalculator', 'Add', [1, 1]).catch(e => {
                expect(e).toEqual(RESPONSE_NOT_VALID);
                done();
            });
            server.respond();
        });

        it('should handle invalid content type',  done => {
            server.respondWith('POST', api, [200, { 'Content-Type': 'cica' }, '{"Exception": null, "Result": 2}']);
            factory.invoke('ICalculator', 'Add', api, [1, 1]).catch(e => {
                expect(e).toEqual(RESPONSE_NOT_VALID);
                done();
            });
            server.respond();
        });

        it('should handle text result',  done => {
            server.respondWith('POST', api, [200, { 'Content-Type': 'text/html' }, 'akarmi']);
            factory.invoke('ICalculator', 'Add', [1, 1]).catch(e => {
                expect(e).toEqual('akarmi');
                done();
            });
            server.respond();
        });

        it('should send the session ID', done => {
            server.respondWith('POST', /http:\/\/localhost:1986\/api\?module=IGetMySessionIdBack&method=GetBack/, xhr =>
                xhr.respond(200, { 'Content-Type': 'application/json' }, `{"Exception": null, "Result": "${new URL(xhr.url).searchParams.get('sessionId')}"}`));
            factory.sessionId = 'cica';
            factory.invoke('IGetMySessionIdBack', 'GetBack').then(result => {
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

            factory.invoke('ICalculator', 'Add', [1, 1]).catch(noop);
            server.respond();

            expect(headers['Content-Type']).toBe('application/json;charset=utf-8');
        });

        it('should handle custom headers', () => {
            factory.headers['my-header'] = 'value';

            let headers = {};

            server.respondWith('POST', api, xhr => {
                headers = xhr.requestHeaders;
            });

            factory.invoke('ICalculator', 'Add', [1, 1]).catch(noop);
            server.respond();

            expect(headers['my-header']).toBe('value');
        });

        it('may be overridden', done => {
            let decoratorCalled = 0;

            factory.constructor.decorate('invoke', function(...args) {
                decoratorCalled++;
                /* eslint-disable no-invalid-this */
                return this.$base(...args);
                /* eslint-enable no-invalid-this */
            });

            server.respondWith('POST', api, [200, { 'Content-Type': 'application/json' }, '{"Exception": null, "Result": 2}']);
            factory.invoke('ICalculator', 'Add', [1, 1]).then(result => {
                expect(result).toBe(2);
                expect(decoratorCalled).toBe(1);
                done();
            });
            server.respond();
        });

        it('may be overridden more than once', done => {
            let
                firstDecoratorCalled = 0,
                secondDecoratorCalled = 0;

            /* eslint-disable no-invalid-this */
            factory.constructor.decorate('invoke', function(...args) {
                firstDecoratorCalled++;
                return this.$base(...args);
            });
            factory.constructor.decorate('invoke', function(...args) {
                secondDecoratorCalled++;
                return this.$base(...args);
            });
            /* eslint-enable no-invalid-this */

            server.respondWith('POST', api, [200, { 'Content-Type': 'application/json' }, '{"Exception": null, "Result": 2}']);
            factory.invoke('ICalculator', 'Add', [1, 1]).then(result => {
                expect(result).toBe(2);
                expect(firstDecoratorCalled).toBe(1);
                expect(secondDecoratorCalled).toBe(1);
                done();
            });
            server.respond();
        });
    });

    describe('API connection', () => {
        it('should be configurable', () => {
            const Calculator = factory.createConnection('ICalculator');

            expect(typeof Calculator).toBe('function');
            expect(Calculator.module).toBe('ICalculator');

            Calculator
                .registerMethod('Add', 'add')
                .registerProperty('PI');

            expect(typeof Calculator.prototype.add).toBe('function');
            expect(typeof Calculator.prototype.PI).toBe('object');
        });

        ['Add', 'AddAsync'].forEach(method => it(`should support methods (${method})`, done => {
            const Calculator = factory
                .createConnection('ICalculator')
                .registerMethod(method, 'add');

            const inst = new Calculator();
            inst.add(1, 2).then(result => {
                expect(result).toEqual(3);
                done();
            });
        }));

        it('should support properties', done => {
            const Calculator = factory
                .createConnection('ICalculator')
                .registerProperty('PI');

            const inst = new Calculator();
            inst.PI.then(PI => {
                expect(PI).toEqual(Math.PI);
                done();
            });
        });

        it('should handle remote exceptions', done => {
            const Calculator = factory
                .createConnection('ICalculator')
                .registerMethod('ParseInt');

            const inst = new Calculator();

            // eslint-disable-next-line new-cap
            inst.ParseInt('cica').catch(e => {
                expect('Message' in e).toBeTrue();
                expect(typeof e.TypeName).toBe('string');
                expect(e.TypeName).toContain('System.FormatException');
                done();
            });
        });

        //
        // SinonJS nem tamogatja a timeout hasznalatat, ezert itt teszteljuk
        //

        it('should timeout', done => {
            factory.timeout = 1;
            const Calculator = factory
                .createConnection('ICalculator')
                .registerMethod('TimeConsumingOperation', 'timeConsumingOperation');

            const inst = new Calculator();
            inst.timeConsumingOperation().catch(e => {
                expect(e).toBe(REQUEST_TIMED_OUT);
                done();
            });
        });

        it('may validate the layout', done => {
            const Calculator = factory
                .createConnection('ICalculator')
                .registerMethod('Add', 'add', [Number, 'Number']);

            const inst = new Calculator();
            inst.add(1, 2).then(result => {
                expect(result).toEqual(3);

                inst.add(1, 'cica').catch(e => {
                    expect(e).toBe(SIGNATURE_NOT_MATCH);
                    done();
                });
            });
        });
    });

    describe('version', () => {
        it('should return the version of the remote host', () => {
            factory.serviceVersion.then(version => {
                expect(typeof version.Major).toBe('number');
                expect(typeof version.Minor).toBe('number');
                expect(typeof version.Patch).toBe('number');
            });
        });
    });
});

describe('ApiConnectionFactory.fromSchema', () => {
    it('should declare methods', done => {
        const config = {
            urlBase: 'http://localhost:1986/api',
            modules: {
                'ICalculator': {
                    alias: 'calculator',
                    methods: {
                        Add: {
                            alias: 'add',
                            layout: [Number, 'Number']
                        }
                    }
                }
            }
        };

        const api = ApiConnectionFactory.fromSchema(config, {fetch});
        console.log(api);

        api.calculator.add(1, 1).then(result => {
            expect(result).toEqual(2);
            done();
        });
    });

    it('should declare properties', done => {
        const config = {
            urlBase: 'http://localhost:1986/api',
            modules: {
                'ICalculator': {
                    alias: 'calculator',
                    properties: {
                        PI: true
                    }
                }
            }
        };

        const api = ApiConnectionFactory.fromSchema(config, {fetch});
        api.calculator.PI.then(result => {
            expect(result).toEqual(Math.PI);
            done();
        });
    });
});