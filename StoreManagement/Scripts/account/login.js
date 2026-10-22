    (function () {
        var toggleBtn = document.getElementById('togglePasswordBtn');
        var toggleIcon = document.getElementById('togglePasswordIcon');
        var passwordInput = document.getElementById('passwordInput');
        var capsLockWarning = document.getElementById('capsLockWarning');

        if (passwordInput && capsLockWarning) {
            var checkCapsLock = function (e) {
                if (typeof e.getModifierState !== 'function') return;
                var isOn = e.getModifierState('CapsLock');
                capsLockWarning.classList.toggle('show', isOn);
            };
            passwordInput.addEventListener('keydown', checkCapsLock);
            passwordInput.addEventListener('keyup', checkCapsLock);
            passwordInput.addEventListener('blur', function () {
                capsLockWarning.classList.remove('show');
            });
        }

        if (toggleBtn && passwordInput) {
            toggleBtn.addEventListener('click', function () {
                var isHidden = passwordInput.type === 'password';
                passwordInput.type = isHidden ? 'text' : 'password';
                toggleIcon.classList.toggle('bi-eye', !isHidden);
                toggleIcon.classList.toggle('bi-eye-slash', isHidden);
                toggleBtn.setAttribute('aria-label', isHidden ? 'Hide password' : 'Show password');
            });
        }

        var form = document.getElementById('loginForm');
        var submitBtn = document.getElementById('loginSubmitBtn');
        var btnLabel = document.getElementById('loginBtnLabel');
        var serverErrorBox = document.getElementById('loginServerError');
        var serverErrorText = document.getElementById('loginServerErrorText');
        var formBox = document.getElementById('loginFormBox');
        var overlay = document.getElementById('loginSuccessOverlay');

        if (!form) return;

        form.addEventListener('submit', function (e) {
            e.preventDefault();

            submitBtn.disabled = true;
            btnLabel.textContent = 'Signing in...';
            serverErrorBox.style.display = 'none';

            fetch(form.action, {
                method: 'POST',
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                body: new FormData(form)
            })
                .then(function (response) {
                    return response.json().catch(function () { return null; }).then(function (data) {
                        return { ok: response.ok, data: data };
                    });
                })
                .then(function (result) {
                    if (result.ok && result.data && result.data.success) {
                        document.querySelector('.auth-wrapper').classList.add('is-leaving');
                        overlay.classList.add('show');
                        setTimeout(function () {
                            window.location.href = result.data.redirectUrl || '/';
                        }, 1400);
                        return;
                    }

                    var message = (result.data && result.data.message) || 'Invalid username or password.';
                    serverErrorText.textContent = message;
                    serverErrorBox.style.display = 'flex';

                    formBox.classList.remove('shake');
                    void formBox.offsetWidth;
                    formBox.classList.add('shake');

                    submitBtn.disabled = false;
                    btnLabel.textContent = 'Sign In';
                })
                .catch(function () {
                    serverErrorText.textContent = 'Network error - please try again.';
                    serverErrorBox.style.display = 'flex';
                    submitBtn.disabled = false;
                    btnLabel.textContent = 'Sign In';
                });
        });
    })();
