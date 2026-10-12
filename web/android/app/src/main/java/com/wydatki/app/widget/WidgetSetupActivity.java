package com.wydatki.app.widget;

import android.app.Activity;
import android.appwidget.AppWidgetManager;
import android.content.Intent;
import android.graphics.Color;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.View;
import android.widget.Button;
import android.widget.CheckBox;
import android.widget.LinearLayout;
import android.widget.RadioButton;
import android.widget.RadioGroup;
import android.widget.ScrollView;
import android.widget.TextView;

import com.wydatki.app.R;

import java.io.IOException;
import java.util.ArrayList;
import java.util.List;

/**
 * Konfiguracja widzetu przy dodawaniu go na pulpit: logowanie (jesli trzeba), wybor budzetu, wybor do czterech limitow.
 * Ustawienia sie potem nie zmieniaja — zeby je zmienic, trzeba dodac widzet od nowa.
 *
 * Uklad zgodny z zasadami dialogow aplikacji: komunikat bledu NA GORZE tresci, przyciski do prawej.
 *
 * Gdy widzet juz istnieje, a jego sesja wygasla, ta sama aktywnosc sluzy tylko do ponownego zalogowania.
 */
public class WidgetSetupActivity extends Activity {
    private enum Step {LOGIN, LOADING, BUDGETS, LIMITS}

    private final Handler ui = new Handler(Looper.getMainLooper());

    private WidgetStore store;
    private int appWidgetId = AppWidgetManager.INVALID_APPWIDGET_ID;
    private Step step = Step.LOGIN;
    private Step retryStep = Step.LOGIN;

    private List<WidgetApi.Budget> budgets = new ArrayList<>();
    private String defaultBudgetId;
    private WidgetApi.Budget chosenBudget;
    private List<WidgetApi.LimitRow> limits = new ArrayList<>();

    private TextView errorView;
    private TextView titleView;
    private LinearLayout content;
    private LinearLayout buttons;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setResult(RESULT_CANCELED);
        store = new WidgetStore(this);

        appWidgetId = getIntent().getIntExtra(
            AppWidgetManager.EXTRA_APPWIDGET_ID, AppWidgetManager.INVALID_APPWIDGET_ID);
        if (appWidgetId == AppWidgetManager.INVALID_APPWIDGET_ID) {
            finish();
            return;
        }

