// Admin "Gửi thông báo" page (Huy): live preview of the bell item and the recipient field shown only for
// "Một người dùng". Text is set with textContent (never HTML), matching how notifications are rendered.
(function () {
    "use strict";
    const form = document.getElementById("notification-create");
    if (!form) return;
    const title = form.querySelector("[data-preview='title']");
    const content = form.querySelector("[data-preview='content']");
    const audience = form.querySelector("[data-audience]");
    const target = form.querySelector("[data-target-field]");
    const help = document.getElementById("target-help");
    const out = name => document.querySelector(`[data-preview-${name}]`);

    function update() {
        out("title").textContent = title.value.trim() || "Tiêu đề thông báo";
        out("content").textContent = content.value.trim() || "Nội dung sẽ hiển thị ở đây.";
        out("audience").textContent = audience.options[audience.selectedIndex]?.text ?? "";
        const single = audience.value === "User";
        // Hidden only when it is empty: a value typed for another audience stays visible so its error can be fixed.
        const input = target.querySelector("input");
        const hide = !single && !input.value.trim();
        target.hidden = hide;
        help.hidden = hide;
    }
    [title, content, audience].forEach(element => element.addEventListener("input", update));
    audience.addEventListener("change", update);
    update();
})();
