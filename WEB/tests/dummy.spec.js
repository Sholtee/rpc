const Foo = window.Foo;

describe('Foo class', () => {
    describe('isTrue()', () => {
        it('should return true', () => expect(Foo.isTrue()).toBeTruthy());
    });
});