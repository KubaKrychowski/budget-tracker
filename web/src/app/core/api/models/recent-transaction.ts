export interface RecentTransaction {
  /** Publiczny BusinessId (Guid). */
  id: string;
  date: string;
  description: string;
  amount: number;
  categoryName: string | null;
  status: string;
}
