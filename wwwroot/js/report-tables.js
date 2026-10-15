/*
 * Report table pager (browser side) for report tables that are not paged by
 * the server: rows per page 10 / 20 / 50 / 100 / All and page numbers, in
 * the same style as the server pager (_Pager.cshtml). Pages that already
 * have a server pager (.vt-pager) are left alone. Opt out with
 * <table data-no-pager>.
 */
(function () {
    var SIZES = [10, 20, 50, 100], DEFAULT = 20, MIN_ROWS = 11;
    var T = window.__reportPagerText || { showing: 'Showing', of: 'of', rows: 'Rows', all: 'All', prev: 'Previous', next: 'Next' };

    function css() {
        if (document.getElementById('vt-pager-css')) return;
        var s = document.createElement('style');
        s.id = 'vt-pager-css';
        s.textContent =
            '.vt-pager{display:flex;flex-wrap:wrap;align-items:center;justify-content:space-between;gap:.75rem;padding:.75rem 1rem}' +
            '.vt-pager .info{font-size:13px;color:#64748b}.vt-pager .info b{color:#0f172a}' +
            '.vt-pager .pages{display:flex;gap:4px;flex-wrap:wrap}' +
            '.vt-pager .pg{min-width:36px;height:36px;padding:0 10px;border-radius:10px;border:1px solid #e2e8f0;background:#fff;display:inline-grid;place-items:center;font-weight:700;font-size:14px;color:#334155;cursor:pointer}' +
            '.vt-pager .pg:hover{background:#f1f5f9}.vt-pager .pg.active{background:#0b57d0;border-color:#0b57d0;color:#fff}' +
            '.vt-pager .pg[disabled]{opacity:.4;pointer-events:none}.vt-pager .pg.gap{border:none;background:none;cursor:default}' +
            '.vt-pager .size{display:flex;align-items:center;gap:6px;font-size:13px;color:#64748b}' +
            '@media print{.vt-pager[data-client]{display:none}}';
        document.head.appendChild(s);
    }

    function window_(page, last) {
        var shown = [1, last], out = [], prev = 0;
        for (var i = page - 2; i <= page + 2; i++) if (i >= 1 && i <= last) shown.push(i);
        shown = shown.filter(function (v, i, a) { return a.indexOf(v) === i; }).sort(function (a, b) { return a - b; });
        shown.forEach(function (p) { if (prev && p - prev > 1) out.push(0); out.push(p); prev = p; });
        return out;
    }

    function attach(table) {
        var body = table.tBodies[0];
        if (!body) return;
        // Data rows only (not "no records" rows spanning the table).
        var rows = [].filter.call(body.rows, function (r) { return !(r.cells.length === 1 && r.cells[0].colSpan > 1); });
        if (rows.length < MIN_ROWS) return;

        var size = DEFAULT, page = 1;
        var bar = document.createElement('nav');
        bar.className = 'vt-pager';
        bar.setAttribute('data-client', '');
        var host = table.closest('.table-responsive') || table;
        host.parentNode.insertBefore(bar, host.nextSibling);

        // Printing shows every row.
        window.addEventListener('beforeprint', function () { rows.forEach(function (r) { r.style.display = ''; }); });
        window.addEventListener('afterprint', render);

        function render() {
            var total = rows.length, all = size === 0;
            var last = all ? 1 : Math.max(1, Math.ceil(total / size));
            if (page > last) page = last;
            var from = all ? 0 : (page - 1) * size, to = all ? total : Math.min(total, from + size);
            rows.forEach(function (r, i) { r.style.display = i >= from && i < to ? '' : 'none'; });

            var h = '<div class="info">' + T.showing + ' <b>' + (from + 1) + '–' + to + '</b> ' + T.of + ' <b>' + total + '</b></div>';
            if (last > 1) {
                h += '<div class="pages"><button type="button" class="pg" data-p="' + (page - 1) + '"' + (page === 1 ? ' disabled' : '') + ' title="' + T.prev + '"><i class="bi bi-chevron-left"></i></button>';
                window_(page, last).forEach(function (p) {
                    h += p === 0 ? '<span class="pg gap">…</span>' : '<button type="button" class="pg' + (p === page ? ' active' : '') + '" data-p="' + p + '">' + p + '</button>';
                });
                h += '<button type="button" class="pg" data-p="' + (page + 1) + '"' + (page === last ? ' disabled' : '') + ' title="' + T.next + '"><i class="bi bi-chevron-right"></i></button></div>';
            }
            h += '<div class="size">' + T.rows + ' <select class="form-select form-select-sm w-auto">' +
                SIZES.map(function (s) { return '<option value="' + s + '"' + (s === size ? ' selected' : '') + '>' + s + '</option>'; }).join('') +
                '<option value="0"' + (size === 0 ? ' selected' : '') + '>' + T.all + '</option></select></div>';
            bar.innerHTML = h;
        }

        bar.addEventListener('click', function (e) {
            var b = e.target.closest('[data-p]');
            if (!b) return;
            page = parseInt(b.getAttribute('data-p'), 10);
            render();
            table.scrollIntoView({ block: 'nearest' });
        });
        bar.addEventListener('change', function (e) {
            size = parseInt(e.target.value, 10);
            page = 1;
            render();
        });
        render();
    }

    function init() {
        if (!/^\/(reports|transactions)\b/i.test(location.pathname)) return;
        if (document.querySelector('.vt-pager:not([data-client])')) return; // server-paged page
        css();
        document.querySelectorAll('main table, .container table, .container-fluid table').forEach(function (t) {
            if (!t.hasAttribute('data-no-pager') && !t.closest('[data-no-pager]') && !t.closest('.dropdown-menu')) attach(t);
        });
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init); else init();
})();
