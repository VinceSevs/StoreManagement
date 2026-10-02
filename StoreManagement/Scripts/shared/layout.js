            (function () {
                var sidebar = document.getElementById('appSidebar');
                var overlay = document.getElementById('sidebarOverlay');
                var toggleBtn = document.getElementById('sidebarToggleBtn');

                function openSidebar() {
                    sidebar.classList.add('open');
                    overlay.classList.add('show');
                }
                function closeSidebar() {
                    sidebar.classList.remove('open');
                    overlay.classList.remove('show');
                }

                if (toggleBtn) {
                    toggleBtn.addEventListener('click', function () {
                        sidebar.classList.contains('open') ? closeSidebar() : openSidebar();
                    });
                }
                if (overlay) {
                    overlay.addEventListener('click', closeSidebar);
                }
            })();

            (function () {
                var menu = document.getElementById('appUserMenu');
                var btn = document.getElementById('appUserAvatarBtn');
                var dropdown = document.getElementById('appUserDropdown');
                if (!menu || !btn || !dropdown) return;

                function closeMenu() {
                    dropdown.classList.remove('show');
                    btn.setAttribute('aria-expanded', 'false');
                }
                function toggleMenu() {
                    var isOpen = dropdown.classList.toggle('show');
                    btn.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
                }

                btn.addEventListener('click', function (e) {
                    e.stopPropagation();
                    toggleMenu();
                });
                document.addEventListener('click', function (e) {
                    if (!menu.contains(e.target)) closeMenu();
                });
                document.addEventListener('keydown', function (e) {
                    if (e.key === 'Escape') closeMenu();
                });
            })();

            function toggleCarSubmenu(event) {
                event.preventDefault();
                var toggle = document.getElementById('carSubmenuToggle');
                var submenu = document.getElementById('carSubmenu');
                var isOpen = submenu.classList.toggle('open');
                toggle.classList.toggle('collapsed', !isOpen);
                toggle.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
            }

            function toggleNncrSubmenu(event) {
                event.preventDefault();
                var toggle = document.getElementById('nncrSubmenuToggle');
                var submenu = document.getElementById('nncrSubmenu');
                var isOpen = submenu.classList.toggle('open');
                toggle.classList.toggle('collapsed', !isOpen);
                toggle.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
            }

            function toggleComplaintSubmenu(event) {
                event.preventDefault();
                var toggle = document.getElementById('complaintSubmenuToggle');
                var submenu = document.getElementById('complaintSubmenu');
                var isOpen = submenu.classList.toggle('open');
                toggle.classList.toggle('collapsed', !isOpen);
                toggle.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
            }
