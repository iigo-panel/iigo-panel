# IIGO Panel Redesign Plan (issue #297)

Branch: `redesign`. This plan is presentation-only. Follow it in order, one phase per commit.

## 0. Ground rules (read first)

Do:
- Change markup, CSS, layout components, shared presentational components and the two static JS files.
- Keep every route, `@page`, `@attribute [Authorize...]`, `AuthorizeView` (including `Policy`, `Roles` and `Context` names), `IsInRole` check, `@rendermode`, `@inject`, `@onclick`/`@bind` target, `EditForm` model/handler, and every `@code` block exactly as it is, except where a step below says otherwise.
- Keep all button and label text unless a step gives new text.
- Use Bootstrap 5.3 classes first. Add custom CSS only to `wwwroot/css/app.css`.

Do not:
- Add packages, npm, a build step, chart libraries, or CDN links.
- Change `IIGO/Program.cs` (the CSP lives there), services, data, or routes.
- Add inline `<script>` blocks or `on*=""` HTML attributes. The CSP blocks them.
- Use Bootstrap JS dropdowns, tooltips or popovers (`data-bs-toggle="dropdown"` etc.). The page loads `bootstrap.min.js` without Popper, so they throw. Use the `<details>` pattern and `title` attributes in this plan instead.
- Remove or change any timer, event subscription, or `Dispose` method.
- Add `@onclick` or any other Blazor event to a statically rendered file (list in section 1.2).
- Add AI attribution to commits.

The project can't be built on Linux. After each phase: run `git diff --check`, re-read every changed `.razor` file for balanced tags and braces, and grep that no static file gained an `@on` event. The Windows build happens later.

## 1. Current state (survey summary)

### 1.1 Shell
- `Components/App.razor`: Bootstrap 5.3.2 CSS (jsDelivr), Bootstrap Icons 1.11.3, `blazor.bootstrap.css`, `css/app.css`, `IIGO.styles.css`. Scripts at the end of body: `blazor.web.js`, `bootstrap.min.js` (cdnjs, no Popper), `blazor.bootstrap.js`, `js/navmenu.js`.
- `Components/Routes.razor`: `<StatefulReconnection />` (NuGet package, owns the reconnect UI), `AuthorizeRouteView` with `DefaultLayout=MainLayout`, `FocusOnNavigate Selector="h1"`.
- `Layout/MainLayout.razor`: `.page` > `.sidebar` (NavMenu) + `main` (top row shows `Environment.MachineName`; `article.content`; fixed `.bottom-row` footer with logo, year, iigo.dev link, version, build date). `#blazor-error-ui` at the end.
- `Layout/MainLayout.razor.css`: blue-to-purple gradient sidebar (`#35ACFF` 70%, `#A05BEC` 90%), hard-coded light greys, 641px breakpoint, notes that `blazor.bootstrap.css` sets `main` to 100vh.
- `Layout/NavMenu.razor`: flat list. Home, Websites [ViewWebsites], App Pools [ViewAppPools], Check updates [Administrator role], Processes + Status [ViewSystemStatus, one AuthorizeView], Firewall [ViewFirewallRules], Task Scheduler [ViewScheduledTasks], Services [ViewServices], License Status [Administrator], Users [Administrator], Roles [ViewRoles], API Keys [ViewApiKeys], user name link to `Account/Manage`, Logout form (with `currentUrl` ReturnUrl code). NotAuthorized: Login. Icons are `oi oi-list-rich` (open-iconic is not loaded, so they render nothing) and custom `bi-*-nav-menu` SVG classes from `NavMenu.razor.css`.
- `Layout/NavMenu.razor.css`: checkbox-hack mobile toggle, scoped `.bi` rule that forces 1.25rem background-image icons.
- `wwwroot/js/navmenu.js`: closes the mobile menu after a click in `.nav-scrollable`.
- `wwwroot/css/app.css`: Helvetica font, hard-coded link and `.btn-primary` colors, white-ring focus shadow, validation outlines, `.blazor-error-boundary`, `.darker-border-checkbox`. None of it adapts to dark mode.
- `wwwroot/css/site.css`: not referenced anywhere. Dead file.

### 1.2 Render modes (important)
- The layouts (`MainLayout`, `NavMenu`, `AccountLayout`, `ManageLayout`) render statically. Interactivity is set per page. Anything in the shell (theme toggle, sidebar toggle, account menu) must work with plain JS or no JS.
- Interactive pages (`@rendermode InteractiveServer`): Home, Websites, WebsiteDetails, AddWebsite, AppPools, AppPool, AppPoolApplications, AppPoolMonitor, HttpsMonitor, Firewall, Processes, Status, Tasks, CheckUpdates, Users, User, AddUser, Roles, RolePermissions, ApiKeys.
- Static pages (no `@rendermode`): Services (`[StreamRendering]`), LicenseStatus, Configuration, Auth, Error, every page under `Account/`.

### 1.3 Behaviors that must survive untouched
- Timers: Status (1 s `System.Timers.Timer`), AppPools (5 s), MonitorStatus (10 s `System.Threading.Timer`). CheckUpdates subscribes to `WindowsUpdateService.Changed`. Processes samples CPU in `OnAfterRenderAsync`.
- Render-time side effects. These must still run exactly once per row per render:
  - Status: `var sent = i.PCSent!.NextValue();` and `var received = i.PCReceived!.NextValue();` (calling `NextValue()` twice changes the reading).
  - Services: `GetServiceDescription(...)` and `GetDelayedStatus(...)`.
  - Processes: `GetProcessUser(proc)` and `GetCommandLineOfProcess(proc)`.
- Confirmations: Blazor.Bootstrap `ConfirmDialog` on Websites, WebsiteDetails, Firewall, ApiKeys. `window.confirm` via `JsRuntime` on Users (delete), Roles (delete), User (disable MFA).
- Toasts: Blazor.Bootstrap `<Toasts ... Placement="ToastsPlacement.TopRight" />` on Websites, AppPools, Firewall, ApiKeys.
- Modals: Blazor.Bootstrap `<Modal>` on WebsiteDetails (3) and Firewall (1).
- EnableAuthenticator loads `/lib/qrcode.min.js` and `/js/qr.js` with `<script src>` and uses ids `qrCode` and `qrCodeData`.
- `MustChangePasswordMiddleware` redirects flagged users to `/Account/Manage/ChangePassword`. Static files are served before it, so new CSS/JS files are fine.

### 1.4 Visual problems to fix
No icons in the nav, flat ungrouped nav, mixed `h1`/`h3` page titles, raw `<p><em>Loading...</em></p>`, plain text states (Started/Stopped), `&nbsp;` button spacing, every row action a full-size solid button, inline truncation styles, `border-top:2px solid #000` on Status (invisible in dark), inline green/red text on LicenseStatus, `.container` nested inside content on WebsiteDetails, `form-control` on a select in AddWebsite, Identity pages using the scaffold layout (`col-md-4` forms with `h1` + `h2` + `hr`), no dark mode.

## 2. Design direction

A clean hosting control panel:
- Dark navy sidebar in both themes, grouped into sections (Dashboard, Web server, System, Administration), Bootstrap Icons on every link, the brand gradient kept as an accent (logo text and active-item bar). Collapses to an icon rail on desktop (persisted). Off-canvas with a backdrop below 992px.
- Sticky top bar: sidebar toggle, server name, theme toggle, account menu (user name, Manage account, Log out).
- Light grey canvas, white cards (dark: near-black canvas, slate cards). One card per block of content.
- Every page starts with a `PageHeader`: optional breadcrumb, `h1` title, optional subtitle, actions on the right.
- Tables: hover, middle-aligned, small uppercase muted header, flush inside a card, horizontal scroll on small screens, sticky header on long lists.
- Status values shown as soft pill badges with a colored dot.
- Button hierarchy: one solid primary action per page; outline buttons for secondary and row actions; small icon+text row buttons.
- Consistent loading, empty, and error states.
- Light/dark via Bootstrap 5.3 `data-bs-theme` on `<html>`, defaulting to the OS setting, saved in `localStorage`.

## 3. Design tokens

All tokens live at the top of `wwwroot/css/app.css`. `app.css` loads after `bootstrap.min.css`, so on equal specificity it wins.

Rule: both theme blocks match `<html>` (the light block through `:root`). Any `--bs-*` color set in the light block MUST also be set in the dark block, or the light value leaks into dark mode.

| Token | Light | Dark | Used for |
|---|---|---|---|
| `--iigo-canvas` | `#f4f6fa` | `#0d121b` | page background (`body`) |
| `--iigo-surface` | `#ffffff` | `#141b27` | cards, top bar (via `--bs-body-bg`) |
| `--iigo-surface-muted` | `#f8f9fb` | `#19212f` | table header, card header |
| `--iigo-border` | `#e3e7ee` | `#263042` | all borders |
| `--iigo-text-muted` | `#667085` | `#98a2b3` | secondary text in custom CSS |
| `--iigo-shadow-sm` | soft 1-3px | darker 1-2px | cards |
| `--iigo-shadow` | 8px 24px | 8px 24px | menus, error bar |
| `--iigo-sidebar-*` | same in both | same in both | sidebar (always dark) |
| `--iigo-brand-gradient` | `#35acff` to `#a05bec` | same | logo text, active bar |
| `--iigo-sidebar-w` | `248px` | | sidebar width |
| `--iigo-sidebar-w-collapsed` | `68px` | | icon rail width |
| `--iigo-topbar-h` | `56px` | | top bar height, toast offset |

Mapping to Bootstrap variables:

| Bootstrap variable | Set to | Why |
|---|---|---|
| `--bs-body-bg` (+ `-rgb`) | surface | cards, inputs, modals, tables, dropdowns all read it |
| `--bs-secondary-bg` / `--bs-tertiary-bg` (+ `-rgb`) | slate-tinted greys | hover fills, code blocks |
| `--bs-border-color` | `--iigo-border` value | inputs, tables, dividers |
| `--bs-body-font-family` | `system-ui` stack (Segoe UI on Windows) | replaces Helvetica |
| `--bs-body-font-size` | `.9375rem` | denser admin UI |
| `--bs-border-radius` / `-sm` / `-lg` | `.5rem` / `.375rem` / `.75rem` | buttons, inputs, cards |
| `.card { --bs-card-border-color; --bs-card-cap-bg }` | border token / transparent | cards use the translucent border by default |
| `thead { --bs-table-bg }` | surface-muted | opaque sticky headers |

Primary color: keep Bootstrap's default blue. Remove the old `#1b6ec2` `.btn-primary` and `#006bb7` link overrides. Bootstrap's `.btn-primary`, focus rings and checkboxes are compiled from Sass, so recoloring primary would need many overrides. Not worth it.

Typography: page title 1.5rem/600, card title 1rem/600, table header .75rem uppercase 600 with .04em tracking, nav section title .6875rem uppercase.

Spacing: use Bootstrap spacing utilities (`g-3`, `mb-3`, `gap-2`). Content padding is 1rem on mobile, 1.5rem top / 2rem sides from 992px.

## 4. Files to create or replace

### 4.1 `wwwroot/css/app.css` (replace the whole file)

