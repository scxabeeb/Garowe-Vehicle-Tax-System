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
        var rows = dataRows(body);
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
            rows = dataRows(body); // current order, after any sorting
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
        table.__pagerRender = function () { page = 1; render(); };
    }

    /** Data rows only (not "no records" rows spanning the table). */
    function dataRows(body) {
        return [].filter.call(body.rows, function (r) { return !(r.cells.length === 1 && r.cells[0].colSpan > 1); });
    }

    var MONTHS = { jan: 0, feb: 1, mar: 2, apr: 3, may: 4, jun: 5, jul: 6, aug: 7, sep: 8, oct: 9, nov: 10, dec: 11 };

    /** A cell's sort value: number (amounts, counts, %), date, or text. */
    function sortValue(cell) {
        var t = (cell ? cell.innerText : '').trim();
        if (!t || t === '-' || t === '—') return { k: 0, v: '' };
        var n = t.replace(/[$,%\s]/g, '');
        if (/^-?\d+(\.\d+)?$/.test(n)) return { k: 1, v: parseFloat(n) };
        var m = /^(\d{4})-(\d{2})-(\d{2})(?:\s+(\d{1,2}):(\d{2}))?/.exec(t);
        if (m) return { k: 2, v: new Date(+m[1], m[2] - 1, +m[3], +(m[4] || 0), +(m[5] || 0)).getTime() };
        m = /^(\d{1,2})[\s-]([A-Za-z]{3})[A-Za-z]*[\s-](\d{4})(?:,?\s+(\d{1,2}):(\d{2}))?/.exec(t);
        if (m && MONTHS[m[2].toLowerCase()] !== undefined)
            return { k: 2, v: new Date(+m[3], MONTHS[m[2].toLowerCase()], +m[1], +(m[4] || 0), +(m[5] || 0)).getTime() };
        return { k: 3, v: t.toLowerCase() };
    }

    function attachSort(table, serverPaged) {
        var head = table.tHead, body = table.tBodies[0];
        if (!head || !body || table.hasAttribute('data-no-sort')) return;
        var cells = head.rows[head.rows.length - 1].cells;
        if (dataRows(body).length < 2) return;
        var state = { col: -1, dir: 1 };
        [].forEach.call(cells, function (th, col) {
            var label = th.innerText.trim();
            if (!label || th.colSpan > 1 || /^(actions|#)$/i.test(label)) return;
            th.style.cursor = 'pointer';
            th.style.userSelect = 'none';
            th.title = T.sort || 'Sort';
            var arrow = document.createElement('span');
            arrow.className = 'vt-sort';
            arrow.textContent = ' ⇅';
            arrow.style.opacity = '.35';
            th.appendChild(arrow);
            th.addEventListener('click', function () {
                state.dir = state.col === col ? -state.dir : 1;
                state.col = col;
                var rows = dataRows(body).map(function (r, i) { return { r: r, i: i, s: sortValue(r.cells[col]) }; });
                rows.sort(function (a, b) {
                    if (a.s.k === 0 && b.s.k !== 0) return 1;   // blanks last
                    if (b.s.k === 0 && a.s.k !== 0) return -1;
                    var x = a.s.v, y = b.s.v, c;
                    if (a.s.k !== b.s.k) c = String(x).localeCompare(String(y));
                    else c = typeof x === 'number' ? x - y : x.localeCompare(y);
                    return (c || a.i - b.i) * (c ? state.dir : 1);
                });
                var empty = [].filter.call(body.rows, function (r) { return r.cells.length === 1 && r.cells[0].colSpan > 1; });
                rows.forEach(function (o) { body.appendChild(o.r); });
                empty.forEach(function (r) { body.appendChild(r); });
                head.querySelectorAll('.vt-sort').forEach(function (a) { a.textContent = ' ⇅'; a.style.opacity = '.35'; });
                arrow.textContent = state.dir === 1 ? ' ▲' : ' ▼';
                arrow.style.opacity = '1';
                if (table.__pagerRender) table.__pagerRender();
            });
        });
        if (serverPaged && !table.parentNode.querySelector('.vt-sort-hint')) {
            var hint = document.createElement('div');
            hint.className = 'vt-sort-hint small text-muted px-3 pb-2';
            hint.textContent = T.sortHint || 'Sorting applies to the rows on this page. Choose All rows to sort everything.';
            var host = table.closest('.table-responsive') || table;
            host.parentNode.insertBefore(hint, host.nextSibling);
        }
    }

    function init() {
        if (!/^\/(reports|transactions)\b/i.test(location.pathname)) return;
        var serverPaged = !!document.querySelector('.vt-pager:not([data-client])');
        css();
        document.querySelectorAll('main table, .container table, .container-fluid table').forEach(function (t) {
            if (t.closest('.dropdown-menu')) return;
            if (!serverPaged && !t.hasAttribute('data-no-pager') && !t.closest('[data-no-pager]')) attach(t);
            // Server-paged tables are the ones followed by a server pager.
            var paged = false;
            for (var el = t.parentElement, up = 0; serverPaged && el && up < 4; el = el.parentElement, up++)
                if (el.querySelector('.vt-pager:not([data-client])')) { paged = true; break; }
            attachSort(t, paged);
        });
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init); else init();
})();
