/* Photo lightbox for pages that show attachments (same behavior as the CAR pages).
   The page defines window.ATTACHMENT_PHOTOS = [{ url, caption, fileName }, ...]
   and renders the Views/Shared/_PartialView/_AttachmentLightbox.cshtml markup. */
(function () {
    var current = 0;

    function photos() { return window.ATTACHMENT_PHOTOS || []; }
    function el(id) { return document.getElementById(id); }

    function render() {
        var list = photos();
        var photo = list[current];
        if (!photo) return;

        el('lightboxImage').src = photo.url;
        el('lightboxDownload').href = photo.url;
        el('lightboxDownload').setAttribute('download', photo.fileName || 'attachment');
        el('lightboxCaption').textContent = photo.caption || photo.fileName || '';
        el('lightboxCounter').textContent = list.length > 1 ? (current + 1) + ' / ' + list.length : '';
        el('lightboxPrev').style.display = list.length > 1 ? 'flex' : 'none';
        el('lightboxNext').style.display = list.length > 1 ? 'flex' : 'none';
    }

    window.openLightbox = function (index) {
        current = index;
        window.currentLightboxIndex = index;
        render();
        el('lightboxOverlay').classList.add('active');
        document.body.style.overflow = 'hidden';
    };

    window.closeLightbox = function () {
        el('lightboxOverlay').classList.remove('active');
        document.body.style.overflow = '';
    };

    window.handleOverlayClick = function (e) {
        if (e.target.id === 'lightboxOverlay') window.closeLightbox();
    };

    window.showLightboxPhoto = function (index) {
        if (index < 0 || index >= photos().length) return;
        current = index;
        window.currentLightboxIndex = index;
        render();
    };

    window.currentLightboxIndex = 0;

    document.addEventListener('keydown', function (e) {
        var overlay = el('lightboxOverlay');
        if (!overlay || !overlay.classList.contains('active')) return;
        if (e.key === 'Escape') window.closeLightbox();
        else if (e.key === 'ArrowLeft') window.showLightboxPhoto(current - 1);
        else if (e.key === 'ArrowRight') window.showLightboxPhoto(current + 1);
    });
})();
