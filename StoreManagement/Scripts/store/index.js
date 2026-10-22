    function openModal(url) {
        $.get(url, function (html) {
            var $box = $('#modalBox');
            $box.html(html);
            $box.toggleClass('is-view', $box.find('[data-store-view]').length > 0);
            $('#modalOverlay').show().attr('aria-hidden', 'false');
        });
    }

    function closeModal() {
        $('#modalOverlay').hide().attr('aria-hidden', 'true');
        $('#modalBox').empty().removeClass('is-view');
    }

    function postToModal(url, formSelector) {
        $.post(url, $(formSelector).serialize(), function (html) {
            var $box = $('#modalBox');
            $box.html(html);
            $box.toggleClass('is-view', $box.find('[data-store-view]').length > 0);
        });
    }

    // Row actions menu (View / Update), same behavior as the LEADS list pages
    function toggleRowActionsMenu(event, triggerBtn) {
        if (event) { event.stopPropagation(); }

        var menu = triggerBtn.closest('.main-actions-menu');
        var dropdown = menu.querySelector('.main-actions-dropdown');
        var wasOpen = dropdown.classList.contains('is-open');

        closeAllRowActionMenus();

        if (!wasOpen) {
            var rect = triggerBtn.getBoundingClientRect();
            var height = dropdown.offsetHeight || 90;
            var openUpward = (window.innerHeight - rect.bottom) < (height + 10) && rect.top > height;

            dropdown.style.position = 'fixed';
            if (openUpward) {
                dropdown.style.top = 'auto';
                dropdown.style.bottom = (window.innerHeight - rect.top + 6) + 'px';
            } else {
                dropdown.style.top = (rect.bottom + 6) + 'px';
                dropdown.style.bottom = 'auto';
            }
            dropdown.style.left = 'auto';
            dropdown.style.right = (window.innerWidth - rect.right) + 'px';

            dropdown.classList.add('is-open');
            triggerBtn.classList.add('is-active');
            triggerBtn.setAttribute('aria-expanded', 'true');
        }
    }

    function closeAllRowActionMenus() {
        document.querySelectorAll('.main-actions-dropdown.is-open').forEach(function (dropdown) {
            dropdown.classList.remove('is-open');
            dropdown.style.position = '';
            dropdown.style.top = '';
            dropdown.style.bottom = '';
            dropdown.style.left = '';
            dropdown.style.right = '';
        });
        document.querySelectorAll('.main-actions-trigger.is-active').forEach(function (btn) {
            btn.classList.remove('is-active');
            btn.setAttribute('aria-expanded', 'false');
        });
    }

    document.addEventListener('click', function (event) {
        if (!event.target.closest('.main-actions-menu')) { closeAllRowActionMenus(); }
    });
    document.addEventListener('scroll', closeAllRowActionMenus, true);
    document.addEventListener('keydown', function (event) {
        if (event.key === 'Escape') {
            closeAllRowActionMenus();
            if ($('#modalOverlay').is(':visible')) { closeModal(); }
        }
    });
    $(document).on('mousedown', '#modalOverlay', function (e) {
        if (e.target === this) { closeModal(); }
    });

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
        var pageSize = parseInt(document.getElementById('storePageSize').value, 10) || 8;
        var currentPage = 1;
        var rows = Array.from(document.querySelectorAll('#storeTable tbody tr.store-row'));
        var totalRows = rows.length;

        function totalPages() {
            return Math.max(1, Math.ceil(totalRows / pageSize));
        }

        function renderPage() {
            var startIndex = (currentPage - 1) * pageSize;
            var endIndex = startIndex + pageSize;

            rows.forEach(function (row, i) {
                row.style.display = (i >= startIndex && i < endIndex) ? '' : 'none';
            });

            var infoEl = document.getElementById('storePageInfo');
            if (infoEl) {
                infoEl.textContent = totalRows === 0
                    ? 'Showing 0 entries'
                    : 'Showing ' + (startIndex + 1) + ' to ' + Math.min(endIndex, totalRows) + ' of ' + totalRows + ' entries';
            }

            renderPaginationControls();
        }

        // First, last and one either side of the current page, "..." for gaps
        function pageWindow(current, total) {
            var range = [];
            for (var i = Math.max(2, current - 1); i <= Math.min(total - 1, current + 1); i++) { range.push(i); }
            if (current - 1 > 2) { range.unshift('...'); }
            if (current + 1 < total - 1) { range.push('...'); }
            range.unshift(1);
            if (total > 1) { range.push(total); }
            return range;
        }

        function renderPaginationControls() {
            var pager = document.getElementById('storePagination');
            if (!pager) return;
            pager.innerHTML = '';
            if (totalRows === 0) return;

            var pages = totalPages();

            function addButton(label, page, cls, opts) {
                opts = opts || {};
                var btn = document.createElement('button');
                btn.type = 'button';
                btn.className = 'btn main-pagination-btn ' + cls;
                if (opts.html) { btn.innerHTML = label; } else { btn.textContent = label; }
                if (opts.label) { btn.setAttribute('aria-label', opts.label); }
                if (opts.disabled) {
                    btn.disabled = true;
                } else {
                    btn.addEventListener('click', function () {
                        currentPage = page;
                        renderPage();
                    });
                }
                pager.appendChild(btn);
            }

            addButton('<i class="fa fa-chevron-left"></i>', currentPage - 1, 'main-pagination-nav-btn',
                { html: true, label: 'Previous page', disabled: currentPage === 1 });

            pageWindow(currentPage, pages).forEach(function (p) {
                if (p === '...') {
                    var gap = document.createElement('span');
                    gap.className = 'main-pagination-ellipsis';
                    gap.textContent = '...';
                    pager.appendChild(gap);
                } else {
                    addButton(String(p), p, p === currentPage ? 'main-pagination-btn-active' : '');
                }
            });

            addButton('<i class="fa fa-chevron-right"></i>', currentPage + 1, 'main-pagination-nav-btn',
                { html: true, label: 'Next page', disabled: currentPage === pages });
        }

        document.getElementById('storePageSize').addEventListener('change', function () {
            pageSize = parseInt(this.value, 10) || 8;
            currentPage = 1;
            renderPage();
        });

        renderPage();
    }
