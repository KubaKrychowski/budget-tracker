import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient, httpResource } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzDropdownModule } from 'ng-zorro-antd/dropdown';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { TranslatePipe, TranslateService } from '@ngx-translate/core';
import { ConfirmDialogService } from '../../../core/confirm-dialog/confirm-dialog.service';
import { ErrorMessages } from '../../../core/errors/error-messages';
import { valueOf } from '../../../core/api/resource-value';
import { parseAmount } from '../../../core/parse-amount';
import { CategoryOption } from '../../../core/api/models/category-option';
import { CategoryRule, CategoryRuleRequest, RuleDirection } from '../../../core/api/models/category-rule';
import { RulePreview } from '../../../core/api/models/rule-preview';

/** Pola formularza reguły — jeden kształt dla tworzenia i edycji, tak jak w API. */
interface RuleDraft {
  pattern: string;
  transactionTypePattern: string;
  direction: RuleDirection;
  categoryId: string | null;
  priority: number;
  minAmount: number | null;
  maxAmount: number | null;
  note: string;
}

/**
 * Zakładka „Reguły predykatu" — przegląd, kreator, edycja i usuwanie reguł kategoryzacji.
 *
 * Osobny komponent, nie kolejna sekcja w `Settings`: tamten ma już ~500 linii wokół budżetów,
 * a to jest inny slice domeny (kategoryzacja), dzielący z nim wyłącznie miejsce na ekranie.
 */
@Component({
  selector: 'app-rules',
  imports: [
    FormsModule,
    NzAlertModule, NzButtonModule, NzDropdownModule, NzIconModule, NzInputModule,
    NzInputNumberModule, NzModalModule, NzSelectModule, NzSpinModule, NzTableModule, NzTagModule,
    TranslatePipe,
  ],
  templateUrl: './rules.html',
  styleUrl: './rules.scss',
})
export class Rules {
  /** Parser polskiego formatu kwot dla pól `nz-input-number` — uzasadnienie przy `parseAmount`. */
  protected readonly parseAmount = parseAmount;

  private readonly http = inject(HttpClient);
  private readonly translate = inject(TranslateService);
  private readonly message = inject(NzMessageService);
  private readonly errorMessages = inject(ErrorMessages);
  private readonly confirmDialog = inject(ConfirmDialogService);

  private readonly rulesResource = httpResource<CategoryRule[]>(() => '/api/categorization/rules');
  private readonly categoriesResource = httpResource<CategoryOption[]>(() => '/api/categories');

  /** Bezpieczny odczyt — `value()` RZUCA w stanie błędu (patrz core/api/resource-value.ts). */
  private readonly rulesValue = valueOf(this.rulesResource);
  private readonly categoriesValue = valueOf(this.categoriesResource);

  protected readonly loading = this.rulesResource.isLoading;
  protected readonly categories = computed(() => this.categoriesValue() ?? []);

  /**
   * Reguły w kolejności, w jakiej NAPRAWDĘ się stosują.
   *
   * ⚠️ Sortowanie priorytetem rosnąco nie jest preferencją — `RuleCategorizer` robi
   * `OrderBy(Priority).ThenBy(Id)` i bierze PIERWSZE trafienie, więc niższa liczba wygrywa.
   * Tabela posortowana nazwą albo kategorią ukrywałaby jedyną własność, która decyduje o wyniku.
   */
  protected readonly rules = computed(() =>
    [...(this.rulesValue() ?? [])].sort((a, b) => a.priority - b.priority || a.categoryName.localeCompare(b.categoryName, 'pl')),
  );

  /**
   * Priorytety występujące więcej niż raz, rosnąco.
   *
   * Remis jest legalny i nie jest losowy — rozstrzyga go `Id`, czyli kolejność dodania — ale `Id`
   * nie wychodzi z API, więc dla użytkownika o wyniku decyduje coś, czego nie widać na ekranie.
   */
  protected readonly tiedPriorities = computed(() => {
    const counts = new Map<number, number>();
    for (const rule of this.rules()) counts.set(rule.priority, (counts.get(rule.priority) ?? 0) + 1);
    return [...counts.entries()].filter(([, n]) => n > 1).map(([p]) => p).sort((a, b) => a - b);
  });

