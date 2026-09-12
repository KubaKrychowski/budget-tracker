import { BudgetOption } from './budget-option';
import { BudgetPoint } from './budget-point';
import { CategorySpend } from './category-spend';
import { DashboardMetrics } from './dashboard-metrics';
import { RecentTransaction } from './recent-transaction';

/** Odpowiednik kontraktu z BudgetTracker.Api.Features.Dashboard.Contracts. */
export interface DashboardResponse {
  metrics: DashboardMetrics;
  byCategory: CategorySpend[];
  budgetProgress: BudgetPoint[];
  recentTransactions: RecentTransaction[];
  budgets: BudgetOption[];
  /** Budzet, na ktorym backend faktycznie policzyl limit (wybrany albo domyslny). */
  selectedBudgetId: string | null;
  /** false => aplikacja jest pusta (pierwsze uruchomienie), a nie tylko wybrany okres. */
  hasAnyTransactions: boolean;
}
