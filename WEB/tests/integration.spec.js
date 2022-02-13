/********************************************************************************
*  integration.spec.js                                                          *
*  Author: Denes Solti                                                          *
********************************************************************************/

describe('Calculator', () => {
    const { ApiConnection } = window.apiconnector;

    let calculator;

    beforeAll(async() => {
        const conn = new ApiConnection('http://localhost:1986/api');
        calculator = await conn.createAPI('ICalculator');
    });

    it('should Add', async() => expect(await calculator.add(1, 2)).toEqual(3));

    it('should AddAsync', async() => expect(await calculator.addAsync(1, 2)).toEqual(3));

    it('should Add a negative number', async() => expect(await calculator.add(1, -2)).toEqual(-1));

    it('should provide the PI', async() => expect(await calculator.PI).toEqual(Math.PI));

    it('should throw on extra argument', async() => {
        try {
            await calculator.add(1, -2, 3);
        } catch (e) {
            expect(e.message).toBe('The length of the args array does not match the parameter count.');
            expect(e.typeName).toContain('System.Text.Json.JsonException');
        }
    });

    it('should throw on missing argument', async() => {
        try {
            await calculator.add(1);
        } catch (e) {
            expect(e.message).toContain('The length of the args array does not match the parameter count.');
            expect(e.typeName).toContain('System.Text.Json.JsonException');
        }
    });

    it('should throw on invalid argument', async() => {
        try {
            await calculator.add('1', '2');
        } catch (e) {
            expect(e.message).toContain('The JSON value could not be converted to System.Int32.');
            expect(e.typeName).toContain('System.Text.Json.JsonException');
        }
    });

    it('should forward the module exception', async() => {
        try {
            await calculator.parseInt('cica');
        } catch (e) {
            expect(e.message).toBe('Input string was not in a correct format.');
            expect(e.typeName).toContain('System.FormatException');
        }
    });
});