        buildFrame();
        show(Step.LOGIN);
    }

    /** Po powrocie z logowania w przegladarce (aktywnosc zwrotna zapisala tokeny) przechodzimy dalej. */
    @Override
    protected void onResume() {
        super.onResume();
        if (appWidgetId == AppWidgetManager.INVALID_APPWIDGET_ID || step != Step.LOGIN || !store.signedIn()) return;

        if (store.hasWidget(appWidgetId)) {
            refreshAndFinish();
        } else {
            loadBudgets();
        }
    }

    private void buildFrame() {
        int pad = dp(20);

        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setPadding(pad, pad, pad, pad);

        errorView = new TextView(this);
        errorView.setTextColor(Color.rgb(0xA3, 0x2D, 0x2D));
        errorView.setBackgroundColor(Color.rgb(0xFC, 0xEB, 0xEB));
        errorView.setPadding(dp(12), dp(10), dp(12), dp(10));
        errorView.setVisibility(View.GONE);
        root.addView(errorView, matchWrap(0, 0, 0, dp(12)));

        titleView = new TextView(this);
        titleView.setTextSize(TypedValue.COMPLEX_UNIT_SP, 18);
        titleView.setTextColor(Color.BLACK);
        root.addView(titleView, matchWrap(0, 0, 0, dp(12)));

        content = new LinearLayout(this);
        content.setOrientation(LinearLayout.VERTICAL);
        ScrollView scroll = new ScrollView(this);
        scroll.addView(content);
        root.addView(scroll, new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT, 0, 1f));

        buttons = new LinearLayout(this);
        buttons.setOrientation(LinearLayout.HORIZONTAL);
        buttons.setGravity(Gravity.END);
        root.addView(buttons, matchWrap(0, dp(12), 0, 0));

        setContentView(root);
    }

    private void show(Step next) {
        step = next;
        content.removeAllViews();
        buttons.removeAllViews();

        switch (next) {
            case LOGIN:
                titleView.setText(R.string.setup_title);
                content.addView(text(getString(R.string.setup_sign_in_info)));
                addButton(getString(R.string.setup_cancel), v -> finish());
                addButton(getString(R.string.setup_sign_in), v -> startActivity(
                    WidgetAuth.authorizationIntent(this, appWidgetId)));
                break;
            case LOADING:
                titleView.setText(R.string.setup_title);
                content.addView(text(getString(R.string.setup_loading)));
                break;
            case BUDGETS:
                showBudgets();
                break;
            case LIMITS:
                showLimits();
                break;
        }
    }

    private void showBudgets() {
        titleView.setText(R.string.setup_pick_budget);

        RadioGroup group = new RadioGroup(this);
        List<RadioButton> options = new ArrayList<>();
        for (WidgetApi.Budget budget : budgets) {
            RadioButton option = new RadioButton(this);
            option.setId(View.generateViewId());
            option.setText(budget.disabled ? getString(R.string.setup_budget_disabled, budget.name) : budget.name);
            option.setChecked(budget.id.equals(defaultBudgetId));
            group.addView(option);
            options.add(option);
        }
        content.addView(group);

        addButton(getString(R.string.setup_cancel), v -> finish());
        addButton(getString(R.string.setup_next), v -> {
            for (int i = 0; i < options.size(); i++) {
                if (options.get(i).isChecked()) {
                    chosenBudget = budgets.get(i);
                    loadLimits(chosenBudget);
                    return;
                }
            }
            showError(getString(R.string.setup_error_pick_budget));
        });
    }

    private void showLimits() {
        titleView.setText(getString(R.string.setup_pick_limits, WidgetConfig.MAX_LIMITS));

        List<CheckBox> boxes = new ArrayList<>();
        if (limits.isEmpty()) {
            content.addView(text(getString(R.string.setup_no_limits)));
        }

        for (WidgetApi.LimitRow limit : limits) {
            CheckBox box = new CheckBox(this);
            box.setText(limit.name);
            box.setOnCheckedChangeListener((button, checked) -> {
                if (checked && checkedCount(boxes) > WidgetConfig.MAX_LIMITS) {
                    button.setChecked(false);
                    showError(getString(R.string.setup_error_max, WidgetConfig.MAX_LIMITS));
                } else {
                    hideError();
                }
            });
            content.addView(box);
            boxes.add(box);
        }

        addButton(getString(R.string.setup_back), v -> show(Step.BUDGETS));
        if (!limits.isEmpty()) {
            addButton(getString(R.string.setup_add), v -> {
                List<String> ids = new ArrayList<>();
                for (int i = 0; i < boxes.size(); i++) {
                    if (boxes.get(i).isChecked()) ids.add(limits.get(i).id);
                }
                if (ids.isEmpty()) {
                    showError(getString(R.string.setup_error_pick_one));
                    return;
                }
                store.saveWidget(appWidgetId, chosenBudget.id, chosenBudget.name, ids);
                refreshAndFinish();
            });
        }
    }

    private void loadBudgets() {
        retryStep = Step.LOGIN;
        show(Step.LOADING);
        hideError();
        new Thread(() -> {
            try {
                String raw = WidgetApi.fetchRaw(WidgetAuth.accessToken(this), null);
                WidgetApi.Limits data = WidgetApi.parse(raw);
                ui.post(() -> {
                    budgets = data.budgets;
                    defaultBudgetId = data.selectedBudgetId;
                    show(Step.BUDGETS);
                });
            } catch (IOException e) {
                ui.post(() -> failed(e));
            }
        }).start();
    }

    private void loadLimits(WidgetApi.Budget budget) {
        retryStep = Step.BUDGETS;
        show(Step.LOADING);
        hideError();
        new Thread(() -> {
            try {
                String raw = WidgetApi.fetchRaw(WidgetAuth.accessToken(this), budget.id);
                WidgetApi.Limits data = WidgetApi.parse(raw);
                ui.post(() -> {
                    limits = data.limits;
                    show(Step.LIMITS);
                });
            } catch (IOException e) {
                ui.post(() -> failed(e));
            }
        }).start();
    }

    /** Pierwsze rysowanie robimy sami — Android tego od aktywnosci konfiguracji wymaga. */
    private void refreshAndFinish() {
        Intent result = new Intent().putExtra(AppWidgetManager.EXTRA_APPWIDGET_ID, appWidgetId);
        setResult(RESULT_OK, result);

        AppWidgetManager manager = AppWidgetManager.getInstance(getApplicationContext());
        int id = appWidgetId;
        android.content.Context app = getApplicationContext();
        new Thread(() -> LimitsWidgetProvider.updateWidget(app, manager, id)).start();
        finish();
    }

    private void failed(IOException error) {
        if (error instanceof AuthRequiredException) {
            show(Step.LOGIN);
            showError(getString(R.string.setup_error_auth));
        } else {
            show(retryStep);
            showError(getString(R.string.setup_error_network));
        }
    }

    private int checkedCount(List<CheckBox> boxes) {
        int count = 0;
        for (CheckBox box : boxes) if (box.isChecked()) count++;
        return count;
    }

    private void showError(String message) {
        errorView.setText(message);
        errorView.setVisibility(View.VISIBLE);
    }

    private void hideError() {
        errorView.setVisibility(View.GONE);
    }

    private TextView text(String value) {
        TextView view = new TextView(this);
        view.setText(value);
        view.setTextColor(Color.DKGRAY);
        return view;
    }

    private void addButton(String label, View.OnClickListener onClick) {
        Button button = new Button(this);
        button.setText(label);
        button.setOnClickListener(onClick);
        buttons.addView(button);
    }

    private LinearLayout.LayoutParams matchWrap(int left, int top, int right, int bottom) {
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(
            LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        params.setMargins(left, top, right, bottom);
        return params;
    }

    private int dp(int value) {
        return Math.round(TypedValue.applyDimension(
            TypedValue.COMPLEX_UNIT_DIP, value, getResources().getDisplayMetrics()));
    }
}
