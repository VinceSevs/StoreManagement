	const ITEMS_BY_CAR = window.CAPA_LIST_DATA.itemsByCar;
	const PAGE_SIZE = 8;
	let currentStatusFilter = 'all';
	let currentSearchQuery = '';
	let currentClassificationFilter = '';
	let currentPage = 1;
	let lastMatchingRows = [];
	let lastTotalPages = 1;
	let sortState = { column: null, direction: null };

	function sortRowsByColumn(columnIndex, iconId, getValue) {
		sortState.direction = (sortState.column === columnIndex && sortState.direction === 'asc') ? 'desc' : 'asc';
		sortState.column = columnIndex;

		const tbody = document.querySelector('#capaTable tbody');
		const rows = Array.from(tbody.querySelectorAll('tr[data-search]'));

		rows.sort(function (a, b) {
			const cmp = getValue(a).localeCompare(getValue(b), undefined, { numeric: true, sensitivity: 'base' });
			return sortState.direction === 'asc' ? cmp : -cmp;
		});

		rows.forEach(function (row) {
			const detail = row.nextElementSibling;
			const hasDetail = detail && detail.classList.contains('item-detail-row');
			tbody.appendChild(row);
			if (hasDetail) tbody.appendChild(detail);
		});

		document.querySelectorAll('.sort-icon').forEach(function (icon) {
			icon.className = icon.id === iconId
				? 'bi sort-icon ' + (sortState.direction === 'asc' ? 'bi-sort-up' : 'bi-sort-down')
				: 'bi bi-arrow-down-up sort-icon';
		});

		applyFilters();
	}

	function cellText(columnIndex) {
		return function (row) { return row.cells[columnIndex].textContent.trim().toLowerCase(); };
	}

	function toggleItemDetails(rowEl, event) {
		if (event && event.target.closest('a')) return;

		const detailRow = rowEl.nextElementSibling;
		if (!detailRow || !detailRow.classList.contains('item-detail-row')) return;

		detailRow.classList.toggle('d-none');

		const icon = rowEl.querySelector('.expand-icon');
		if (icon) {
			icon.classList.toggle('bi-chevron-right');
			icon.classList.toggle('bi-chevron-down');
		}
	}

    function toggleDateIssuedSort() {
        sortRowsByColumn(0, 'dateIssuedSortIcon', function (row) { return row.getAttribute('data-created') || ''; });
    }
    function toggleCarNoSort() {
        sortRowsByColumn(1, 'carNoSortIcon', cellText(1));
    }
    function toggleStockMovementSort() {
        sortRowsByColumn(2, 'stockMovementSortIcon', cellText(2));
    }
    function toggleItemCodeSort() {
        sortRowsByColumn(3, 'itemCodeSortIcon', cellText(3));
    }
    function toggleDescriptionSort() {
        sortRowsByColumn(4, 'descriptionSortIcon', cellText(4));
    }
    function toggleUTDSort() {
        sortRowsByColumn(5, 'utdSortIcon', cellText(5));
    }
    function toggleUOMSort() {
        sortRowsByColumn(6, 'uomSortIcon', cellText(6));
    }
    function toggleQuantitySort() {
        sortRowsByColumn(7, 'quantitySortIcon', cellText(7));
    }
    function toggleNatureOfDefectSort() {
        sortRowsByColumn(8, 'natureOfDefectSortIcon', cellText(8));
    }
    function toggleCarClassificationSort() {
        sortRowsByColumn(9, 'carClassificationSortIcon', cellText(9));
    }
    function toggleStatusSort() {
        sortRowsByColumn(10, 'statusSortIcon', cellText(10));
    }

	function applyFilters() {
		currentPage = 1;
		renderPage();
	}

	function renderPage() {
		document.querySelectorAll('#capaTable tbody tr.item-detail-row').forEach(function (row) {
			row.classList.add('d-none');
		});
		document.querySelectorAll('#capaTable tbody .expand-icon').forEach(function (icon) {
			icon.classList.remove('bi-chevron-down');
			icon.classList.add('bi-chevron-right');
		});

		const allRows = Array.from(document.querySelectorAll('#capaTable tbody tr[data-search]'));
		const matchingRows = allRows.filter(function (row) {
			const matchesSearch = !currentSearchQuery || (row.getAttribute('data-search') || '').includes(currentSearchQuery);
			const matchesStatus = currentStatusFilter === 'all' || row.getAttribute('data-status') === currentStatusFilter;
			const matchesClassification = !currentClassificationFilter || row.getAttribute('data-classification') === currentClassificationFilter;
			return matchesSearch && matchesStatus && matchesClassification;
		});
		lastMatchingRows = matchingRows;

		const totalMatches = matchingRows.length;
		const totalPages = Math.max(1, Math.ceil(totalMatches / PAGE_SIZE));
		if (currentPage > totalPages) currentPage = totalPages;
		lastTotalPages = totalPages;

		const startIndex = (currentPage - 1) * PAGE_SIZE;
		const endIndex = startIndex + PAGE_SIZE;

		allRows.forEach(function (row) { row.style.display = 'none'; });
		matchingRows.slice(startIndex, endIndex).forEach(function (row) { row.style.display = ''; });

		const infoEl = document.getElementById('capaPageInfo');
		if (infoEl) {
			infoEl.textContent = totalMatches === 0
				? '0 of ' + window.CAPA_LIST_DATA.totalCount + ' shown'
				: (startIndex + 1) + ' to ' + Math.min(endIndex, totalMatches) + ' of ' + totalMatches + ' shown';
		}

		const countEl = document.getElementById('capaCount');
		if (countEl) countEl.textContent = totalMatches + ' of ' + window.CAPA_LIST_DATA.totalCount + ' match' + (totalMatches === 1 ? '' : 'es');

		renderPaginationControls(totalPages);
	}

	function renderPaginationControls(totalPages) {
		const pager = document.getElementById('capaPagination');
		if (!pager) return;
		pager.innerHTML = '';

		function addPageItem(label, page, opts) {
			opts = opts || {};
			const li = document.createElement('li');
			li.className = 'page-item' + (opts.disabled ? ' disabled' : '') + (opts.active ? ' active' : '');
			const btn = document.createElement('button');
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

		const MAX_VISIBLE = 5;
		let windowStart = Math.max(1, currentPage - Math.floor(MAX_VISIBLE / 2));
		let windowEnd = windowStart + MAX_VISIBLE - 1;
		if (windowEnd > totalPages) {
			windowEnd = totalPages;
			windowStart = Math.max(1, windowEnd - MAX_VISIBLE + 1);
		}

		addPageItem('Previous', currentPage - 1, { disabled: currentPage === 1 });
		for (let p = windowStart; p <= windowEnd; p++) {
			addPageItem(String(p), p, { active: p === currentPage });
		}
		addPageItem('Next', currentPage + 1, { disabled: currentPage === totalPages });
	}

	function jumpToPage() {
		const input = document.getElementById('capaPageJumpInput');
		if (!input) return;
		let target = parseInt(input.value, 10);
		if (isNaN(target) || target < 1) target = 1;
		if (target > lastTotalPages) target = lastTotalPages;
		currentPage = target;
		renderPage();
		input.value = '';
	}

	document.getElementById('capaPageJumpBtn').addEventListener('click', jumpToPage);
	document.getElementById('capaPageJumpInput').addEventListener('keydown', function (e) {
		if (e.key === 'Enter') { e.preventDefault(); jumpToPage(); }
	});

	document.getElementById('capaSearch').addEventListener('input', function () {
		currentSearchQuery = this.value.trim().toLowerCase();
		applyFilters();
	});

	document.getElementById('classificationFilter').addEventListener('change', function () {
		currentClassificationFilter = this.value;
		applyFilters();
	});

	function exportToExcel() {
		const dateFrom = document.getElementById('exportDateFrom').value; 
		const dateTo = document.getElementById('exportDateTo').value;

		const matchingRows = lastMatchingRows.filter(function (row) {
			const created = row.getAttribute('data-created') || '';
			if (dateFrom && (!created || created < dateFrom)) return false;
			if (dateTo && (!created || created > dateTo)) return false;
			return true;
		});

		if (matchingRows.length === 0) {
			alert('No reports found for the selected filters/date range.');
			return;
		}

		const headers = ['Date Created', 'Car No.', 'Stock Movement', 'Item Code', 'Description', 'UTD', 'UOM', 'Quantity', 'Nature of Defect', 'Car Classification', 'Status'];
		const rows = [];

		matchingRows.forEach(function (row) {
			const dateCreated = row.cells[0].textContent.trim();
			const reportNumber = row.getAttribute('data-report-number') || row.cells[1].textContent.trim();
			const stockMovement = row.cells[2].textContent.trim();
			const carClassification = row.cells[9].textContent.trim();
            const status = row.cells[10].textContent.trim();

			const items = ITEMS_BY_CAR[reportNumber] || [];
			if (items.length === 0) {
				rows.push([dateCreated, reportNumber, stockMovement, '', '', '', '', '', '', carClassification, status]);
				return;
			}

			items.forEach(function (item) {
				rows.push([
					dateCreated, reportNumber, stockMovement,
					item.itemCode, item.itemDescription, item.utd, item.uom, item.quantity, item.defectName,
					carClassification, status
				]);
			});
		});

		const worksheet = XLSX.utils.aoa_to_sheet([headers, ...rows]);
		worksheet['!cols'] = [{ wch: 14 }, { wch: 14 }, { wch: 16 }, { wch: 14 }, { wch: 28 }, { wch: 14 }, { wch: 10 }, { wch: 10 }, { wch: 20 }, { wch: 18 }, { wch: 16 }];

		const workbook = XLSX.utils.book_new();
		XLSX.utils.book_append_sheet(workbook, worksheet, 'CAR Reports');

		const today = new Date().toISOString().slice(0, 10);
		XLSX.writeFile(workbook, `CAR_Reports_${today}.xlsx`);

		fetch(window.CAPA_LIST_DATA.logCapaActionUrl, {
			method: 'POST',
			headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
			body: 'logType=' + encodeURIComponent('Export to Excel')
		}).catch(() => {});
	}

	document.querySelectorAll('.stat-card').forEach(function (card) {
		card.addEventListener('click', function () {
			document.querySelectorAll('.stat-card').forEach(c => c.classList.remove('active'));
			this.classList.add('active');
			currentStatusFilter = this.dataset.status;
			applyFilters();
		});
	});

	renderPage();
