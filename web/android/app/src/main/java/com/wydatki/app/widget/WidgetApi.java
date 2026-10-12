package com.wydatki.app.widget;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.io.IOException;
import java.math.BigDecimal;
import java.math.RoundingMode;
import java.text.NumberFormat;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

/** Odczyt GET /api/limits (LimitsResponseDto) i formatowanie kwot widzetu. */
final class WidgetApi {
    static final class Budget {
        final String id;
        final String name;
        final boolean disabled;

        Budget(String id, String name, boolean disabled) {
            this.id = id;
            this.name = name;
            this.disabled = disabled;
        }
    }

    static final class LimitRow {
        final String id;
        final String name;
        final BigDecimal remaining;
        final int percent;
        final String state;

        LimitRow(String id, String name, BigDecimal remaining, int percent, String state) {
            this.id = id;
            this.name = name;
            this.remaining = remaining;
            this.percent = percent;
            this.state = state;
        }
    }

    static final class Limits {
        final String selectedBudgetId;
        final List<Budget> budgets;
        final List<LimitRow> limits;

        Limits(String selectedBudgetId, List<Budget> budgets, List<LimitRow> limits) {
            this.selectedBudgetId = selectedBudgetId;
            this.budgets = budgets;
            this.limits = limits;
        }
    }

    private WidgetApi() {
    }

    /** Surowa odpowiedz; `budgetId == null` daje budzet domyslny (i liste wszystkich budzetow). */
    static String fetchRaw(String accessToken, String budgetId) throws IOException {
        String url = WidgetConfig.API_URL + "/api/limits" + (budgetId == null ? "" : "?budgetId=" + budgetId);
        WidgetHttp.Response response = WidgetHttp.get(url, accessToken);

        if (response.status == 401) throw new AuthRequiredException("API odrzucilo token widzetu");
        if (response.status != 200) throw new IOException("API odpowiedzialo " + response.status);
        return response.body;
    }

    static Limits parse(String json) throws IOException {
        try {
            JSONObject root = new JSONObject(json);

            JSONArray selected = root.optJSONArray("selectedBudgetIds");
            String selectedId = selected != null && selected.length() > 0 ? selected.getString(0) : null;

            List<Budget> budgets = new ArrayList<>();
            JSONArray budgetArray = root.optJSONArray("budgets");
            for (int i = 0; budgetArray != null && i < budgetArray.length(); i++) {
                JSONObject b = budgetArray.getJSONObject(i);
                budgets.add(new Budget(b.getString("id"), b.getString("name"), b.optBoolean("disabled")));
            }

            List<LimitRow> limits = new ArrayList<>();
            JSONArray limitArray = root.optJSONArray("limits");
            for (int i = 0; limitArray != null && i < limitArray.length(); i++) {
                JSONObject l = limitArray.getJSONObject(i);
                limits.add(new LimitRow(
                    l.getString("id"),
                    l.getString("categoryName"),
                    new BigDecimal(l.getString("remaining")),
                    l.getInt("percent"),
                    l.getString("state")));
            }

            return new Limits(selectedId, budgets, limits);
        } catch (JSONException | NumberFormatException e) {
            throw new IOException("Nieczytelna odpowiedz API", e);
        }
    }

    /** Pelne zlotowki z odstepem tysiecy, np. "1 200"; ujemne bez znaku (znak niesie tekst „przekroczono"). */
    static String formatAmount(BigDecimal amount) {
        NumberFormat format = NumberFormat.getIntegerInstance(new Locale("pl", "PL"));
        format.setRoundingMode(RoundingMode.HALF_UP);
        return format.format(amount.abs());
    }
}
