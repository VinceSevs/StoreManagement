    $(function () {
        setupPcrTable();
    });

    function setupPcrTable() {
        var PAGE_SIZE = 10;
        var currentPage = 1;
        var allRows = Array.from(document.querySelectorAll('#pcrTable tbody tr.pcr-row'));
        var originalRowOrder = allRows.slice();
        var visibleRows = allRows.slice();
        var sortState = { column: null, direction: null };

        var searchInput = document.getElementById('pcrSearch');
        var statusSelect = document.getElementById('pcrStatusFilter');
        var dateFromInput = document.getElementById('pcrDateFrom');
        var dateToInput = document.getElementById('pcrDateTo');

        function reorderDom(rows) {
            var tbody = document.querySelector('#pcrTable tbody');
            rows.forEach(function (row) { tbody.appendChild(row); });
        }

        function getSortValue(row, colIndex) {
            if (colIndex === 0) return parseInt(row.getAttribute('data-status'), 10) || 0;
            if (colIndex === 1) return row.getAttribute('data-date') || '';
            var cell = row.querySelectorAll('td')[colIndex];
            return cell ? cell.textContent.trim() : '';
        }

        function compareRows(a, b, colIndex) {
            var va = getSortValue(a, colIndex);
            var vb = getSortValue(b, colIndex);
            if (typeof va === 'number' && typeof vb === 'number') return va - vb;
            return String(va).localeCompare(String(vb), undefined, { numeric: true, sensitivity: 'base' });
        }

        function updateSortIndicators() {
            document.querySelectorAll('.pcr-sortable').forEach(function (th) {
                th.classList.remove('sort-asc', 'sort-desc');
                var icon = th.querySelector('.sort-icon');
                if (icon) icon.className = 'bi bi-arrow-down-up sort-icon';
            });

            if (sortState.column === null) return;

            var activeTh = document.querySelector('.pcr-sortable[data-col="' + sortState.column + '"]');
            if (!activeTh) return;

            activeTh.classList.add(sortState.direction === 'asc' ? 'sort-asc' : 'sort-desc');
            var activeIcon = activeTh.querySelector('.sort-icon');
            if (activeIcon) {
                activeIcon.className = 'bi ' + (sortState.direction === 'asc' ? 'bi-caret-up-fill' : 'bi-caret-down-fill') + ' sort-icon';
            }
        }

        function sortByColumn(colIndex) {
            sortState.direction = (sortState.column === colIndex && sortState.direction === 'asc') ? 'desc' : 'asc';
            sortState.column = colIndex;

            allRows.sort(function (a, b) {
                var cmp = compareRows(a, b, colIndex);
                return sortState.direction === 'asc' ? cmp : -cmp;
            });
            reorderDom(allRows);
            updateSortIndicators();
            applyFilters();
        }

        function resetSort() {
            sortState = { column: null, direction: null };
            allRows = originalRowOrder.slice();
            reorderDom(allRows);
            updateSortIndicators();
        }

        document.querySelectorAll('.pcr-sortable').forEach(function (th) {
            th.addEventListener('click', function () {
                sortByColumn(parseInt(th.getAttribute('data-col'), 10));
            });
        });

        function rowMatches(row) {
            var term = (searchInput.value || '').trim().toLowerCase();
            if (term && row.textContent.toLowerCase().indexOf(term) === -1) return false;

            var status = statusSelect.value;
            if (status && row.getAttribute('data-status') !== status) return false;

            var rowDate = row.getAttribute('data-date');
            var from = dateFromInput.value;
            var to = dateToInput.value;
            if (from && rowDate < from) return false;
            if (to && rowDate > to) return false;

            return true;
        }

        function applyFilters() {
            visibleRows = allRows.filter(rowMatches);
            currentPage = 1;
            renderPage();
        }

        function clearFilters() {
            searchInput.value = '';
            statusSelect.value = '';
            dateFromInput.value = '';
            dateToInput.value = '';
            resetSort();
            applyFilters();
        }

        function renderPage() {
            var totalRows = visibleRows.length;
            var totalPages = Math.max(1, Math.ceil(totalRows / PAGE_SIZE));
            if (currentPage > totalPages) currentPage = totalPages;

            var startIndex = (currentPage - 1) * PAGE_SIZE;
            var endIndex = startIndex + PAGE_SIZE;
            var visibleSet = new Set(visibleRows.slice(startIndex, endIndex));

            allRows.forEach(function (row) {
                row.style.display = visibleSet.has(row) ? '' : 'none';
            });

            var infoEl = document.getElementById('pcrPageInfo');
            if (infoEl) {
                infoEl.textContent = totalRows === 0
                    ? 'No PCRs found'
                    : (startIndex + 1) + ' to ' + Math.min(endIndex, totalRows) + ' of ' + totalRows + ' shown';
            }

            var countEl = document.getElementById('pcrResultCount');
            if (countEl) {
                countEl.textContent = totalRows === allRows.length
                    ? totalRows + ' total'
                    : totalRows + ' of ' + allRows.length + ' match filters';
            }

            renderPaginationControls(totalPages);
        }

        function renderPaginationControls(totalPages) {
            var pager = document.getElementById('pcrPagination');
            if (!pager) return;
            pager.innerHTML = '';

            function addPageItem(label, page, opts) {
                opts = opts || {};
                var li = document.createElement('li');
                li.className = 'page-item' + (opts.disabled ? ' disabled' : '') + (opts.active ? ' active' : '');

                if (opts.ellipsis) {
                    li.innerHTML = '<span class="page-link" style="border:none;background:transparent;box-shadow:none;">&hellip;</span>';
                    pager.appendChild(li);
                    return;
                }

                var btn = document.createElement('button');
                btn.type = 'button';
                btn.className = 'page-link';
                btn.textContent = label;
                if (!opts.disabled && !opts.active) {
                    btn.addEventListener('click', function () {
                        currentPage = page;
                        renderPage();
                    });
                }
                li.appendChild(btn);
                pager.appendChild(li);
            }

            if (totalPages <= 1) return;

            addPageItem('Previous', currentPage - 1, { disabled: currentPage === 1 });

            addPageItem('1', 1, { active: currentPage === 1 });

            var windowStart = Math.max(2, currentPage - 1);
            var windowEnd = Math.min(totalPages - 1, currentPage + 1);

            if (windowStart > 2) {
                addPageItem(null, null, { ellipsis: true });
            }

            for (var p = windowStart; p <= windowEnd; p++) {
                addPageItem(String(p), p, { active: p === currentPage });
            }

            if (windowEnd < totalPages - 1) {
                addPageItem(null, null, { ellipsis: true });
            }

            if (totalPages > 1) {
                addPageItem(String(totalPages), totalPages, { active: currentPage === totalPages });
            }

            addPageItem('Next', currentPage + 1, { disabled: currentPage === totalPages });
        }

        function exportToExcel() {
            if (typeof XLSX === 'undefined') {
                alert('Excel export library failed to load. Check your internet connection and try again.');
                return;
            }

            var headers = ['Status', 'Create Date', 'PCR No.', 'Store Name', 'Item', 'Complaint Issue', 'Vendor', 'Vendor Remarks', 'QA Remarks'];
            var data = [headers];

            visibleRows.forEach(function (row) {
                var cells = row.querySelectorAll('td');
                var rowData = [];
                for (var i = 0; i < cells.length - 1; i++) { 
                    rowData.push(cells[i].textContent.trim());
                }
                data.push(rowData);
            });

            var worksheet = XLSX.utils.aoa_to_sheet(data);
            worksheet['!cols'] = headers.map(function () { return { wch: 18 }; });

            var workbook = XLSX.utils.book_new();
            XLSX.utils.book_append_sheet(workbook, worksheet, 'PCR List');

            var today = new Date().toISOString().slice(0, 10);
            XLSX.writeFile(workbook, 'ProductComplaintReport_' + today + '.xlsx');
        }

        function jumpToPage() {
            var input = document.getElementById('pcrPageJumpInput');
            if (!input) return;
            var totalPages = Math.max(1, Math.ceil(visibleRows.length / PAGE_SIZE));
            var target = parseInt(input.value, 10);
            if (isNaN(target) || target < 1) target = 1;
            if (target > totalPages) target = totalPages;
            currentPage = target;
            renderPage();
            input.value = '';
        }

        document.getElementById('pcrPageJumpBtn').addEventListener('click', jumpToPage);
        document.getElementById('pcrPageJumpInput').addEventListener('keydown', function (e) {
            if (e.key === 'Enter') { e.preventDefault(); jumpToPage(); }
        });

        var searchTimer;
        searchInput.addEventListener('input', function () {
            clearTimeout(searchTimer);
            searchTimer = setTimeout(applyFilters, 200);
        });
        statusSelect.addEventListener('change', applyFilters);
        dateFromInput.addEventListener('change', applyFilters);
        dateToInput.addEventListener('change', applyFilters);
        document.getElementById('pcrClearFilters').addEventListener('click', clearFilters);
        document.getElementById('pcrExportBtn').addEventListener('click', exportToExcel);

        renderPage();
    }
