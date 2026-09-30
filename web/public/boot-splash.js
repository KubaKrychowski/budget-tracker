// Zapala okładkę z index.html tylko wtedy, gdy trwa logowanie: powrót z Identity (/auth-callback) albo brak sesji
// w magazynie biblioteki OIDC. Odświeżenie strony z ważną sesją okładki nie pokazuje.
// Osobny plik, nie skrypt w HTML: polityka CSP nie ma zezwolenia na skrypty inline.
// Błąd odczytu magazynu = pokaż okładkę: Angular i tak ją zdejmie, gdy okaże się zbędna.
(function () {
  var show = true;
  try {
    var callback = window.location.pathname.indexOf('/auth-callback') === 0;
    var hasSession = false;
    [window.sessionStorage, window.localStorage].forEach(function (storage) {
      for (var i = 0; i < storage.length; i++) {
        var key = storage.key(i);
        var value = storage.getItem(key) || '';
        // Pusty wynik po wylogowaniu (`null`) to brak sesji — liczy się tylko zapisany obiekt.
        var stored = key.indexOf('authnResult') !== -1 && value.charAt(0) === '{';
        if (stored || /"authnResult"\s*:\s*\{/.test(value)) hasSession = true;
      }
    });
    show = callback || !hasSession;
  } catch (e) {
    show = true;
  }
  if (show) document.documentElement.classList.add('boot-splash-on');
})();