Copy the existing `.blazor-error-boundary` and `.blazor-error-boundary::after` rules (with the long base64 SVG) unchanged into the marked spot. Everything else below replaces the old file.

```css
/* IIGO styles. Loaded after bootstrap.css, so these rules win on equal specificity.
   Both theme blocks match <html>, so every --bs-* color in the light block must also be in the dark block. */

/* ---------- Tokens: light (also the default before theme.js runs) ---------- */
:root,
[data-bs-theme=light] {
    --iigo-canvas: #f4f6fa;
    --iigo-surface: #ffffff;
    --iigo-surface-muted: #f8f9fb;
    --iigo-border: #e3e7ee;
    --iigo-text-muted: #667085;
    --iigo-shadow-sm: 0 1px 2px rgba(16, 24, 40, .05), 0 1px 3px rgba(16, 24, 40, .06);
    --iigo-shadow: 0 8px 24px rgba(16, 24, 40, .12);

    --bs-body-bg: #ffffff;
    --bs-body-bg-rgb: 255, 255, 255;
    --bs-secondary-bg: #eef1f5;
    --bs-secondary-bg-rgb: 238, 241, 245;
    --bs-tertiary-bg: #f6f8fa;
    --bs-tertiary-bg-rgb: 246, 248, 250;
    --bs-border-color: #e3e7ee;
}

/* ---------- Tokens: dark ---------- */
[data-bs-theme=dark] {
    --iigo-canvas: #0d121b;
    --iigo-surface: #141b27;
    --iigo-surface-muted: #19212f;
    --iigo-border: #263042;
    --iigo-text-muted: #98a2b3;
    --iigo-shadow-sm: 0 1px 2px rgba(0, 0, 0, .35);
    --iigo-shadow: 0 8px 24px rgba(0, 0, 0, .5);

    --bs-body-bg: #141b27;
    --bs-body-bg-rgb: 20, 27, 39;
    --bs-secondary-bg: #1f2937;
    --bs-secondary-bg-rgb: 31, 41, 55;
    --bs-tertiary-bg: #19212f;
    --bs-tertiary-bg-rgb: 25, 33, 47;
    --bs-border-color: #263042;
}

/* ---------- Tokens: same in both themes ---------- */
:root {
    --iigo-sidebar-w: 248px;
    --iigo-sidebar-w-collapsed: 68px;
    --iigo-topbar-h: 56px;

    --iigo-sidebar-bg: #0f172a;
    --iigo-sidebar-border: rgba(255, 255, 255, .06);
    --iigo-sidebar-fg: #cbd5e1;
    --iigo-sidebar-fg-strong: #ffffff;
    --iigo-sidebar-heading: #94a3b8;
    --iigo-sidebar-hover-bg: rgba(255, 255, 255, .06);
    --iigo-sidebar-active-bg: rgba(53, 172, 255, .16);
    --iigo-brand-gradient: linear-gradient(135deg, #35acff 0%, #a05bec 100%);

    --bs-body-font-family: system-ui, -apple-system, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
    --bs-body-font-size: .9375rem;
    --bs-border-radius: .5rem;
    --bs-border-radius-sm: .375rem;
    --bs-border-radius-lg: .75rem;
}

/* ---------- Base ---------- */
body {
    background-color: var(--iigo-canvas);
}

h1:focus {
    outline: none;
}

.skip-link {
    position: absolute;
    left: -999px;
    top: .5rem;
    z-index: 2000;
    padding: .5rem 1rem;
    background: var(--bs-body-bg);
    border-radius: var(--bs-border-radius);
}

    .skip-link:focus {
        left: 1rem;
    }

/* ---------- App shell ---------- */
.app-shell {
    display: flex;
    min-height: 100vh;
}

.sidebar-toggle-input {
    position: absolute;
    width: 0;
    height: 0;
    opacity: 0;
    pointer-events: none;
}

.app-sidebar {
    position: fixed;
    top: 0;
    bottom: 0;
    left: 0;
    z-index: 1040;
    width: var(--iigo-sidebar-w);
    display: flex;
    flex-direction: column;
    background: var(--iigo-sidebar-bg);
    color: var(--iigo-sidebar-fg);
    border-right: 1px solid var(--iigo-sidebar-border);
    transform: translateX(-100%);
    transition: transform .2s ease, width .2s ease;
}

.sidebar-toggle-input:checked ~ .app-sidebar {
    transform: none;
}

.sidebar-backdrop {
    display: none;
}

.sidebar-toggle-input:checked ~ .sidebar-backdrop {
    display: block;
    position: fixed;
    inset: 0;
    z-index: 1035;
    background: rgba(0, 0, 0, .45);
}

.app-main {
    flex: 1 1 auto;
    min-width: 0;
    display: flex;
    flex-direction: column;
}

/* blazor.bootstrap.css sizes <main>; the class selector overrides it */
.app-content {
    flex: 1 0 auto;
    height: auto;
    padding: 1.25rem 1rem 2rem;
}

.app-footer {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: .5rem;
    padding: .75rem 1rem;
    font-size: .8125rem;
    color: var(--iigo-text-muted);
    border-top: 1px solid var(--iigo-border);
}

@media (min-width: 992px) {
    .app-sidebar {
        position: sticky;
        height: 100vh;
        flex: 0 0 var(--iigo-sidebar-w);
        transform: none;
    }

    .sidebar-toggle-input:checked ~ .sidebar-backdrop {
        display: none;
    }

    .app-content {
        padding: 1.5rem 2rem 2.5rem;
    }

    .app-footer {
        padding: .75rem 2rem;
    }

    html[data-sidebar=collapsed] .app-sidebar {
        width: var(--iigo-sidebar-w-collapsed);
        flex-basis: var(--iigo-sidebar-w-collapsed);
    }

    html[data-sidebar=collapsed] .app-sidebar .nav-label,
    html[data-sidebar=collapsed] .app-sidebar .nav-section-title,
    html[data-sidebar=collapsed] .app-sidebar .brand-text {
        display: none;
    }

    html[data-sidebar=collapsed] .sidebar-nav .nav-link,
    html[data-sidebar=collapsed] .sidebar-brand {
        justify-content: center;
    }
}

/* ---------- Top bar ---------- */
.app-topbar {
    position: sticky;
    top: 0;
    z-index: 1020;
    display: flex;
    align-items: center;
    gap: .5rem;
    height: var(--iigo-topbar-h);
    padding: 0 1rem;
    background: var(--bs-body-bg);
    border-bottom: 1px solid var(--iigo-border);
}

.topbar-server {
    display: flex;
    align-items: center;
    gap: .5rem;
    min-width: 0;
    font-weight: 600;
}

.btn-icon {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    gap: .375rem;
    min-width: 2.25rem;
    height: 2.25rem;
    padding: 0 .5rem;
    border: 1px solid transparent;
    border-radius: var(--bs-border-radius);
    background: transparent;
    color: var(--bs-secondary-color);
    cursor: pointer;
    list-style: none;
}

    .btn-icon:hover,
    .btn-icon:focus-visible {
        background: var(--bs-tertiary-bg);
        color: var(--bs-body-color);
    }

    .btn-icon::-webkit-details-marker {
        display: none;
    }

.theme-icon-light {
    display: none;
}

html[data-bs-theme=dark] .theme-icon-light {
    display: inline-block;
}

html[data-bs-theme=dark] .theme-icon-dark {
    display: none;
}

.account-menu {
    position: relative;
}

    .account-menu > .dropdown-menu {
        display: block;
        top: 100%;
        right: 0;
        left: auto;
        margin-top: .25rem;
        min-width: 14rem;
        box-shadow: var(--iigo-shadow);
    }

    .account-menu .dropdown-item {
        display: flex;
        align-items: center;
        gap: .5rem;
    }

/* ---------- Sidebar navigation ---------- */
.sidebar-brand {
    display: flex;
    align-items: center;
    gap: .625rem;
    flex-shrink: 0;
    height: var(--iigo-topbar-h);
    padding: 0 1.125rem;
    border-bottom: 1px solid var(--iigo-sidebar-border);
    color: var(--iigo-sidebar-fg-strong);
    text-decoration: none;
}

    .sidebar-brand img {
        width: 28px;
        height: 28px;
    }

.brand-text {
    font-size: 1.125rem;
    font-weight: 700;
    letter-spacing: .02em;
    background: var(--iigo-brand-gradient);
    -webkit-background-clip: text;
    background-clip: text;
    color: transparent;
}

.sidebar-nav {
    flex: 1 1 auto;
    overflow-y: auto;
    padding: .75rem .625rem 1rem;
}

.nav-section + .nav-section {
    margin-top: .75rem;
}

/* Hide a section whose links were all filtered out by authorization */
.nav-section:not(:has(.nav-link)) {
    display: none;
}

.nav-section-title {
    padding: .5rem .75rem .25rem;
    font-size: .6875rem;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: .08em;
    color: var(--iigo-sidebar-heading);
}

.sidebar-nav .nav-link {
    position: relative;
    display: flex;
    align-items: center;
    gap: .75rem;
    margin: 1px 0;
    padding: .5rem .75rem;
    border-radius: var(--bs-border-radius);
    color: var(--iigo-sidebar-fg);
    font-size: .875rem;
}

    .sidebar-nav .nav-link .bi {
        flex-shrink: 0;
        width: 1.25rem;
        font-size: 1.05rem;
        text-align: center;
    }

    .sidebar-nav .nav-link:hover {
        background: var(--iigo-sidebar-hover-bg);
        color: var(--iigo-sidebar-fg-strong);
    }

    .sidebar-nav .nav-link.active {
        background: var(--iigo-sidebar-active-bg);
        color: var(--iigo-sidebar-fg-strong);
    }

        .sidebar-nav .nav-link.active::before {
            content: "";
            position: absolute;
            left: -.625rem;
            top: .375rem;
            bottom: .375rem;
            width: 3px;
            border-radius: 0 3px 3px 0;
            background: var(--iigo-brand-gradient);
        }

/* ---------- Page header ---------- */
.page-header {
    display: flex;
    flex-wrap: wrap;
    align-items: flex-end;
    justify-content: space-between;
    gap: .75rem 1rem;
    margin-bottom: 1.25rem;
}

    .page-header .breadcrumb {
        margin-bottom: .25rem;
        font-size: .8125rem;
    }

.page-title-row {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: .5rem .75rem;
}

.page-title {
    margin: 0;
    font-size: 1.5rem;
    font-weight: 600;
    line-height: 1.25;
}

.page-subtitle {
    margin: .25rem 0 0;
    color: var(--iigo-text-muted);
}

.page-actions {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: .5rem;
}

/* ---------- Cards ---------- */
.card {
    --bs-card-border-color: var(--iigo-border);
    --bs-card-cap-bg: transparent;
    box-shadow: var(--iigo-shadow-sm);
}

.iigo-card-header {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: .5rem 1rem;
    padding: .875rem 1rem;
}

.iigo-card-title {
    margin: 0;
    font-size: 1rem;
    font-weight: 600;
}

.iigo-card-subtitle {
    margin: .125rem 0 0;
    font-size: .8125rem;
    color: var(--iigo-text-muted);
}

.stat-card .stat-label {
    display: flex;
    align-items: center;
    gap: .5rem;
    font-size: .8125rem;
    color: var(--iigo-text-muted);
}

.stat-card .stat-value {
    margin-top: .25rem;
    font-size: 1.5rem;
    font-weight: 600;
    font-variant-numeric: tabular-nums;
}

.stat-card .stat-meta {
    font-size: .8125rem;
    color: var(--iigo-text-muted);
}

/* Key/value list for detail pages */
.kv-list {
    display: grid;
    grid-template-columns: minmax(8rem, max-content) 1fr;
    gap: .5rem 1.5rem;
    margin: 0;
}

    .kv-list dt {
        font-weight: 500;
        color: var(--iigo-text-muted);
    }

    .kv-list dd {
        min-width: 0;
        margin: 0;
        overflow-wrap: anywhere;
    }

@media (max-width: 575.98px) {
    .kv-list {
        grid-template-columns: 1fr;
        gap: .125rem;
    }

        .kv-list dd {
            margin-bottom: .5rem;
        }
}

/* ---------- Tables ---------- */
.iigo-table {
    margin-bottom: 0;
}

    .iigo-table > thead {
        --bs-table-bg: var(--iigo-surface-muted);
    }

        .iigo-table > thead > tr > th {
            font-size: .75rem;
            font-weight: 600;
            text-transform: uppercase;
            letter-spacing: .04em;
            white-space: nowrap;
            color: var(--iigo-text-muted);
        }

    .iigo-table > :not(caption) > * > *:first-child {
        padding-left: 1rem;
    }

    .iigo-table > :not(caption) > * > *:last-child {
        padding-right: 1rem;
    }

    .iigo-table > tbody > tr:last-child > * {
        border-bottom-width: 0;
    }

/* Long lists: scroll inside the card so the header can stick */
.table-scroll {
    max-height: calc(100vh - 16rem);
    min-height: 12rem;
    overflow: auto;
}

    .table-scroll > .table > thead > tr > th {
        position: sticky;
        top: 0;
        z-index: 1;
    }

.cell-truncate {
    display: block;
    max-width: 16rem;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.cell-truncate-lg {
    max-width: 26rem;
}

.tabular-nums {
    font-variant-numeric: tabular-nums;
}

.row-actions {
    display: flex;
    flex-wrap: wrap;
    justify-content: flex-end;
    gap: .25rem;
}

/* ---------- Status badges ---------- */
.status-badge {
    display: inline-flex;
    align-items: center;
    gap: .375rem;
    font-weight: 500;
}

    .status-badge .bi {
        font-size: .45rem;
    }

/* ---------- Empty and loading states ---------- */
.empty-state {
    padding: 2.5rem 1rem;
    text-align: center;
    color: var(--iigo-text-muted);
}

.empty-state-icon {
    display: block;
    margin-bottom: .5rem;
    font-size: 2rem;
}

.empty-state-title {
    margin-bottom: .25rem;
    font-weight: 600;
    color: var(--bs-body-color);
}

.loading-state {
    display: flex;
    align-items: center;
    gap: .5rem;
    padding: 1.5rem 1rem;
    color: var(--iigo-text-muted);
}

/* ---------- Forms ---------- */
.form-actions {
    display: flex;
    flex-wrap: wrap;
    justify-content: flex-end;
    gap: .5rem;
    margin-top: 1rem;
    padding-top: 1rem;
    border-top: 1px solid var(--iigo-border);
}

.mw-input {
    max-width: 26rem;
}

.valid.modified:not([type=checkbox]) {
    outline: 1px solid var(--bs-form-valid-border-color);
}

.invalid {
    outline: 1px solid var(--bs-form-invalid-border-color);
}

.validation-message {
    color: var(--bs-form-invalid-color);
}

.darker-border-checkbox.form-check-input {
    border-color: #929292;
}

/* ---------- Account (Identity) pages ---------- */
.auth-shell {
    display: flex;
    flex-direction: column;
    min-height: 100vh;
}

.auth-topbar {
    display: flex;
    justify-content: flex-end;
    padding: .75rem 1rem;
}

.auth-main {
    flex: 1 0 auto;
    display: flex;
    justify-content: center;
    align-items: flex-start;
    padding: 6vh 1rem 2rem;
}

.auth-card {
    width: 100%;
    max-width: 26rem;
}

.auth-brand {
    display: flex;
    align-items: center;
    gap: .625rem;
    margin-bottom: 1.5rem;
    font-size: 1.25rem;
    font-weight: 700;
}

.auth-footer {
    padding: 1rem;
    text-align: center;
    font-size: .8125rem;
    color: var(--iigo-text-muted);
}

/* The QR code needs a light background to scan in dark mode */
.qr-code-frame {
    display: inline-block;
    padding: .75rem;
    background: #fff;
    border: 1px solid var(--iigo-border);
    border-radius: var(--bs-border-radius);
}

.recovery-codes {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(9rem, 1fr));
    gap: .5rem;
}

    .recovery-codes code {
        display: block;
        padding: .375rem .5rem;
        text-align: center;
        background: var(--bs-tertiary-bg);
        border-radius: var(--bs-border-radius-sm);
    }

/* ---------- Blazor and Blazor.Bootstrap ---------- */
/* Keep toasts below the sticky top bar */
.toast-container.top-0 {
    top: var(--iigo-topbar-h) !important;
}

#blazor-error-ui {
    display: none;
    position: fixed;
    left: 1rem;
    right: 1rem;
    bottom: 1rem;
    z-index: 1100;
    padding: .75rem 3rem .75rem 1rem;
    color: var(--bs-danger-text-emphasis);
    background: var(--bs-danger-bg-subtle);
    border: 1px solid var(--bs-danger-border-subtle);
    border-radius: var(--bs-border-radius);
    box-shadow: var(--iigo-shadow);
}

    #blazor-error-ui .reload {
        margin-left: .5rem;
        font-weight: 600;
    }

    #blazor-error-ui .dismiss {
        position: absolute;
        right: .875rem;
        top: .7rem;
        color: inherit;
        cursor: pointer;
    }

/* PASTE the existing .blazor-error-boundary and .blazor-error-boundary::after rules here, unchanged */
```

