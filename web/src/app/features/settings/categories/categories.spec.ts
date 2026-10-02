import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideTranslateService, TranslateService } from '@ngx-translate/core';
import { provideNzIcons } from 'ng-zorro-antd/icon';
import { pl_PL, provideNzI18n } from 'ng-zorro-antd/i18n';
import { describe, expect, it, beforeEach } from 'vitest';
import { APP_ICONS } from '../../../core/icons';
import { ConfirmDialogService } from '../../../core/confirm-dialog/confirm-dialog.service';
import { ConfirmDialogOptions } from '../../../core/confirm-dialog/confirm-dialog-options';
import { ManagedCategory } from '../../../core/api/models/managed-category';
import { Categories } from './categories';

class FakeConfirmDialogService {
  lastOptions: ConfirmDialogOptions | null = null;
  private resolve: ((value: boolean) => void) | null = null;

  confirm(options: ConfirmDialogOptions): Promise<boolean> {
    this.lastOptions = options;
    return new Promise((resolve) => { this.resolve = resolve; });
  }

  respond(confirmed: boolean): void {
    this.resolve?.(confirmed);
    this.resolve = null;
  }
}

/** Dostęp do składowych `protected` — test sprawdza zachowanie, nie układ szablonu. */
interface CategoriesInternals {
  openCreate(): void;
  openEdit(category: ManagedCategory): void;
  remove(category: ManagedCategory): Promise<void>;
  save(): Promise<void>;
  name: { set(value: string): void };
  type: { set(value: string): void; (): string };
  canSave(): boolean;
  typeLocked(): boolean;
  editorError(): string | null;
}

/**
 * Zakładka „Kategorie". Testujemy to, czego nie widać w szablonie, a co decyduje o poprawności:
 * kategorie wspólne nie mają menu, typ używanej kategorii jest zablokowany, usuwanie idzie przez potwierdzenie,
 * a błąd z API (np. nazwa zajęta) ląduje w oknie edytora, a nie znika w toaście pod maską.
 */
