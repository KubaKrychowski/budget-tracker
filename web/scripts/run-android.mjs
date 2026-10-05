// Buduje APK (debug) i uruchamia je na emulatorze albo podłączonym telefonie: `npm run android`.
//
// Zastępuje `cap run android`, które na Windowsie wywołuje `./gradlew` i pada na „'gradlew' is not recognized”
// (cmd.exe nie rozumie `./`). Instalację i start robi `native-run` — to samo narzędzie, którego używa Capacitor.
//
// Argumenty przechodzą do native-run, np. `npm run android -- --target pixel_5_-_api_33`
// (lista celów: `npx native-run android --list`). Bez `--target` native-run bierze pierwsze urządzenie,
// a gdy żadne nie działa — uruchamia emulator.
import { spawnSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { join, resolve } from 'node:path';

const windows = process.platform === 'win32';
const androidDir = resolve(import.meta.dirname, '..', 'android');
const apk = join(androidDir, 'app', 'build', 'outputs', 'apk', 'debug', 'app-debug.apk');

/** Wersja główna JDK spod danego katalogu, `null` gdy go nie ma albo nie odpowiada. */
function jdkMajor(home) {
  const java = join(home, 'bin', windows ? 'java.exe' : 'java');
  if (!existsSync(java)) return null;
  // ⚠️ `java -version` pisze na STDERR, nie na stdout.
  const { stdout, stderr } = spawnSync(java, ['-version'], { encoding: 'utf8' });
  const match = /version "(\d+)/.exec(`${stderr}${stdout}`);
  return match ? Number(match[1]) : null;
}

/**
 * Capacitor 8 kompiluje pod Javę 21. Gdy JAVA_HOME wskazuje starsze JDK, bierzemy JDK dołączone do Android Studio
 * (tam, gdzie instalator kładzie je domyślnie), zamiast kazać przestawiać zmienną dla całego systemu.
 */
function resolveJavaHome() {
  const candidates = [
    process.env.JAVA_HOME,
    windows && 'C:\\Program Files\\Android\\Android Studio\\jbr',
    process.platform === 'darwin' && '/Applications/Android Studio.app/Contents/jbr/Contents/Home',
  ].filter(Boolean);

  const home = candidates.find((dir) => (jdkMajor(dir) ?? 0) >= 21);
  if (!home) {
    console.error('Brak JDK 21+. Zainstaluj Android Studio albo ustaw JAVA_HOME na JDK 21.');
    process.exit(1);
  }
  return home;
}

function run(command, args, options) {
  const result = spawnSync(command, args, { stdio: 'inherit', shell: windows, ...options });
  if (result.status !== 0) process.exit(result.status ?? 1);
}

const javaHome = resolveJavaHome();
console.log(`Gradle: JDK z ${javaHome}`);
// Pełna ścieżka, nie samo `gradlew.bat`: przy ustawionym NoDefaultCurrentDirectoryInExePath cmd.exe nie szuka
// programów w katalogu bieżącym, nawet z poprawnym `cwd`.
run(`"${join(androidDir, windows ? 'gradlew.bat' : 'gradlew')}"`, ['assembleDebug'], {
  cwd: androidDir,
  env: { ...process.env, JAVA_HOME: javaHome },
});

/**
 * Bez `--target` native-run nie uruchamia emulatora sam („ERR_NO_TARGET”) — wybieramy więc cel za niego:
 * podłączony telefon albo działający emulator, a gdy nie ma żadnego, pierwszy zdefiniowany AVD.
 */
function defaultTarget() {
  const { stdout } = spawnSync('npx', ['native-run', 'android', '--list', '--json'], { encoding: 'utf8', shell: windows });
  const { devices = [], virtualDevices = [] } = JSON.parse(stdout);
  const target = devices[0] ?? virtualDevices[0];
  if (!target) {
    console.error('Brak telefonu i emulatora. Załóż emulator w Android Studio (Device Manager).');
    process.exit(1);
  }
  return target.id;
}

const args = process.argv.slice(2);
if (!args.includes('--target')) args.push('--target', defaultTarget());

run('npx', ['native-run', 'android', '--app', `"${apk}"`, ...args]);
