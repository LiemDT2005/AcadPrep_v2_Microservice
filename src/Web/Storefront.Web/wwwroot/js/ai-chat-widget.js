// AcadPrep AI Tutor widget (FTOEIC-style, Huy 2026-11-05): server-side sessions with a picker, rolling 24h
// token usage ring + popover + 7-day chart, quota lock with Retry. Talks only to /Ai/Chat handlers (no JWT here).
(function () {
    "use strict";
    const root = document.getElementById("ai-chat-widget");
    if (!root) return;
    const $ = id => document.getElementById(id);
    const endpoint = root.dataset.endpoint || "/Ai/Chat";
    const antiforgery = () => (root.querySelector("input[name=__RequestVerificationToken]") || {}).value || "";
    const TZ = "Asia/Ho_Chi_Minh";
    const RING = 94.25;
    const WELCOME = "Chào bạn! Mình là AcadPrep AI Tutor. Bạn có câu hỏi nào về từ vựng, ngữ pháp hay TOEIC không?";
    const state = { loaded: false, sessionId: null, sessions: [], usage: null, pending: null, waiting: false, lockTimer: null };

    const els = {
        win: $("ai-chat-window"), toggle: $("ai-chat-toggle"), close: $("ai-chat-close"), list: $("ai-chat-messages"),
        form: $("ai-chat-form"), input: $("ai-chat-input"), send: $("ai-chat-send"), title: $("ai-session-title"),
        sessionToggle: $("ai-session-toggle"), sessionMenu: $("ai-session-menu"), newChat: $("ai-chat-new"),
        del: $("ai-chat-delete"), ring: $("ai-usage-ring"), ringValue: $("ai-usage-ring-value"), popover: $("ai-usage-popover"),
        lock: $("ai-lock-banner"), lockText: $("ai-lock-text"), retry: $("ai-lock-retry"), detail: $("ai-usage-detail")
    };

    // ---------- helpers ----------
    function escapeHtml(text) {
        return String(text).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;").replace(/'/g, "&#039;");
    }
    // The tutor is told not to use LaTeX, but older answers (and the odd new one) may still contain simple symbols
    // such as $\rightarrow$; show them as plain characters instead of raw markup.
    const LATEX_SYMBOLS = { rightarrow: "→", to: "→", leftarrow: "←", Rightarrow: "⇒", Leftarrow: "⇐",
        leftrightarrow: "↔", Leftrightarrow: "⇔", times: "×", neq: "≠", ne: "≠", approx: "≈", le: "≤", leq: "≤",
        ge: "≥", geq: "≥", checkmark: "✓" };
    function replaceLatexSymbols(text) {
        return String(text).replace(/\$?\\([A-Za-z]+)\$?/g, (match, name) =>
            Object.prototype.hasOwnProperty.call(LATEX_SYMBOLS, name) ? LATEX_SYMBOLS[name] : match);
    }
    function renderMarkdown(text) {
        text = replaceLatexSymbols(text);
        if (window.marked && window.DOMPurify) return window.DOMPurify.sanitize(window.marked.parse(text, { breaks: true }));
        return escapeHtml(text).replace(/\n/g, "<br>");
    }
    const fmtNumber = n => Number(n || 0).toLocaleString("vi-VN");
    function fmtTime(iso) {
        return new Intl.DateTimeFormat("vi-VN", { timeZone: TZ, hour: "2-digit", minute: "2-digit", day: "2-digit", month: "2-digit" }).format(new Date(iso));
    }
    function fmtRelative(iso) {
        const ms = new Date(iso).getTime() - Date.now();
        if (ms <= 0) return "ngay bây giờ";
        const minutes = Math.ceil(ms / 60000);
        const h = Math.floor(minutes / 60), m = minutes % 60;
        return h > 0 ? `${h} giờ ${m} phút` : `${m} phút`;
    }
    async function call(handler, options, query) {
        const url = `${endpoint}?handler=${handler}${query || ""}`;
        try {
            const response = await fetch(url, Object.assign({ credentials: "same-origin", headers: { "X-Requested-With": "XMLHttpRequest" } }, options));
            if (response.redirected) return { status: 401, data: { message: "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại." } };
            const type = (response.headers.get("content-type") || "").toLowerCase();
            if (!type.includes("json")) return { status: response.ok ? 502 : response.status, data: { message: "Máy chủ trả về định dạng ngoài dự kiến." } };
            return { status: response.status, data: await response.json() };
        } catch (e) {
            return { status: 0, data: { message: "AI đang bận hoặc mất kết nối mạng. Vui lòng thử lại." } };
        }
    }
    const post = (handler, body, query) => call(handler, {
        method: "POST", body: body ? JSON.stringify(body) : null,
        headers: { "Content-Type": "application/json", "RequestVerificationToken": antiforgery(), "X-Requested-With": "XMLHttpRequest" }
    }, query);

    // ---------- messages ----------
    function bubble(role, content, extraHtml) {
        const row = document.createElement("div");
        const user = role === "user";
        row.className = `flex gap-2 max-w-[88%] ${user ? "self-end flex-row-reverse" : "self-start items-end"}`;
        const icon = user ? "person" : (role === "system" ? "info" : "smart_toy");
        const cls = user ? "bg-primary text-white rounded-2xl rounded-br-none px-3 py-2"
            : role === "system" ? "bg-error-container text-on-error-container rounded-2xl rounded-bl-none px-3 py-2"
            : "bg-surface-container-lowest text-on-surface border border-outline-variant rounded-2xl rounded-bl-none px-3 py-2 ai-bubble";
        row.innerHTML = `<div class="w-7 h-7 rounded-full flex items-center justify-center shrink-0 ${user ? "bg-primary text-white" : "bg-surface-container-lowest border border-outline-variant/40 text-primary"}"><span class="material-symbols-outlined text-[16px]">${icon}</span></div>` +
            `<div class="${cls}">${role === "model" ? renderMarkdown(content) : escapeHtml(content)}${extraHtml || ""}</div>`;
        els.list.appendChild(row);
        els.list.scrollTop = els.list.scrollHeight;
        return row;
    }
    function typing(on) {
        const old = $("ai-typing");
        if (old) old.remove();
        if (!on) return;
        const row = document.createElement("div");
        row.id = "ai-typing";
        row.className = "self-start flex gap-1 px-3 py-2 rounded-2xl bg-surface-container-lowest border border-outline-variant";
        row.innerHTML = '<span class="w-2 h-2 rounded-full bg-primary/50 animate-bounce"></span><span class="w-2 h-2 rounded-full bg-primary/50 animate-bounce" style="animation-delay:150ms"></span><span class="w-2 h-2 rounded-full bg-primary/50 animate-bounce" style="animation-delay:300ms"></span>';
        els.list.appendChild(row);
        els.list.scrollTop = els.list.scrollHeight;
    }
    function newChat() {
        state.sessionId = null;
        els.title.textContent = "Cuộc trò chuyện mới";
        els.del.disabled = true;
        els.list.innerHTML = "";
        bubble("model", WELCOME);
    }

    // ---------- usage ring / popover / lock ----------
    function renderUsage(usage) {
        if (!usage) return;
        state.usage = usage;
        const started = !!usage.windowStart;
        const percent = started ? Math.min(100, usage.percentUsed || 0) : 0;
        els.ringValue.setAttribute("stroke-dashoffset", String(RING * (1 - percent / 100)));
        els.ring.title = started ? `Đã dùng ${percent}%` : "Chưa sử dụng";
        $("ai-usage-percent").textContent = `${percent}%`;
        $("ai-usage-bar").setAttribute("aria-valuenow", String(percent));
        $("ai-usage-bar-value").style.width = `${percent}%`;
        $("ai-usage-numbers").textContent = started ? `${fmtNumber(usage.tokensUsed)} / ${fmtNumber(usage.tokenLimit)} token` : "Chưa sử dụng";
        $("ai-usage-remaining").textContent = started ? `Còn lại ${fmtNumber(usage.remaining)} token` : `Hạn mức ${fmtNumber(usage.tokenLimit)} token / 24 giờ`;
        $("ai-usage-reset").textContent = started && usage.resetAt ? `Đặt lại sau ${fmtRelative(usage.resetAt)} (${fmtTime(usage.resetAt)})` : "Bắt đầu tính khi bạn gửi tin đầu tiên";
        $("ai-detail-percent").textContent = `${percent}%`;
        $("ai-detail-bar").style.width = `${percent}%`;
        $("ai-detail-numbers").textContent = $("ai-usage-numbers").textContent;
        $("ai-detail-reset").textContent = $("ai-usage-reset").textContent;
        if (usage.locked && usage.resetAt) lock(usage.resetAt); else unlock();
    }
    function lock(resetAt) {
        els.input.disabled = true;
        els.send.disabled = true;
        els.lock.hidden = false;
        const update = () => {
            if (new Date(resetAt).getTime() <= Date.now()) { unlock(); refreshState(); return; }
            els.lockText.textContent = `Bạn đã dùng hết hạn mức hôm nay. Thử lại sau ${fmtRelative(resetAt)} (${fmtTime(resetAt)}).`;
        };
        update();
        clearInterval(state.lockTimer);
        state.lockTimer = setInterval(update, 30000);
    }
    function unlock() {
        clearInterval(state.lockTimer);
        els.lock.hidden = true;
        els.input.disabled = false;
        els.send.disabled = state.waiting || !els.input.value.trim();
    }

    async function loadDaily() {
        const chart = $("ai-daily-chart"), labels = $("ai-daily-labels");
        chart.innerHTML = labels.innerHTML = "";
        const result = await call("Daily", { method: "GET" });
        if (result.status !== 200) { $("ai-daily-total").textContent = result.data.message || "Chưa tải được biểu đồ."; return; }
        const days = result.data.days || [], limit = Math.max(1, result.data.tokenLimit || 1);
        const weekday = new Intl.DateTimeFormat("vi-VN", { weekday: "short", timeZone: "UTC" });
        days.forEach((day, index) => {
            const date = new Date(`${day.date}T00:00:00Z`);
            const height = Math.min(100, (day.tokens / limit) * 100);
            const bar = document.createElement("div");
            bar.className = `flex-1 rounded-t ${day.tokens > limit ? "bg-error" : "bg-primary"} ${index === days.length - 1 ? "" : "opacity-80"}`;
            bar.style.height = `${day.tokens > 0 ? Math.max(height, 2) : 0}%`;
            bar.title = `${weekday.format(date)}, ${day.date.slice(8, 10)}/${day.date.slice(5, 7)} · ${fmtNumber(day.tokens)} token`;
            bar.setAttribute("aria-label", bar.title);
            chart.appendChild(bar);
            const label = document.createElement("span");
            label.className = "flex-1 text-center";
            label.textContent = index === days.length - 1 ? "Hôm nay" : weekday.format(date);
            labels.appendChild(label);
        });
        $("ai-daily-total").textContent = `Tổng 7 ngày: ${fmtNumber(result.data.totalTokens)} token · Hạn mức ${fmtNumber(limit)} token / 24 giờ`;
    }

    // ---------- sessions ----------
    function renderSessions() {
        els.sessionMenu.innerHTML = "";
        const add = (label, id) => {
            const item = document.createElement("button");
            item.type = "button";
            item.setAttribute("role", "option");
            item.className = `w-full text-left px-3 py-2 rounded-lg hover:bg-surface-container-high truncate ${id === state.sessionId ? "font-semibold text-primary" : ""}`;
            item.textContent = label;
            item.addEventListener("click", () => { closeMenus(); id ? openSession(id) : newChat(); });
            els.sessionMenu.appendChild(item);
        };
        add("+ Cuộc trò chuyện mới", null);
        state.sessions.forEach(session => add(session.title, session.id));
    }
    async function openSession(id) {
        const result = await call("Messages", { method: "GET" }, `&sessionId=${encodeURIComponent(id)}`);
        if (result.status !== 200) { bubble("system", result.data.message || "Không tải được cuộc trò chuyện."); return; }
        state.sessionId = id;
        const session = state.sessions.find(item => item.id === id);
        els.title.textContent = session ? session.title : "Cuộc trò chuyện";
        els.del.disabled = false;
        els.list.innerHTML = "";
        (result.data.items || []).forEach(message => bubble(message.role === "User" ? "user" : "model", message.content));
    }
    async function refreshState() {
        const result = await call("State", { method: "GET" });
        if (result.status === 401) { bubble("system", result.data.message); return; }
        if (result.status !== 200) return;
        state.loaded = true;
        renderUsage(result.data.usage);
        state.sessions = (result.data.sessions && result.data.sessions.items) || [];
        renderSessions();
    }

    // ---------- send / retry ----------
    async function send(text, requestId) {
        if (state.waiting) return;
        state.waiting = true;
        els.send.disabled = true;
        state.pending = { text, requestId };
        typing(true);
        const result = await post("Send", { requestId, sessionId: state.sessionId, message: text });
        typing(false);
        state.waiting = false;
        if (result.status === 200) {
            state.pending = null;
            const data = result.data;
            if (!state.sessionId) {
                state.sessionId = data.sessionId;
                els.title.textContent = data.sessionTitle;
                els.del.disabled = false;
                state.sessions.unshift({ id: data.sessionId, title: data.sessionTitle });
                renderSessions();
            }
            bubble("model", data.reply);
            renderUsage(data.usage);
        } else if (result.status === 429) {
            bubble("system", result.data.message || "Bạn đã dùng hết hạn mức hôm nay. Thử lại sau.");
            if (result.data.usage) renderUsage(Object.assign({}, result.data.usage, { locked: result.data.code !== "MessageTooLarge" }));
            if (result.data.code === "MessageTooLarge") state.pending = null;
        } else if (result.status === 401 || result.status === 400 || result.status === 403 || result.status === 404) {
            state.pending = null;
            bubble("system", result.data.message || "Không thể gửi tin nhắn.");
        } else {
            // Network/timeout/5xx/409: keep the same RequestId so a retry never answers or charges twice.
            const row = bubble("system", result.data.message || "AI đang bận. Vui lòng thử lại.",
                '<button type="button" class="ai-retry mt-1 block font-semibold underline">Thử lại</button>');
            row.querySelector(".ai-retry").addEventListener("click", () => { row.remove(); retry(); });
        }
        if (!els.input.disabled) els.send.disabled = !els.input.value.trim();
    }
    function retry() {
        if (state.pending) send(state.pending.text, state.pending.requestId);
        else refreshState();
    }
    function newRequestId() {
        if (window.crypto && window.crypto.randomUUID) return window.crypto.randomUUID();
        return "10000000-1000-4000-8000-100000000000".replace(/[018]/g, c => (c ^ (Math.random() * 16) >> c / 4).toString(16));
    }

    // ---------- UI wiring ----------
    function closeMenus() {
        els.sessionMenu.hidden = true; els.popover.hidden = true;
        els.sessionToggle.setAttribute("aria-expanded", "false"); els.ring.setAttribute("aria-expanded", "false");
    }
    function toggleWindow() {
        const open = els.win.hidden;
        els.win.hidden = !open;
        els.toggle.setAttribute("aria-expanded", String(open));
        if (open) {
            if (!state.loaded) { newChat(); refreshState(); }
            els.input.focus();
        } else closeMenus();
    }
    els.toggle.addEventListener("click", toggleWindow);
    els.close.addEventListener("click", toggleWindow);
    els.newChat.addEventListener("click", () => { closeMenus(); els.detail.hidden = true; newChat(); });
    els.sessionToggle.addEventListener("click", event => {
        event.stopPropagation();
        const open = els.sessionMenu.hidden;
        closeMenus();
        els.sessionMenu.hidden = !open;
        els.sessionToggle.setAttribute("aria-expanded", String(open));
    });
    els.ring.addEventListener("click", event => {
        event.stopPropagation();
        const open = els.popover.hidden;
        closeMenus();
        els.popover.hidden = !open;
        els.ring.setAttribute("aria-expanded", String(open));
    });
    $("ai-usage-see-all").addEventListener("click", () => { closeMenus(); els.detail.hidden = false; loadDaily(); });
    $("ai-usage-back").addEventListener("click", () => { els.detail.hidden = true; });
    els.retry.addEventListener("click", retry);
    els.del.addEventListener("click", async () => {
        if (!state.sessionId) return;
        const message = "Bạn có chắc muốn xóa cuộc trò chuyện này? Token đã dùng không được hoàn lại.";
        const ok = window.acadprepConfirm ? await window.acadprepConfirm(message, { confirmText: "Xóa" }) : window.confirm(message);
        if (!ok) return;
        const result = await post("Delete", null, `&sessionId=${encodeURIComponent(state.sessionId)}`);
        if (result.status !== 200) { bubble("system", result.data.message || "Không xóa được cuộc trò chuyện."); return; }
        state.sessions = state.sessions.filter(item => item.id !== state.sessionId);
        renderSessions();
        newChat();
    });
    document.addEventListener("click", event => { if (!els.popover.contains(event.target) && !els.sessionMenu.contains(event.target)) closeMenus(); });
    document.addEventListener("keydown", event => { if (event.key === "Escape") closeMenus(); });
    els.form.addEventListener("submit", event => {
        event.preventDefault();
        const text = els.input.value.trim();
        if (!text || state.waiting || els.input.disabled) return;
        els.input.value = "";
        els.input.style.height = "auto";
        bubble("user", text);
        send(text, newRequestId());
    });
    els.input.addEventListener("keydown", event => {
        if (event.key === "Enter" && !event.shiftKey) { event.preventDefault(); els.form.requestSubmit(); }
    });
    els.input.addEventListener("input", function () {
        this.style.height = "auto";
        this.style.height = `${this.scrollHeight}px`;
        els.send.disabled = state.waiting || !this.value.trim();
    });

    // Exposed for unit tests only.
    window.__aiChatWidget = { renderUsage, state, fmtRelative };
})();