  /**
   * Zestaw remisów, który użytkownik już przyjął do wiadomości przyciskiem „Rozumiem".
   *
   * ⚠️ Pamiętany jest ZESTAW, a nie samo „zamknięte". Na realnych danych remis jest normą
   * (`BaselineSeed` celowo grupuje reguły po priorytecie — 19 zremisowanych wartości na 148
   * reguł), więc alert pokazywany przy każdej wizycie byłby szumem. Ale alert zamknięty NA
   * ZAWSZE przestałby ostrzegać o NOWYM remisie, dopisanym później. Dlatego wraca, gdy zmieni
   * się lista zremisowanych priorytetów, i tylko wtedy.
   *
   * `localStorage`, nie backend: to wygoda tej przeglądarki, nie dana domenowa — ten sam wybór
   * co pamięć zakresu wykresu na dashboardzie. Brak dostępu do magazynu (tryb prywatny, blokada)
   * nie może wywrócić ekranu, więc każdy odczyt i zapis jest w `try`.
   */
  private readonly acknowledgedTies = signal<string | null>(Rules.readAcknowledgedTies());

  private static readonly TiesStorageKey = 'rules.acknowledgedTiedPriorities';

  private static readAcknowledgedTies(): string | null {
    try {
      return localStorage.getItem(Rules.TiesStorageKey);
    } catch {
      return null;
    }
  }

  /** Czy pokazać ostrzeżenie: są remisy i nie jest to zestaw już przyjęty do wiadomości. */
  protected readonly showTiesWarning = computed(() => {
    const tied = this.tiedPriorities();
    return tied.length > 0 && tied.join(',') !== this.acknowledgedTies();
  });

  /** „Rozumiem" — zapamiętuje obecny zestaw remisów, żeby alert nie wracał, dopóki się nie zmieni. */
  protected acknowledgeTies(): void {
    const signature = this.tiedPriorities().join(',');
    this.acknowledgedTies.set(signature);
    try {
      localStorage.setItem(Rules.TiesStorageKey, signature);
    } catch {
      // Bez magazynu przyjęcie działa do końca wizyty — lepsze to niż błąd na ekranie.
    }
  }

  protected readonly directions: readonly RuleDirection[] = ['Any', 'Expense', 'Income'];

  // ── Edytor ─────────────────────────────────────────────────────────────────────────

  protected readonly editorOpen = signal(false);
  protected readonly saving = signal(false);

  /** `null` znaczy „tworzymy nową", inaczej edytujemy regułę o tym identyfikatorze. */
  protected readonly editedId = signal<string | null>(null);
  protected readonly draft = signal<RuleDraft>(Rules.emptyDraft());

  protected readonly editorTitle = computed(() =>
    this.editedId() === null ? 'settings.rules.editor.createTitle' : 'settings.rules.editor.editTitle',
  );

  private static emptyDraft(): RuleDraft {
    return {
      pattern: '',
      transactionTypePattern: '',
      direction: 'Expense',
      categoryId: null,
      // Domyślnie DALEKO, nie 0: nowa reguła nie powinna po cichu wyprzedzać reguł bazowych.
      priority: 500,
      minAmount: null,
      maxAmount: null,
      note: '',
    };
  }

  /**
   * Błąd formularza albo `null`.
   *
   * ⚠️ Powtarza trzy reguły `CategoryRuleValidator` z backendu wyłącznie po to, żeby powiedzieć
   * je PRZED wysłaniem. Źródłem prawdy zostaje backend — jego 400 jest pokazywane, nie ukrywane.
   * Regexu tu NIE walidujemy: składnia .NET i JS się różni, więc sprawdzenie w przeglądarce
   * dawałoby fałszywą pewność w obie strony.
   */
  protected readonly draftError = computed<string | null>(() => {
    const d = this.draft();
    if (!d.pattern.trim() && !d.transactionTypePattern.trim()) return 'settings.rules.editor.patternRequired';
    if (!d.categoryId) return 'settings.rules.editor.categoryRequired';
    if (d.minAmount !== null && d.maxAmount !== null && d.minAmount >= d.maxAmount) {
      return 'settings.rules.editor.rangeInvalid';
    }
    return null;
  });

  protected openCreate(): void {
    this.editedId.set(null);
    this.draft.set(Rules.emptyDraft());
    this.preview.set(null);
    this.editorOpen.set(true);
  }

