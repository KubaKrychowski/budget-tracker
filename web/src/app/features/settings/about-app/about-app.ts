import { DatePipe } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { AppUpdateService } from '../../../core/app-update/app-update.service';

/**
 * Ustawienia → „O aplikacji” (makieta Figma „Mobile (propozycja)”: 420:1542, 420:1675, 420:1794, 420:1918).
 *
 * Zainstalowana i najnowsza wersja oraz ręczne „Sprawdź aktualizacje”. Przycisk pomija godzinną blokadę paska i pyta
 * GitHuba od razu; wynik — także błąd — jest zawsze widać na ekranie, a znaleziona wersja wraca na pasek pod nagłówkiem
 * nawet po zamknięciu go krzyżykiem. Sekcja jest tylko w aplikacji na Androida (`AppUpdateService.isSupported`).
 */
@Component({
  selector: 'app-about-app',
  imports: [DatePipe, NzAlertModule, NzButtonModule, TranslatePipe],
  templateUrl: './about-app.html',
  styleUrl: './about-app.scss',
})
export class AboutApp implements OnInit {
  protected readonly updates = inject(AppUpdateService);
  private readonly translate = inject(TranslateService);

  protected locale(): string {
    return this.translate.currentLang() || 'pl';
  }

  ngOnInit(): void {
    void this.updates.loadInstalled();
  }

  protected check(): void {
    void this.updates.checkNow();
  }
}
