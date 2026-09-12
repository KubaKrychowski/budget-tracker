import {
  ArrowRightOutline,
  CheckOutline,
  CloseOutline,
  GithubOutline,
} from '@ant-design/icons-angular/icons';

/**
 * Ikony landingu — rejestrowane pojedynczo, nie całym pakietem.
 *
 * Ta sama zasada co w `web/src/app/core/icons.ts`: `@ant-design/icons-angular` waży kilkaset
 * kilobajtów, a strona używa czterech ikon. Import całości zjadłby budżet bundla, który dla
 * landingu i tak jest napięty.
 */
export const LANDING_ICONS = [
  ArrowRightOutline,
  CheckOutline,
  CloseOutline,
  GithubOutline,
];
