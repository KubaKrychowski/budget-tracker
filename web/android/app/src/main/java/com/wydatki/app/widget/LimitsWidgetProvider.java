package com.wydatki.app.widget;

import android.app.PendingIntent;
import android.appwidget.AppWidgetManager;
import android.appwidget.AppWidgetProvider;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.view.View;
import android.widget.RemoteViews;

import com.wydatki.app.R;

import java.io.IOException;
import java.text.DateFormat;
import java.util.ArrayList;
import java.util.Date;
import java.util.List;

/**
 * Widzet limitow na pulpicie: do czterech wybranych limitow jednego budzetu z paskiem wykorzystania i kwota, ktora
 * zostala. Odswiezany co 30 minut (minimum, na jakie pozwala Android) i po dodaniu.
 *
 * Wywolania sieciowe ida w tle (goAsync), bo onUpdate dziala na watku glownym.
 */
public class LimitsWidgetProvider extends AppWidgetProvider {
    private static final int[] ROWS = {R.id.row_1, R.id.row_2, R.id.row_3, R.id.row_4};
    private static final int[] NAMES = {R.id.name_1, R.id.name_2, R.id.name_3, R.id.name_4};
    private static final int[] AMOUNTS = {R.id.amount_1, R.id.amount_2, R.id.amount_3, R.id.amount_4};
    private static final int[] BARS_OK = {R.id.bar_ok_1, R.id.bar_ok_2, R.id.bar_ok_3, R.id.bar_ok_4};
    private static final int[] BARS_WARN = {R.id.bar_warn_1, R.id.bar_warn_2, R.id.bar_warn_3, R.id.bar_warn_4};
    private static final int[] BARS_OVER = {R.id.bar_over_1, R.id.bar_over_2, R.id.bar_over_3, R.id.bar_over_4};

    @Override
    public void onUpdate(Context context, AppWidgetManager manager, int[] appWidgetIds) {
        PendingResult pending = goAsync();
        new Thread(() -> {
            try {
                for (int id : appWidgetIds) updateWidget(context, manager, id);
            } finally {
                pending.finish();
            }
        }).start();
    }

    @Override
    public void onDeleted(Context context, int[] appWidgetIds) {
        AppWidgetManager manager = AppWidgetManager.getInstance(context);
        boolean anyLeft = manager.getAppWidgetIds(new ComponentName(context, LimitsWidgetProvider.class)).length > 0;
        new WidgetStore(context).forget(appWidgetIds, anyLeft);
    }

    /** Odswieza jeden widzet: pobiera dane (albo bierze ostatnie zapamietane) i rysuje. Wolac poza watkiem glownym. */
    static void updateWidget(Context context, AppWidgetManager manager, int id) {
        WidgetStore store = new WidgetStore(context);
        RemoteViews views = new RemoteViews(context.getPackageName(), R.layout.widget_limits);
        views.setOnClickPendingIntent(R.id.root, openAppIntent(context));

        if (!store.hasWidget(id)) {
            showMessage(views, context.getString(R.string.widget_not_configured));
            views.setOnClickPendingIntent(R.id.root, setupIntent(context, id));
            manager.updateAppWidget(id, views);
            return;
        }

        String json;
        long at = System.currentTimeMillis();
        boolean offline = false;

        try {
            json = WidgetApi.fetchRaw(WidgetAuth.accessToken(context), store.budgetId(id));
            store.saveCache(id, json, at);
        } catch (AuthRequiredException e) {
            showMessage(views, context.getString(R.string.widget_sign_in_again));
            views.setOnClickPendingIntent(R.id.root, setupIntent(context, id));
            manager.updateAppWidget(id, views);
            return;
        } catch (IOException e) {
            json = store.cache(id);
            at = store.cacheAt(id);
            offline = true;
            if (json == null) {
                showMessage(views, context.getString(R.string.widget_no_data));
                manager.updateAppWidget(id, views);
                return;
            }
        }

        try {
            render(context, views, store, id, WidgetApi.parse(json), at, offline);
        } catch (IOException e) {
            showMessage(views, context.getString(R.string.widget_no_data));
        }
        manager.updateAppWidget(id, views);
    }

    private static void render(Context context, RemoteViews views, WidgetStore store, int id,
                               WidgetApi.Limits data, long at, boolean offline) {
        List<WidgetApi.LimitRow> shown = new ArrayList<>();
        for (String limitId : store.limitIds(id)) {
            for (WidgetApi.LimitRow row : data.limits) {
                if (row.id.equals(limitId)) shown.add(row);
            }
        }

        views.setTextViewText(R.id.header, context.getString(R.string.widget_header, store.budgetName(id)));
        String time = DateFormat.getTimeInstance(DateFormat.SHORT).format(new Date(at));
        views.setTextViewText(R.id.status, offline ? context.getString(R.string.widget_offline, time) : time);

        if (shown.isEmpty()) {
            showMessage(views, context.getString(R.string.widget_empty));
            return;
        }

        views.setViewVisibility(R.id.message, View.GONE);
        for (int i = 0; i < ROWS.length; i++) {
            if (i >= shown.size()) {
                views.setViewVisibility(ROWS[i], View.GONE);
                continue;
            }

            WidgetApi.LimitRow row = shown.get(i);
            boolean over = "Over".equalsIgnoreCase(row.state);
            boolean warning = "Warning".equalsIgnoreCase(row.state);
            String amount = WidgetApi.formatAmount(row.remaining);
            int progress = Math.max(0, Math.min(100, row.percent));

            views.setViewVisibility(ROWS[i], View.VISIBLE);
            views.setTextViewText(NAMES[i], row.name);
            views.setTextViewText(AMOUNTS[i], over
                ? context.getString(R.string.widget_over, amount)
                : context.getString(R.string.widget_remaining, amount));

            views.setViewVisibility(BARS_OK[i], !over && !warning ? View.VISIBLE : View.GONE);
            views.setViewVisibility(BARS_WARN[i], warning ? View.VISIBLE : View.GONE);
            views.setViewVisibility(BARS_OVER[i], over ? View.VISIBLE : View.GONE);
            views.setProgressBar(BARS_OK[i], 100, progress, false);
            views.setProgressBar(BARS_WARN[i], 100, progress, false);
            views.setProgressBar(BARS_OVER[i], 100, progress, false);
        }
    }

    private static void showMessage(RemoteViews views, String text) {
        for (int row : ROWS) views.setViewVisibility(row, View.GONE);
        views.setViewVisibility(R.id.message, View.VISIBLE);
        views.setTextViewText(R.id.message, text);
    }

    private static PendingIntent openAppIntent(Context context) {
        Intent launch = context.getPackageManager().getLaunchIntentForPackage(context.getPackageName());
        return PendingIntent.getActivity(context, 0, launch, PendingIntent.FLAG_IMMUTABLE | PendingIntent.FLAG_UPDATE_CURRENT);
    }

    private static PendingIntent setupIntent(Context context, int appWidgetId) {
        Intent intent = new Intent(context, WidgetSetupActivity.class)
            .putExtra(AppWidgetManager.EXTRA_APPWIDGET_ID, appWidgetId);
        return PendingIntent.getActivity(
            context, appWidgetId, intent, PendingIntent.FLAG_IMMUTABLE | PendingIntent.FLAG_UPDATE_CURRENT);
    }
}