### 4.2 `wwwroot/js/theme.js` (new)

```js
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
```

The `apply()` guard (only set when different) prevents an observer loop.

### 4.3 `wwwroot/js/navmenu.js` (replace)

```js
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
```

### 4.4 `Components/App.razor`

Only add one line in `<head>`, directly after `<link rel="stylesheet" href="IIGO.styles.css" />`:

```html
    <script src="js/theme.js"></script>
```

Leave every other line, including the CDN links, SRI hashes, `ResourcePreloader`, `ImportMap`, and the body scripts, as is. Same-origin `<script src>` is allowed by `script-src 'self'`.

## 5. Shared components (Components/Shared)

Add `@using IIGO.Components.Shared` to `Components/_Imports.razor`. Then delete the now-duplicate `@using IIGO.Components.Shared` line from `Pages/AppPoolMonitor.razor`, `Pages/HttpsMonitor.razor` and `Pages/WebsiteDetails.razor`.

None of these components handle events, so they work on static and interactive pages. The card is named `SectionCard` because `BlazorBootstrap` (imported globally) already has a `Card`. If the compiler reports an ambiguous name for any component below, prefix it with `Iigo` and update the uses.

Razor gotcha: when a component has more than one `RenderFragment` parameter and you pass a named one (`<Actions>`), the body must go in an explicit `<ChildContent>` tag. Example in 5.2.

### 5.1 `PageHeader.razor`

```razor
@* Top of every page: optional breadcrumb, the page h1 (FocusOnNavigate targets it), subtitle and actions *@

<div class="page-header">
    <div>
        @if (ParentHref != null)
        {
            <nav aria-label="breadcrumb">
                <ol class="breadcrumb">
                    <li class="breadcrumb-item"><a href="@ParentHref">@ParentText</a></li>
                    <li class="breadcrumb-item active" aria-current="page">@Title</li>
                </ol>
            </nav>
        }
        <div class="page-title-row">
            <h1 class="page-title">@Title</h1>
            @TitleExtra
        </div>
        @if (Subtitle != null)
        {
            <p class="page-subtitle">@Subtitle</p>
        }
    </div>
    @if (Actions != null)
    {
        <div class="page-actions">@Actions</div>
    }
</div>

@code {
    [Parameter, EditorRequired]
    public string Title { get; set; } = "";

    [Parameter]
    public string? Subtitle { get; set; }

    // Breadcrumb parent, e.g. "Websites" and "/websites"
    [Parameter]
    public string? ParentText { get; set; }

    [Parameter]
    public string? ParentHref { get; set; }

    // Shown next to the title, e.g. a StatusBadge
    [Parameter]
    public RenderFragment? TitleExtra { get; set; }

    // Buttons on the right
    [Parameter]
    public RenderFragment? Actions { get; set; }
}
```

### 5.2 `SectionCard.razor`

```razor
@* Card holding one block of page content. Flush drops the body padding so a table reaches the edges. *@

<section class="card @Class">
    @if (Title != null || Actions != null)
    {
        <div class="card-header iigo-card-header">
            <div class="me-auto">
                @if (Title != null)
                {
                    <h2 class="iigo-card-title">@Title</h2>
                }
                @if (Subtitle != null)
                {
                    <p class="iigo-card-subtitle">@Subtitle</p>
                }
            </div>
            @if (Actions != null)
            {
                <div class="page-actions">@Actions</div>
            }
        </div>
    }
    @if (Flush)
    {
        @ChildContent
    }
    else
    {
        <div class="card-body">@ChildContent</div>
    }
</section>

@code {
    [Parameter]
    public string? Title { get; set; }

    [Parameter]
    public string? Subtitle { get; set; }

    [Parameter]
    public bool Flush { get; set; }

    // Extra classes for the card, e.g. "h-100"
    [Parameter]
    public string? Class { get; set; }

    [Parameter]
    public RenderFragment? Actions { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
```

Usage with actions:

```razor
<SectionCard Title="Bindings" Flush="true">
    <Actions>
        <button ...>Add Binding</button>
    </Actions>
    <ChildContent>
        <div class="table-responsive">...</div>
    </ChildContent>
</SectionCard>
```

### 5.3 `StatusBadge.razor`

```razor
@* Soft colored pill for a state such as Started, Stopped or Enabled. Shows the text as given. *@

<span class="badge status-badge text-@(Tone)-emphasis bg-@(Tone)-subtle border border-@(Tone)-subtle">
    <i class="bi bi-circle-fill" aria-hidden="true"></i>@(Text ?? Status)
</span>

@code {
    [Parameter, EditorRequired]
    public string? Status { get; set; }

    // Shown instead of Status when set
    [Parameter]
    public string? Text { get; set; }

    // success, danger, warning, info or secondary; picked from Status when not set
    [Parameter]
    public string? Variant { get; set; }

    string Tone => Variant ?? ToneFor(Status);

    static string ToneFor(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "started" or "running" or "enabled" or "active" or "valid" or "succeeded" or "allow" or "ready" => "success",
        "stopped" or "failed" or "invalid" or "block" or "error" => "danger",
        "starting" or "stopping" or "startpending" or "stoppending" or "continuepending" or "pausepending"
            or "paused" or "pending" or "expired" or "queued" => "warning",
        "downloading" or "installing" or "checking" => "info",
        _ => "secondary"
    };
}
```

