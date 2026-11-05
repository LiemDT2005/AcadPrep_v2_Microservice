(function () {
    "use strict";
    let busy = false;
    let refreshPending = false;
    const headers = { "X-Requested-With": "XMLHttpRequest" };
    const status = document.getElementById("notification-action-status");
    if (!status) return;

    function requireSession(response) {
        if (response.status === 401) {
            window.location.assign("/Account/Login?ReturnUrl=" + encodeURIComponent(window.location.pathname + window.location.search));
            return false;
        }
        return true;
    }

    async function refreshPanel(updateBadges) {
        const refreshed = await fetch(window.location.href, { headers, credentials: "same-origin", redirect: "error", cache: "no-store" });
        if (!requireSession(refreshed)) return;
        if (!refreshed.ok || !(refreshed.headers.get("content-type") || "").includes("text/html")) throw new Error();
        const documentCopy = new DOMParser().parseFromString(await refreshed.text(), "text/html");
        const nextPanel = documentCopy.getElementById("notification-panel");
        if (!nextPanel || nextPanel.querySelector('[role="alert"]')) throw new Error();
        document.getElementById("notification-panel").replaceWith(document.importNode(nextPanel, true));
        if (updateBadges) {
            const badges = documentCopy.querySelectorAll("[data-notification-badge]");
            document.querySelectorAll("[data-notification-badge]").forEach((badge, index) => {
                if (badges[index]) badge.replaceWith(document.importNode(badges[index], true));
            });
        }
    }

    async function refreshFromMenu() {
        if (busy) { refreshPending = true; return; }
        busy = true;
        try {
            // The menu already refreshed its own badge/items. Keep it open while refreshing the list/filter/paging.
            await refreshPanel(false);
        } catch {
            status.textContent = "Thông báo đã thay đổi. Chưa thể tải lại danh sách. Vui lòng làm mới trang.";
        } finally {
            busy = false;
            if (refreshPending) { refreshPending = false; await refreshFromMenu(); }
        }
    }
    document.addEventListener("acadprep:notifications-changed", refreshFromMenu);

    document.addEventListener("submit", async function (event) {
        const form = event.target;
        if (!(form instanceof HTMLFormElement) || !form.closest("#notification-panel") || form.method.toLowerCase() !== "post" || event.defaultPrevented) return;
        event.preventDefault();
        if (busy) return;
        busy = true;
        const panel = document.getElementById("notification-panel");
        panel.setAttribute("aria-busy", "true");
        panel.querySelectorAll("button").forEach(button => button.disabled = true);
        status.textContent = "Đang xử lý…";
        let message = "";
        let confirmed = false;
        try {
            const response = await fetch(form.action, {
                method: "POST", headers, credentials: "same-origin", redirect: "error",
                body: new FormData(form)
            });
            if (!requireSession(response)) return;
            if (!(response.headers.get("content-type") || "").includes("application/json")) throw new Error();
            const result = await response.json();
            message = result.message || "Thao tác chưa thành công.";
            confirmed = response.ok;
            status.textContent = message;
            if (!result.refresh) return;

            // Re-render from the real page/API: never guess badge deltas or paging totals.
            await refreshPanel(true);
        } catch {
            status.textContent = confirmed
                ? message + " Chưa thể tải lại danh sách. Vui lòng làm mới trang."
                : "Chưa xác nhận được kết quả thao tác. Vui lòng làm mới trang để kiểm tra.";
        } finally {
            busy = false;
            const current = document.getElementById("notification-panel");
            current?.removeAttribute("aria-busy");
            current?.querySelectorAll("button").forEach(button => button.disabled = false);
            if (refreshPending) { refreshPending = false; await refreshFromMenu(); }
        }
    });
})();
