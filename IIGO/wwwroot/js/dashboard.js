// Dashboard charts. Loaded as an ES module by Home.razor. ApexCharts (4.7.0, the last MIT release) is
// loaded on demand from the pinned CDN file that the Content-Security-Policy allows.
const apexUrl = 'https://cdnjs.cloudflare.com/ajax/libs/apexcharts/4.7.0/apexcharts.min.js';
const apexIntegrity = 'sha512-xHtgQEymzQrphqglnjawC6hqXjIlzaGwK4h5xfYKIr/rM5CI9RXazkGZHzn5lIBxzU7vCb8uacAe5ACUGyz/Ow==';

let apexPromise = null;
let charts = {};
let maxPoints = 60;
let observer = null;
let dotnet = null;
let disposed = false;

function loadApex() {
    if (window.ApexCharts) {
        return Promise.resolve();
    }
    if (!apexPromise) {
        apexPromise = new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = apexUrl;
            script.integrity = apexIntegrity;
            script.crossOrigin = 'anonymous';
            script.referrerPolicy = 'no-referrer';
            script.onload = () => resolve();
            script.onerror = () => {
                script.remove();
                apexPromise = null;
                reject(new Error('ApexCharts failed to load.'));
            };
            document.head.appendChild(script);
        });
    }
    return apexPromise;
}

function themeColors() {
    const css = getComputedStyle(document.documentElement);
    const get = (name, fallback) => css.getPropertyValue(name).trim() || fallback;
    const attr = document.documentElement.getAttribute('data-bs-theme');
    const dark = attr ? attr === 'dark' : window.matchMedia('(prefers-color-scheme: dark)').matches;
    return {
        mode: dark ? 'dark' : 'light',
        muted: get('--iigo-text-muted', '#667085'),
        border: get('--iigo-border', '#e3e7ee'),
        text: get('--bs-body-color', dark ? '#dee2e6' : '#212529'),
        primary: get('--bs-primary', '#0d6efd'),
        success: get('--bs-success', '#198754'),
        info: get('--bs-info', '#0dcaf0')
    };
}

function seriesColors(kind, c) {
    if (kind === 'cpu') return [c.primary];
    if (kind === 'memory') return [c.info];
    return [c.success, c.primary];
}

function formatRate(bytes) {
    if (bytes >= 1048576) return (bytes / 1048576).toFixed(1) + ' MB/s';
    return (bytes / 1024).toFixed(bytes >= 10240 ? 0 : 1) + ' KB/s';
}

function themeOptions(kind, c) {
    return {
        theme: { mode: c.mode },
        colors: seriesColors(kind, c),
        grid: { borderColor: c.border },
        xaxis: {
            labels: { style: { colors: c.muted } },
            axisBorder: { color: c.border },
            axisTicks: { color: c.border }
        },
        yaxis: { labels: { style: { colors: [c.muted] } } },
        legend: { labels: { colors: c.text } },
        tooltip: { theme: c.mode }
    };
}

function buildOptions(kind, seriesNames, c) {
    const percent = kind !== 'network';
    const yaxis = percent
        ? { min: 0, max: 100, tickAmount: 4, labels: { formatter: v => Math.round(v) + '%' } }
        : { min: 0, forceNiceScale: true, labels: { formatter: v => formatRate(v) } };
    const base = {
        chart: {
            type: 'area',
            height: 200,
            background: 'transparent',
            fontFamily: 'inherit',
            toolbar: { show: false },
            zoom: { enabled: false },
            animations: { enabled: false },
            redrawOnParentResize: true
        },
        series: seriesNames.map(name => ({ name, data: [] })),
        dataLabels: { enabled: false },
        stroke: { curve: 'smooth', width: 2 },
        fill: {
            type: 'gradient',
            gradient: { shadeIntensity: 1, opacityFrom: 0.3, opacityTo: 0.03, stops: [0, 100] }
        },
        markers: { size: 0 },
        legend: { show: kind === 'network', position: 'top', horizontalAlign: 'right' },
        xaxis: {
            type: 'datetime',
            tickAmount: 4,
            labels: { datetimeUTC: false, format: 'HH:mm:ss' },
            tooltip: { enabled: false }
        },
        yaxis,
        tooltip: {
            x: { format: 'HH:mm:ss' },
            y: { formatter: v => percent ? v.toFixed(1) + '%' : formatRate(v) }
        }
    };
    const themed = themeOptions(kind, c);
    // Merge the theme parts into the base options without losing the formatters above
    base.theme = themed.theme;
    base.colors = themed.colors;
    base.grid = themed.grid;
    base.xaxis.labels.style = themed.xaxis.labels.style;
    base.xaxis.axisBorder = themed.xaxis.axisBorder;
    base.xaxis.axisTicks = themed.xaxis.axisTicks;
    base.yaxis.labels.style = themed.yaxis.labels.style;
    base.legend.labels = themed.legend.labels;
    base.tooltip.theme = themed.tooltip.theme;
    return base;
}

function onVisibility() {
    if (dotnet) {
        dotnet.invokeMethodAsync('OnVisibilityChanged', document.hidden).catch(() => { });
    }
}

// options: { cpu, memory, network } element ids (omit or null to skip a chart), maxPoints
export async function init(dotnetRef, options) {
    dotnet = dotnetRef;
    disposed = false;
    maxPoints = options.maxPoints || 60;
    await loadApex();
    if (disposed) return;

    const c = themeColors();
    const defs = [
        ['cpu', options.cpu, ['CPU']],
        ['memory', options.memory, ['Memory']],
        ['network', options.network, ['In', 'Out']]
    ];
    for (const [kind, id, names] of defs) {
        const el = id ? document.getElementById(id) : null;
        if (!el) continue;
        const chart = new window.ApexCharts(el, buildOptions(kind, names, c));
        await chart.render();
        if (disposed) {
            chart.destroy();
            return;
        }
        charts[kind] = { chart, data: names.map(() => []), names };
    }

    observer = new MutationObserver(() => {
        const colors = themeColors();
        for (const kind of Object.keys(charts)) {
            charts[kind].chart.updateOptions(themeOptions(kind, colors), false, false);
        }
    });
    observer.observe(document.documentElement, { attributes: true, attributeFilter: ['data-bs-theme'] });
    document.addEventListener('visibilitychange', onVisibility);
    if (document.hidden) {
        onVisibility();
    }
}

// Append one point to a chart. values has one number per series.
export function push(kind, timestamp, values) {
    const entry = charts[kind];
    if (!entry) return;
    entry.data.forEach((points, i) => {
        points.push([timestamp, values[i]]);
        if (points.length > maxPoints) {
            points.splice(0, points.length - maxPoints);
        }
    });
    entry.chart.updateSeries(entry.names.map((name, i) => ({ name, data: entry.data[i] })), false);
}

export function dispose() {
    disposed = true;
    document.removeEventListener('visibilitychange', onVisibility);
    if (observer) {
        observer.disconnect();
        observer = null;
    }
    for (const kind of Object.keys(charts)) {
        try {
            charts[kind].chart.destroy();
        } catch (e) {
            // The element may already be gone
        }
    }
    charts = {};
    dotnet = null;
}
