const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.join(__dirname, '../../src/Web/Storefront.Web/wwwroot/js/notification-menu.js'), 'utf8');

function setup(responses) {
    const handlers = {}, calls = [], events = [];
    const status = {}, list = {}, unread = {};
    const toggle = { querySelectorAll: () => [], appendChild() {} };
    const panel = { contains: () => true };
    const root = {
        dataset: { endpoint: '/Notifications' },
        contains: () => true,
        querySelector(selector) {
            return selector.includes('toggle') ? toggle : selector.includes('status') ? status :
                selector.includes('unread') ? unread : selector.includes('list') ? list : { value: 'antiforgery' };
        }
    };
    class Item {
        dataset = { notificationId: 'test-notification', read: 'false' };
        disabled = false;
        closest(selector) { return selector === '[data-notification-id]' ? this : null; }
        querySelector() { return { classList: { toggle() {} } }; }
    }
    const item = new Item();
    const readAllButton = new Item();
    readAllButton.closest = selector => selector === '[data-notification-read-all]' ? readAllButton : null;
    vm.runInNewContext(source, {
        Element: Item,
        CustomEvent: class { constructor(type) { this.type = type; } },
        FormData: class { fields = {}; append(key, value) { this.fields[key] = value; } },
        window: { location: { assign() {} } },
        document: {
            querySelector: () => root, getElementById: () => panel, createElement: () => ({}),
            addEventListener: (name, handler) => handlers[name] = handler,
            dispatchEvent: event => events.push(event.type)
        },
        fetch: async (url, options) => {
            calls.push({ url, options });
            const response = responses.shift();
            if (response instanceof Error) throw response;
            return response;
        }
    });
    return { item, status, calls, events, async read(target = item) {
        handlers.click({ target });
        await new Promise(setImmediate);
    }, readAllButton };
}
const response = (status, data, type = 'application/json') => ({
    status, ok: status >= 200 && status < 300, headers: { get: () => type }, json: async () => data
});

test('confirmed menu read refreshes the authoritative badge and notifies the full list', async () => {
    const state = setup([response(200, { refresh: true }), response(200, { unreadCount: 0, items: [] })]);
    await state.read();
    assert.equal(state.calls.length, 2);
    assert.equal(state.calls[0].options.body.fields.id, 'test-notification');
    assert.deepEqual(state.events, ['acadprep:notifications-changed']);
});

test('a forbidden read displays the server error without announcing a successful change', async () => {
    const state = setup([response(403, { message: 'Không có quyền.' })]);
    await state.read();
    assert.equal(state.calls.length, 1);
    assert.equal(state.status.textContent, 'Không có quyền.');
    assert.equal(state.item.disabled, false);
    assert.deepEqual(state.events, []);
});

test('a vanished notification refreshes both views instead of keeping stale items', async () => {
    const state = setup([response(404, { refresh: true }), response(200, { unreadCount: 0, items: [] })]);
    await state.read();
    assert.deepEqual(state.events, ['acadprep:notifications-changed']);
});

test('an HTML response is not mistaken for a completed read', async () => {
    const state = setup([response(200, {}, 'text/html')]);
    await state.read();
    assert.match(state.status.textContent, /Chưa đánh dấu/);
    assert.deepEqual(state.events, []);
});

test('read all refreshes the menu and full list without passing an individual ID', async () => {
    const state = setup([response(200, { refresh: true }), response(200, { unreadCount: 0, items: [] })]);
    await state.read(state.readAllButton);
    assert.equal(state.calls[0].url, '/Notifications?handler=ReadAll');
    assert.deepEqual(Object.keys(state.calls[0].options.body.fields), ['__RequestVerificationToken']);
    assert.deepEqual(state.events, ['acadprep:notifications-changed']);
});
