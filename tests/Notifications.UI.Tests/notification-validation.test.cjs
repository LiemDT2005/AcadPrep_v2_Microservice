const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

// Exercise the production adapter with the jQuery registration boundary stubbed; no network or DOM needed.
let method;
let adapter;
let textMethod;
const jquery = value => typeof value === 'function' ? undefined : { valid() {} };
jquery.validator = {
    addMethod(name, callback) {
        if (name === 'notificationtext') textMethod = callback;
        else { assert.equal(name, 'notificationaudience'); method = callback; }
    },
    unobtrusive: { adapters: { add(name, parameters, callback) {
        assert.equal(name, 'notificationaudience');
        assert.deepEqual(Array.from(parameters), ['other']);
        adapter = callback;
    } } }
};
vm.runInNewContext(fs.readFileSync(path.join(__dirname,
    '../../src/Web/Storefront.Web/wwwroot/js/notification-validation.js'), 'utf8'), { jQuery: jquery });

for (const [audience, value, expected] of [
    ['User', '', false], ['User', 'bad', false],
    ['User', '00000000-0000-0000-0000-000000000000', false],
    ['User', '11111111-1111-1111-1111-111111111111', true],
    ['All', '', true], ['Learner', '', true], ['Moderator', '', true],
    ['All', '11111111-1111-1111-1111-111111111111', false],
    ['Learner', '11111111-1111-1111-1111-111111111111', false],
    ['Moderator', '11111111-1111-1111-1111-111111111111', false]
]) {
    test(`${audience} target ${JSON.stringify(value)}`, () => {
        const element = { form: { elements: { 'Input.Audience': { value: audience } } } };
        assert.equal(method(value, element, 'Input.Audience'), expected);
    });
}
test('adapter resolves nested form field and error message', () => {
    const options = { element: { name: 'Input.TargetUserId' }, params: { other: 'Audience' },
        rules: {}, messages: {}, message: 'validation message' };
    adapter(options);
    assert.equal(options.rules.notificationaudience, 'Input.Audience');
    assert.equal(options.messages.notificationaudience, 'validation message');
});
test('whitespace-only content is invalid', () => {
    assert.equal(textMethod('  \t '), false);
    assert.equal(textMethod(' Content '), true);
});
