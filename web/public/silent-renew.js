// Ta strona ładuje się WYŁĄCZNIE w ukrytej ramce (silent renew), nigdy w widocznej karcie —
// Identity przekierowuje tu z odpowiedzią po odnowieniu sesji, a angular-auth-oidc-client
// nasłuchuje na tym evencie na oknie NADRZĘDNYM (ta sama origin, więc dostęp jest legalny).
//
// Osobny plik, nie skrypt inline: nagłówki tej strony (staticwebapp.config.json) pozwalają na `script-src 'self'`,
// a hash skryptu inline zależałby od końców linii w checkoucie, więc łamałby się po cichu.
window.parent.dispatchEvent(new CustomEvent('oidc-silent-renew-message', { detail: window.location.href }));
