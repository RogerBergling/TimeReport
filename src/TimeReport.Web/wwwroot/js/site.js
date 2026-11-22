(() => {
    "use strict";

    const root = document.documentElement;

    const safeStorage = {
        get(key) {
            try { return localStorage.getItem(key); } catch { return null; }
        },
        set(key, value) {
            try { localStorage.setItem(key, value); } catch { /* ignore */ }
        },
    };

    // Theme -------------------------------------------------------------
    const themeButton = document.getElementById("theme-toggle");
    if (themeButton) {
        themeButton.addEventListener("click", () => {
            const next = root.dataset.theme === "light" ? "dark" : "light";
            root.dataset.theme = next;
            safeStorage.set("timereport-theme", next);
            themeButton.setAttribute(
                "aria-label",
                next === "light" ? "Switch to dark theme" : "Switch to light theme");
            window.dispatchEvent(new CustomEvent("timereport:theme", { detail: next }));
        });
    }

    // Matrix background --------------------------------------------------
    const matrixButton = document.getElementById("matrix-toggle");
    const matrixOff = safeStorage.get("timereport-matrix") === "off";
    if (matrixOff) {
        root.dataset.matrix = "off";
        matrixButton?.setAttribute("aria-pressed", "false");
    }
    matrixButton?.addEventListener("click", () => {
        const off = root.dataset.matrix === "off";
        if (off) {
            delete root.dataset.matrix;
        } else {
            root.dataset.matrix = "off";
        }
        safeStorage.set("timereport-matrix", off ? "on" : "off");
        matrixButton.setAttribute("aria-pressed", off ? "true" : "false");
        window.dispatchEvent(new CustomEvent("timereport:matrix", { detail: off }));
    });

    // Mobile navigation --------------------------------------------------
    const menuButton = document.getElementById("menu-toggle");
    const mobileNav = document.getElementById("mobile-nav");
    if (menuButton && mobileNav) {
        menuButton.addEventListener("click", () => {
            const open = menuButton.getAttribute("aria-expanded") !== "true";
            menuButton.setAttribute("aria-expanded", open ? "true" : "false");
            mobileNav.hidden = !open;
        });
    }

    // Confirmation prompts ----------------------------------------------
    document.querySelectorAll("form[data-confirm]").forEach((form) => {
        form.addEventListener("submit", (event) => {
            const message = form.dataset.confirm || "Continue?";
            if (!window.confirm(message)) {
                event.preventDefault();
            }
        });
    });

    // Copy buttons -------------------------------------------------------
    document.querySelectorAll("[data-copy-target]").forEach((button) => {
        button.addEventListener("click", async () => {
            const target = document.getElementById(button.dataset.copyTarget);
            if (!target) return;
            const text = target.value || target.textContent || "";
            try {
                await navigator.clipboard.writeText(text.trim());
                const previous = button.textContent;
                button.textContent = "Copied";
                setTimeout(() => { button.textContent = previous; }, 1400);
            } catch {
                target.focus();
                target.select?.();
            }
        });
    });

    // Password visibility ------------------------------------------------
    document.querySelectorAll("[data-toggle-password]").forEach((button) => {
        button.addEventListener("click", () => {
            const target = document.getElementById(button.dataset.togglePassword);
            if (!target) return;
            const show = target.type === "password";
            target.type = show ? "text" : "password";
            button.setAttribute("aria-pressed", show ? "true" : "false");
            button.setAttribute("aria-label", show ? "Hide password" : "Show password");
        });
    });

    // Auto-submit selects ------------------------------------------------
    document.querySelectorAll("select[data-auto-submit]").forEach((select) => {
        select.addEventListener("change", () => {
            select.form?.requestSubmit();
        });
    });

    // Entry type visibility ----------------------------------------------
    const typeSelect = document.getElementById("TypeId");
    if (typeSelect) {
        const projectField = document.querySelector("[data-entry-project]");
        const awayField = document.querySelector("[data-entry-away]");
        const projectSelect = projectField?.querySelector("select");
        const update = () => {
            const option = typeSelect.options[typeSelect.selectedIndex];
            const category = option?.dataset.category || "work";
            if (projectField) projectField.hidden = category !== "work";
            if (awayField) awayField.hidden = category !== "away";
            if (projectSelect && category !== "work") projectSelect.value = "";
        };
        typeSelect.addEventListener("change", update);
        update();
    }

    // Print ---------------------------------------------------------------
    document.querySelectorAll("[data-print]").forEach((button) => {
        button.addEventListener("click", () => window.print());
    });

    // Demo helpers -------------------------------------------------------
    const demoOtp = document.querySelector("[data-demo-otp]");
    if (demoOtp) {
        const refreshDemoOtp = async () => {
            try {
                const response = await fetch("/demo/otp", {
                    headers: { "Accept": "application/json" },
                    cache: "no-store",
                });
                if (!response.ok) return;
                const payload = await response.json();
                if (payload && payload.code) demoOtp.textContent = payload.code;
            } catch {
                // Keep the server-rendered code if the refresh fails.
            }
        };
        refreshDemoOtp();
        setInterval(refreshDemoOtp, 5000);
    }

    document.querySelectorAll("[data-demo-fill]").forEach((button) => {
        button.addEventListener("click", () => {
            const username = document.getElementById("Username");
            const password = document.getElementById("Password");
            if (username) username.value = "guest";
            if (password) password.value = "guest";
            username?.focus();
        });
    });

    // Matrix canvas ------------------------------------------------------
    const canvas = document.getElementById("matrix");
    if (!canvas || !canvas.getContext) return;
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;

    const context = canvas.getContext("2d");
    const glyphs = "01{}[]()<>/\\|=+-_TOTALShoursminutesweekmonth0123456789";
    let columns = 0;
    let drops = [];
    let fontSize = 14;
    let lastFrame = 0;
    let frame = 0;

    function matrixEnabled() {
        return root.dataset.matrix !== "off";
    }

    function resize() {
        canvas.width = Math.max(1, window.innerWidth);
        canvas.height = Math.max(1, window.innerHeight);
        fontSize = window.innerWidth < 700 ? 12 : 14;
        columns = Math.ceil(canvas.width / (fontSize * 1.15));
        drops = Array.from({ length: columns }, () => Math.random() * -40);
    }

    function color() {
        const rgb = getComputedStyle(root).getPropertyValue("--matrix-rgb").trim() || "56, 189, 248";
        return `rgba(${rgb}, 0.42)`;
    }

    function draw(now) {
        frame = requestAnimationFrame(draw);
        if (!matrixEnabled() || document.hidden) return;
        if (now - lastFrame < 55) return;
        lastFrame = now;

        context.fillStyle = "rgba(7, 11, 20, 0.10)";
        if (root.dataset.theme === "light") {
            context.fillStyle = "rgba(244, 247, 252, 0.14)";
        }
        context.fillRect(0, 0, canvas.width, canvas.height);
        context.font = `${fontSize}px ui-monospace, monospace`;
        context.fillStyle = color();

        for (let index = 0; index < drops.length; index++) {
            const char = glyphs[Math.floor(Math.random() * glyphs.length)];
            const x = index * fontSize * 1.15;
            const y = drops[index] * fontSize;
            context.fillText(char, x, y);
            if (y > canvas.height && Math.random() > 0.975) {
                drops[index] = 0;
            } else {
                drops[index] += 1;
            }
        }
    }

    resize();
    window.addEventListener("resize", resize, { passive: true });
    window.addEventListener("timereport:theme", () => {
        context.clearRect(0, 0, canvas.width, canvas.height);
    });
    frame = requestAnimationFrame(draw);
    window.addEventListener("beforeunload", () => cancelAnimationFrame(frame), { once: true });
})();
