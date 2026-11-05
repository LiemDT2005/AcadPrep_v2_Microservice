const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

// Exercise the production adapters with the jQuery registration boundary stubbed; no network or DOM needed.
const methods = {};
const adapters = {};
let boolAdapter;
const jquery = value => (typeof value === 'function' ? undefined : { valid() { return true; } });
jquery.validator = {
    addMethod(name, callback) { methods[name] = callback; },
    unobtrusive: { adapters: {
        addBool(name) { boolAdapter = name; },
        add(name, parameters, callback) { adapters[name] = { parameters: Array.from(parameters), callback }; }
    } }
};
vm.runInNewContext(fs.readFileSync(path.join(__dirname,
    '../../src/Web/Storefront.Web/wwwroot/js/billing-validation.js'), 'utf8'), { jQuery: jquery });

test('adapters are registered for the server data-val names', () => {
    assert.equal(boolAdapter, 'billingguid');
    assert.deepEqual(adapters.billingdaterange.parameters, ['other']);
});

for (const [value, expected] of [
    ['', true], [null, true], ['bad', false],
    ['00000000-0000-0000-0000-000000000000', false],
    ['33333333-3333-3333-3333-333333333333', true],
    ['  33333333-3333-3333-3333-333333333333  ', true],
    ['33333333-3333-3333-3333-33333333333', false]
]) {
    test(`guid ${JSON.stringify(value)} -> ${expected}`, () => assert.equal(methods.billingguid(value), expected));
}

for (const [from, to, expected] of [
    ['', '', true], ['2026-10-01', '', true], ['', '2026-10-01', true],
    ['2026-10-01', '2026-10-01', true], ['2026-10-01', '2026-10-31', true], ['2026-10-20', '2026-10-19', false]
]) {
    test(`date range ${from}..${to} -> ${expected}`, () => {
        const element = { form: { elements: { 'Filter.From': { value: from } } } };
        assert.equal(methods.billingdaterange(to, element, 'Filter.From'), expected);
    });
}

test('date range adapter resolves the nested start field', () => {
    const options = { element: { name: 'Filter.To' }, params: { other: 'From' }, message: 'msg', rules: {}, messages: {} };
    adapters.billingdaterange.callback(options);
    assert.equal(options.rules.billingdaterange, 'Filter.From');
    assert.equal(options.messages.billingdaterange, 'msg');
});

for (const [value, allowed, expected] of [
    ['10000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000002,10000000-0000-0000-0000-000000000003', true],
    ['10000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000002', false],
    ['', '10000000-0000-0000-0000-000000000002', false],
    ['10000000-0000-0000-0000-000000000002', '', false]
]) {
    test(`valid plan ${value || '(empty)'} -> ${expected}`, () => assert.equal(methods.validplan(value, {}, allowed), expected));
}

test('valid plan adapter passes the allowed list', () => {
    const options = { element: { name: 'Input.PlanId' }, params: { allowed: 'a,b' }, message: 'msg', rules: {}, messages: {} };
    adapters.validplan.callback(options);
    assert.equal(options.rules.validplan, 'a,b');
    assert.equal(options.messages.validplan, 'msg');
});
