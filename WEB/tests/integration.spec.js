/********************************************************************************
*  integration.spec.js                                                          *
*  Author: Denes Solti                                                          *
********************************************************************************/

const { ApiConnection } = window.apiconnector;

describe('Calculator', () => {
    let calculator;

    beforeAll(done => {
        const conn = new ApiConnection('http://localhost:1986/api');
        conn.createAPI('ICalculator').then(api => {
            calculator = api;
            done();
        });
    });

    it('should Add', done => {
        calculator.add(1, 2).then(result => {
            expect(result).toEqual(3);
            done();
        });
    });

    it('should AddAsync', done => {
        calculator.addAsync(1, 2).then(result => {
            expect(result).toEqual(3);
            done();
        });
    });

    it('should Add a negative number', done => {
        calculator.add(1, -2).then(result => {
            expect(result).toEqual(-1);
            done();
        });
    });

    it('should provide the PI', done => {
        calculator.PI.then(PI => {
            expect(PI).toEqual(Math.PI);
            done();
        });
    });

    it('should throw on extra argument', done => {
        calculator.add(1, -2, 3).catch(e => {
            expect(e.message).toBe('The length of the args array does not match the parameter count.');
            expect(e.typeName).toContain('System.Text.Json.JsonException');
            done();
        });
    });

    it('should throw on missing argument', done => {
        calculator.add(1).catch(e => {
            expect(e.message).toContain('The length of the args array does not match the parameter count.');
            expect(e.typeName).toContain('System.Text.Json.JsonException');
            done();
        });
    });

    it('should throw on invalid argument', done => {
        calculator.add('1', '2').catch(e => {
            expect(e.message).toContain('The JSON value could not be converted to System.Int32.');
            expect(e.typeName).toContain('System.Text.Json.JsonException');
            done();
        });
    });

    it('should forward the module exception', done => {
        calculator.parseInt('cica').catch(e => {
            expect(e.message).toBe('Input string was not in a correct format.');
            expect(e.typeName).toContain('System.FormatException');
            done();
        });
    });
});