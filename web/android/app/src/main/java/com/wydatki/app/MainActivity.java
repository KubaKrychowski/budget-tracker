package com.wydatki.app;

import android.os.Build;
import android.os.Bundle;
import android.view.WindowManager;
import android.webkit.WebView;

import androidx.core.graphics.Insets;
import androidx.core.view.ViewCompat;
import androidx.core.view.WindowCompat;
import androidx.core.view.WindowInsetsCompat;
import androidx.core.view.WindowInsetsControllerCompat;

import com.getcapacitor.BridgeActivity;
import com.getcapacitor.WebViewListener;

import java.util.Locale;

/**
 * Aplikacja w trybie pelnoekranowym: pasek statusu i systemowe przyciski nawigacji sa ukryte
 * i wracaja tylko chwilowo po przeciagnieciu od krawedzi ekranu (decyzja z 2026-10-06).
 *
 * Odstep od otworu na kamere podajemy stronie sami, jako zmienna CSS --safe-area-inset-top.
 * Wtyczka SystemBars Capacitora ma to wylaczone (capacitor.config.ts, insetsHandling: 'disable'):
 * na WebView starszym niz 140 zamiast przekazac odstep do CSS dokladala margines nad WebView,
 * czyli czarny pas w miejscu ukrytego paska statusu.
 */
public class MainActivity extends BridgeActivity {

    /** Ostatni odstep od gornej krawedzi (dp) - wstrzykiwany ponownie po kazdym przeladowaniu strony. */
    private int topInsetDp = 0;

    @Override
    public void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        // Tresc rysuje sie pod paskami, wiec po ich schowaniu nie zostaja puste pasy u gory i na dole.
        WindowCompat.setDecorFitsSystemWindows(getWindow(), false);
        // Bez tego Android zostawia czarny pas w miejscu otworu na kamere, gdy pasek statusu jest ukryty.
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
            getWindow().getAttributes().layoutInDisplayCutoutMode =
                WindowManager.LayoutParams.LAYOUT_IN_DISPLAY_CUTOUT_MODE_SHORT_EDGES;
        }

        // Paski sa ukryte, wiec jedyny staly odstep to otwor na kamere (i zaokraglone rogi).
        ViewCompat.setOnApplyWindowInsetsListener(getWindow().getDecorView(), (view, insets) -> {
            Insets cutout = insets.getInsets(WindowInsetsCompat.Type.displayCutout());
            topInsetDp = Math.round(cutout.top / getResources().getDisplayMetrics().density);
            injectSafeAreaCss();
            return insets;
        });

        bridge.addWebViewListener(new WebViewListener() {
            @Override
            public void onPageLoaded(WebView webView) {
                injectSafeAreaCss();
            }
        });
    }

    /**
     * Ponownie przy kazdym odzyskaniu fokusu, a nie raz w onCreate: system przywraca paski po powrocie
     * z innej aplikacji (np. z przegladarki po logowaniu) i po zamknieciu okien dialogowych.
     */
    @Override
    public void onWindowFocusChanged(boolean hasFocus) {
        super.onWindowFocusChanged(hasFocus);
        if (hasFocus) {
            hideSystemBars();
        }
    }

    private void hideSystemBars() {
        WindowInsetsControllerCompat controller =
            WindowCompat.getInsetsController(getWindow(), getWindow().getDecorView());
        // Przeciagniecie od krawedzi pokazuje paski na chwile, nad trescia, bez przesuwania ukladu.
        controller.setSystemBarsBehavior(WindowInsetsControllerCompat.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE);
        controller.hide(WindowInsetsCompat.Type.systemBars());
    }

    private void injectSafeAreaCss() {
        if (bridge == null || bridge.getWebView() == null) {
            return;
        }
        String script = String.format(
            Locale.US,
            "document.documentElement.style.setProperty('--safe-area-inset-top', '%dpx');",
            topInsetDp
        );
        bridge.getWebView().post(() -> bridge.getWebView().evaluateJavascript(script, null));
    }
}
