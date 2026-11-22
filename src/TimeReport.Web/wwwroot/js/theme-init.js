(() => {
    try {
        const saved = localStorage.getItem("timereport-theme");
        if (saved === "light" || saved === "dark") {
            document.documentElement.dataset.theme = saved;
        } else if (saved === "system") {
            document.documentElement.dataset.theme =
                window.matchMedia("(prefers-color-scheme: light)").matches ? "light" : "dark";
        }
    } catch {
        // Storage can be disabled; the server-rendered dark theme remains.
    }
})();
