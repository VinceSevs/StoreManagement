(function () {
    'use strict';

    var DATA = window.NNCR_LIST_DATA || { totalCount: 0, rows: [] };
    var PAGE_SIZE = 8;

    var table = document.getElementById('nncrTable');
    if (!table) return;
    var tbody = table.querySelector('tbody');
    var noMatchRow = document.getElementById('nncrNoMatchRow');

    var state = {
        status: 'all',
        search: '',
        supplier: '',
        dateFrom: '',
        dateTo: '',
        page: 1,
        totalPages: 1,
        matching: [],
        sortKey: null,
        sortDir: null
    };

    function allRows() {
        return Array.prototype.slice.call(tbody.querySelectorAll('tr.nncr-row'));
    }

    // ---------- Filtering ----------
    function rowMatches(row) {
        if (state.status !== 'all' && row.getAttribute('data-status') !== state.status) return false;
        if (state.search && (row.getAttribute('data-search') || '').indexOf(state.search) === -1) return false;
        if (state.supplier && (row.getAttribute('data-supplier') || '').toLowerCase() !== state.supplier) return false;
        var date = row.getAttribute('data-date') || '';
        if (state.dateFrom && (!date || date < state.dateFrom)) return false;
        if (state.dateTo && (!date || date > state.dateTo)) return false;
        return true;
    }

    function applyFilters() {
        state.page = 1;
        renderPage();
    }

    function renderPage() {
        var rows = allRows();
        var matching = rows.filter(rowMatches);
        state.matching = matching;

        var total = matching.length;
        var totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));
        if (state.page > totalPages) state.page = totalPages;
        state.totalPages = totalPages;

        var start = (state.page - 1) * PAGE_SIZE;
        var end = start + PAGE_SIZE;

        rows.forEach(function (row) { row.style.display = 'none'; });
        matching.slice(start, end).forEach(function (row) { row.style.display = ''; });

        if (noMatchRow) noMatchRow.classList.toggle('d-none', total > 0 || rows.length === 0);

        var info = document.getElementById('nncrPageInfo');
        if (info) {
            info.textContent = total === 0
                ? '0 of ' + DATA.totalCount + ' shown'
                : (start + 1) + ' to ' + Math.min(end, total) + ' of ' + total + ' shown';
        }

        var count = document.getElementById('nncrCount');
        if (count) count.textContent = total + ' of ' + DATA.totalCount + ' match' + (total === 1 ? '' : 'es');

        renderPagination(totalPages);
    }

    function renderPagination(totalPages) {
        var pager = document.getElementById('nncrPagination');
        if (!pager) return;
        pager.innerHTML = '';
        if (totalPages <= 1) return;

        function addItem(label, page, opts) {
            opts = opts || {};
            var li = document.createElement('li');
            li.className = 'page-item' + (opts.disabled ? ' disabled' : '') + (opts.active ? ' active' : '');
            var btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'page-link';
            btn.textContent = label;
            if (!opts.disabled && !opts.active) {
                btn.addEventListener('click', function () {
                    state.page = page;
                    renderPage();
                });
            }
            li.appendChild(btn);
            pager.appendChild(li);
        }

        var MAX_VISIBLE = 5;
        var winStart = Math.max(1, state.page - Math.floor(MAX_VISIBLE / 2));
        var winEnd = winStart + MAX_VISIBLE - 1;
        if (winEnd > totalPages) {
            winEnd = totalPages;
            winStart = Math.max(1, winEnd - MAX_VISIBLE + 1);
        }

        addItem('Previous', state.page - 1, { disabled: state.page === 1 });
        for (var p = winStart; p <= winEnd; p++) {
            addItem(String(p), p, { active: p === state.page });
        }
        addItem('Next', state.page + 1, { disabled: state.page === totalPages });
    }

    function jumpToPage() {
        var input = document.getElementById('nncrPageJumpInput');
        if (!input) return;
        var target = parseInt(input.value, 10);
        if (isNaN(target) || target < 1) target = 1;
        if (target > state.totalPages) target = state.totalPages;
        state.page = target;
        renderPage();
        input.value = '';
    }

    // ---------- Sorting ----------
    function sortBy(th) {
        var key = th.getAttribute('data-sort-key') || ('col' + th.getAttribute('data-sort-col'));
        state.sortDir = (state.sortKey === key && state.sortDir === 'asc') ? 'desc' : 'asc';
        state.sortKey = key;

        var getValue;
        var sortKey = th.getAttribute('data-sort-key');
        if (sortKey === 'date' || sortKey === 'due') {
            getValue = function (row) { return row.getAttribute('data-' + sortKey) || ''; };
        } else {
            var col = parseInt(th.getAttribute('data-sort-col'), 10);
            getValue = function (row) { return row.cells[col].textContent.trim().toLowerCase(); };
        }

        var rows = allRows();
        rows.sort(function (a, b) {
            var cmp = getValue(a).localeCompare(getValue(b), undefined, { numeric: true, sensitivity: 'base' });
            return state.sortDir === 'asc' ? cmp : -cmp;
        });
        rows.forEach(function (row) { tbody.appendChild(row); });
        if (noMatchRow) tbody.appendChild(noMatchRow);

        table.querySelectorAll('th.sortable .sort-icon').forEach(function (icon) {
            icon.className = icon.parentNode === th
                ? 'bi sort-icon ' + (state.sortDir === 'asc' ? 'bi-sort-up' : 'bi-sort-down')
                : 'bi bi-arrow-down-up sort-icon';
        });

        applyFilters();
    }

    table.querySelectorAll('th.sortable').forEach(function (th) {
        th.addEventListener('click', function () { sortBy(th); });
    });

    // ---------- Row navigation ----------
    tbody.addEventListener('click', function (e) {
        if (e.target.closest('a')) return;
        var row = e.target.closest('tr.nncr-row');
        if (!row) return;
        var href = row.getAttribute('data-href');
        if (!href) return;
        if (e.ctrlKey || e.metaKey) {
            window.open(href, '_blank');
        } else {
            window.location.href = href;
        }
    });

    // ---------- Excel export ----------
    function exportToExcel() {
        if (typeof XLSX === 'undefined') {
            alert('The Excel library failed to load. Please check your connection and try again.');
            return;
        }
        if (state.matching.length === 0) {
            alert('No reports found for the selected filters/date range.');
            return;
        }

        var headers = [
            'Date of Incident', 'Date Filed', 'Due Date', 'NNCR No.', 'Status', 'Supplier', 'Site Warehouse',
            'Type of Non-conformance', 'Interception', 'Details of the Non-conformance',
            'QA Remarks', 'Issued By', 'Supplier Decision', 'Supplier Remarks',
            'Responded By', 'Responded Date', 'Closed By', 'Closed Date'
        ];

        var rows = state.matching.map(function (row) {
            var d = DATA.rows[parseInt(row.getAttribute('data-index'), 10)] || {};
            return [
                d.dateOfIncident, d.dateFiled, d.dueDate, d.reportNumber, d.status, d.supplier, d.siteWarehouse,
                d.nonconformanceType, d.interception, d.details,
                d.qaRemarks, d.issuedBy, d.supplierDecision, d.vendorRemarks,
                d.respondedBy, d.respondedDate, d.closedBy, d.closedDate
            ].map(function (v) { return v == null ? '' : v; });
        });

        var worksheet = XLSX.utils.aoa_to_sheet([headers].concat(rows));
        worksheet['!cols'] = [
            { wch: 14 }, { wch: 14 }, { wch: 14 }, { wch: 16 }, { wch: 22 }, { wch: 28 }, { wch: 22 },
            { wch: 30 }, { wch: 30 }, { wch: 50 },
            { wch: 36 }, { wch: 20 }, { wch: 16 }, { wch: 36 },
            { wch: 28 }, { wch: 20 }, { wch: 20 }, { wch: 20 }
        ];

        var workbook = XLSX.utils.book_new();
        XLSX.utils.book_append_sheet(workbook, worksheet, 'NNCR Reports');

        var today = new Date().toISOString().slice(0, 10);
        XLSX.writeFile(workbook, 'NNCR_Reports_' + today + '.xlsx');
    }

    // ---------- Wiring ----------
    function selectStatusCard(card) {
        document.querySelectorAll('.stat-card').forEach(function (c) { c.classList.remove('active'); });
        card.classList.add('active');
        state.status = card.getAttribute('data-status') || 'all';
        applyFilters();
    }

    document.querySelectorAll('.stat-card').forEach(function (card) {
        card.addEventListener('click', function () { selectStatusCard(card); });
        card.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); selectStatusCard(card); }
        });
    });

    var searchEl = document.getElementById('nncrSearch');
    if (searchEl) searchEl.addEventListener('input', function () {
        state.search = this.value.trim().toLowerCase();
        applyFilters();
    });

    var supplierEl = document.getElementById('supplierFilter');
    if (supplierEl) supplierEl.addEventListener('change', function () {
        state.supplier = this.value.toLowerCase();
        applyFilters();
    });

    var fromEl = document.getElementById('dateFrom');
    if (fromEl) fromEl.addEventListener('change', function () {
        state.dateFrom = this.value;
        applyFilters();
    });

    var toEl = document.getElementById('dateTo');
    if (toEl) toEl.addEventListener('change', function () {
        state.dateTo = this.value;
        applyFilters();
    });

    var exportBtn = document.getElementById('exportExcelBtn');
    if (exportBtn) exportBtn.addEventListener('click', exportToExcel);

    var jumpBtn = document.getElementById('nncrPageJumpBtn');
    if (jumpBtn) jumpBtn.addEventListener('click', jumpToPage);
    var jumpInput = document.getElementById('nncrPageJumpInput');
    if (jumpInput) jumpInput.addEventListener('keydown', function (e) {
        if (e.key === 'Enter') { e.preventDefault(); jumpToPage(); }
    });

    renderPage();
})();
