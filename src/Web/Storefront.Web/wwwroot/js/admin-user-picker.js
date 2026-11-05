// Admin recipient picker (Huy): type an email or a name into an <input data-user-picker="handlerUrl">, pick an
// account from the suggestions, and the input receives the account GUID (the value the form and its FE/BE
// validation already expect). A GUID can still be pasted directly.
(function () {
    "use strict";
    const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    const escapeHtml = value => String(value ?? "").replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;").replace(/'/g, "&#039;");

    document.querySelectorAll("input[data-user-picker]").forEach(input => {
        const wrapper = document.createElement("div");
        wrapper.className = "relative";
        input.parentNode.insertBefore(wrapper, input);
        wrapper.appendChild(input);
        const list = document.createElement("ul");
        list.id = `${input.id}-suggestions`;
        list.setAttribute("role", "listbox");
        list.hidden = true;
        list.className = "absolute left-0 right-0 mt-1 z-50 max-h-64 overflow-y-auto rounded-lg border border-outline-variant bg-surface-container-lowest shadow-lg";
        wrapper.appendChild(list);
        const chosen = document.createElement("p");
        chosen.className = "text-body-sm text-primary mt-1";
        chosen.setAttribute("aria-live", "polite");
        wrapper.after(chosen);
        input.setAttribute("autocomplete", "off");
        input.setAttribute("aria-autocomplete", "list");
        input.setAttribute("aria-controls", list.id);
        input.placeholder = input.placeholder || "Nhập email hoặc tên để tìm, hoặc dán ID";

        let timer = 0, request = 0;
        const close = () => { list.hidden = true; input.setAttribute("aria-expanded", "false"); };
        const pick = item => {
            input.value = item.id;
            chosen.textContent = `Đã chọn: ${item.email} — ${item.fullName}`;
            close();
            input.dispatchEvent(new Event("change", { bubbles: true }));
            if (window.jQuery?.fn?.valid) window.jQuery(input).valid();
        };
        async function search(text) {
            const current = ++request;
            try {
                const response = await fetch(`${input.dataset.userPicker}&q=${encodeURIComponent(text)}`,
                    { headers: { "X-Requested-With": "XMLHttpRequest" }, credentials: "same-origin" });
                if (current !== request) return;
                const data = response.ok ? await response.json() : { items: [], message: "Không thể tìm người dùng lúc này." };
                const items = data.items || [];
                list.innerHTML = items.length
                    ? items.map((item, index) => `<li role="option" tabindex="-1" data-index="${index}"
                          class="px-md py-sm cursor-pointer hover:bg-surface-container-low">
                          <span class="block font-semibold">${escapeHtml(item.email)}</span>
                          <span class="block text-body-sm text-on-surface-variant">${escapeHtml(item.fullName)} · ${escapeHtml((item.roles || []).join(", "))} · ${escapeHtml(item.status)}</span>
                      </li>`).join("")
                    : `<li class="px-md py-sm text-body-sm text-on-surface-variant">${escapeHtml(data.message || "Không tìm thấy người dùng phù hợp.")}</li>`;
                list.querySelectorAll("[data-index]").forEach(node => node.addEventListener("mousedown", event => {
                    event.preventDefault();
                    pick(items[Number(node.dataset.index)]);
                }));
                list.hidden = false;
                input.setAttribute("aria-expanded", "true");
            } catch {
                if (current === request) close();
            }
        }
        input.addEventListener("input", () => {
            chosen.textContent = "";
            clearTimeout(timer);
            const text = input.value.trim();
            if (text.length < 2 || GUID.test(text)) { close(); return; }
            timer = setTimeout(() => search(text), 300);
        });
        input.addEventListener("keydown", event => {
            if (event.key === "Escape") close();
            if (event.key === "Enter" && !list.hidden) {
                const first = list.querySelector("[data-index]");
                if (first) { event.preventDefault(); first.dispatchEvent(new MouseEvent("mousedown")); }
            }
        });
        input.addEventListener("blur", () => setTimeout(close, 150));
    });
})();
