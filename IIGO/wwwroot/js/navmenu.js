// Closes the mobile sidebar after a link click, and the account menu on an outside click or Escape.
// Lives here instead of inline handlers because the Content-Security-Policy blocks them.
function closeNavAndMenus(except) {
    var toggle = document.getElementById('sidebar-toggle');
    if (toggle && !except) toggle.checked = false;
    document.querySelectorAll('details.account-menu[open]').forEach(function (menu) {
        if (!except || !menu.contains(except)) menu.removeAttribute('open');
    });
}

document.addEventListener('click', function (e) {
    var toggle = document.getElementById('sidebar-toggle');
    if (toggle && e.target.closest('.app-sidebar a')) toggle.checked = false;
    closeNavAndMenus(e.target);
});

document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') closeNavAndMenus(null);
});
