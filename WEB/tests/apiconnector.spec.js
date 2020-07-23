/********************************************************************************
*  apiconnector.spec.js                                                         *
*  Author: Denes Solti                                                          *
********************************************************************************/
'use strict';

describe('ApiConnectionFactory', () => {
    const api = 'http://127.0.0.1:1986/api?module=ICalculator&method=Add';

    var server, factory;

    beforeEach(function() {
        server = sinon.createFakeServer();
        server.autoRespond = false;

        factory = new ApiConnectionFactory('http://127.0.0.1:1986/api');
    });

    afterEach(function() {
        server.restore();
    });

    describe('post()', () => {

        it('should return a Promise', () => expect(factory.post(api, [1, 1])).toBeInstanceOf(Promise));

        it('should deserialize the result', done => {
            server.respondWith('POST', api, [200, { 'Content-Type': 'application/json' }, '{"Exception": null, "Result": 2}']);
            factory.post(api, [1, 1]).then(result => {
                expect(result).toBe(2);
                done();
            });
            server.respond();
        });

        it('should handle malformed result', done => {
            server.respondWith('POST', api, [200, { 'Content-Type': 'application/json' }, '1986']);
            factory.post(api, [1, 1]).catch(e => {
                expect(e).toEqual(RESPONSE_NOT_VALID);
                done();
            });
            server.respond();
        });

        it('should handle invalid content type',  done => {
            server.respondWith('POST', api, [200, { 'Content-Type': 'cica' }, '{"Exception": null, "Result": 2}']);
            factory.post(api, [1, 1]).catch(e => {
                expect(e).toEqual(RESPONSE_NOT_VALID);
                done();
            });
            server.respond();
        });

        it('should handle text result',  done => {
            server.respondWith('POST', api, [200, { 'Content-Type': 'text/html' }, 'akarmi']);
            factory.post(api, [1, 1]).catch(e => {
                expect(e).toEqual('akarmi');
                done();
            });
            server.respond();
        });
    });

    describe('createConnection()', () => {
        it('should return a configurable connection', () => {
            const calculator = factory.createConnection('ICalculator');

            expect(typeof calculator).toBe('function');
            expect(calculator.module).toBe('ICalculator');

            calculator.registerMethod('Add', 'add');

            const inst = new calculator();
            expect(inst.hasOwnProperty('add')).toBeTrue();
        });

        it('should return a connection that invokes the server', done => {
            server.respondWith('POST', api, xhr => {
                const args = JSON.parse(xhr.requestBody);

                xhr.respond(200, { 'Content-Type': 'application/json' }, JSON.stringify({
                    Result: args[0] + args[1]
                }));
            });

            const calculator = factory
                .createConnection('ICalculator')
                .registerMethod('Add', 'add');

            const inst = new calculator();
            inst.add(1, 2).then(result => {
                expect(result).toEqual(3);
                done();
            });

            server.respond();
        });
    });
});