  protected openEdit(rule: CategoryRule): void {
    this.editedId.set(rule.id);
    this.draft.set({
      pattern: rule.pattern ?? '',
      transactionTypePattern: rule.transactionTypePattern ?? '',
      direction: rule.direction,
      categoryId: rule.categoryId,
      priority: rule.priority,
      minAmount: rule.minAmount,
      maxAmount: rule.maxAmount,
      note: rule.note ?? '',
    });
    this.preview.set(null);
    this.editorOpen.set(true);
  }

  protected closeEditor(): void {
    this.editorOpen.set(false);
  }

  protected patch<K extends keyof RuleDraft>(key: K, value: RuleDraft[K]): void {
    this.draft.update((d) => ({ ...d, [key]: value }));
  }

  private requestBody(): CategoryRuleRequest {
    const d = this.draft();
    return {
      pattern: d.pattern.trim() || null,
      transactionTypePattern: d.transactionTypePattern.trim() || null,
      direction: d.direction,
      categoryId: d.categoryId!,
      priority: d.priority,
      minAmount: d.minAmount,
      maxAmount: d.maxAmount,
      note: d.note.trim() || null,
    };
  }

  protected async save(): Promise<void> {
    if (this.draftError() !== null || this.saving()) return;

    this.saving.set(true);
    try {
      const id = this.editedId();
      const body = this.requestBody();
      if (id === null) {
        await firstValueFrom(this.http.post<CategoryRule>('/api/categorization/rules', body));
      } else {
        await firstValueFrom(this.http.put<CategoryRule>(`/api/categorization/rules/${id}`, body));
      }
      this.editorOpen.set(false);
      this.rulesResource.reload();
    } catch (error) {
      this.message.error(this.errorMessages.of(error));
    } finally {
      this.saving.set(false);
    }
  }

  // ── Podgląd trafień ────────────────────────────────────────────────────────────────

  protected readonly preview = signal<RulePreview | null>(null);
  protected readonly previewing = signal(false);

  /**
   * Liczy, co reguła złapie, nic nie zapisując.
   *
   * ⚠️ Podgląd jest wyłączony, dopóki formularz ma błąd — backend odrzuciłby takie żądanie
   * tym samym 400 co zapis, a komunikat „popraw wzorzec" jest czytelniejszy niż błąd sieci.
   */
  protected async runPreview(): Promise<void> {
    if (this.draftError() !== null || this.previewing()) return;

    this.previewing.set(true);
    try {
      const result = await firstValueFrom(
        this.http.post<RulePreview>('/api/categorization/rules/preview', this.requestBody()),
      );
      this.preview.set(result);
    } catch (error) {
      this.preview.set(null);
      this.message.error(this.errorMessages.of(error));
    } finally {
      this.previewing.set(false);
    }
  }

  // ── Usuwanie ───────────────────────────────────────────────────────────────────────

  /**
   * Usuwa regułę po potwierdzeniu.
   *
   * ⚠️ Modal mówi wprost, czego usunięcie NIE robi: transakcje skategoryzowane tą regułą
   * zostają w swoich kategoriach. Bez tego zdania naturalne założenie jest odwrotne —
   * a to jest operacja, której nie da się cofnąć jednym kliknięciem.
   */
  protected async remove(rule: CategoryRule): Promise<void> {
    const confirmed = await this.confirmDialog.confirm({
      header: this.translate.instant('settings.rules.delete.body', {
        rule: rule.pattern ?? rule.transactionTypePattern ?? '',
        category: rule.categoryName,
        priority: rule.priority,
      }),
      description: this.translate.instant('settings.rules.delete.note'),
      confirmText: this.translate.instant('settings.rules.delete.confirm'),
    });
    if (!confirmed) return;

    try {
      await firstValueFrom(this.http.delete(`/api/categorization/rules/${rule.id}`));
      this.rulesResource.reload();
    } catch (error) {
      this.message.error(this.errorMessages.of(error));
    }
  }

  // ── Formatowanie ───────────────────────────────────────────────────────────────────

  /** Widełki jako jeden tekst — dwie kolumny na „od" i „do" rozrywałyby jedną wielkość. */
  protected range(rule: CategoryRule): string | null {
    const { minAmount: min, maxAmount: max } = rule;
    if (min === null && max === null) return null;
    if (min !== null && max !== null) return `${min}–${max}`;
    return min !== null ? `od ${min}` : `do ${max}`;
  }
}