describe('Categories', () => {
  let fixture: ComponentFixture<Categories>;
  let http: HttpTestingController;
  let confirmDialog: FakeConfirmDialogService;
  let internals: CategoriesInternals;

  const category = (over: Partial<ManagedCategory> = {}): ManagedCategory => ({
    id: crypto.randomUUID(),
    name: 'Hobby',
    type: 'Expense',
    isShared: false,
    transactions: 0,
    rules: 0,
    limits: 0,
    inUse: false,
    ...over,
  });

  const text = (): string => (fixture.nativeElement.textContent as string).replace(/\s+/g, ' ');
  const modalText = (): string => (document.body.textContent ?? '').replace(/\s+/g, ' ');

  const settle = async (categories: ManagedCategory[]): Promise<void> => {
    fixture.detectChanges();
    http.match((r) => r.url === '/api/categories/manage').forEach((r) => r.flush(categories));
    await fixture.whenStable();
    fixture.detectChanges();
  };

  beforeEach(async () => {
    confirmDialog = new FakeConfirmDialogService();

    await TestBed.configureTestingModule({
      imports: [Categories],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideTranslateService(),
        provideNzIcons(APP_ICONS),
        provideNzI18n(pl_PL),
        { provide: ConfirmDialogService, useValue: confirmDialog },
      ],
    }).compileComponents();

    TestBed.inject(TranslateService).setTranslation('pl', {
      settings: {
        budgets: { actions: { edit: 'Edytuj' } },
        categories: {
          intro: 'Kategorie porządkują transakcje.',
          sharedTitle: 'Kategorie wspólne są tylko do odczytu',
          sharedDescription: 'Wspólnych nie zmienisz.',
          listTitle: 'Lista kategorii',
          create: 'Nowa kategoria',
          empty: 'Brak kategorii.',
          usage: 'Transakcje: {{transactions}} · reguły: {{rules}} · limity: {{limits}}',
          columns: { name: 'Nazwa', type: 'Typ', scope: 'Zakres', usage: 'Użycie', actions: 'Akcje' },
          type: { Expense: 'Wydatek', Income: 'Wpływ' },
          scope: { shared: 'Wspólna', own: 'Własna' },
          editor: {
            createTitle: 'Nowa kategoria', editTitle: 'Edycja kategorii', type: 'Typ',
            typeHint: 'Typ decyduje o transakcjach.', typeLockedHint: 'Typ jest zablokowany.',
            name: 'Nazwa', namePlaceholder: 'np. Hobby', nameHint: 'Nazwa musi być unikalna.',
            cancel: 'Anuluj', save: 'Zapisz',
          },
          delete: {
            header: 'Usunąć kategorię „{{name}}”?', description: 'Zniknie z list.',
            confirm: 'Usuń kategorię', inUse: 'Kategoria jest używana.',
          },
          toast: { created: 'Dodano.', updated: 'Zapisano.', deleted: 'Usunięto.' },
        },
      },
    });
    TestBed.inject(TranslateService).use('pl');

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Categories);
    internals = fixture.componentInstance as unknown as CategoriesInternals;
  });

  it('shows own and shared categories with their type, scope and usage', async () => {
    await settle([
      category({ name: 'Hobby', transactions: 12 }),
      category({ name: 'Wynagrodzenie', type: 'Income', isShared: true, rules: 2 }),
    ]);

    expect(text()).toContain('Hobby');
    expect(text()).toContain('Własna');
    expect(text()).toContain('Transakcje: 12 · reguły: 0 · limity: 0');
    expect(text()).toContain('Wynagrodzenie');
    expect(text()).toContain('Wpływ');
    expect(text()).toContain('Wspólna');
  });

  it('shows the shared-read-only alert above the list', async () => {
    await settle([category()]);

    const html = fixture.nativeElement as HTMLElement;
    const alert = html.querySelector('nz-alert');
    const header = html.querySelector('.cat__header');

    expect(alert?.textContent).toContain('Kategorie wspólne są tylko do odczytu');
    // Alert PRZED nagłówkiem listy — reguła alertów z makiety.
    expect(alert!.compareDocumentPosition(header!) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('gives only own categories a row menu', async () => {
    await settle([category({ name: 'Hobby' }), category({ name: 'Paliwo', isShared: true })]);

    const rows = [...(fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr')];
    const menuButtons = rows.map((row) => row.querySelectorAll('button').length);

    expect(menuButtons).toEqual([1, 0]);
  });

  it('creates a category with the chosen type and name', async () => {
    await settle([]);

    internals.openCreate();
    internals.type.set('Income');
    internals.name.set('  Korepetycje  ');
    const saving = internals.save();

    const request = http.expectOne((r) => r.url === '/api/categories' && r.method === 'POST');
    expect(request.request.body).toEqual({ name: 'Korepetycje', type: 'Income' });
    request.flush(category({ name: 'Korepetycje', type: 'Income' }));
    await saving;
    fixture.detectChanges();

    // Po zapisie lista odświeża się z serwera.
    expect(http.match((r) => r.url === '/api/categories/manage').length).toBeGreaterThan(0);
  });

  it('does not allow saving an empty name', async () => {
    await settle([]);

    internals.openCreate();
    internals.name.set('   ');

    expect(internals.canSave()).toBe(false);
    await internals.save();
    http.expectNone((r) => r.url === '/api/categories');
  });

  it('locks the type of a category that is already in use', async () => {
    const used = category({ inUse: true, transactions: 3 });
    await settle([used]);

    internals.openEdit(used);
    fixture.detectChanges();

    expect(internals.typeLocked()).toBe(true);
    expect(modalText()).toContain('Typ jest zablokowany.');
  });

  it('leaves the type of an unused category editable', async () => {
    const unused = category();
    await settle([unused]);

    internals.openEdit(unused);

    expect(internals.typeLocked()).toBe(false);
  });

  it('shows the API error inside the editor instead of closing it', async () => {
    await settle([]);

    internals.openCreate();
    internals.name.set('Jedzenie');
    const saving = internals.save();
    http.expectOne((r) => r.url === '/api/categories')
      .flush({ title: 'Kategoria o tej nazwie już istnieje.' }, { status: 409, statusText: 'Conflict' });
    await saving;
    fixture.detectChanges();

    expect(internals.editorError()).not.toBeNull();
    expect(modalText()).toContain('Nowa kategoria');
  });

  it('deletes a category only after confirmation', async () => {
    const target = category({ name: 'Zwierzęta' });
    await settle([target]);

    const removal = internals.remove(target);
    expect(confirmDialog.lastOptions?.header).toBe('Usunąć kategorię „Zwierzęta”?');
    expect(confirmDialog.lastOptions?.danger).toBe(true);
    http.expectNone((r) => r.method === 'DELETE');

    confirmDialog.respond(true);
    await Promise.resolve();
    http.expectOne((r) => r.method === 'DELETE' && r.url === `/api/categories/${target.id}`).flush(null);
    await removal;
  });

  it('does not delete when the confirmation is declined', async () => {
    const target = category();
    await settle([target]);

    const removal = internals.remove(target);
    confirmDialog.respond(false);
    await removal;

    http.expectNone((r) => r.method === 'DELETE');
  });
});
