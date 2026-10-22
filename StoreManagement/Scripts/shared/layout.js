/* App shell behaviour (sidebar collapse / drawer, submenus, user menu).
   Based on LEADS Scripts/sidebar.js, written without jQuery. */
(function () {
    var appWrapper = document.querySelector('.app-wrapper');
    var sidebar = document.getElementById('mainSidebar');
    var overlay = document.querySelector('.sidebar-overlay');
    var toggle = document.getElementById('sidebarToggle');
    if (!appWrapper || !sidebar || !toggle) return;

    var COLLAPSED_KEY = 'leads.sidebarCollapsed';

    function isDesktop() { return window.innerWidth > 767; }
    function isCollapsed() { return appWrapper.classList.contains('sidebar-collapsed'); }

    function readCollapsed() {
        try {
            // Expanded unless the user collapsed it
            return localStorage.getItem(COLLAPSED_KEY) === 'true';
        } catch (e) { return false; }
    }

    function setCollapsed(collapsed) {
        appWrapper.classList.toggle('sidebar-collapsed', collapsed);
        toggle.classList.toggle('active', collapsed);
        toggle.setAttribute('aria-expanded', collapsed ? 'false' : 'true');
        try { localStorage.setItem(COLLAPSED_KEY, collapsed ? 'true' : 'false'); } catch (e) { }
    }

    function openDrawer() {
        sidebar.classList.add('sidebar-open');
        if (overlay) overlay.classList.add('active');
    }

    function closeDrawer() {
        sidebar.classList.remove('sidebar-open');
        if (overlay) overlay.classList.remove('active');
    }

    function setGroupOpen(group, open) {
        var caret = group.querySelector('.sidebar-caret');
        group.classList.toggle('open', open);
        if (caret) caret.setAttribute('aria-expanded', open ? 'true' : 'false');
    }

    // Initial state
    if (isDesktop()) {
        toggle.classList.toggle('active', isCollapsed());
    } else {
        // The drawer is always full width on phones; the rail only exists on desktop
        appWrapper.classList.remove('sidebar-collapsed');
        toggle.classList.remove('active');
    }
    toggle.setAttribute('aria-expanded', !isDesktop() || !isCollapsed() ? 'true' : 'false');

    // Hover labels for the icon-only rail
    Array.prototype.forEach.call(sidebar.querySelectorAll('.sidebar-link, .sidebar-parent-link'), function (el) {
        var label = el.querySelector('.sidebar-label');
        if (label && !el.getAttribute('title')) el.setAttribute('title', label.textContent.trim());
    });

    toggle.addEventListener('click', function () {
        if (!isDesktop()) {
            // Mobile: slide the full sidebar in/out
            sidebar.classList.contains('sidebar-open') ? closeDrawer() : openDrawer();
        } else {
            // Desktop: collapse to an icon-only rail (remembered across pages)
            setCollapsed(!isCollapsed());
        }
    });

    if (overlay) overlay.addEventListener('click', closeDrawer);

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') closeDrawer();
    });

    // Close the drawer after choosing a menu item on mobile
    Array.prototype.forEach.call(
        sidebar.querySelectorAll('.sidebar-link:not(.sidebar-parent), .sidebar-sublink'),
        function (el) {
            el.addEventListener('click', function () { if (!isDesktop()) closeDrawer(); });
        });

    // A parent row expands / collapses its submenu first; the child links navigate.
    // On the icon-only rail the submenus are hidden, so the sidebar expands and
    // the group opens.
    Array.prototype.forEach.call(sidebar.querySelectorAll('.sidebar-parent-link'), function (link) {
        link.addEventListener('click', function (e) {
            e.preventDefault();
            var group = link.closest('.sidebar-group');
            if (isDesktop() && isCollapsed()) {
                setCollapsed(false);
                setGroupOpen(group, true);
            } else {
                setGroupOpen(group, !group.classList.contains('open'));
            }
        });
    });

    // The caret toggles a submenu; same as the parent label
    Array.prototype.forEach.call(sidebar.querySelectorAll('.sidebar-caret'), function (caret) {
        caret.addEventListener('click', function (e) {
            e.preventDefault();
            e.stopPropagation();
            var group = caret.closest('.sidebar-group');
            setGroupOpen(group, !group.classList.contains('open'));
        });
    });

    window.addEventListener('resize', function () {
        if (isDesktop()) {
            closeDrawer();
            // Back on desktop: restore the user's saved choice
            var collapsed = readCollapsed();
            appWrapper.classList.toggle('sidebar-collapsed', collapsed);
            toggle.classList.toggle('active', collapsed);
        } else {
            // Mobile uses the slide-in drawer; don't overwrite the saved desktop choice
            appWrapper.classList.remove('sidebar-collapsed');
            toggle.classList.remove('active');
        }
    });

    // User profile dropdown
    var userProfile = document.getElementById('userProfileToggle');
    if (userProfile) {
        userProfile.addEventListener('click', function (e) {
            if (e.target.closest('.user-dropdown-menu')) return; // let menu items work
            e.stopPropagation();
            userProfile.classList.toggle('open');
        });
        document.addEventListener('click', function (e) {
            if (!e.target.closest('#userProfileToggle')) userProfile.classList.remove('open');
        });
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') userProfile.classList.remove('open');
        });
    }
})();
