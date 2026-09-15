import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { NzDrawerModule } from 'ng-zorro-antd/drawer';
import { TranslatePipe } from '@ngx-translate/core';
import { ErrorMessages } from '../errors/error-messages';
import { TerminalService } from './terminal.service';
import { appendHistory, loadHistory } from './cli-history';

interface TerminalEntry {
  readonly kind: 'command' | 'result' | 'error';
  readonly text: string;
}

/**
 * Panel terminala (issue #25) — wołany z ikony w nagłówku (`App`). Jedna linia = jedno wywołanie
 * `POST /api/cli/execute`; sama składnia komend i pełna lista żyją po stronie API (`help`), więc
 * front nie duplikuje żadnej wiedzy o tym, co wolno wpisać.
 */
@Component({
  selector: 'app-terminal',
  imports: [FormsModule, NzDrawerModule, TranslatePipe],
  templateUrl: './terminal.html',
  styleUrl: './terminal.scss',
})
export class Terminal {
  private readonly http = inject(HttpClient);
  private readonly errors = inject(ErrorMessages);
  protected readonly service = inject(TerminalService);

  protected readonly input = signal('');
  protected readonly entries = signal<readonly TerminalEntry[]>([]);
  protected readonly busy = signal(false);

  private history = loadHistory();
  private historyIndex: number | null = null;

  protected close(): void {
    this.service.close();
  }

  protected async submit(): Promise<void> {
    const line = this.input().trim();
    if (!line || this.busy()) return;

    this.entries.update((e) => [...e, { kind: 'command', text: line }]);
    this.history = appendHistory(line);
    this.historyIndex = null;
    this.input.set('');
    this.busy.set(true);

    try {
      const result = await firstValueFrom(this.http.post('/api/cli/execute', { line }));
      this.entries.update((e) => [...e, { kind: 'result', text: JSON.stringify(result, null, 2) }]);
    } catch (err) {
      this.entries.update((e) => [...e, { kind: 'error', text: this.errors.of(err) }]);
    } finally {
      this.busy.set(false);
    }
  }

  /** Strzałka góra: jak w prawdziwym terminalu — od najnowszej wpisanej komendy wstecz. */
  protected historyUp(): void {
    if (this.history.length === 0) return;
    this.historyIndex = this.historyIndex === null
      ? this.history.length - 1
      : Math.max(0, this.historyIndex - 1);
    this.input.set(this.history[this.historyIndex]);
  }

  /** Strzałka dół: w stronę najnowszej, a za nią z powrotem do pustego pola. */
  protected historyDown(): void {
    if (this.historyIndex === null) return;

    if (this.historyIndex >= this.history.length - 1) {
      this.historyIndex = null;
      this.input.set('');
      return;
    }

    this.historyIndex += 1;
    this.input.set(this.history[this.historyIndex]);
  }
}
