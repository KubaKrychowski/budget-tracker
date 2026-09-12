/**
 * Punkt wykresu obszarowego: BILANS budżetu w danym dniu.
 *
 * Nie ma tu linii limitu — zestawianie bilansu z sumą limitów per kategoria to
 * porównywanie dwóch różnych wielkości. Limity wrócą z Etapem 1 jako osobny widok.
 */
export interface BudgetPoint {
  date: string;
  balance: number;
}
