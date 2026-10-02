// Przycisk "pokaż hasło" przy każdym polu hasła na ekranach Identity.
// Osobny plik, a nie skrypt w widoku: CSP ma `script-src 'self'` bez `unsafe-inline`.
// Bez JavaScriptu pole zostaje zwykłym polem hasła — przycisk jest dodatkiem, nie warunkiem logowania.
(function () {
  'use strict';

  var script = document.currentScript;
  var showLabel = (script && script.dataset.showLabel) || 'Show password';
  var hideLabel = (script && script.dataset.hideLabel) || 'Hide password';

  var EYE =
    '<svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" stroke-width="1.8" ' +
    'stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false">' +
    '<path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/>' +
    '<path class="auth-password__slash" d="M3 3l18 18"/></svg>';

  function attach(input) {
    var wrapper = document.createElement('div');
    wrapper.className = 'auth-password';
    input.parentNode.insertBefore(wrapper, input);
    wrapper.appendChild(input);

    var button = document.createElement('button');
    button.type = 'button';
    button.className = 'auth-password__toggle';
    button.innerHTML = EYE;
    wrapper.appendChild(button);

    function render(visible) {
      var label = visible ? hideLabel : showLabel;
      button.setAttribute('aria-label', label);
      button.setAttribute('title', label);
      button.setAttribute('aria-pressed', visible ? 'true' : 'false');
      button.classList.toggle('is-visible', visible);
    }

    button.addEventListener('click', function () {
      var visible = input.type === 'password';
      input.type = visible ? 'text' : 'password';
      render(visible);
      input.focus();
    });

    render(false);
  }

  document.querySelectorAll('input[type="password"]').forEach(attach);
})();
