const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const now = Date.parse('2026-11-05T12:00:00Z');
class Clock extends Date { static now() { return now; } }
const elements = new Map();
const document = {
    getElementById(id) {
        if (!elements.has(id)) elements.set(id, { dataset: {}, addEventListener() {} });
        return elements.get(id);
    },
    addEventListener() {}
};
const window = {};
vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../../src/Web/Storefront.Web/wwwroot/js/ai-chat-widget.js'), 'utf8'),
    { document, window, Date: Clock });

for (const [milliseconds, expected] of [
    [-1, 'ngay bây giờ'], [0, 'ngay bây giờ'], [1, '1 phút'],
    [59000, '1 phút'], [3599999, '1 giờ 0 phút'], [3600000, '1 giờ 0 phút'],
    [3600001, '1 giờ 1 phút'], [86399999, '24 giờ 0 phút'], [86400000, '24 giờ 0 phút']
]) {
    test(`countdown at ${milliseconds}ms never renders 60 minutes`, () => {
        assert.equal(window.__aiChatWidget.fmtRelative(new Date(now + milliseconds).toISOString()), expected);
    });
}
