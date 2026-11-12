// Drives the reconnect dialog in App.razor. Blazor adds the components-reconnect-* classes and raises
// components-reconnect-state-changed; this opens and closes the dialog and wires up its buttons.
// Lives here instead of inline handlers because the Content-Security-Policy blocks them.
(function () {
    var modal = document.getElementById('components-reconnect-modal');
    if (!modal) return;

    function retryWhenVisible() {
        if (document.visibilityState === 'visible') retry();
    }

    async function retry() {
        document.removeEventListener('visibilitychange', retryWhenVisible);
        try {
            if (await Blazor.reconnect()) return;
            // The server no longer has this circuit; try to resume it, or start over
            if (await Blazor.resumeCircuit()) {
                modal.close();
            } else {
                location.reload();
            }
        } catch (e) {
            document.addEventListener('visibilitychange', retryWhenVisible);
        }
    }

    async function resume() {
        try {
            if (!await Blazor.resumeCircuit()) location.reload();
        } catch (e) {
            modal.classList.replace('components-reconnect-paused', 'components-reconnect-resume-failed');
        }
    }

    modal.addEventListener('components-reconnect-state-changed', function (e) {
        var state = e.detail.state;
        if (state === 'show' || state === 'paused') {
            if (!modal.open) modal.showModal();
        } else if (state === 'hide') {
            modal.close();
        } else if (state === 'failed') {
            document.addEventListener('visibilitychange', retryWhenVisible);
        } else if (state === 'rejected') {
            location.reload();
        }
    });

    // The dialog only closes when the connection comes back
    modal.addEventListener('cancel', function (e) { e.preventDefault(); });

    document.getElementById('components-reconnect-button').addEventListener('click', retry);
    document.getElementById('components-resume-button').addEventListener('click', resume);
    document.getElementById('components-reload-button').addEventListener('click', function () { location.reload(); });
})();
