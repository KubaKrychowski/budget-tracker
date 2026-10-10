package com.wydatki.app.widget;

import android.content.Context;
import android.content.Intent;
import android.net.Uri;
import android.util.Base64;

import org.json.JSONException;
import org.json.JSONObject;

import java.io.IOException;
import java.io.UnsupportedEncodingException;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.security.SecureRandom;
import java.util.LinkedHashMap;
import java.util.Map;

/**
 * Logowanie widzetu: kod autoryzacyjny + PKCE w systemowej przegladarce (RFC 8252) i odswiezanie tokenu.
 */
final class WidgetAuth {
    /** Margines, w ktorym access token jest jeszcze uznawany za wazny — zeby nie wygasl w locie. */
    private static final long EXPIRY_MARGIN_MS = 60_000L;

    /** Jedno odswiezanie naraz: refresh token jest obracany, wiec dwa rownolegle uzycia unieważnilyby sesje. */
    private static final Object REFRESH_LOCK = new Object();

    private WidgetAuth() {
    }

    /** Intent otwierajacy logowanie w przegladarce; zapamietuje verifier pod `state`. */
    static Intent authorizationIntent(Context context, int appWidgetId) {
        String verifier = randomString(32);
        String state = randomString(16);
        new WidgetStore(context).savePending(state, verifier, appWidgetId);

        Uri uri = Uri.parse(WidgetConfig.IDENTITY_URL + "/connect/authorize").buildUpon()
            .appendQueryParameter("client_id", WidgetConfig.CLIENT_ID)
            .appendQueryParameter("redirect_uri", WidgetConfig.REDIRECT_URI)
            .appendQueryParameter("response_type", "code")
            .appendQueryParameter("scope", WidgetConfig.SCOPE)
            .appendQueryParameter("state", state)
            .appendQueryParameter("code_challenge", challenge(verifier))
            .appendQueryParameter("code_challenge_method", "S256")
            .build();

        return new Intent(Intent.ACTION_VIEW, uri);
    }

    /**
     * Konczy logowanie z adresu powrotu. Zwraca id widzetu, dla ktorego sie logowano, albo -1, gdy adres nie pasuje do
     * zadnego rozpoczetego logowania (nieznany `state`) — wtedy NIC nie wymieniamy.
     */
    static int completeAuthorization(Context context, Uri callback) throws IOException {
        String state = callback.getQueryParameter("state");
        if (state == null) return -1;

        WidgetStore store = new WidgetStore(context);
        String[] pending = store.takePending(state);
        if (pending == null) return -1;

        String code = callback.getQueryParameter("code");
        if (code == null) {
            throw new AuthRequiredException(callback.getQueryParameter("error"));
        }

        Map<String, String> form = new LinkedHashMap<>();
        form.put("grant_type", "authorization_code");
        form.put("code", code);
        form.put("redirect_uri", WidgetConfig.REDIRECT_URI);
        form.put("client_id", WidgetConfig.CLIENT_ID);
        form.put("code_verifier", pending[0]);

        saveTokenResponse(store, WidgetHttp.postForm(WidgetConfig.IDENTITY_URL + "/connect/token", form));
        return Integer.parseInt(pending[1]);
    }

    /** Wazny access token; odswieza go, gdy wygasl. Rzuca AuthRequiredException, gdy sesja widzetu przestala istniec. */
    static String accessToken(Context context) throws IOException {
        WidgetStore store = new WidgetStore(context);

        synchronized (REFRESH_LOCK) {
            if (!store.signedIn()) throw new AuthRequiredException("Brak sesji widzetu");

            String access = store.accessToken();
            if (access != null && store.accessTokenExpiresAt() - EXPIRY_MARGIN_MS > System.currentTimeMillis()) {
                return access;
            }

            Map<String, String> form = new LinkedHashMap<>();
            form.put("grant_type", "refresh_token");
            form.put("refresh_token", store.refreshToken());
            form.put("client_id", WidgetConfig.CLIENT_ID);

            WidgetHttp.Response response = WidgetHttp.postForm(WidgetConfig.IDENTITY_URL + "/connect/token", form);
            if (response.status == 400 || response.status == 401) {
                store.clearTokens();
                throw new AuthRequiredException("Sesja widzetu wygasla");
            }

            saveTokenResponse(store, response);
            return store.accessToken();
        }
    }

    private static void saveTokenResponse(WidgetStore store, WidgetHttp.Response response) throws IOException {
        if (response.status != 200) throw new IOException("Serwer tozsamosci odpowiedzial " + response.status);

        try {
            JSONObject json = new JSONObject(response.body);
            long expiresInSeconds = json.optLong("expires_in", 900L);
            store.saveTokens(
                json.getString("access_token"),
                json.has("refresh_token") ? json.getString("refresh_token") : null,
                System.currentTimeMillis() + expiresInSeconds * 1000L);
        } catch (JSONException e) {
            throw new IOException("Nieczytelna odpowiedz serwera tozsamosci", e);
        }
    }

    private static String randomString(int bytes) {
        byte[] buffer = new byte[bytes];
        new SecureRandom().nextBytes(buffer);
        return base64Url(buffer);
    }

    private static String challenge(String verifier) {
        try {
            return base64Url(MessageDigest.getInstance("SHA-256").digest(verifier.getBytes("US-ASCII")));
        } catch (NoSuchAlgorithmException | UnsupportedEncodingException e) {
            throw new IllegalStateException(e);
        }
    }

    private static String base64Url(byte[] bytes) {
        return Base64.encodeToString(bytes, Base64.URL_SAFE | Base64.NO_PADDING | Base64.NO_WRAP);
    }
}
