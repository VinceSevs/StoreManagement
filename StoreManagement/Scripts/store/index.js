    function openModal(url) {
        $.get(url, function (html) {
            $('#modalBox').html(html);
            $('#modalOverlay').show();
        });
    }

    function closeModal() {
        $('#modalOverlay').hide();
        $('#modalBox').empty();
    }

    function postToModal(url, formSelector) {
        $.post(url, $(formSelector).serialize(), function (html) {
            $('#modalBox').html(html);
        });
    }

    function setupAutocomplete(inputId, boxId, field) {
        var timer;
        var $input = $('#' + inputId);
        var $box = $('#' + boxId);

        $input.on('input', function () {
            clearTimeout(timer);
            var term = $input.val();

            if (term.length < 1) { $box.hide().empty(); return; }

            timer = setTimeout(function () {
                $.getJSON(window.STORE_INDEX_DATA.suggestionsUrl, { field: field, term: term }, function (data) {
                    $box.empty();
                    if (data.length === 0) { $box.hide(); return; }
                    data.forEach(function (item) {
                        $('<button type="button" class="list-group-item list-group-item-action"></button>')
                            .text(item.Display)
                            .appendTo($box)
                            .on('click', function () {
                                $input.val(item.Value);
                                $box.hide().empty();
                                $input.closest('form').submit();
                            });
                    });
                    $box.show();
                });
            }, 250);
        });

        $(document).on('click', function (e) {
            if (!$(e.target).closest('#' + inputId).length && !$(e.target).closest('#' + boxId).length) {
                $box.hide();
            }
        });
    }

    $(function () {
        setupAutocomplete('search', 'searchSuggestions', 'search');
        setupStorePagination();
    });

    function setupStorePagination() {
        var PAGE_SIZE = 8;
        var currentPage = 1;
        var rows = Array.from(document.querySelectorAll('#storeTable tbody tr.store-row'));
        var totalRows = rows.length;
        var totalPages = Math.max(1, Math.ceil(totalRows / PAGE_SIZE));

        function renderPage() {
            var startIndex = (currentPage - 1) * PAGE_SIZE;
            var endIndex = startIndex + PAGE_SIZE;

            rows.forEach(function (row, i) {
                row.style.display = (i >= startIndex && i < endIndex) ? '' : 'none';
            });

            var infoEl = document.getElementById('storePageInfo');
            if (infoEl) {
                infoEl.textContent = totalRows === 0
                    ? 'No stores found'
                    : (startIndex + 1) + ' to ' + Math.min(endIndex, totalRows) + ' of ' + totalRows + ' shown';
            }

            renderPaginationControls();
        }

        function renderPaginationControls() {
            var pager = document.getElementById('storePagination');
            if (!pager) return;
            pager.innerHTML = '';

            function addPageItem(label, page, opts) {
                opts = opts || {};
                var li = document.createElement('li');
                li.className = 'page-item' + (opts.disabled ? ' disabled' : '') + (opts.active ? ' active' : '');
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

            var MAX_VISIBLE = 5;
            var windowStart = Math.max(1, currentPage - Math.floor(MAX_VISIBLE / 2));
            var windowEnd = windowStart + MAX_VISIBLE - 1;
            if (windowEnd > totalPages) {
                windowEnd = totalPages;
                windowStart = Math.max(1, windowEnd - MAX_VISIBLE + 1);
            }

            addPageItem('Previous', currentPage - 1, { disabled: currentPage === 1 });
            for (var p = windowStart; p <= windowEnd; p++) {
                addPageItem(String(p), p, { active: p === currentPage });
            }
            addPageItem('Next', currentPage + 1, { disabled: currentPage === totalPages });
        }

        function jumpToPage() {
            var input = document.getElementById('storePageJumpInput');
            if (!input) return;
            var target = parseInt(input.value, 10);
            if (isNaN(target) || target < 1) target = 1;
            if (target > totalPages) target = totalPages;
            currentPage = target;
            renderPage();
            input.value = '';
        }

        document.getElementById('storePageJumpBtn').addEventListener('click', jumpToPage);
        document.getElementById('storePageJumpInput').addEventListener('keydown', function (e) {
            if (e.key === 'Enter') { e.preventDefault(); jumpToPage(); }
        });

        renderPage();
    }