The mapping covers IIS `ObjectState`, `ServiceControllerStatus`, `TaskState`, firewall Allow/Block and Enabled/Disabled, API key states, license Valid/Invalid, and update statuses. Unknown values (Unknown, Disabled, Inactive, Off) are grey.

### 5.4 `EmptyState.razor`

```razor
@* Shown when a list has nothing to display *@

<div class="empty-state">
    <i class="bi @Icon empty-state-icon" aria-hidden="true"></i>
    <div class="empty-state-title">@Title</div>
    @if (Message != null)
    {
        <p class="mb-0">@Message</p>
    }
    @if (ChildContent != null)
    {
        <div class="mt-3">@ChildContent</div>
    }
</div>

@code {
    [Parameter]
    public string Icon { get; set; } = "bi-inbox";

    [Parameter, EditorRequired]
    public string Title { get; set; } = "";

    [Parameter]
    public string? Message { get; set; }

    // Optional action buttons
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
```

### 5.5 `LoadingState.razor`

```razor
@* Replaces the old <p><em>Loading...</em></p> *@

<div class="loading-state" role="status">
    <span class="spinner-border spinner-border-sm" aria-hidden="true"></span>
    <span>@Text</span>
</div>

@code {
    [Parameter]
    public string Text { get; set; } = "Loading...";
}
```

### 5.6 Existing shared components

- `MonitorStatus.razor`: replace only the markup above `@code`:
  ```razor
  <div class="d-flex flex-wrap align-items-center gap-2 mb-3 small text-body-secondary">
      <i class="bi bi-clock-history" aria-hidden="true"></i>
      <span>Last check: @Format(Monitor.LastRun)</span>
      @if (Monitor.IsRunning)
      {
          <span class="badge text-info-emphasis bg-info-subtle border border-info-subtle">Check in progress</span>
      }
  </div>
  @if (Monitor.LastError != null)
  {
      <div class="alert alert-danger">Last check failed at @Format(Monitor.LastAttempt): @Monitor.LastError</div>
  }
  ```
  Keep the timer and `Dispose`.
- `MonitorSettings.razor`: replace the outer `<div class="card mt-3"><div class="card-body"><h5 class="card-title">Settings</h5>` and its two closing `</div>` with `<SectionCard Title="Settings">` ... `</SectionCard>`. Wrap the Save button and message in `<div class="d-flex align-items-center gap-3">`. Leave everything else.
- `ConnectAsFields.razor`: no change.

### 5.7 Patterns (no component, copy the markup)

Table inside a card:
```razor
<SectionCard Flush="true">
    <div class="table-responsive">
        <table class="table table-hover align-middle iigo-table">
            ...
        </table>
    </div>
</SectionCard>
```
For long lists (Processes, Services, Firewall, Tasks) use `<div class="table-scroll">` instead of `table-responsive` to get a sticky header. Use one or the other, not both.

Truncated cell (replaces the inline `max-width/ellipsis` styles; table cells ignore max-width, so wrap the text):
```razor
<td><div class="cell-truncate" title="@value">@value</div></td>
```
Add `cell-truncate-lg` for the wide ones (300px and 400px in the old styles).

Row actions (replaces `<text>&nbsp;</text>` spacing):
```razor
<td class="text-end">
    <div class="row-actions">
        <button ...>...</button>
    </div>
</td>
```
Give the matching `<th>` `class="text-end"`.

Alert with icon:
```razor
<div class="alert alert-danger d-flex align-items-start gap-2">
    <i class="bi bi-exclamation-triangle-fill" aria-hidden="true"></i>
    <div>@message</div>
</div>
```
Icons: success `bi-check-circle-fill`, danger `bi-exclamation-triangle-fill`, warning `bi-exclamation-circle-fill`, info `bi-info-circle-fill`.

Modal footer (WebsiteDetails, Firewall): replace `<div class="text-end">` with `<div class="d-flex justify-content-end gap-2 mt-3">`, Cancel becomes `btn btn-outline-secondary`, Save stays `btn btn-primary`. Keep `type` and handlers.

### 5.8 Button hierarchy

| Use | Classes | Icon (`<i class="bi ... me-1" aria-hidden="true"></i>` before text) |
|---|---|---|
| Main page action (Add, Create, Submit, Save) | `btn btn-primary` | `bi-plus-lg` for Add/Create, none for Save/Submit |
| Secondary page action (Reload, Monitor, Recheck, Back, Cancel) | `btn btn-outline-secondary` | `bi-arrow-clockwise` Reload/Recheck, `bi-heart-pulse` Monitor, `bi-shield-check` HTTPS Monitor |
| Row action, neutral (Edit, Restart, Recycle, Permissions) | `btn btn-sm btn-outline-secondary` | `bi-pencil`, `bi-arrow-clockwise`, `bi-sliders` |
| Row Start / Enable | `btn btn-sm btn-outline-success` | `bi-play-fill` / `bi-toggle-on` |
| Row Stop | `btn btn-sm btn-outline-danger` | `bi-stop-fill` |
| Row Disable (API key) | `btn btn-sm btn-outline-warning` | `bi-toggle-off` |
| Row Delete | `btn btn-sm btn-outline-danger` | `bi-trash` |
| Destructive form submit (Disable 2FA, Reset authenticator, Generate codes) | `btn btn-danger` | none |

Detail-page header actions (WebsiteDetails Restart/Start/Stop) use the same colors without `btn-sm`.

## 6. Layout changes (Phase 1)

### 6.1 `Layout/MainLayout.razor` (replace)

```razor
@using IIGO.Services
@inherits LayoutComponentBase

<a class="skip-link" href="#main-content">Skip to content</a>

<div class="app-shell">
    @* State holder for the mobile sidebar; toggled by theme.js and the backdrop label *@
    <input type="checkbox" id="sidebar-toggle" class="sidebar-toggle-input" aria-hidden="true" tabindex="-1" />
    <aside id="app-sidebar" class="app-sidebar" data-bs-theme="dark">
        <NavMenu />
    </aside>
    <label for="sidebar-toggle" class="sidebar-backdrop" aria-hidden="true"></label>

    <div class="app-main">
        <TopBar />
        <main id="main-content" class="app-content">
            @Body
        </main>
        <footer class="app-footer">
            <img src="/images/TheScripters_Icon.svg" alt="" />
            <span>&copy; @DateTime.Now.Year <a href="https://iigo.dev" target="_blank" rel="noopener">iigo.dev</a></span>
            <span class="ms-auto">v@(Constants.IIGOVersion) &middot; @(Constants.BuildDate.ToString("yyyy-MM-dd"))</span>
        </footer>
    </div>
</div>

<div id="blazor-error-ui" role="alert">
    <i class="bi bi-exclamation-triangle-fill me-2" aria-hidden="true"></i>An unhandled error has occurred.
    <a href="" class="reload">Reload</a>
    <a class="dismiss" aria-label="Dismiss"><i class="bi bi-x-lg" aria-hidden="true"></i></a>
</div>
```

Keep the `reload` and `dismiss` classes on `<a>` elements; `blazor.web.js` wires them up.

### 6.2 `Layout/MainLayout.razor.css` (replace)

Keep this file non-empty so `IIGO.styles.css` (linked in App.razor) is still generated:

```css
/* Shell styles live in wwwroot/css/app.css. This keeps the scoped bundle (IIGO.styles.css) generated. */
.app-footer img {
    width: 20px;
    height: 20px;
}
```

### 6.3 `Layout/NavMenu.razor.css`

Delete the file. Its scoped `.bi` rule would break Bootstrap Icons, and its classes are no longer used.

### 6.4 `Layout/NavMenu.razor` (replace)

Every link, `href`, policy check and role check from the old file is here. Only order, grouping, icons and label "Home" to "Dashboard" change. The account link and logout move to `TopBar` (6.5). Remove the old `@implements`, `@inject` and `@code` (they only served logout). Do not carry over the commented-out Register/Counter/Weather/Auth blocks.

```razor
<a class="sidebar-brand" href="">
    <img src="favicon-32x32.png" alt="" />
    <span class="brand-text">IIGO</span>
</a>

<nav class="sidebar-nav" aria-label="Main">
    <AuthorizeView>
        <Authorized>
            <div class="nav-section">
                <NavLink class="nav-link" href="" Match="NavLinkMatch.All" title="Dashboard">
                    <i class="bi bi-speedometer2" aria-hidden="true"></i><span class="nav-label">Dashboard</span>
                </NavLink>
            </div>

            <div class="nav-section">
                <div class="nav-section-title">Web server</div>
                <AuthorizeView Policy="@IIGO.Services.Feature.ViewWebsites">
                    <Authorized Context="auth">
                        <NavLink class="nav-link" href="/websites" title="Websites">
                            <i class="bi bi-globe2" aria-hidden="true"></i><span class="nav-label">Websites</span>
                        </NavLink>
                    </Authorized>
                </AuthorizeView>
                <AuthorizeView Policy="@IIGO.Services.Feature.ViewAppPools">
                    <Authorized Context="auth">
                        <NavLink class="nav-link" href="/apppools" title="App Pools">
                            <i class="bi bi-boxes" aria-hidden="true"></i><span class="nav-label">App Pools</span>
                        </NavLink>
                    </Authorized>
                </AuthorizeView>
            </div>

            <div class="nav-section">
                <div class="nav-section-title">System</div>
                <AuthorizeView Policy="@IIGO.Services.Feature.ViewSystemStatus">
                    <Authorized Context="auth">
                        <NavLink class="nav-link" href="/status" title="Status">
                            <i class="bi bi-activity" aria-hidden="true"></i><span class="nav-label">Status</span>
                        </NavLink>
                        <NavLink class="nav-link" href="/processes" title="Processes">
                            <i class="bi bi-cpu" aria-hidden="true"></i><span class="nav-label">Processes</span>
                        </NavLink>
                    </Authorized>
                </AuthorizeView>
                <AuthorizeView Policy="@IIGO.Services.Feature.ViewServices">
                    <Authorized Context="auth">
                        <NavLink class="nav-link" href="/services" title="Services">
                            <i class="bi bi-gear-wide-connected" aria-hidden="true"></i><span class="nav-label">Services</span>
                        </NavLink>
                    </Authorized>
                </AuthorizeView>
                <AuthorizeView Policy="@IIGO.Services.Feature.ViewScheduledTasks">
                    <Authorized Context="auth">
                        <NavLink class="nav-link" href="/tasks" title="Task Scheduler">
                            <i class="bi bi-calendar-check" aria-hidden="true"></i><span class="nav-label">Task Scheduler</span>
                        </NavLink>
                    </Authorized>
                </AuthorizeView>
                <AuthorizeView Policy="@IIGO.Services.Feature.ViewFirewallRules">
                    <Authorized Context="auth">
                        <NavLink class="nav-link" href="/firewall" title="Firewall">
                            <i class="bi bi-shield-lock" aria-hidden="true"></i><span class="nav-label">Firewall</span>
                        </NavLink>
                    </Authorized>
                </AuthorizeView>
                @if (context.User.IsInRole("Administrator"))
                {
                    <NavLink class="nav-link" href="/checkupdates" title="Check updates">
                        <i class="bi bi-cloud-arrow-down" aria-hidden="true"></i><span class="nav-label">Check updates</span>
                    </NavLink>
                }
            </div>

            <div class="nav-section">
                <div class="nav-section-title">Administration</div>
                @if (context.User.IsInRole("Administrator"))
                {
                    <NavLink class="nav-link" href="/users" title="Users">
                        <i class="bi bi-people" aria-hidden="true"></i><span class="nav-label">Users</span>
                    </NavLink>
                }
                <AuthorizeView Policy="@IIGO.Services.Feature.ViewRoles">
                    <Authorized Context="auth">
                        <NavLink class="nav-link" href="/roles" title="Roles">
                            <i class="bi bi-person-badge" aria-hidden="true"></i><span class="nav-label">Roles</span>
                        </NavLink>
                    </Authorized>
                </AuthorizeView>
                <AuthorizeView Policy="@IIGO.Services.Feature.ViewApiKeys">
                    <Authorized Context="auth">
                        <NavLink class="nav-link" href="/apikeys" title="API Keys">
                            <i class="bi bi-key" aria-hidden="true"></i><span class="nav-label">API Keys</span>
                        </NavLink>
                    </Authorized>
                </AuthorizeView>
                @if (context.User.IsInRole("Administrator"))
                {
                    <NavLink class="nav-link" href="/license" title="License Status">
                        <i class="bi bi-patch-check" aria-hidden="true"></i><span class="nav-label">License Status</span>
                    </NavLink>
                }
            </div>
        </Authorized>
        <NotAuthorized>
            <div class="nav-section">
                <NavLink class="nav-link" href="Account/Login" title="Login">
                    <i class="bi bi-box-arrow-in-right" aria-hidden="true"></i><span class="nav-label">Login</span>
                </NavLink>
            </div>
        </NotAuthorized>
    </AuthorizeView>
</nav>
```

