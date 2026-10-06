package com.wydatki.app;

import android.os.Build;
import android.os.Bundle;
import android.view.WindowManager;

import androidx.core.view.WindowCompat;
import androidx.core.view.WindowInsetsCompat;
import androidx.core.view.WindowInsetsControllerCompat;

import com.getcapacitor.BridgeActivity;

/**
 * Aplikacja w trybie pelnoekranowym: pasek statusu i systemowe przyciski nawigacji sa ukryte
 * i wracaja tylko chwilowo po przeciagnieciu od krawedzi ekranu (decyzja z 2026-10-06).
 *
 * Tresc zaczyna sie od samej gornej krawedzi i ignoruje otwor na kamere (decyzja wlasciciela: jak w
 * React Native bez SafeAreaView) - kamera moze przykryc fragment naglowka. Wtyczka SystemBars Capacitora
 * ma obsluge odstepow wylaczona (capacitor.config.ts, insetsHandling: 'disable'), bo dokladala margines
 * nad WebView, czyli pas w miejscu ukrytego paska statusu.
 */
public class MainActivity extends BridgeActivity {

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
}
