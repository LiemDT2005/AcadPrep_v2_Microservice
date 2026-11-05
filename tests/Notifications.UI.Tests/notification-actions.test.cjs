const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');
const source = fs.readFileSync(path.join(__dirname, '../../src/Web/Storefront.Web/wwwroot/js/notification-actions.js'), 'utf8');

function setup(responses) {
    const handlers = {};
    const calls = [], redirects = [];
    const button = { disabled: false };
    const panel = {
        setAttribute() {}, removeAttribute() {}, querySelectorAll: () => [button],
        replaceWith(next) { this.replacement = next; }
    };
    const status = { textContent: '' };
    const badge = { replaceWith(next) { this.replacement = next; } };
    const nextPanel = { querySelector: () => null };
    const nextBadge = { countFromServer: 5 };
    class Form {
        method = 'post'; action = 'https://localhost/Notifications?handler=Read';
        closest() { return panel; }
    }
    const form = new Form();
    vm.runInNewContext(source, {
        HTMLFormElement: Form,
        FormData: class { constructor(value) { this.form = value; } },
        DOMParser: class { parseFromString() { return {
            getElementById: () => nextPanel, querySelectorAll: () => [nextBadge]
        }; } },
        document: {
            getElementById: id => id === 'notification-action-status' ? status : panel,
            addEventListener: (name, fn) => handlers[name] = fn,
            querySelectorAll: () => [badge], importNode: node => node
        },
        window: { location: { pathname: '/Notifications', search: '?unreadOnly=true',
            href: 'https://localhost/Notifications?unreadOnly=true', assign: url => redirects.push(url) } },
        fetch: async (url, options) => {
            calls.push({ url, options });
            const next = responses.shift();
            if (next instanceof Error) throw next;
            return next;
        }
    });
    return { calls, redirects, panel, status, button, badge, nextPanel, nextBadge,
        changed: () => handlers['acadprep:notifications-changed'](),
        submit: (cancelled = false) => handlers.submit({ target: form, defaultPrevented: cancelled, preventDefault() {} }) };
}
function response(status, type, data) {
    return { status, ok: status >= 200 && status < 300, headers: { get: () => type },
        json: async () => data, text: async () => '<section></section>' };
}

test('successful action refreshes panel and authoritative badge without navigation', async () => {
    const state = setup([response(200, 'application/json', { message: 'Done', refresh: true }), response(200, 'text/html')]);
    await state.submit();
    assert.equal(state.calls.length, 2);
    assert.equal(state.calls[0].options.method, 'POST');
    assert.ok(state.calls[0].options.body.form);
    assert.equal(state.calls[0].options.redirect, 'error');
    assert.equal(state.panel.replacement, state.nextPanel);
    assert.equal(state.badge.replacement, state.nextBadge);
    assert.equal(state.status.textContent, 'Done');
    assert.equal(state.button.disabled, false);
    assert.equal(state.redirects.length, 0);
});
test('expired session navigates to login with local return URL', async () => {
    const state = setup([response(401, 'application/json')]);
    await state.submit();
    assert.equal(state.calls.length, 1);
    assert.match(state.redirects[0], /^\/Account\/Login\?ReturnUrl=/);
});
test('forbidden action does not alter the list or badge', async () => {
    const state = setup([response(403, 'application/json', { message: 'Forbidden' })]);
    await state.submit();
    assert.equal(state.status.textContent, 'Forbidden');
    assert.equal(state.calls.length, 1);
    assert.equal(state.panel.replacement, undefined);
});
test('HTML response is not treated as action success', async () => {
    const state = setup([response(200, 'text/html')]);
    await state.submit();
    assert.match(state.status.textContent, /Chưa xác nhận/);
    assert.equal(state.panel.replacement, undefined);
});
test('refresh failure preserves confirmation without reporting the write failed', async () => {
    const state = setup([response(200, 'application/json', { message: 'Done', refresh: true }), new Error('offline')]);
    await state.submit();
    assert.match(state.status.textContent, /^Done.*Chưa thể tải lại/);
    assert.equal(state.panel.replacement, undefined);
});
test('cancelled confirmation sends no request', async () => {
    const state = setup([]);
    await state.submit(true);
    assert.equal(state.calls.length, 0);
});
test('concurrent submits send only one write', async () => {
    const state = setup([response(200, 'application/json', { message: 'Done' })]);
    await Promise.all([state.submit(), state.submit()]);
    assert.equal(state.calls.length, 1);
});

test('reading from the menu refreshes the current filtered page without replacing the open menu', async () => {
    const state = setup([response(200, 'text/html')]);
    await state.changed();
    assert.equal(state.calls.length, 1);
    assert.match(state.calls[0].url, /unreadOnly=true/);
    assert.equal(state.panel.replacement, state.nextPanel);
    assert.equal(state.badge.replacement, undefined);
});

test('menu refresh failure leaves the list intact and tells the user to refresh', async () => {
    const state = setup([new Error('offline')]);
    await state.changed();
    assert.equal(state.panel.replacement, undefined);
    assert.match(state.status.textContent, /Chưa thể tải lại danh sách/);
});

test('a menu change during a page write is refreshed after the write completes', async () => {
    let finishWrite;
    const state = setup([new Promise(resolve => finishWrite = resolve), response(200, 'text/html'), response(200, 'text/html')]);
    const writing = state.submit();
    await state.changed();
    finishWrite(response(200, 'application/json', { message: 'Done', refresh: true }));
    await writing;
    assert.equal(state.calls.length, 3);
    assert.equal(state.panel.replacement, state.nextPanel);
});
