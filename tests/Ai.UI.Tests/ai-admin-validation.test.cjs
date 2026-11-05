const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

// Exercise the production adapter with the jQuery registration boundary stubbed; no DOM needed.
let method;
let adapter;
const jquery = value => (typeof value === 'function' ? undefined : { valid() { return true; } });
jquery.validator = {
    addMethod(name, callback) { assert.equal(name, 'aitokenlimit'); method = callback; },
    unobtrusive: { parse() {}, adapters: { add(name, params, callback) { assert.equal(name, 'aitokenlimit'); assert.deepEqual(Array.from(params), ['max']); adapter = callback; } } }
};
vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../../src/Web/Storefront.Web/wwwroot/js/ai-admin-validation.js'), 'utf8'), { jQuery: jquery, document: { querySelectorAll: () => [] } });

for (const [value, expected] of [
    ['0', true], ['15000', true], ['100000000', true], ['', true],
    ['-1', false], ['100000001', false], ['1.5', false], ['abc', false]
]) {
    test(`token limit ${JSON.stringify(value)} -> ${expected}`, () => assert.equal(method(value, {}, '100000000'), expected));
}

test('adapter passes the max parameter', () => {
    const options = { params: { max: '100000000' }, message: 'msg', rules: {}, messages: {} };
    adapter(options);
    assert.equal(options.rules.aitokenlimit, '100000000');
    assert.equal(options.messages.aitokenlimit, 'msg');
});
