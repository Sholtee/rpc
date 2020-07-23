/********************************************************************************
*  apiconnector.spec.js                                                         *
*  Author: Denes Solti                                                          *
********************************************************************************/
'use strict';

describe('ApiConnectionFactory', () => {
    describe('post()', () => {
        const api = 'http://127.0.0.1:1986/api?module=ICalculator&method=Add';

        var server, factory;

        beforeEach(function () {
            server = sinon.createFakeServer();
            server.autoRespond = false;

            factory = new ApiConnectionFactory(api);
        });

        afterEach(function () {
            server.restore();
        });

        it('should return a Promise', () => expect(factory.post(api, [1, 1])).toBeInstanceOf(Promise));

        it('should deserialize the result', done => {
            server.respondWith('POST', api, [200, { 'Content-Type': 'application/json' }, '{"Exception": null, "Result": 2}']);
            factory.post(api, [1, 1]).then(result => {
                expect(result).toBe(2);
                done();
            });
            server.respond();
        });
    });
});
