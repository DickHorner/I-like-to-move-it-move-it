# plan.md

## Ziel & Anforderungen

Ein portables Windows-Programm (Single EXE) mit GUI für einfache Bedienung, das Programme von `C:\` nach `D:\` migriert. Keine zusätzliche Softwareinstallation erfordert — Benutzer führt nur die EXE als Administrator aus.

---

## Implementierungsentscheidung (zusammengefasst)

* **Implementierung:** C# (.NET 6/7) WinForms, Build: `dotnet publish -r win-x64 -c Release /p:PublishSingleFile=true /p:PublishTrimmed=true /p:SelfContained=true`.
* **Warum:** einfache GUI, starke Bibliotheken für Registry/Services/Process, schnelle Implementierung, gutes Fehler-Handling.
* **Admin:** EXE enthält Manifest `requireAdministrator` (erzwungenes UAC Prompt).
* **Externe Tools:** `robocopy` + `cmd.exe` (mklink) werden aufgerufen — beide vorhanden in Windows 11.

---

## Feature-Roadmap (konkrete Tasks)

### Phase A — Basis (MVP)

1. **Projekt-Setup** – C# WinForms Projekt, Installer-free build, Admin-Manifest.
2. **Scanner** – Registry + Startmenu + Services + Filesize → `inventory.json`.
3. **Analyzer** – Scoring, Category assignment → `inventory_scored.json`.
4. **UI** – Wizard: Scan → Analyse/Ergebnisse anzeigen (Liste mit Kategorie/Score). DryRun Button.
5. **Logging** – JSONL, lokal, lesbar.
6. **Build/Packaging** – single EXE build + README.

**Akzeptanzkriterien:** Scan zeigt erwartete Apps; DryRun erzeugt Plan, keine Änderungen.

---

### Phase B — Safe Executor & Rollback

1. **Planner** – erzeugt `plan.json` mit Steps für selektierte Apps.
2. **Executor (Safe)** – führt Steps aus, nutzt `robocopy` und `mklink /J`. Führt ServiceStop/Start durch. Bei Fehler: automatischer Rollback.
3. **Rollback** – sichere Umkehr, Restore `.old`.
4. **Monitor** – prüft App-Start und EventLog für N Stunden (konfigurierbar).
5. **UI erweitern** – Statusanzeige, detaillierte Logs, „Undo“-Button.

**Akzeptanzkriterien:** Move einer portablen App (<1GB) erfolgreich; Rollback erfolgreich bei simuliertem Fehler.

---

### Phase C — Edge Cases & Robustheit

1. **MSI Handling:** Erkennung MSI ProductCodes; wenn möglich, MSI-Installationsvariablen verwenden oder Deinstall+Install automatisieren. (Vorsichtig!)
2. **UWP/WindowsApps:** Erkennen und Markieren als `NotMoveable`. Für Store-Apps UI Link auf Settings → Apps → Verschieben.
3. **Launcher/Games:** Integration mit Steam/Epic APIs oder Verwendung Launcher-Move-Funktion.
4. **EDR/AV Detection:** Early detection & user guidance.

**Akzeptanzkriterien:** Deutlich reduzierte manuelle Interventionen; robust gegenüber Upgrades.

---

## UX / UI Design (Kurz)

* **Startscreen:** großes Warnfeld („Backup erstellen / Admin starten“), Button „Scan starten“.
* **Resultlist:** Tabelle mit Spalten: Name | Größe | Score | Kategorie | Empfohlene Aktion | Checkbox (auswählbar). Buttons: `Plan anzeigen`, `DryRun`, `Move Selected`.
* **Progress Wizard:** Fortschrittsbalken, Log Stream, „Pause & Abort“ mit Safe-Abort Verhaltensweise (komplett abbrechen und Rollback).
* **Final Screen:** Ergebnis, Empfehlungen, Button „Backup löschen (nach 7 Tagen)“.

---

## Technische Details & Snippets (Kernfunktionen)

### 1) Manifest (Admin-rechte)

Füge dem Projekt `app.manifest` mit:

```xml
<requestedExecutionLevel level="requireAdministrator" uiAccess="false" />
```

### 2) Registry-Scan (C# Beispiel)

```csharp
using Microsoft.Win32;
List<AppEntry> ScanUninstallKeys() {
  string[] keys = {
    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
  };
  var apps = new List<AppEntry>();
  using (var lm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)) {
    foreach (var key in keys) {
      using (var k = lm.OpenSubKey(key)) {
        if (k == null) continue;
        foreach (var sub in k.GetSubKeyNames()) {
          using (var s = k.OpenSubKey(sub)) {
            var name = s.GetValue("DisplayName") as string;
            if (string.IsNullOrEmpty(name)) continue;
            apps.Add(new AppEntry {
              DisplayName = name,
              Publisher = s.GetValue("Publisher") as string,
              InstallLocation = s.GetValue("InstallLocation") as string,
              UninstallString = s.GetValue("UninstallString") as string
            });
          }
        }
      }
    }
  }
  return apps;
}
```

### 3) Robocopy-Aufruf & ExitCode-Auswertung

```csharp
ProcessStartInfo psi = new ProcessStartInfo("robocopy");
psi.Arguments = $""{src}" "{dst}" /MIR /COPYALL /XJ /R:3 /W:5 /LOG:"{logfile}"";
psi.CreateNoWindow = true; psi.UseShellExecute = false;
var p = Process.Start(psi);
p.WaitForExit();
int code = p.ExitCode; // 0-7 success, >=8 error
```

### 4) Junction anlegen (mklink /J)

```csharp
string cmd = $"/C mklink /J "{src}" "{dst}"";
Process.Start(new ProcessStartInfo("cmd.exe", cmd) { CreateNoWindow = true, UseShellExecute = false }).WaitForExit();
```

### 5) Services stoppen / starten

```csharp
using System.ServiceProcess;
var sc = new ServiceController(serviceName);
if (sc.Status != ServiceControllerStatus.Stopped) {
  sc.Stop(); sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
}
// Start analog
```

### 6) Verification – Dateianschluss & Größe

Zähle Dateien & Summiere Größen in Src/Dst; vergleiche. Optional Probe-SHA für große Dateien.

---

## Tests & QA (konkret)

* **Testumgebung:** VM mit Snapshot; Windows 11 Pro (x64).
* **Testfälle:**

  1. Portable App (keine Services).
  2. App mit Windows Service (Stop/Start).
  3. MSI App / Update → Vorsicht, Breakdown testen.
  4. UWP App → Markiert NotMoveable.
  5. Robuste Rollback: Abbruch während Copy (simulate Kill process).
* **Automatisierte Tests:** Unit tests für Scanner/Analyzer; Integration tests on VM via PowerShell/WinRM scripts.
* **Acceptance:** 1) Keine Datenverluste; 2) App funktionsfähig nach Move; 3) Rollback bringt System in vorherigen Zustand.

---

## Packaging & Signatur

* **Publishing:** `dotnet publish` mit oben genannten Flags.
* **Code Signing:** Stark empfohlen (Authenticode) — erhöht Vertrauen und verhindert UAC/SmartScreen Warnungen.
* **Distribution:** ZIP mit single EXE + README + LICENSE + quickstart PDF (Screenshots).

---

## Sicherheits- / Rechts-Hinweise (zu zeigen im UI)

* „Vor dem Ausführen: Backup erstellen.“
* „Admin-Privilegien erforderlich.“
* „Programme mit Treibern, AntiCheat oder MSI-Repair können Probleme verursachen — wir empfehlen Deinstall+Neuinstall, wenn unsicher.“
* Optionale Checkbox: „Ich habe ein Backup“ — ohne Haken kein Live-Migrate (nur DryRun).

---

## Zeitplan (orientierend)

* **MVP (Phase A):** 1–2 Wochen (1 dev, 0.5 QA) — Scanner, Analyzer, GUI DryRun.
* **Phase B:** +1–2 Wochen — Executor, Rollback, Logs, Monitor.
* **Phase C (Edgecases & Stability):** +2–3 Wochen — MSI Handling, Launcher Integration, erweitertes QA.
  Diese Schätzung geht von einem erfahreneren Entwickler aus, der Windows API & C# kennt.

---

## Deliverables

* `mover.exe` (single file), `README.md`, `UserQuickStart.pdf`, `logs` Ordner, `inventory.json/plan.json` Beispiele, Testsuite (VM Scripts), `CHANGELOG.md`.

---

## Nächste Schritte (Kategorie: To-Dos / Ziele / Ideen)

**Ziele**

* Ein portables, sicheres Migrations-Tool, das Administratoren ohne zusätzliche Software ausführen können.

**To-Dos**

1. Ich implementiere (oder du gibst mir Auftrag) eine MVP-Version in C# mit GUI.
2. Build als self-contained EXE erstellen, Manifest hinzufügen.
3. Testen in einer VM (Snapshot).
4. Pilotlauf auf einer Maschine mit einer portablen App.

**Ideen**

* Später: CLI-Modus für automatisierte Deployments; MSI-Integration; Gruppenrichtlinien-kompatible Reports.
