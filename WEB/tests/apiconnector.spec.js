/********************************************************************************
*  apiconnector.spec.js                                                         *
*  Author: Denes Solti                                                          *
********************************************************************************/
'use strict';

describe('ApiConnectionFactory', () => {
    const noop = function() {};

    //
    // Proxy generalas az elso megszolitaskor sokaig tarthat (ha a generalt asm-ek gyorsitotarazasa nincs beallitva)
    //

    jasmine.DEFAULT_TIMEOUT_INTERVAL = 20000;

    var factory;

    beforeEach(() => {
        factory = new ApiConnectionFactory('http://localhost:1986/api');
    });

    describe('invoke()', () => {
        const api = 'http://localhost:1986/api?module=ICalculator&method=Add';

        var server;

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

        it('should set the content type', () => {
            var headers;

            server.respondWith('POST', api, xhr => {
                headers = xhr.requestHeaders;
            });

            factory.invoke('ICalculator', 'Add', [1, 1]).catch(noop);
            server.respond();

            expect(headers['Content-Type']).toBe('application/json;charset=utf-8');
        });

        it('should handle custom headers', () => {
            factory.headers['my-header'] = 'value';

            var headers;

            server.respondWith('POST', api, xhr => {
                headers = xhr.requestHeaders;
            });

            factory.invoke('ICalculator', 'Add', [1, 1]).catch(noop);
            server.respond();

            expect(headers['my-header']).toBe('value');
        });
    });

    describe('createConnection()', () => {
        it('should return a configurable connection', () => {
            const Calculator = factory.createConnection('ICalculator');

            expect(typeof Calculator).toBe('function');
            expect(Calculator.module).toBe('ICalculator');

            Calculator
                .registerMethod('Add', 'add')
                .registerProperty('PI');

            expect(typeof Calculator.prototype.add).toBe('function');
            expect(typeof Calculator.prototype.PI).toBe('object');
        });

        ['Add', 'AddAsync'].forEach(method => it(`should return an API connection that may have methods (${method})`, done => {
            const Calculator = factory
                .createConnection('ICalculator')
                .registerMethod(method, 'add');

            const inst = new Calculator();
            inst.add(1, 2).then(result => {
                expect(result).toEqual(3);
                done();
            });
        }));

        it('should return an API connection that may have properties', done => {
            const Calculator = factory
                .createConnection('ICalculator')
                .registerProperty('PI');

            const inst = new Calculator();
            inst.PI.then(PI => {
                expect(PI).toEqual(Math.PI);
                done();
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