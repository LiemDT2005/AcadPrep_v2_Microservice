(function ($) {
    "use strict";
    // Mirrors AiTokenLimitAttribute: whole number of tokens between 0 and max; the server validates again.
    $.validator.addMethod("aitokenlimit", function (value, element, max) {
        const text = String(value || "").trim();
        return text.length === 0 || (/^\d+$/.test(text) && Number(text) <= Number(max));
    });
    $.validator.unobtrusive.adapters.add("aitokenlimit", ["max"], function (options) {
        options.rules.aitokenlimit = options.params.max;
        options.messages.aitokenlimit = options.message;
    });
    $(function () {
        document.querySelectorAll("form.ai-limit-form").forEach(function (form) { $.validator.unobtrusive.parse(form); });
    });
})(jQuery);