Empty sections hide themselves through the `:has()` rule in app.css. No C# visibility logic is added.

### 6.5 `Layout/TopBar.razor` (new)

The `@code` block is the one removed from NavMenu, unchanged. It keeps the logout ReturnUrl working.

```razor
@implements IDisposable

@inject NavigationManager NavigationManager

<header class="app-topbar">
    <button type="button" class="btn-icon d-lg-none" data-iigo-action="toggle-nav" aria-controls="app-sidebar" aria-label="Open navigation" title="Open navigation">
        <i class="bi bi-list fs-5" aria-hidden="true"></i>
    </button>
    <button type="button" class="btn-icon d-none d-lg-inline-flex" data-iigo-action="toggle-sidebar" aria-label="Collapse or expand the sidebar" title="Collapse or expand the sidebar">
        <i class="bi bi-layout-sidebar" aria-hidden="true"></i>
    </button>

    <div class="topbar-server" title="Server">
        <i class="bi bi-hdd-network text-body-secondary" aria-hidden="true"></i>
        <span class="text-truncate">@System.Environment.MachineName</span>
    </div>

    <div class="ms-auto d-flex align-items-center gap-1">
        <button type="button" class="btn-icon" data-iigo-action="toggle-theme" aria-label="Switch between light and dark theme" title="Switch between light and dark theme">
            <i class="bi bi-moon-stars theme-icon-dark" aria-hidden="true"></i>
            <i class="bi bi-sun theme-icon-light" aria-hidden="true"></i>
        </button>

        <AuthorizeView>
            <Authorized>
                <details class="account-menu">
                    <summary class="btn-icon" aria-label="Account menu">
                        <i class="bi bi-person-circle fs-5" aria-hidden="true"></i>
                        <span class="d-none d-sm-inline text-body">@context.User.Identity?.Name</span>
                        <i class="bi bi-chevron-down small" aria-hidden="true"></i>
                    </summary>
                    <div class="dropdown-menu">
                        <div class="dropdown-header">Signed in as <strong>@context.User.Identity?.Name</strong></div>
                        <a class="dropdown-item" href="Account/Manage">
                            <i class="bi bi-person-gear" aria-hidden="true"></i> Manage account
                        </a>
                        <div class="dropdown-divider"></div>
                        <form action="Account/Logout" method="post">
                            <AntiforgeryToken />
                            <input type="hidden" name="ReturnUrl" value="@currentUrl" />
                            <button type="submit" class="dropdown-item">
                                <i class="bi bi-box-arrow-right" aria-hidden="true"></i> Logout
                            </button>
                        </form>
                    </div>
                </details>
            </Authorized>
        </AuthorizeView>
    </div>
</header>

@code {
    private string? currentUrl;

    protected override void OnInitialized()
    {
        currentUrl = NavigationManager.ToBaseRelativePath(NavigationManager.Uri);
        NavigationManager.LocationChanged += OnLocationChanged;
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        currentUrl = NavigationManager.ToBaseRelativePath(e.Location);
        StateHasChanged();
    }

    public void Dispose()
    {
        NavigationManager.LocationChanged -= OnLocationChanged;
    }
}
```

The `<details>` menu needs no JS to open. navmenu.js closes it on outside click and Escape. Enhanced navigation re-renders it closed.

### 6.6 `Components/Routes.razor`

No change. `StatefulReconnection` owns its reconnect UI. In testing, check it is readable in both themes. If it is not, add overrides to app.css only, after inspecting its markup in DevTools. Do not edit the package.

## 7. Page-by-page checklist (Phase 3)

Common steps for every page below unless noted:
- Replace the title `<h1>`/`<h3>` with `<PageHeader ...>`. Keep `<PageTitle>`.
- Replace `<p><em>Loading...</em></p>` with `<LoadingState />`.
- Wrap tables in the card pattern (5.7). Table classes: `table table-hover align-middle iigo-table`. Drop the old plain `table` / `table-sm` classes.
- Apply the button hierarchy (5.8) and row-actions pattern. Keep `@onclick`, `disabled` and conditions exactly.
- Replace inline truncation styles with `cell-truncate`.
- Do not touch `@code` unless the item says so.

### Batch 3a: IIS pages

**Websites.razor**
- `<PageHeader Title="Websites" Subtitle="Sites hosted in IIS on this server.">` with `<Actions>`: Add Website (`@if (isAdmin)`, `btn btn-primary`, `bi-plus-lg`), Reload (`btn btn-outline-secondary`, `bi-arrow-clockwise`), HTTPS Monitor (`btn btn-outline-secondary`, `bi-shield-check`). Note: the actions currently render only after `sites` loads. Keep them inside the `else` branch by putting the `PageHeader` with actions inside `else`, and a `PageHeader Title="Websites"` without actions in the loading branch.
- Keep `<Toasts>` and `<ConfirmDialog @ref="dialog" />` where they are.
- Name column: `<a class="fw-semibold" href="/websites/@site.Id">`. Status: `<StatusBadge Status="@site.State.ToString()" />`. Binding and Path: `cell-truncate` with `title` set to the same expression (evaluate the Path expression once into a local at the top of the row: `var path = site.Applications[0].VirtualDirectories[0].PhysicalPath;`).
- ID column: `class="text-body-secondary tabular-nums"`.
- Actions: `row-actions`, Restart outline-secondary, Start outline-success, Stop outline-danger, Delete outline-danger, all `btn-sm` with icons. Keep the `@if (isAdmin)` and state conditions.
- If `sites.Count == 0`, show `<EmptyState Icon="bi-globe2" Title="No websites" Message="IIS has no sites on this server." />` inside the card instead of the table.

**WebsiteDetails.razor**
- Loading branch: `<LoadingState />`. Remove the top `<a href="/websites">&laquo; Back to List</a>`; the breadcrumb replaces it.
- Loaded branch, keep `<PageTitle>Website: @site.Name</PageTitle>`, then:
  ```razor
  <PageHeader Title="@site.Name" Subtitle="@($"Website ID {site.Id}")" ParentText="Websites" ParentHref="/websites">
      <TitleExtra><StatusBadge Status="@site.State.ToString()" /></TitleExtra>
      <Actions>
          @if (isAdmin)
          {
              ... Restart / Start / Stop buttons with the same conditions and handlers, non-sm colors from 5.8 ...
          }
      </Actions>
  </PageHeader>
  ```
- Keep the `LastMessage` alert (add icon per 5.7) and `<ConfirmDialog @ref="dialog" />`.
- Remove `<div class="container">`, `row`/`col-10`/`col-2` wrappers. Details: `<SectionCard Title="Details" Class="mb-3">` with a `kv-list` (`dt` Physical path / `dd` path + Edit button `btn btn-sm btn-outline-secondary ms-2` under `canEdit`; `dt` Connect as / `dd`). Do not show Status here again (it is in the header).
- Bindings: `<SectionCard Title="Bindings" Flush="true" Class="mb-3">`, Add Binding in `<Actions>` (`btn btn-sm btn-primary`, `bi-plus-lg`, under `canEdit`), table in `<ChildContent>`. Keep the `@if (canEdit)` header/cell pairs. SNI badge: `badge text-secondary-emphasis bg-secondary-subtle border border-secondary-subtle ms-1`.
- Applications: same pattern. Replace `<p class="mt-2"><em>This site has no applications.</em></p>` with `<EmptyState Icon="bi-diagram-3" Title="This site has no applications." />`. Physical path cell: `cell-truncate cell-truncate-lg`.
- Modals: only the footer change from 5.7. Leave fields and validation alone.

**AddWebsite.razor**
- `<PageHeader Title="Add Website" ParentText="Websites" ParentHref="/websites" />`.
- Error alert: keep condition, add icon, fix class typo `alert-dismissable` (remove it; there is no close button).
- Replace `<div class="row"><div class="col-md-8 mx-auto">` with `<div class="row"><div class="col-xl-8">` and put the `EditForm` inside `<SectionCard>`.
- `InputSelect` `class="form-control"` to `class="form-select"`.
- Buttons in `<div class="form-actions">`: Back first as `btn btn-outline-secondary`, Submit `btn btn-primary`. Keep `type="submit" @onclick="SubmitWebsite"` exactly as is.

**AppPools.razor**
- `<PageHeader Title="Application Pools" Subtitle="Refreshes every 5 seconds.">` with action Monitor (`btn btn-outline-secondary`, `bi-heart-pulse`). Same loading-branch rule as Websites.
- Status: `<StatusBadge Status="@pool.State.ToString()" />`. Applications count link: `<a class="badge rounded-pill text-primary-emphasis bg-primary-subtle text-decoration-none" ...>`.
- Actions: Recycle outline-secondary, Start outline-success, Stop outline-danger, `btn-sm`, `row-actions`. Keep `canEdit` header/cell pairs. Keep the timer.

**AppPool.razor**
- Loading: `<LoadingState />`. Remove the `&laquo; Back to List` link and the empty `<div class="container"><div class="row"></div></div>`.
- Loaded: add `<PageTitle>Application Pool: @appPool.Name</PageTitle>`, `<PageHeader Title="@appPool.Name" Subtitle="Application pool" ParentText="App Pools" ParentHref="/apppools" />`, then `<SectionCard Title="Settings">` with a `kv-list` of the four values (Enable 32-bit, Maximum Memory, CPU Limit, CPU Limit Action). Same expressions.

