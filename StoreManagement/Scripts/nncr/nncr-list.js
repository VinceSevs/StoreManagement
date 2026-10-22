(function () {
    'use strict';

    var DATA = window.NNCR_LIST_DATA || { totalCount: 0, rows: [] };
    var PAGE_SIZE = 10;

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

        rows.forEach(function (row) {
            row.style.display = 'none';
            row.classList.remove('is-even');
        });
        matching.slice(start, end).forEach(function (row, i) {
            row.style.display = '';
            row.classList.toggle('is-even', i % 2 === 1);
        });

        if (noMatchRow) noMatchRow.classList.toggle('d-none', total > 0 || rows.length === 0);

        var info = document.getElementById('nncrPageInfo');
        if (info) {
            info.textContent = total === 0
                ? 'Showing 0 of ' + DATA.totalCount + ' entries'
                : 'Showing ' + (start + 1) + ' to ' + Math.min(end, total) + ' of ' + total + ' entries';
        }

        renderPagination(totalPages);
    }

    // First, last and the pages around the current one; "..." for the gaps
    function buildPageWindow(current, totalPages) {
        var pages = [];
        for (var p = 1; p <= totalPages; p++) {
            if (p === 1 || p === totalPages || Math.abs(p - current) <= 1) pages.push(p);
            else if (pages[pages.length - 1] !== '...') pages.push('...');
        }
        return pages;
    }

    function renderPagination(totalPages) {
        var pager = document.getElementById('nncrPagination');
        if (!pager) return;
        pager.innerHTML = '';
        if (state.matching.length === 0) return;

        function addButton(content, page, className, disabled, label) {
            var btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'btn main-pagination-btn' + (className ? ' ' + className : '');
            if (label) btn.setAttribute('aria-label', label);
            btn.innerHTML = content;
            if (disabled) {
                btn.disabled = true;
            } else {
                btn.addEventListener('click', function () {
                    state.page = page;
                    renderPage();
                });
            }
            pager.appendChild(btn);
        }

        addButton('<i class="fa fa-chevron-left"></i>', state.page - 1, 'main-pagination-nav-btn', state.page === 1, 'Previous page');
        buildPageWindow(state.page, totalPages).forEach(function (p) {
            if (p === '...') {
                var gap = document.createElement('span');
                gap.className = 'main-pagination-ellipsis';
                gap.textContent = '...';
                pager.appendChild(gap);
            } else {
                addButton(String(p), p, p === state.page ? 'main-pagination-btn-active' : '', false, 'Page ' + p);
            }
        });
        addButton('<i class="fa fa-chevron-right"></i>', state.page + 1, 'main-pagination-nav-btn', state.page === totalPages, 'Next page');
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

        table.querySelectorAll('th.sortable').forEach(function (header) {
            var active = header === th;
            var icon = header.querySelector('.sort-icon');
            var btn = header.querySelector('.main-column-sort');
            if (icon) icon.className = 'fa sort-icon ' + (active ? (state.sortDir === 'asc' ? 'fa-sort-asc' : 'fa-sort-desc') : 'fa-sort');
            if (btn) btn.classList.toggle('is-active', active);
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
        document.querySelectorAll('.nncr-stat').forEach(function (c) { c.classList.remove('is-active'); });
        card.classList.add('is-active');
        state.status = card.getAttribute('data-status') || 'all';
        applyFilters();
    }

    document.querySelectorAll('.nncr-stat').forEach(function (card) {
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

    var searchBtn = document.getElementById('nncrSearchBtn');
    if (searchBtn && searchEl) searchBtn.addEventListener('click', function () {
        state.search = searchEl.value.trim().toLowerCase();
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

    var sizeEl = document.getElementById('nncrPageSize');
    if (sizeEl) {
        PAGE_SIZE = parseInt(sizeEl.value, 10) || PAGE_SIZE;
        sizeEl.addEventListener('change', function () {
            PAGE_SIZE = parseInt(this.value, 10) || 10;
            applyFilters();
        });
    }

    renderPage();
})();
