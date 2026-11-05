// In-page confirmation dialog (Huy) replacing window.confirm() on the Notification, AI and Billing pages, styled
// like the rest of Storefront. Usage: await window.acadprepConfirm("…") or <form data-confirm="…">.
(function () {
    "use strict";
    if (window.acadprepConfirm) return;

    let dialog;
    function build() {
        dialog = document.createElement("dialog");
        dialog.setAttribute("aria-labelledby", "acadprep-confirm-title");
        dialog.className = "rounded-2xl border border-outline-variant bg-surface-container-lowest text-on-surface p-0 shadow-2xl backdrop:bg-black/40";
        dialog.style.width = "min(420px, calc(100vw - 32px))";
        dialog.innerHTML = `
            <form method="dialog" class="p-lg">
                <h2 id="acadprep-confirm-title" class="font-semibold text-lg mb-sm">Xác nhận</h2>
                <p data-confirm-message class="text-body-md text-on-surface-variant mb-lg"></p>
                <div class="flex justify-end gap-sm">
                    <button value="cancel" class="rounded-lg border border-outline-variant px-md py-sm font-semibold">Hủy</button>
                    <button value="ok" data-confirm-ok class="rounded-lg bg-error text-white px-md py-sm font-semibold">Đồng ý</button>
                </div>
            </form>`;
        document.body.appendChild(dialog);
    }

    window.acadprepConfirm = function (message, options) {
        if (!dialog) build();
        dialog.querySelector("[data-confirm-message]").textContent = message;
        dialog.querySelector("[data-confirm-ok]").textContent = options?.confirmText || "Đồng ý";
        dialog.returnValue = "";
        return new Promise(resolve => {
            dialog.addEventListener("close", () => resolve(dialog.returnValue === "ok"), { once: true });
            dialog.showModal();
            dialog.querySelector("[data-confirm-ok]").focus();
        });
    };

    // <form data-confirm="message" [data-confirm-text="Xóa"]>: ask first, then submit normally (other submit
    // handlers such as notification-actions.js still run on the confirmed submit).
    document.addEventListener("submit", async event => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement) || !form.dataset.confirm) return;
        if (form.dataset.confirmed === "true") { delete form.dataset.confirmed; return; }
        event.preventDefault();
        event.stopImmediatePropagation();
        if (!await window.acadprepConfirm(form.dataset.confirm, { confirmText: form.dataset.confirmText })) return;
        form.dataset.confirmed = "true";
        form.requestSubmit(event.submitter ?? undefined);
    }, true);
})();
