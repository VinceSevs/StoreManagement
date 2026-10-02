    function hideLayoutElements() {
        document.getElementById("navbarLLII")?.classList.add("visually-hidden");
        document.getElementById("footerLLII2")?.classList.add("visually-hidden");
    }

    document.addEventListener("DOMContentLoaded", () => {
        hideLayoutElements();

    });

    let capaItemCounter = 0;

    const ALLOWED_ATTACHMENT_TYPES = [
        'application/pdf',
        'application/msword',
        'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
        'application/vnd.ms-excel',
        'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'
    ];

    function isAllowedAttachment(file) {
        return file.type.startsWith('image/') || ALLOWED_ATTACHMENT_TYPES.includes(file.type);
    }

    function fileIconFor(fileName) {
        const ext = (fileName.split('.').pop() || '').toLowerCase();
        if (ext === 'pdf') return '📕';
        if (ext === 'doc' || ext === 'docx') return '📄';
        if (ext === 'xls' || ext === 'xlsx') return '📊';
        return '📎';
    }

    function readAsDataUrl(file) {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload = e => resolve(e.target.result);
            reader.onerror = reject;
            reader.readAsDataURL(file);
        });
    }

    function compressImageFile(file, maxDimension = 1600, quality = 0.82) {
        return readAsDataUrl(file).then(originalDataUrl => new Promise(resolve => {
            const img = new Image();
            img.onload = () => {
                let { width, height } = img;
                if (width <= maxDimension && height <= maxDimension) {
                    resolve(originalDataUrl);
                    return;
                }
                const scale = maxDimension / Math.max(width, height);
                width = Math.round(width * scale);
                height = Math.round(height * scale);
                const canvas = document.createElement('canvas');
                canvas.width = width;
                canvas.height = height;
                canvas.getContext('2d').drawImage(img, 0, 0, width, height);
                resolve(canvas.toDataURL('image/jpeg', quality));
            };
            img.onerror = () => resolve(originalDataUrl);
            img.src = originalDataUrl;
        }));
    }

    function initItemAttachments(block) {
        let attachments = [];
        let idSeq = 0;
        const fileInput = block.querySelector('.item-attach-input');
        const grid = block.querySelector('.item-attach-grid');
        const empty = block.querySelector('.item-attach-empty');

        function render() {
            empty.style.display = attachments.length ? 'none' : 'block';
            grid.innerHTML = attachments.map(a => `
                <div class="col">
                    <div class="card attach-thumb h-100">
                        <button type="button" class="btn-close" aria-label="Remove" data-remove-id="${a.id}"></button>
                        ${a.isImage
                            ? `<img src="${a.dataUrl}" class="card-img-top" alt="Attachment">`
                            : `<div class="card-img-top d-flex flex-column align-items-center justify-content-center bg-light" style="aspect-ratio:4/3;">
                                   <span style="font-size:2rem;line-height:1;">${fileIconFor(a.fileName)}</span>
                                   <small class="text-secondary text-truncate px-2" style="max-width:90%;">${escapeHtml(a.fileName)}</small>
                               </div>`}
                        <div class="card-body p-2">
                            <input type="text" class="form-control form-control-sm" placeholder="Caption (optional)" value="${a.caption.replace(/"/g, '&quot;')}" data-caption-id="${a.id}">
                        </div>
                    </div>
                </div>
            `).join('');

            grid.querySelectorAll('[data-remove-id]').forEach(btn => {
                btn.addEventListener('click', () => {
                    attachments = attachments.filter(a => a.id !== Number(btn.dataset.removeId));
                    render();
                });
            });
            grid.querySelectorAll('[data-caption-id]').forEach(input => {
                input.addEventListener('input', () => {
                    const found = attachments.find(a => a.id === Number(input.dataset.captionId));
                    if (found) found.caption = input.value;
                });
            });
        }

        fileInput.addEventListener('change', (event) => {
            const files = Array.from(event.target.files || []);
            files.forEach(file => {
                if (!isAllowedAttachment(file)) return;
                const isImage = file.type.startsWith('image/');
                const process = isImage ? compressImageFile(file) : readAsDataUrl(file);
                process.then(dataUrl => {
                    attachments.push({ id: ++idSeq, dataUrl, caption: '', fileName: file.name, isImage });
                    render();
                });
            });
            event.target.value = '';
        });

        render();
        block.getItemAttachments = () => attachments;
        block.resetAttachments = () => { attachments = []; render(); };
    }

    function initItemAutocomplete(block) {
        block._acMatches = [];
        block._acActiveIndex = -1;

        const itemSearch = block.querySelector('.item-search');
        const itemIdField = block.querySelector('[data-field="item_id"]');
        const itemDropdown = block.querySelector('.item-ac-dropdown');
        const itemCodeDisplay = block.querySelector('.item-code-display');

        function renderItemDropdown() {
            const query = itemSearch.value;
            if (!block._acMatches.length) {
                itemDropdown.innerHTML = query.trim() ? '<div class="list-group-item text-secondary fst-italic small">No catalog match.</div>' : '';
                itemDropdown.classList.toggle('open', !!query.trim());
                return;
            }
            itemDropdown.innerHTML = block._acMatches.map((it, i) => `
                <button type="button" class="list-group-item list-group-item-action d-flex justify-content-between align-items-center ${i === block._acActiveIndex ? 'active' : ''}" data-index="${i}">
                    <span>${highlightMatch(it.name, query)}</span>
                    <small class="font-mono">${escapeHtml(it.code || '')}</small>
                </button>
            `).join('');
            itemDropdown.classList.add('open');
            itemDropdown.querySelectorAll('[data-index]').forEach(btn => {
                btn.addEventListener('mousedown', () => selectItemMatch(Number(btn.dataset.index)));
            });
        }

        function selectItemMatch(i) {
            const it = block._acMatches[i];
            if (!it) return;
            itemSearch.value = it.name;
            itemIdField.value = it.id;
            itemCodeDisplay.value = it.code || '';
            closeItemDropdown();
        }

        function closeItemDropdown() {
            block._acMatches = [];
            block._acActiveIndex = -1;
            itemDropdown.classList.remove('open');
            itemDropdown.innerHTML = '';
        }

        itemSearch.addEventListener('input', () => {
            itemIdField.value = '';
            itemCodeDisplay.value = '';
            block._acMatches = filterCatalog(itemSearch.value);
            block._acActiveIndex = -1;
            renderItemDropdown();
        });
        itemSearch.addEventListener('focus', () => {
            block._acMatches = filterCatalog(itemSearch.value);
            block._acActiveIndex = -1;
            renderItemDropdown();
        });
        itemSearch.addEventListener('keydown', (e) => {
            if (!block._acMatches.length) return;
            if (e.key === 'ArrowDown') { e.preventDefault(); block._acActiveIndex = Math.min(block._acActiveIndex + 1, block._acMatches.length - 1); renderItemDropdown(); }
            else if (e.key === 'ArrowUp') { e.preventDefault(); block._acActiveIndex = Math.max(block._acActiveIndex - 1, 0); renderItemDropdown(); }
            else if (e.key === 'Enter') { if (block._acActiveIndex >= 0) { e.preventDefault(); selectItemMatch(block._acActiveIndex); } }
            else if (e.key === 'Escape') { closeItemDropdown(); }
        });

        document.addEventListener('click', (e) => {
            if (!itemSearch.closest('.autocomplete-wrap').contains(e.target)) closeItemDropdown();
        });
    }
    function populateDefectSelect(select) {
        if (!select) return;
        const current = select.value;
        select.innerHTML = '<option value="" disabled' + (current ? '' : ' selected') + '>Select defect&hellip;</option>' +
            DEFECT_CATALOG.map(d => `<option value="${d.id}" data-category="${escapeHtml(d.category)}">${escapeHtml(d.name)}</option>`).join('') +
            '<option value="other">Other (please specify)</option>';
        if (current) select.value = current;
    }

    function populateAllDefectSelects() {
        document.querySelectorAll('.defect-select').forEach(populateDefectSelect);
    }

    function initDefectSelect(block) {
        const select = block.querySelector('.defect-select');
        const categoryDisplay = block.querySelector('.defect-category-display');
        const otherInput = block.querySelector('.defect-other-input');
        if (!select) return;

        select.addEventListener('change', () => {
            const opt = select.selectedIndex >= 0 ? select.options[select.selectedIndex] : null;
            if (select.value === 'other') {
                categoryDisplay.value = 'Other';
                otherInput.classList.remove('d-none');
            } else {
                categoryDisplay.value = opt ? (opt.dataset.category || '') : '';
                otherInput.classList.add('d-none');
                otherInput.value = '';
            }
        });

        populateDefectSelect(select);
    }

    function addCapaItem() {
        capaItemCounter++;
        const template = document.getElementById('capaItemTemplate');
        const fragment = template.content.cloneNode(true);
        const block = fragment.querySelector('.capa-item-block');
        block.dataset.itemIndex = capaItemCounter;

        block.querySelector('.remove-item-btn').addEventListener('click', () => {
            block.remove();
        });

        document.getElementById('addItemBtn').parentElement.insertAdjacentElement('beforebegin', block);
        initItemAttachments(block);
        initItemAutocomplete(block);
        initVendorItemSelect(block);
        initDefectSelect(block);
        applyItemFieldsRequiredState(block);
    }

    let currentVendorItems = null;

    async function loadVendorItemsForSupplier(vendorId) {
        if (!vendorId) {
            resetVendorItemRestriction();
            return;
        }
        try {
            const res = await fetch(window.CAPA_CONFIG.vendorItemListUrl + '?vendorId=' + encodeURIComponent(vendorId));
            currentVendorItems = res.ok ? await res.json() : [];
        } catch (err) {
            console.error('Could not load vendor items', err);
            currentVendorItems = [];
        }
        applyItemModeToAllBlocks();
    }

    function resetVendorItemRestriction() {
        if (currentVendorItems === null) return;
        currentVendorItems = null;
        applyItemModeToAllBlocks();
    }

    function handleSupplierChange() {
        const supplierSelect = document.getElementById('carSupplierSelect');
        loadVendorItemsForSupplier(supplierSelect.value);
    }

    function applyItemModeToAllBlocks() {
        document.querySelectorAll('.capa-item-block').forEach(applyItemModeToBlock);
    }

    function applyItemModeToBlock(block) {
        const searchWrap = block.querySelector('.item-search-wrap');
        const vendorSelect = block.querySelector('.item-vendor-select');
        const itemIdField = block.querySelector('[data-field="item_id"]');
        const itemCodeDisplay = block.querySelector('.item-code-display');
        if (!searchWrap || !vendorSelect) return;

        if (currentVendorItems) {
            searchWrap.classList.add('d-none');
            vendorSelect.classList.remove('d-none');

            const current = vendorSelect.value;
            vendorSelect.innerHTML = '<option value="" disabled selected>Select item&hellip;</option>' +
                currentVendorItems.map(it => `<option value="${it.id}" data-code="${escapeHtml(it.code || '')}">${escapeHtml(it.name)}</option>`).join('');
            if (current) vendorSelect.value = current;
        } else {
            searchWrap.classList.remove('d-none');
            vendorSelect.classList.add('d-none');
            vendorSelect.value = '';
            itemIdField.value = '';
            itemCodeDisplay.value = '';
        }
    }

    function initVendorItemSelect(block) {
        const vendorSelect = block.querySelector('.item-vendor-select');
        const itemIdField = block.querySelector('[data-field="item_id"]');
        const itemCodeDisplay = block.querySelector('.item-code-display');
        if (!vendorSelect) return;

        vendorSelect.addEventListener('change', () => {
            const opt = vendorSelect.selectedIndex >= 0 ? vendorSelect.options[vendorSelect.selectedIndex] : null;
            itemIdField.value = vendorSelect.value || '';
            itemCodeDisplay.value = opt ? (opt.dataset.code || '') : '';
        });

        applyItemModeToBlock(block);
    }

    document.addEventListener('DOMContentLoaded', () => {
        const block0 = document.getElementById('capaItemBlock0');
        initItemAttachments(block0);
        initItemAutocomplete(block0);
        initVendorItemSelect(block0);
        initDefectSelect(block0);
    });
