export interface DashboardMetrics {
  totalExpenses: number;
  totalIncome: number;
  budgetBalance: number;
  topCategoryName: string | null;
  topCategoryAmount: number;
  largestExpenseDescription: string | null;
  largestExpenseAmount: number;
  toReviewCount: number;
}
