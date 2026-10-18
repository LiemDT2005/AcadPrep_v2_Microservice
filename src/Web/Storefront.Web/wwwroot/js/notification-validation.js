(function ($) {
    "use strict";
    const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    $.validator.addMethod("notificationtext", function (value) {
        return String(value || "").trim().length > 0;
    });
    $.validator.addMethod("notificationaudience", function (value, element, other) {
        const audience = element.form.elements[other].value;
        const target = String(value || "");
        return audience === "User"
            ? guid.test(target) && target !== "00000000-0000-0000-0000-000000000000"
            : target.length === 0;
    });
    $.validator.unobtrusive.adapters.add("notificationaudience", ["other"], function (options) {
        const prefix = options.element.name.slice(0, options.element.name.lastIndexOf(".") + 1);
        options.rules.notificationaudience = prefix + options.params.other;
        options.messages.notificationaudience = options.message;
    });
    $(function () {
        const form = document.getElementById("notification-create");
        if (!form) return;
        const audience = form.elements["Input.Audience"];
        const target = form.elements["Input.TargetUserId"];
        const title = form.elements["Input.Title"];
        $(title).rules("add", { normalizer: function (value) { return value.trim(); } });
        $(form.elements["Input.Content"]).rules("add", {
            notificationtext: true,
            messages: { notificationtext: "Vui lòng nhập nội dung." }
        });
        function update() {
            target.setAttribute("aria-required", audience.value === "User" ? "true" : "false");
            $(target).valid();
        }
        audience.addEventListener("change", update);
        form.addEventListener("submit", function () {
            title.value = title.value.trim();
        });
    });
})(jQuery);
