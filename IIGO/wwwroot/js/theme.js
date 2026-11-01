// Applies the saved light/dark theme and sidebar state before the page paints, and handles the
// theme and sidebar buttons. A static file because the Content-Security-Policy blocks inline scripts.
(function () {
    var root = document.documentElement;
    var themeKey = 'iigo-theme';
    var sidebarKey = 'iigo-sidebar';

    function read(key) {
        try { return localStorage.getItem(key); } catch (e) { return null; }
    }

    function write(key, value) {
        try { localStorage.setItem(key, value); } catch (e) { }
    }

    function currentTheme() {
        var saved = read(themeKey);
        if (saved === 'light' || saved === 'dark') return saved;
        return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    }

    function apply() {
        var theme = currentTheme();
        if (root.getAttribute('data-bs-theme') !== theme) root.setAttribute('data-bs-theme', theme);
        var sidebar = read(sidebarKey) === 'collapsed' ? 'collapsed' : 'expanded';
        if (root.getAttribute('data-sidebar') !== sidebar) root.setAttribute('data-sidebar', sidebar);
    }

    apply();

    // Enhanced navigation can reset <html> attributes to the server markup; put them back
    new MutationObserver(apply).observe(root, { attributes: true, attributeFilter: ['data-bs-theme', 'data-sidebar'] });

    // Follow the OS setting until the user picks a theme
    if (window.matchMedia) {
        var query = window.matchMedia('(prefers-color-scheme: dark)');
        if (query.addEventListener) query.addEventListener('change', apply);
    }

    document.addEventListener('click', function (e) {
        var button = e.target.closest('[data-iigo-action]');
        if (!button) return;
        var action = button.getAttribute('data-iigo-action');
        if (action === 'toggle-theme') {
            write(themeKey, root.getAttribute('data-bs-theme') === 'dark' ? 'light' : 'dark');
            apply();
        } else if (action === 'toggle-sidebar') {
            write(sidebarKey, root.getAttribute('data-sidebar') === 'collapsed' ? 'expanded' : 'collapsed');
            apply();
        } else if (action === 'toggle-nav') {
            var toggle = document.getElementById('sidebar-toggle');
            if (toggle) toggle.checked = !toggle.checked;
        }
    });
})();
