    function setupPasswordToggle(inputId, btnId, iconId) {
        var input = document.getElementById(inputId);
        var btn = document.getElementById(btnId);
        var icon = document.getElementById(iconId);
        if (!input || !btn || !icon) return;

        btn.addEventListener('click', function () {
            var isHidden = input.type === 'password';
            input.type = isHidden ? 'text' : 'password';
            icon.classList.toggle('bi-eye', !isHidden);
            icon.classList.toggle('bi-eye-slash', isHidden);
            btn.setAttribute('aria-label', isHidden ? 'Hide password' : 'Show password');
        });
    }

    setupPasswordToggle('currentPassword', 'toggleCurrentPwd', 'toggleCurrentPwdIcon');
    setupPasswordToggle('newPassword', 'toggleNewPwd', 'toggleNewPwdIcon');
    setupPasswordToggle('confirmPassword', 'toggleConfirmPwd', 'toggleConfirmPwdIcon');

    function setupCapsLockWarning(inputId, warningId) {
        var input = document.getElementById(inputId);
        var warning = document.getElementById(warningId);
        if (!input || !warning) return;

        var checkCapsLock = function (e) {
            if (typeof e.getModifierState !== 'function') return;
            warning.classList.toggle('show', e.getModifierState('CapsLock'));
        };

        input.addEventListener('keydown', checkCapsLock);
        input.addEventListener('keyup', checkCapsLock);
        input.addEventListener('blur', function () { warning.classList.remove('show'); });
    }

    setupCapsLockWarning('currentPassword', 'capsLockCurrent');
    setupCapsLockWarning('newPassword', 'capsLockNew');
    setupCapsLockWarning('confirmPassword', 'capsLockConfirm');

    function calcPasswordScore(pwd) {
        var score = 0;
        if (pwd.length >= 6) score++;
        if (pwd.length >= 10) score++;
        if (/[A-Z]/.test(pwd) && /[a-z]/.test(pwd)) score++;
        if (/[0-9]/.test(pwd)) score++;
        if (/[^A-Za-z0-9]/.test(pwd)) score++;
        return score;
    }

    $('#newPassword').on('input', function () {
        var pwd = $(this).val();
        var score = calcPasswordScore(pwd);
        var pct = (score / 5) * 100;
        var label = '';
        var color = '#dc3545';

        if (!pwd) {
            pct = 0;
        } else if (score >= 4) {
            label = 'Strong'; color = '#198754';
        } else if (score >= 3) {
            label = 'Medium'; color = '#fd7e14';
        } else {
            label = 'Weak'; color = '#dc3545';
        }

        $('#pwdStrengthFill').css({ width: pct + '%', 'background-color': color });
        $('#pwdStrengthLabel').text(label);
    });

    $(function () {
        $('#changePasswordBtn').on('click', function () {
            var currentPassword = $('#currentPassword').val();
            var newPassword = $('#newPassword').val();
            var confirmPassword = $('#confirmPassword').val();
            var alertBox = $('#changePasswordAlert');

            if (!currentPassword || !newPassword || !confirmPassword) {
                alertBox.removeClass('alert-success').addClass('alert-danger').text('All fields are required.').show();
                return;
            }

            $('#changePasswordBtn').prop('disabled', true);

            $.ajax({
                url: window.ACCOUNT_DATA.changePasswordUrl,
                type: 'POST',
                data: {
                    currentPassword: currentPassword,
                    newPassword: newPassword,
                    confirmPassword: confirmPassword,
                    __RequestVerificationToken: $('input[name="__RequestVerificationToken"]').val()
                },
                success: function (response) {
                    if (response.success) {
                        alertBox.removeClass('alert-danger').addClass('alert-success').text(response.message).show();
                        $('#currentPassword, #newPassword, #confirmPassword').val('');
                        $('#pwdStrengthFill').css('width', '0%');
                        $('#pwdStrengthLabel').text('');
                    } else {
                        alertBox.removeClass('alert-success').addClass('alert-danger').text(response.message).show();
                    }
                    $('#changePasswordBtn').prop('disabled', false);
                },
                error: function () {
                    alertBox.removeClass('alert-success').addClass('alert-danger').text('Server error. Please try again later.').show();
                    $('#changePasswordBtn').prop('disabled', false);
                }
            });
        });
    });