**AppPoolApplications.razor**
- `<PageHeader Title="Applications" Subtitle="@($"Application pool {Name}")" ParentText="App Pools" ParentHref="/apppools" />`.
- Keep the info alert (add `bi-funnel` icon).
- Card + table. Add `else if (applications.Count == 0)` with `<EmptyState Title="No applications use this pool." />`.

**AppPoolMonitor.razor** and **HttpsMonitor.razor**
- `<PageHeader Title="..." Subtitle="(the existing text-muted paragraph text)" ParentText="App Pools|Websites" ParentHref="/apppools|/websites" />`. Remove the paragraph and the "Back to ..." button (the breadcrumb replaces it).
- `<MonitorStatus ... />` directly under the header.
- `<div class="row g-3">`; left `col-lg-7` holds the cards in `<div class="d-flex flex-column gap-3">`; right `col-lg-5` keeps the `AuthorizeView Roles="Administrator"` with both `MonitorSettings` branches exactly.
- Each `card mt-3` becomes `<SectionCard Title="..." Flush="true">` (title text unchanged, use `Title="@($"Restarts in the last {HistoryDays} days")"` style for interpolated ones). The "No ... found/restarted" paragraphs become `<EmptyState Icon="bi-check-circle" Title="..." />` with the same text; "first check hasn't run yet" uses `Icon="bi-hourglass-split"`. The "All events" `h6` becomes a second `SectionCard` titled "All events" (AppPoolMonitor).
- HttpsMonitor: keep `BadgeClass(...)` badges as they are.

### Batch 3b: System pages

**Status.razor** (interactive, 1 s timer: do not touch `@code`)
- `<PageHeader Title="System Status" Subtitle="Live view, refreshed every second." />`.
- Replace the single table with:
  1. Stat cards: `<div class="row g-3 mb-3">` with four `<div class="col-sm-6 col-xl-3"><div class="card stat-card h-100"><div class="card-body">`:
     - `bi-memory` "Memory in use", value `@PercentInUse`, meta `@Available available of @Total`.
     - `bi-cpu` "CPU usage", value `@CpuUsage`.
     - `bi-list-task` "Processes", value `<a href="/processes">@Processes</a>`.
     - `bi-clock-history` "Up time", value `@UpTime`, meta `days:hours:minutes:seconds`.
     Markup per card: `<div class="stat-label"><i class="bi ..." aria-hidden="true"></i> Label</div><div class="stat-value">...</div><div class="stat-meta">...</div>`.
  2. `<div class="row g-3">`: `col-xl-4` `<SectionCard Title="Memory" Class="h-100">` with a `kv-list` of Total, Available, % Free, % In Use, Paged Pool, Non-Paged Pool. `col-xl-8` `<SectionCard Title="Drives" Class="h-100">` with one block per drive:
     ```razor
     @foreach (var d in allDrives)
     {
         var volume = "";
         if (!String.IsNullOrWhiteSpace(d.VolumeLabel))
         {
             volume = $"({d.VolumeLabel})";
         }
         var total = d.TotalSize;
         var free = d.TotalFreeSpace;
         var usedPercent = total > 0 ? (int)Math.Round((double)(total - free) / total * 100) : 0;
         <div class="mb-3">
             <div class="d-flex justify-content-between align-items-baseline gap-2 mb-1">
                 <span class="fw-semibold"><i class="bi bi-hdd me-1" aria-hidden="true"></i>@d.Name @volume</span>
                 <span class="small text-body-secondary tabular-nums">@(((double)(total - free) / 1073741824).ToString("#,###.##")) GB used of @((total / 1073741824).ToString("#,###.##")) GB</span>
             </div>
             <div class="progress" role="progressbar" aria-label="@d.Name used space" aria-valuenow="@usedPercent" aria-valuemin="0" aria-valuemax="100">
                 <div class="progress-bar @(usedPercent >= 90 ? "bg-danger" : usedPercent >= 80 ? "bg-warning" : "")" style="width: @(usedPercent)%"></div>
             </div>
             <div class="small text-body-secondary tabular-nums mt-1">@((free / 1073741824).ToString("#,###.##")) GB free (@(Convert.ToDecimal(((double)free / ((double)total)) * 100).ToString("###.##"))%)</div>
         </div>
     }
     ```
     The width is an `int`, so no culture decimal separator ends up in CSS.
  3. `<SectionCard Title="Network interfaces" Flush="true" Class="mt-3">` with a table: Interface | Addresses | Sent (`text-end`) | Received (`text-end`). The first two lines inside the `foreach` stay exactly `var sent = i.PCSent!.NextValue();` and `var received = i.PCReceived!.NextValue();`. Addresses: one `<div class="font-monospace small">@address</div>` per address from the existing `GetIPAddresses(...)` call (call it once per row). Sent/Received cells: `KBps` value on the first line, `kbps` value in `<span class="small text-body-secondary">` on a second line, same formulas.
- Remove the `border-top:2px solid #000` and `vertical-align` inline styles.

**Processes.razor**
- `<PageHeader Title="Processes" Subtitle="Sorted by CPU, then memory. CPU is sampled over one second when the page opens." />`.
- Loading: `<LoadingState Text="Sampling CPU usage..." />`.
- Card with `table-scroll` (sticky header). At the top of the `foreach` add `var user = GetProcessUser(proc);` and `var commandLine = GetCommandLineOfProcess(proc);` and use those locals in the cells (each method still runs once per row).
- PID, Memory, CPU: `class="text-end tabular-nums"` on `th` and `td`. Leave the CPU cell's inner `@{ ... }` block as is.
- Command line: `<div class="cell-truncate cell-truncate-lg font-monospace small" title="@commandLine">@commandLine</div>`.

**Services.razor** (static SSR with `[StreamRendering]`: no events)
- Add `<PageTitle>Services</PageTitle>`. Put `<PageHeader Title="Services" />` once at the top, above the loading check.
- Card with `table-scroll`. Keep `var info = GetServiceDescription(service.ServiceName);` as the first line of the loop. Name and Description: `cell-truncate` / `cell-truncate cell-truncate-lg` with the existing `title` values.
- Status: `<StatusBadge Status="@service.Status.ToString()" Variant="@(service.Status == ServiceControllerStatus.Stopped ? "secondary" : null)" />` (stopped services are normal here, so grey not red).
- Keep the Startup Type expression (it calls `GetDelayedStatus` once).

**Tasks.razor**
- `<PageHeader Title="Scheduled Tasks" />` (matches its PageTitle).
- Card with `table-scroll`. State: `<StatusBadge Status="@task.State.ToString()" />`. Status: `<StatusBadge Status="@(task.IsActive ? "Active" : "Inactive")" />`. Dates: `text-nowrap tabular-nums`, same expressions.
- `tasks.Count == 0`: `<EmptyState Icon="bi-calendar-x" Title="No scheduled tasks in the root folder." />`.

**Firewall.razor**
- `<PageHeader Title="Firewall" Subtitle="Windows Defender Firewall rules.">` with Add Rule (`@if (canEdit)`, `btn btn-primary`, `bi-plus-lg`) in `<Actions>`. Same loading-branch rule as Websites (the old Add Rule only showed after load).
- Keep `<Toasts>` and `<ConfirmDialog>`.
- Put the table in `<SectionCard Flush="true">`. Its first child is a toolbar: `<div class="d-flex flex-wrap align-items-center gap-2 p-3 border-bottom">` holding an `<div class="input-group mw-input">` (a `<span class="input-group-text"><i class="bi bi-search" aria-hidden="true"></i></span>` plus the existing search input with `class="form-control"`; remove its inline `style` and `w-auto flex-grow-1`; keep `@bind`, `@bind:event`, placeholder and aria-label) and the count `<span class="text-body-secondary small">`. Then the `table-scroll` table.
- Action column: `<StatusBadge Status="@rule.Action.ToString()" />`. Status column: `<StatusBadge Status="@(rule.Enabled ? "Enabled" : "Disabled")" />`.
- Keep the name cell's `overflow-wrap:anywhere` style and its Razor comment. Local Ports: `<div class="cell-truncate" title="...">` with the same expressions; keep the comment.
- Row actions per 5.8. Modal footer per 5.7. Profiles checkboxes: change the `<span class="me-3">Profiles</span>` to `<span class="form-label d-block">Profiles</span>`.

**CheckUpdates.razor**
- `<PageHeader Title="Check for Updates">` with `<Actions>`: last-checked text (`<span class="text-body-secondary small">`), Recheck (`btn btn-outline-secondary`, `bi-arrow-clockwise`), Install Selected (`btn btn-primary`, `bi-download`, inside `@if (_updates != null)`). Keep every `disabled` expression and label expression. Drop `btn-sm`.
- Keep both alerts above the content; add icons.
- `_updates == null`: `IsChecking` branch becomes `<LoadingState Text="Checking for updates in the background. This page will update when the check finishes." />`; else branch `<EmptyState Icon="bi-hourglass-split" Title="No check has finished yet." Message="Updates are checked automatically once a week." />`.
- Progress block: wrap in `<SectionCard Class="mb-3">`; keep `Progress`/`ProgressBar`.
- `_updates.Count == 0`: `<EmptyState Icon="bi-check-circle" Title="Your system is up to date." />` inside a `SectionCard`.
- Table in a flush card. Replace inline `width` styles on `th` with `class="text-nowrap"` (Size, Status) and `style="width:1%"` stays on the checkbox column. Add `class="form-check-input"` and `aria-label="Select all updates"` / `aria-label="Select update"` to the checkboxes; keep their bindings.
- Status cell: `@if (status == UpdateItemStatus.Pending) { <span class="text-body-secondary">@RenderStatus(status)</span> } else { <StatusBadge Status="@status.ToString()" Text="@RenderStatus(status)" /> }`. Keep `RenderStatus` unchanged.

**LicenseStatus.razor** (static)
- `<PageHeader Title="License Status" />`.
- Invalid branch: danger alert with icon, text "Invalid License!".
- Valid branch:
  ```razor
  <SectionCard Title="License">
      <Actions><StatusBadge Status="@(licenseStatus ? "Valid" : "Invalid")" /></Actions>
      <ChildContent>
          <dl class="kv-list">
              <dt>Company</dt><dd>@licenseData.CompanyName</dd>
              <dt>Initial Version</dt><dd>@licenseData.InitialVersion</dd>
              <dt>Date</dt><dd>@licenseData.LicenseDate</dd>
              <dt>Term</dt><dd>@licenseData.LicenseTerm</dd>
              <dt>Type</dt><dd>@licenseData.LicenseType</dd>
          </dl>
      </ChildContent>
  </SectionCard>
  ```
  This removes the inline green/red color.

### Batch 3c: Admin pages and misc

