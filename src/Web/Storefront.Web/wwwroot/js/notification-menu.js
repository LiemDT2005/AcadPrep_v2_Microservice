// Facebook-style notification dropdown for the navbar bell (Huy). Clicking the bell opens the newest notifications;
// clicking an unread one marks it read; only "Xem tất cả" goes to /Notifications. All data comes from the
// Notifications page handlers (server-side relay to the Gateway); the JWT never reaches the browser.
(function () {
    "use strict";
    if (window.__acadprepNotificationMenu) return;
    window.__acadprepNotificationMenu = true;

    const headers = { "X-Requested-With": "XMLHttpRequest" };
    const root = () => document.querySelector("[data-notification-menu]");
    const panel = () => document.getElementById("notification-menu-panel");
    const toggle = () => root()?.querySelector("[data-notification-toggle]");
    let loading = false;

    function escapeHtml(value) {
        return String(value).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;").replace(/'/g, "&#039;");
    }
    function relativeTime(iso) {
        const seconds = Math.max(0, (Date.now() - new Date(iso).getTime()) / 1000);
        if (seconds < 60) return "Vừa xong";
        if (seconds < 3600) return `${Math.floor(seconds / 60)} phút trước`;
        if (seconds < 86400) return `${Math.floor(seconds / 3600)} giờ trước`;
        if (seconds < 7 * 86400) return `${Math.floor(seconds / 86400)} ngày trước`;
        return new Date(iso).toLocaleDateString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" });
    }
    function login() {
        window.location.assign("/Account/Login?ReturnUrl=" + encodeURIComponent(window.location.pathname + window.location.search));
    }
    function setStatus(text) {
        const status = root()?.querySelector("[data-notification-menu-status]");
        if (!status) return;
        status.textContent = text || "";
        status.hidden = !text;
    }
    function renderBadge(count) {
        const link = toggle();
        if (!link) return;
        link.querySelectorAll("span:not(.material-symbols-outlined)").forEach(node => node.remove());
        link.title = `${count} thông báo chưa đọc`;
        if (count > 0) {
            const badge = document.createElement("span");
            badge.className = "rounded-full bg-error text-white text-xs px-1.5 min-w-5 text-center";
            badge.textContent = count > 99 ? "99+" : String(count);
            link.appendChild(badge);
        }
        const unread = root()?.querySelector("[data-notification-menu-unread]");
        if (unread) unread.textContent = count > 0 ? `${count} chưa đọc` : "";
    }
    function render(data) {
        const list = root()?.querySelector("[data-notification-menu-list]");
        if (!list) return;
        renderBadge(data.unreadCount);
        setStatus(data.items.length ? "" : "Bạn chưa có thông báo nào.");
        list.innerHTML = data.items.map(item => `
            <li>
                <button type="button" data-notification-id="${escapeHtml(item.id)}" data-read="${item.isRead}"
                        class="w-full text-left px-md py-sm flex gap-sm items-start hover:bg-surface-container-low ${item.isRead ? "" : "bg-primary/5"}">
                    <span class="mt-1.5 w-2 h-2 rounded-full shrink-0 ${item.isRead ? "bg-transparent" : "bg-primary"}" aria-hidden="true"></span>
                    <span class="min-w-0 flex-1">
                        <span class="block ${item.isRead ? "" : "font-semibold"}">${escapeHtml(item.title)}</span>
                        <span data-notification-content class="block text-body-sm text-on-surface-variant line-clamp-2">${escapeHtml(item.content)}</span>
                        <span class="block text-xs ${item.isRead ? "text-on-surface-variant" : "text-primary"} mt-0.5">${escapeHtml(relativeTime(item.createdAt))}${item.isRead ? "" : " · Chưa đọc"}</span>
                    </span>
                </button>
            </li>`).join("");
    }
    async function load() {
        if (loading) return;
        loading = true;
        setStatus("Đang tải…");
        try {
            const response = await fetch(`${root().dataset.endpoint}?handler=Latest`,
                { headers, credentials: "same-origin", cache: "no-store" });
            if (response.status === 401) return login();
            if (!response.ok) throw new Error();
            render(await response.json());
        } catch {
            setStatus("Không thể tải thông báo. Vui lòng thử lại.");
        } finally {
            loading = false;
        }
    }
    async function markRead(button, handler = "Read") {
        const token = root()?.querySelector('input[name="__RequestVerificationToken"]')?.value ?? "";
        const body = new FormData();
        if (handler === "Read") body.append("id", button.dataset.notificationId);
        body.append("__RequestVerificationToken", token);
        button.disabled = true;
        try {
            const response = await fetch(`${root().dataset.endpoint}?handler=${handler}`,
                { method: "POST", headers, credentials: "same-origin", body });
            if (response.status === 401) return login();
            if (!(response.headers.get("content-type") || "").includes("application/json")) throw new Error();
            const result = await response.json();
            if (!response.ok && !result.refresh) {
                setStatus(result.message || "Chưa đánh dấu được thông báo. Vui lòng thử lại.");
                button.disabled = false;
                return;
            }
            await load(); // re-read the real state: never guess the unread count
            if (result.refresh) document.dispatchEvent(new CustomEvent("acadprep:notifications-changed"));
        } catch {
            setStatus("Chưa đánh dấu được thông báo. Vui lòng thử lại.");
        } finally {
            button.disabled = false;
        }
    }
    function open() {
        panel().hidden = false;
        toggle()?.setAttribute("aria-expanded", "true");
        load();
    }
    function close() {
        if (!panel() || panel().hidden) return;
        panel().hidden = true;
        toggle()?.setAttribute("aria-expanded", "false");
    }

    // Delegated: the notifications page may replace the bell element after an action.
    document.addEventListener("click", event => {
        const target = event.target instanceof Element ? event.target : null;
        if (!target || !root()) return;
        if (target.closest("[data-notification-toggle]")) {
            event.preventDefault();
            panel().hidden ? open() : close();
            return;
        }
        const readAll = target.closest("[data-notification-read-all]");
        if (readAll && panel()?.contains(readAll)) {
            markRead(readAll, "ReadAll");
            return;
        }
        const item = target.closest("[data-notification-id]");
        if (item && panel()?.contains(item)) {
            item.querySelector("[data-notification-content]")?.classList.toggle("line-clamp-2");
            if (item.dataset.read !== "true") markRead(item);
            return;
        }
        if (!root().contains(target)) close();
    });
    document.addEventListener("keydown", event => {
        if (event.key === "Escape" && panel() && !panel().hidden) { close(); toggle()?.focus(); }
    });
})();
