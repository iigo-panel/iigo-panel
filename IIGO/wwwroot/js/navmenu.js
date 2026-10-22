// Collapses the mobile nav menu after a click inside it. Lives here instead of an inline
// onclick because the Content-Security-Policy blocks inline handlers.
document.addEventListener('click', function (e) {
    if (e.target.closest('.nav-scrollable')) {
        document.querySelector('.navbar-toggler')?.click();
    }
});