**Users.razor**
- `<PageHeader Title="Users">` with Add User in `<Actions>` (`btn btn-primary`, `bi-person-plus`; was `btn-success`). Remove the old `mb-3` div.
- Error alert with icon. Table in a flush card. Name `fw-semibold`. Roles: one `<span class="badge text-secondary-emphasis bg-secondary-subtle border border-secondary-subtle me-1">@role</span>` per role from the same `userRoles.GetValueOrDefault(user.Id, [])`. MFA: `<StatusBadge Status="@(user.TwoFactorEnabled ? "Enabled" : "Off")" />`.
- Row actions: Edit outline-secondary `bi-pencil`, Delete outline-danger `bi-trash`, `btn-sm`. Last `th` gets `class="text-end"` and visually hidden text: `<span class="visually-hidden">Actions</span>`.
- Confirmation swap (allowed `@code` change): add `<ConfirmDialog @ref="dialog" />` under the header, add `private ConfirmDialog dialog = default!;` to `@code`, and replace only the `JsRuntime.InvokeAsync<bool>("confirm", ...)` line with:
  ```csharp
  bool confirmed = await dialog.ShowAsync(
      title: "Are you sure you want to delete this user?",
      message1: "This can't be undone.",
      message2: "Do you want to proceed?");
  ```
  Everything after it stays. Remove `@inject IJSRuntime JsRuntime` only if nothing else in the file uses `JsRuntime`.

**User.razor**
- Not-found branch: keep alert + Back button (`btn btn-outline-secondary`).
- Loaded: `<PageHeader Title="Edit User" Subtitle="@user.UserName" ParentText="Users" ParentHref="/users" />`; remove the `<h5>`. Remove the bottom "Back to Users" button (breadcrumb replaces it).
- The three cards become `<SectionCard Title="Details|Multi-factor authentication|Reset password" Class="mb-3">`; remove `mt-3` and the `h6.card-title`s. Roles heading becomes `<h3 class="h6 mt-2">Roles</h3>`. Use `<StatusBadge>` for the MFA Enabled/Off badges.
- Buttons: Save `btn btn-primary`; Disable MFA `btn btn-outline-warning`; Reset Password `btn btn-primary` (was `btn-success`).
- Confirmation swap for Disable MFA, same method as Users: title `$"Disable MFA for {user.UserName}?"`, message1 `"Their authenticator key and recovery codes are reset."`, message2 `"Do you want to proceed?"`.

**AddUser.razor**
- `<PageHeader Title="Add User" ParentText="Users" ParentHref="/users" />`. Form inside `<SectionCard>` in `col-xl-8`. Roles card becomes a plain block: `<h3 class="h6 mt-2">Roles</h3>` + checkboxes. Buttons in `form-actions`: Back `btn btn-outline-secondary`, Create User `btn btn-primary`.

**Roles.razor**
- `<PageHeader Title="Roles" />`. Keep the `AuthorizeView Roles="Administrator"` block in place; change its inner div to `<div class="input-group mb-3 mw-input">` with the input (`class="form-control"`, drop `d-inline-block w-auto`) and the Create Role button (`btn btn-primary`, `bi-plus-lg`).
- Table in a flush card. Row actions: Permissions `btn btn-sm btn-outline-secondary` `bi-sliders`; Delete `btn btn-sm btn-outline-danger` `bi-trash`; keep the nested `AuthorizeView` and the Administrator branch text. Built-in text: add `bi-lock` icon.
- Confirmation swap for delete, same method as Users: title "Are you sure you want to delete this role?", message1 "Users lose the permissions this role grants.", message2 "Do you want to proceed?".

**RolePermissions.razor**
- `<PageHeader Title="Role Permissions" Subtitle="@role.Name" ParentText="Roles" ParentHref="/roles" />` inside the loaded branch; `<PageHeader Title="Role Permissions" />` in the loading branch. Remove the `<h5>` and the bottom Back button.
- Both `AuthorizeView` branches stay. Their cards become `<SectionCard Title="Features">`. Lay the checkboxes out in `<div class="row row-cols-1 row-cols-md-2 g-2">` with each `form-check` in a `<div class="col">`. Add `form-switch` to each `form-check` and `role="switch"` to each input. Keep `id`, `checked`, `@onchange`, `disabled`.
- Save row: `<div class="d-flex align-items-center gap-3 mt-3">`.

**ApiKeys.razor**
- `<PageHeader Title="API Keys" />` then keep the explanatory `<p class="text-muted">` (change to `text-body-secondary`).
- New-key alert: add `bi-key-fill` icon; Copy button `btn btn-outline-secondary` with `bi-clipboard`.
- Create card: `<SectionCard Title="Create a key" Class="mb-3">`. Create Key `btn btn-primary` `bi-plus-lg`.
- `keys.Count == 0`: `<EmptyState Icon="bi-key" Title="No API keys have been created." />` in a card.
- Table in a flush card. Key prefix stays `font-monospace`. Status: `<StatusBadge Status="@Status(key)" />` (same mapping as `StatusClass`). Leave `StatusClass` in place.
- Row actions: Disable outline-warning, Enable outline-success, Delete outline-danger, `btn-sm`.

