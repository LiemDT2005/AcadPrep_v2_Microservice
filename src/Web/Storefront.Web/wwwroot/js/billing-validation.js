(function ($) {
    "use strict";
    const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    // Mirrors BillingGuidAttribute; the server always validates again.
    $.validator.addMethod("billingguid", function (value) {
        const text = String(value || "").trim();
        return text.length === 0 || (guid.test(text) && text !== "00000000-0000-0000-0000-000000000000");
    });
    $.validator.unobtrusive.adapters.addBool("billingguid");
    // Mirrors ValidPlanForCheckoutAttribute: the chosen plan must be one of the paid plans on sale.
    $.validator.addMethod("validplan", function (value, element, allowed) {
        const id = String(value || "").toLowerCase();
        return id.length > 0 && String(allowed || "").toLowerCase().split(",").indexOf(id) >= 0;
    });
    $.validator.unobtrusive.adapters.add("validplan", ["allowed"], function (options) {
        options.rules.validplan = options.params.allowed;
        options.messages.validplan = options.message;
    });
    // Mirrors BillingDateRangeAttribute: yyyy-MM-dd strings compare correctly as text.
    $.validator.addMethod("billingdaterange", function (value, element, other) {
        const start = element.form.elements[other] ? element.form.elements[other].value : "";
        return !value || !start || value >= start;
    });
    $.validator.unobtrusive.adapters.add("billingdaterange", ["other"], function (options) {
        const prefix = options.element.name.slice(0, options.element.name.lastIndexOf(".") + 1);
        options.rules.billingdaterange = prefix + options.params.other;
        options.messages.billingdaterange = options.message;
    });
    $(function () {
        const form = document.getElementById("billing-grant");
        if (!form) return;
        form.addEventListener("submit", function (event) {
            if (form.dataset.submitting === "true") { event.preventDefault(); return; }
            if ($(form).valid()) {
                form.dataset.submitting = "true";
                const button = form.querySelector("button[type=submit]");
                if (button) button.disabled = true;
            }
        });
    });
})(jQuery);
