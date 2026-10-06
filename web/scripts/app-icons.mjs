// Ikony i ekran startowy aplikacji mobilnej z logo (public/icons/logo.svg): `node scripts/app-icons.mjs`.
// Tworzy obrazy źródłowe w assets/ i woła `capacitor-assets`, które rozkłada je na wszystkie rozmiary Androida
// (ikona adaptacyjna, ikona okrągła, ekran startowy). Po zmianie logo wystarczy uruchomić to ponownie.
import { execFileSync } from 'node:child_process';
import { mkdirSync, readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import sharp from 'sharp';

const root = resolve(import.meta.dirname, '..');
const out = join(root, 'assets');
mkdirSync(out, { recursive: true });

// Tło ekranu startowego i ikony adaptacyjnej = Neutral/50, to samo co okładka ładowania w index.html.
const background = '#F7FAF8';
const logo = readFileSync(join(root, 'public', 'icons', 'logo.svg'));
const raster = (size) => sharp(logo, { density: 72 * (size / 64) }).resize(size, size).png().toBuffer();

// Ikona „zwykła” (starsze Androidy): samo logo, ono ma własne zaokrąglone tło.
await sharp(await raster(1024)).toFile(join(out, 'icon-only.png'));

// Ikona adaptacyjna: logo w bezpiecznej strefie (ok. 60% boku), bo launcher przycina warstwę do koła lub squircle.
const fg = await raster(620);
await sharp({ create: { width: 1024, height: 1024, channels: 4, background: { r: 0, g: 0, b: 0, alpha: 0 } } })
  .composite([{ input: fg, gravity: 'center' }])
  .png()
  .toFile(join(out, 'icon-foreground.png'));
await sharp({ create: { width: 1024, height: 1024, channels: 4, background } }).png().toFile(join(out, 'icon-background.png'));

// Ekran startowy (Android < 12; od 12 system pokazuje ikonę adaptacyjną na tle splashu).
const splashLogo = await raster(560);
for (const name of ['splash.png', 'splash-dark.png']) {
  await sharp({ create: { width: 2732, height: 2732, channels: 4, background } })
    .composite([{ input: splashLogo, gravity: 'center' }])
    .png()
    .toFile(join(out, name));
}

execFileSync(
  process.platform === 'win32' ? 'npx.cmd' : 'npx',
  ['capacitor-assets', 'generate', '--android', '--splashBackgroundColor', background, '--splashBackgroundColorDark', background],
  { cwd: root, stdio: 'inherit', shell: process.platform === 'win32' },
);
