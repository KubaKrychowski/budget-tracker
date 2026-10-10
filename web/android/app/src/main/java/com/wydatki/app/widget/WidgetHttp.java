package com.wydatki.app.widget;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;
import java.util.Map;

/** Najprostszy klient HTTP widzetu — dwa wywolania, wiec bez dodatkowych bibliotek. */
final class WidgetHttp {
    static final class Response {
        final int status;
        final String body;

        Response(int status, String body) {
            this.status = status;
            this.body = body;
        }
    }

    private static final int TIMEOUT_MS = 15_000;

    private WidgetHttp() {
    }

    static Response get(String url, String bearer) throws IOException {
        HttpURLConnection connection = (HttpURLConnection) new URL(url).openConnection();
        try {
            connection.setConnectTimeout(TIMEOUT_MS);
            connection.setReadTimeout(TIMEOUT_MS);
            connection.setRequestProperty("Accept", "application/json");
            connection.setRequestProperty("Accept-Language", "pl");
            connection.setRequestProperty("Authorization", "Bearer " + bearer);
            return read(connection);
        } finally {
            connection.disconnect();
        }
    }

    static Response postForm(String url, Map<String, String> fields) throws IOException {
        StringBuilder form = new StringBuilder();
        for (Map.Entry<String, String> field : fields.entrySet()) {
            if (form.length() > 0) form.append('&');
            form.append(URLEncoder.encode(field.getKey(), "UTF-8")).append('=')
                .append(URLEncoder.encode(field.getValue(), "UTF-8"));
        }

        HttpURLConnection connection = (HttpURLConnection) new URL(url).openConnection();
        try {
            connection.setConnectTimeout(TIMEOUT_MS);
            connection.setReadTimeout(TIMEOUT_MS);
            connection.setRequestMethod("POST");
            connection.setDoOutput(true);
            connection.setRequestProperty("Content-Type", "application/x-www-form-urlencoded");
            connection.setRequestProperty("Accept", "application/json");
            try (OutputStream out = connection.getOutputStream()) {
                out.write(form.toString().getBytes(StandardCharsets.UTF_8));
            }
            return read(connection);
        } finally {
            connection.disconnect();
        }
    }

    private static Response read(HttpURLConnection connection) throws IOException {
        int status = connection.getResponseCode();
        InputStream stream = status >= 400 ? connection.getErrorStream() : connection.getInputStream();
        if (stream == null) return new Response(status, "");

        try (InputStream in = stream; ByteArrayOutputStream buffer = new ByteArrayOutputStream()) {
            byte[] chunk = new byte[4096];
            int read;
            while ((read = in.read(chunk)) != -1) buffer.write(chunk, 0, read);
            return new Response(status, buffer.toString("UTF-8"));
        }
    }
}
