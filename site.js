// Auto-submit event selectors and confirm destructive actions.
document.querySelectorAll('[data-autosubmit]').forEach(function (el) {
  el.addEventListener('change', function () { el.closest('form').submit(); });
});
document.querySelectorAll('form[data-confirm]').forEach(function (form) {
  form.addEventListener('submit', function (e) {
    if (!window.confirm(form.getAttribute('data-confirm'))) e.preventDefault();
  });
});
// Show the chosen image before upload.
document.querySelectorAll('input[type=file][data-preview]').forEach(function (input) {
  input.addEventListener('change', function () {
    var target = document.getElementById(input.getAttribute('data-preview'));
    if (!target || !input.files || !input.files[0]) return;
    target.src = URL.createObjectURL(input.files[0]);
    target.classList.remove('d-none');
  });
});
