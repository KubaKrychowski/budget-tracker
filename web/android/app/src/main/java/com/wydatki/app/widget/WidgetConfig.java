package com.wydatki.app.widget;

/**
 * Stale widzetu limitow na pulpicie.
 *
 * Adresy musza sie zgadzac z web/mobile/config.json (produkcja). Schemat powrotu i identyfikator klienta musza sie
 * zgadzac z OAuthDefaults.WidgetScheme i OAuthDefaults.WidgetClientId w BudgetTracker.Identity ("budgettracker-widget"):
 * OpenIddict porownuje redirect_uri DOSLOWNIE, a pilnuje tego WidgetClientTests po stronie Identity.
 */
final class WidgetConfig {
    static final String IDENTITY_URL = "https://auth.wydatki.com";
    static final String API_URL = "https://api.wydatki.com";

    static final String CLIENT_ID = "budgettracker-widget";
    static final String SCHEME = "com.wydatki.app.widget";
    static final String REDIRECT_URI = SCHEME + ":/callback";
    static final String SCOPE = "openid offline_access budgettracker_api";

    /** Tyle limitow miesci sie w widzecie. */
    static final int MAX_LIMITS = 4;

    private WidgetConfig() {
    }
}
