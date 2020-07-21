import 'jasmine';
import {foo} from 'dummy';

describe('Dummy module', () => {
    describe('foo()', () => {
        it('should return true', () => expect(foo()).toBeTruthy());
    });
});