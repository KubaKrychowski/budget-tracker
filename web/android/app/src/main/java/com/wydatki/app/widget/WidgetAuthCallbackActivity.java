package com.wydatki.app.widget;

import android.app.Activity;
import android.appwidget.AppWidgetManager;
import android.content.Context;
import android.net.Uri;
import android.os.Bundle;
import android.widget.Toast;

import com.wydatki.app.R;

import java.io.IOException;

/**
 * Odbiera powrot z logowania widzetu (com.wydatki.app.widget:/callback), wymienia kod na tokeny i konczy sie —
 * ekran konfiguracji, ktory czekal pod spodem, sam zauwazy zapisana sesje w onResume.
 *
 * Osobna aktywnosc, a nie MainActivity: ten sam schemat co aplikacja trafilby do Capacitora i Angulara.
 */
public class WidgetAuthCallbackActivity extends Activity {
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        Uri callback = getIntent().getData();
        Context app = getApplicationContext();
        if (callback == null) {
            finish();
            return;
        }

        new Thread(() -> {
            String failure = null;
            int widgetId = -1;
            try {
                widgetId = WidgetAuth.completeAuthorization(app, callback);
                if (widgetId == -1) failure = app.getString(R.string.setup_error_auth);
            } catch (IOException e) {
                failure = app.getString(R.string.setup_error_auth);
            }

            String message = failure;
            runOnUiThread(() -> {
                if (message != null) Toast.makeText(app, message, Toast.LENGTH_LONG).show();
                finish();
            });
        }).start();
    }
}
