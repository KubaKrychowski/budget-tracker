package com.wydatki.app.widget;

import android.content.Context;
import android.content.SharedPreferences;

import androidx.security.crypto.EncryptedSharedPreferences;
import androidx.security.crypto.MasterKey;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;

/**
 * Magazyn widzetu: tokeny w zaszyfrowanych preferencjach (klucz w Android Keystore), ustawienia poszczegolnych
 * widzetow i ostatnia odpowiedz API (do pokazania bez sieci) w zwyklych.
 *
 * Tokeny sa WLASNE widzetu (osobny klient OAuth) — nie dziela refresh tokena z aplikacja, bo OpenIddict obraca te tokeny
 * i drugie uzycie zuzytego uniewaznia autoryzacje, czyli wylogowuje uzytkownika z aplikacji.
 */
final class WidgetStore {
    private static final String TOKENS = "widget_tokens";
    private static final String SETTINGS = "widget_settings";

    private final SharedPreferences tokens;
    private final SharedPreferences settings;

    WidgetStore(Context context) {
        Context app = context.getApplicationContext();
        settings = app.getSharedPreferences(SETTINGS, Context.MODE_PRIVATE);
        try {
            MasterKey key = new MasterKey.Builder(app).setKeyScheme(MasterKey.KeyScheme.AES256_GCM).build();
            tokens = EncryptedSharedPreferences.create(
                app, TOKENS, key,
                EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
                EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM);
        } catch (Exception e) {
            throw new IllegalStateException("Nie mozna otworzyc zaszyfrowanego magazynu widzetu", e);
        }
    }

    String refreshToken() {
        return tokens.getString("refresh_token", null);
    }

    String accessToken() {
        return tokens.getString("access_token", null);
    }

    long accessTokenExpiresAt() {
        return tokens.getLong("expires_at", 0L);
    }

    boolean signedIn() {
        return refreshToken() != null;
    }

    /** Zapisuje tokeny. Gdy serwer nie przyslal nowego refresh tokena, zostaje dotychczasowy. */
    void saveTokens(String access, String refresh, long expiresAtMillis) {
        SharedPreferences.Editor editor =
            tokens.edit().putString("access_token", access).putLong("expires_at", expiresAtMillis);
        if (refresh != null) editor.putString("refresh_token", refresh);
        editor.apply();
    }

    void clearTokens() {
        tokens.edit().clear().apply();
    }

    void savePending(String state, String verifier, int appWidgetId) {
        settings.edit().putString("pending_" + state, verifier + "|" + appWidgetId).apply();
    }

    /** Zwraca {verifier, appWidgetId} i kasuje wpis (state jest jednorazowy) albo null. */
    String[] takePending(String state) {
        String value = settings.getString("pending_" + state, null);
        if (value == null) return null;
        settings.edit().remove("pending_" + state).apply();
        return value.split("\\|", 2);
    }

    void saveWidget(int id, String budgetId, String budgetName, List<String> limitIds) {
        settings.edit()
            .putString("w" + id + "_budget", budgetId)
            .putString("w" + id + "_name", budgetName)
            .putString("w" + id + "_limits", String.join(",", limitIds))
            .apply();
    }

    boolean hasWidget(int id) {
        return settings.getString("w" + id + "_budget", null) != null;
    }

    String budgetId(int id) {
        return settings.getString("w" + id + "_budget", null);
    }

    String budgetName(int id) {
        return settings.getString("w" + id + "_name", "");
    }

    List<String> limitIds(int id) {
        String value = settings.getString("w" + id + "_limits", "");
        return value.isEmpty() ? new ArrayList<>() : new ArrayList<>(Arrays.asList(value.split(",")));
    }

    void saveCache(int id, String json, long at) {
        settings.edit().putString("w" + id + "_cache", json).putLong("w" + id + "_cache_at", at).apply();
    }

    String cache(int id) {
        return settings.getString("w" + id + "_cache", null);
    }

    long cacheAt(int id) {
        return settings.getLong("w" + id + "_cache_at", 0L);
    }

    /** Po usunieciu widzetu z pulpitu. Gdy to byl ostatni, kasuje tez tokeny — sesja bez widzetu nie ma po co zyc. */
    void forget(int[] ids, boolean anyLeft) {
        SharedPreferences.Editor editor = settings.edit();
        for (int id : ids) {
            for (String suffix : new String[]{"_budget", "_name", "_limits", "_cache", "_cache_at"}) {
                editor.remove("w" + id + suffix);
            }
        }
        editor.apply();
        if (!anyLeft) clearTokens();
    }
}
