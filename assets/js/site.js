// Theme toggle (same idea as the panel's theme.js) and mobile menu close-on-click.
(function () {
    var root = document.documentElement;
    var media = window.matchMedia ? window.matchMedia("(prefers-color-scheme: dark)") : null;

    function current() {
        var set = root.getAttribute("data-theme");
        if (set) return set;
        return media && media.matches ? "dark" : "light";
    }

    var toggle = document.getElementById("theme-toggle");
    if (toggle) {
        toggle.addEventListener("click", function () {
            var next = current() === "dark" ? "light" : "dark";
            root.setAttribute("data-theme", next);
            try { localStorage.setItem("iigo-theme", next); } catch (e) { }
        });
    }

    var menu = document.querySelector(".mobile-menu");
    if (menu) {
        menu.addEventListener("click", function (e) {
            if (e.target.closest("a")) menu.removeAttribute("open");
        });
        document.addEventListener("click", function (e) {
            if (!menu.contains(e.target)) menu.removeAttribute("open");
        });
    }

    var year = document.getElementById("year");
    if (year) year.textContent = new Date().getFullYear();
})();
