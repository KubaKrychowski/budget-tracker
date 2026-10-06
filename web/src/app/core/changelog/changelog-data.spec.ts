import changelog from '../../../../public/changelog.json';
import { isNewerVersion } from '../app-update/app-update.service';
import { ChangelogRelease } from './changelog';

/**
 * Sam plik `public/changelog.json`, nie serwis: to on się zdezaktualizował (wpisy o 2.2–2.4 nie powstały, więc
 * „Co nowego” dalej pokazywało 2.1). Reguły, które muszą trzymać przy każdym dopisanym wydaniu.
 */
describe('public/changelog.json', () => {
  const releases = changelog as ChangelogRelease[];

  it('jest uporządkowany od najnowszego wydania, bez powtórzeń', () => {
    const ids = releases.map((r) => r.id);

    expect(new Set(ids).size).toBe(ids.length);
    for (let i = 1; i < ids.length; i++) expect(isNewerVersion(ids[i - 1], ids[i])).toBe(true);
  });

  it('każde wydanie ma datę i tyle samo pozycji po polsku i po angielsku', () => {
    for (const release of releases) {
      expect(release.date).toMatch(/^\d{4}-\d{2}-\d{2}$/);
      expect(release.pl.length).toBeGreaterThan(0);
      expect(release.en).toHaveLength(release.pl.length);
    }
  });

  it('każda pozycja ma tytuł i opis, a link prowadzi do trasy aplikacji', () => {
    for (const item of releases.flatMap((r) => [...r.pl, ...r.en])) {
      expect(item.title.trim()).not.toBe('');
      expect(item.text.trim()).not.toBe('');
      if (item.link) {
        expect(item.link.startsWith('/')).toBe(true);
        expect(item.linkLabel?.trim()).toBeTruthy();
      }
    }
  });
});