**Home.razor**
- Replace `<h1>Dashboard</h1>` with `<PageHeader Title="Dashboard" />`. Nothing else (issue #296 owns the content).

**Auth.razor**, **Configuration.razor**
- `PageHeader` with the existing heading text. Auth: put the hello text in a `SectionCard`. Configuration: add `<PageTitle>Configuration</PageTitle>`.

**Error.razor**
- `<PageHeader Title="Error" Subtitle="An error occurred while processing your request." />`, then a `SectionCard` containing the Request ID paragraph and the Development Mode text (heading `h2 class="h6"`). Keep the text and `@code`.

## 8. Account pages (Phase 4)

These are static SSR. No `@rendermode`, no events. The theme toggle works because it is plain JS.

### 8.1 New `Account/Shared/AuthLayout.razor`

For sign-in flows, which should not show the sidebar.

```razor
@inherits LayoutComponentBase
@inject NavigationManager NavigationManager

<div class="auth-shell">
    <div class="auth-topbar">
        <button type="button" class="btn-icon" data-iigo-action="toggle-theme" aria-label="Switch between light and dark theme" title="Switch between light and dark theme">
            <i class="bi bi-moon-stars theme-icon-dark" aria-hidden="true"></i>
            <i class="bi bi-sun theme-icon-light" aria-hidden="true"></i>
        </button>
    </div>
    <main class="auth-main">
        <div class="card auth-card">
            <div class="card-body p-4 p-sm-5">
                <div class="auth-brand">
                    <img src="favicon-32x32.png" alt="" width="32" height="32" />
                    <span>IIGO</span>
                </div>
                @if (HttpContext is null)
                {
                    <p>Loading...</p>
                }
                else
                {
                    @Body
                }
            </div>
        </div>
    </main>
    <footer class="auth-footer">
        <a href="https://iigo.dev" target="_blank" rel="noopener">iigo.dev</a>
    </footer>
</div>

<div id="blazor-error-ui" role="alert">
    <i class="bi bi-exclamation-triangle-fill me-2" aria-hidden="true"></i>An unhandled error has occurred.
    <a href="" class="reload">Reload</a>
    <a class="dismiss" aria-label="Dismiss"><i class="bi bi-x-lg" aria-hidden="true"></i></a>
</div>

@code {
    [CascadingParameter]
    private HttpContext? HttpContext { get; set; }

    protected override void OnParametersSet()
    {
        if (HttpContext is null)
        {
            // Identity pages need an HttpContext to set cookies; leave interactive mode with a full reload
            NavigationManager.Refresh(forceReload: true);
        }
    }
}
```

The server name and version are intentionally left off the pre-login screen.

Then change `Account/Pages/_Imports.razor` line 2 from `@layout AccountLayout` to `@layout AuthLayout`. `Account/Pages/Manage/_Imports.razor` keeps `@layout ManageLayout`, so Manage pages still use `ManageLayout > AccountLayout > MainLayout`. Leave `AccountLayout.razor` unchanged.

### 8.2 `Account/Shared/ManageLayout.razor` (replace)

```razor
@inherits LayoutComponentBase
@layout AccountLayout

<PageHeader Title="Manage your account" Subtitle="Change your account settings." />

<div class="row g-3">
    <div class="col-lg-3">
        <div class="card">
            <div class="card-body p-2">
                <ManageNavMenu />
            </div>
        </div>
    </div>
    <div class="col-lg-9">
        <div class="card">
            <div class="card-body p-4">
                @Body
            </div>
        </div>
    </div>
</div>
```

### 8.3 `Account/Shared/ManageNavMenu.razor`

Markup only: `<ul class="nav nav-pills flex-column gap-1">`; add an icon before each label (`bi-person` Profile, `bi-envelope` Email, `bi-key` Password, `bi-shield-lock` Two-factor authentication) with `me-2`. Keep hrefs, `Match`, and `@code`.

### 8.4 Other Account/Shared files
- `StatusMessage.razor`: add `d-flex align-items-start gap-2` to the alert and a leading icon (`bi-exclamation-triangle-fill` for danger, `bi-check-circle-fill` for success, chosen by the existing `statusMessageClass`). Keep the cookie logic.
- `ShowRecoveryCodes.razor`: heading `<h2 class="h5 mb-3">Recovery codes</h2>`; replace the `row`/`col-md-12`/per-code `div` with `<div class="recovery-codes">` holding one `<code class="recovery-code">@recoveryCode</code>` per code.
- `ExternalLoginPicker.razor`, `RedirectToLogin.razor`: no change.

### 8.5 Sign-in pages (AuthLayout)

Login, LoginWith2fa, LoginWithRecoveryCode, ForgotPassword, ForgotPasswordConfirmation, ResetPassword, ResetPasswordConfirmation, InvalidPasswordReset, InvalidUser, Lockout, AccessDenied, ConfirmEmail, ConfirmEmailChange, ResendEmailConfirmation.

Apply to each:
- Title `h1`/`h3` becomes `<h1 class="h4 mb-1">` with the same text (keep `text-danger` on Lockout and AccessDenied). Every page keeps exactly one `h1`.
- An `h2` subtitle becomes `<p class="text-body-secondary mb-4">` with the same text. Delete `<hr />`.
- `class="col-md-4"` becomes `class="col-12"`. Keep the `row` wrapper.
- `w-100 btn btn-lg btn-primary` becomes `w-100 btn btn-primary`.
- Where a `<label for="x">` sits next to an `InputText`/`InputCheckbox` that has no `id`, add `id="x"` to the input. Do not change `@bind-Value`, `name`, `autocomplete`, or `FormName`.
- AccessDenied: add `<a href="" class="btn btn-primary mt-3">Back to dashboard</a>`. Lockout: add `<a href="Account/Login" class="btn btn-outline-secondary mt-3">Back to log in</a>`.

Login specifics:
- `<h1 class="h4 mb-1">Log in</h1>`, then `<p class="text-body-secondary mb-4">Use a local account to log in.</p>` (from the old `h2`), no `hr`.
- Remove the empty second column `<div class="col-md-6 col-md-offset-2">...</div>` (it only holds a comment). Keep the other commented-out blocks.
- Remember me: `<div class="form-check mb-3"><InputCheckbox @bind-Value="Input.RememberMe" id="remember-me" class="darker-border-checkbox form-check-input" /><label class="form-check-label" for="remember-me">Remember me</label></div>`.
- Forgot password link: `<p class="mt-3 mb-0 text-center small">`.

### 8.6 Manage pages (ManageLayout)

Index, Email, ChangePassword, SetPassword, TwoFactorAuthentication, EnableAuthenticator, ResetAuthenticator, Disable2fa, GenerateRecoveryCodes.

Apply to each:
- `h3` becomes `<h2 class="h5 mb-3">` (same text); `h4` becomes `<h3 class="h6 mt-4">`.
- `class="col-md-6"` becomes `class="col-lg-8"`.
- `w-100 btn btn-lg btn-primary` becomes `btn btn-primary`.
- Label `id` fix as in 8.5.

Specifics:
- ChangePassword: keep the MustChangePassword warning alert (add icon). Do not change the form.
- TwoFactorAuthentication: replace `style="display: inline-block"` with `class="d-inline-block"`. Group the buttons in `<div class="d-flex flex-wrap gap-2 mb-3">` (put the forget-browser form inside it). Forget this browser `btn btn-outline-secondary`; Disable 2FA `btn btn-outline-danger`; Reset recovery codes `btn btn-outline-secondary`; Add/Set up authenticator app `btn btn-primary`; Reset authenticator app `btn btn-outline-danger`.
- EnableAuthenticator: wrap `<div id="qrCode"></div>` as `<div class="qr-code-frame mb-3"><div id="qrCode"></div></div>`. Keep `id="qrCodeData"` and both `<script src>` tags exactly. `<ol class="list">` becomes `<ol class="ps-3">`.
- Disable2fa, ResetAuthenticator, GenerateRecoveryCodes: keep `btn btn-danger` and the warning alerts (add icon).

### 8.7 Clean-up
- Delete `wwwroot/css/site.css` (not referenced; confirm with a grep for `site.css` first).

## 9. Phases and commits

Commit after each phase with a plain message. No AI attribution lines.

| Phase | Files | Commit message |
|---|---|---|
| 1. Shell | `wwwroot/css/app.css`, `wwwroot/js/theme.js` (new), `wwwroot/js/navmenu.js`, `Components/App.razor`, `Layout/MainLayout.razor`, `Layout/MainLayout.razor.css`, `Layout/NavMenu.razor`, `Layout/NavMenu.razor.css` (delete), `Layout/TopBar.razor` (new) | `Redesign: new app shell, grouped sidebar, top bar, light/dark theme` |
| 2. Shared | `Components/_Imports.razor`, `Shared/PageHeader.razor`, `Shared/SectionCard.razor`, `Shared/StatusBadge.razor`, `Shared/EmptyState.razor`, `Shared/LoadingState.razor`, `Shared/MonitorStatus.razor`, `Shared/MonitorSettings.razor`, remove duplicate `@using` in 3 pages | `Redesign: shared page header, card, status badge, empty and loading states` |
| 3a. IIS pages | Websites, WebsiteDetails, AddWebsite, AppPools, AppPool, AppPoolApplications, AppPoolMonitor, HttpsMonitor | `Redesign: IIS pages` |
| 3b. System pages | Status, Processes, Services, Tasks, Firewall, CheckUpdates, LicenseStatus | `Redesign: system pages` |
| 3c. Admin pages | Users, User, AddUser, Roles, RolePermissions, ApiKeys, Home, Auth, Configuration, Error | `Redesign: admin pages and confirmation dialogs` |
| 4. Account + polish | `Account/Shared/AuthLayout.razor` (new), `Account/Pages/_Imports.razor`, ManageLayout, ManageNavMenu, StatusMessage, ShowRecoveryCodes, all Account pages, delete `site.css` | `Redesign: sign-in and account pages` |

Phase 1 leaves pages looking unstyled-but-working inside the new shell. That is expected.

Per-phase self-check (Linux, before committing):
1. `git diff --check` is clean.
2. Every changed `.razor` file: tags and `@{ }`/`@if { }` braces balance; every `<SectionCard>` that uses `<Actions>` wraps its body in `<ChildContent>`.
3. Grep the static files for new events: `Services.razor`, `LicenseStatus.razor`, `Configuration.razor`, `Auth.razor`, `Error.razor`, `Layout/*.razor`, `Account/**/*.razor` must have no `@onclick`, `@onchange`, `@bind` or `@rendermode` that wasn't there before.
4. Grep the diff for `AuthorizeView`, `Policy=`, `Roles=`, `IsInRole`, `@attribute`, `@page`, `@rendermode`: each one removed must be re-added unchanged in the same file (only NavMenu moves code, and it keeps every check).
5. No `<script>` without `src`, no `on...=` HTML attributes, no `data-bs-toggle`.

## 10. Risks

- **CSS isolation.** Shell styles move to global `app.css`, so there are no `::deep` pitfalls. `NavMenu.razor.css` is deleted because its scoped `.bi` rule would break icon fonts. `MainLayout.razor.css` stays non-empty so `IIGO.styles.css` is still generated; if it ever 404s, the shell still works.
- **Static layout vs interactive pages.** Layouts are static SSR. The theme toggle, sidebar toggle and account menu use `data-iigo-action` + document-level listeners in static JS files, so they survive enhanced navigation and interactive page swaps. Never add `@onclick` to layout components.
- **Enhanced navigation resets `<html>` attributes.** `theme.js` re-applies `data-bs-theme`/`data-sidebar` through a `MutationObserver`. If a flash shows during navigation, also re-apply on Blazor's `enhancedload` event (`Blazor.addEventListener('enhancedload', apply)` after `DOMContentLoaded`).
- **Bootstrap JS without Popper.** No dropdowns, tooltips or popovers via Bootstrap JS. Modals, toasts and ConfirmDialog don't need Popper and keep working.
- **Blazor.Bootstrap in dark mode.** Modal, ConfirmDialog, Toasts and Progress use Bootstrap variables and should follow the theme. `blazor.bootstrap.css` may contain fixed colors or size `main`; check in DevTools and override in `app.css` only. Confirm the toast container has `top-0` (if not, adjust the offset selector).
- **`:has()`** hides empty nav sections. Browsers without `:has()` just show an empty section title. Acceptable.
- **CSP.** Only `js/theme.js` is added, as a same-origin file. Do not touch `Program.cs`. Inline `style` attributes are fine (styles aren't restricted) but prefer classes.
- **Behavior.** Timers, event subscriptions, render-time counter calls (Status), and per-row service/process lookups must be unchanged in count and order. The only `@code` edits allowed: the ConfirmDialog swap on Users/User/Roles, the `ConfirmDialog` field declarations, and the per-row locals noted above.
- **Compile risk on Windows.** New components use only `string`, `bool` and `RenderFragment` parameters. Watch for: missing `<ChildContent>` when `<Actions>` is used; `Title="@site.Name"` style attributes (fine); name clashes with `BlazorBootstrap` types (rename with `Iigo` prefix if needed).
- **Identity labels.** Adding `id` to inputs does not change the posted field names (Blazor derives `name` from the bind expression).
- **Pre-login info.** The old layout showed the machine name and version on the login page; AuthLayout does not.

## 11. Manual test checklist (Windows, after build)

Build and console
- [ ] `dotnet build` succeeds with no new errors.
- [ ] Browser console shows no CSP violations and no JS errors on: `/`, `/websites`, a site detail, `/firewall`, `/status`, `/services`, `/Account/Login`, `/Account/Manage`, `/Account/Manage/EnableAuthenticator`.

Navigation and roles
- [ ] As Administrator: every nav item from section 1.1 is present (Dashboard, Websites, App Pools, Status, Processes, Services, Task Scheduler, Firewall, Check updates, Users, Roles, API Keys, License Status).
- [ ] As a user in a role with only ViewWebsites: only Dashboard and Websites show; the System and Administration section titles are hidden.
- [ ] As a user with no features: only Dashboard shows.
- [ ] Active link is highlighted, including on nested routes (e.g. `/websites/1` highlights Websites).
- [ ] Account menu opens, closes on outside click and Escape; Manage account works; Logout signs out and lands on Login.

Theme and shell
- [ ] Theme defaults to the OS setting; toggle switches; choice survives reload, enhanced navigation, and moving between `/` and `/Account/Manage`.
- [ ] Login page has a working theme toggle.
- [ ] Private window (localStorage may be blocked): no errors, toggle still works for the page.
- [ ] Desktop: sidebar collapse to icon rail persists across reloads; icons have tooltips (`title`).
- [ ] Below 992px: hamburger opens the sidebar, backdrop click closes it, link click closes it, Escape closes it.
- [ ] 375px and 768px widths: header actions wrap, tables scroll horizontally, nothing overflows the viewport.
- [ ] Footer shows year, iigo.dev link, version and build date.

Pages
- [ ] Every page shows a PageHeader with one `h1`; focus lands on it after navigation (no visible outline).
- [ ] Loading and empty states render (e.g. a site with no applications, no API keys, up-to-date updates).
- [ ] Status badges: Started green, Stopped red (grey on Services), Unknown grey, Expired yellow.
- [ ] Sticky table headers on Processes, Services, Firewall, Tasks.
- [ ] Websites: Restart/Start/Stop show toasts below the top bar; Delete opens ConfirmDialog, Cancel does nothing, Confirm deletes.
- [ ] WebsiteDetails: all three modals open, validate, save and cancel in both themes; binding/application delete confirm works.
- [ ] Firewall: filter, add, edit, delete (confirm) work.
- [ ] Users/Roles delete and User Disable MFA now use ConfirmDialog; Cancel does nothing.
- [ ] API Keys: create shows the key; Copy works; Enable/Disable/Delete work.
- [ ] Timers: Status updates every second; App Pools every 5 s; monitor "Last check" updates; leaving these pages logs no errors.
- [ ] CheckUpdates: Recheck and Install Selected enable/disable as before; progress bar shows during install.
- [ ] Form validation messages and red/green outlines are readable in dark mode.

Account (static SSR)
- [ ] Login, wrong password error, lockout page, 2FA login, recovery code login.
- [ ] Default-admin flow: forced redirect to Change password still works and the page is styled.
- [ ] EnableAuthenticator QR code renders on a full page load and is scannable in dark mode.
- [ ] Recovery codes display in the grid.

Errors and reconnect
- [ ] `#blazor-error-ui`: un-hide it in DevTools; it reads well in both themes; Reload and dismiss work.
- [ ] Stop the service while on an interactive page: the StatefulReconnection UI is readable in both themes.

## 12. Proposals needing approval (not part of this plan)

1. Add sidebar links for HTTPS Monitor and App Pool Monitor under the same policies as their pages (ViewWebsites, ViewAppPools). Today they are reachable only from page buttons.
2. Self-host Bootstrap, Bootstrap Icons and Blazor.Bootstrap assets in `wwwroot/lib` so the panel works on servers without internet access. Needs a CSP change to drop the cdnjs entry.
3. Load `bootstrap.bundle.min.js` (includes Popper) to allow real Bootstrap dropdowns and tooltips. Needs a CSP change.
4. Upgrade Bootstrap 5.3.2 to the latest 5.3.x (new SRI hashes and CSP URL).
5. Client-side sorting and filtering for Services, Processes and Tasks. Services is static today, so this needs interactivity (a behavior change).
6. Remove the orphan test pages `Auth.razor` and `Configuration.razor`, or finish Configuration.
7. Format Tasks "Next Run Time" so tasks with no next run show "Never" instead of the default date (display logic change).
