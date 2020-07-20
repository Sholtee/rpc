import 'jasmine';
import foo from  '../src/dummy';

describe('Dummy module', () => {
    describe('foo()', () => {
        it('should return true', () => expect(foo()).toBeTruthy());
    });
});