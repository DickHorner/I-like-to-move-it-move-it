# agents.md

## Zweck

Beschreibung der Komponenten (Agenten) innerhalb des portablen Programms — Verantwortlichkeiten, Schnittstellen, Datenformate und Sicherheitsregeln.

---

## Übersicht Architektur (ein Programm, modulare Komponenten)

Das Programm ist eine einzelne ausführbare Datei, intern in Module (Agenten) gegliedert:

**1. UI / Frontend Agent (WinForms)**

* Wizard-Style: `Scan → Analyse → Plan → Ausführen (DryRun) → Ausführen (Live) → Monitor`.
* Zielgruppe: Nutzer mit wenig Vorkenntnissen. Große Buttons, verständliche Texte, Fortschrittsbalken, prominente Warnhinweise.
* Optionen: „Automatisch alle `MoveableAuto` verschieben“, „Nur Bericht erzeugen“, „Erweiterte Ansicht (für Admins)“.

**2. Orchestrator Agent**

* Koordiniert Ablauf, lädt Module, persistiert Konfiguration/Logs, verwaltet Operationen sequenziell.
* Implementiert Transaktions- und Rollback-Logik (Operationen als Steps mit Umkehrfunktion).

**3. Scanner Agent**

* Quellen:

  * Registry Uninstall Keys: `HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall`, `HKLM:\SOFTWARE\WOW6432Node\...\Uninstall`, `HKCU:\...\Uninstall`.
  * Startmenü Shortcuts (`%ProgramData%` + `%AppData%`).
  * `Get-AppxPackage`-Äquivalent (für UWP erkennen).
  * Services (registry `HKLM\SYSTEM\CurrentControlSet\Services`) — prüft `ImagePath`.
  * Dateisystem: `C:\Program Files`, `C:\Program Files (x86)`.
* Ergebnis: `inventory.json` (Array von App-Objekten, siehe Datenmodell).

**4. Analyzer / Classifier Agent**

* Heuristiken (Scoring): *InstallLocation*, *ServicesPresent*, *sys-Files*, *Publisher*, *EstimatedSize*, *UninstallString*.
* Kategorien: `MoveableAuto`, `MoveableCaution`, `NotMoveable`.
* Output: `inventory_scored.json`.

**5. Planner Agent**

* Erzeugt pro App einen verbindlichen Ablaufplan – Schritte mit Metadaten (Timeouts, Prechecks, Rollback-Funktionen).
* Beispiel Schritt: `StopService(ServiceName)`, `Robocopy(src,dst,options)`, `VerifyCounts`, `RenameSrcToOld`, `CreateJunction`, `StartService`, `MonitorSmokeTest`.

**6. Executor Agent**

* Führt Plan aus.
* Nutzt vorhandene Windows-Tools: `robocopy` (für performantes Kopieren und ACL-Erhalt), `cmd.exe /c mklink /J` (für Junction), Windows ServiceController (Stop/Start), Registry-APIs.
* Behandelt ExitCodes, erzeugt JSONL Logs, und ruft Rollback bei schwerwiegenden Fehlern.

**7. Monitor Agent**

* Nach Ausführung überwacht EventLog, Service Status, Prozessstarts für konfigurierbare Zeit (z. B. 72 Std. als Default „Überwachungszeit“).
* Meldet `OK / Degraded / Error`.

**8. Rollback Agent**

* Implementiert Umkehrschritte für jeden Plan-Step. Sicheres Löschen nur nach Bestätigung. `.old`-Ordner werden nur nach bestätigter Monitoring-Periode gelöscht.

**9. Security Agent**

* Prüft Admin-Rechte beim Start (falls nicht vorhanden: Exit mit verständlicher Anleitung).
* Erstellt optionale System-Wiederherstellungspunkt (wenn möglich). Alternativ Hinweis: „Bitte Image/Backup erstellen“.
* Prüft Ziel-Laufwerk D: (NTFS, freier Platz, Schreibrechte).
* ACLs: Executor verwendet `robocopy /COPYALL`, Log der übernommenen ACLs.

**10. Updater & Telemetry (optional)**

* Minimal optionales Modul für Updates (manuell/offline) und anonymisierte Opt-in Telemetrie (für Debugging). Nicht automatisch aktiv.

---

## Datenmodell (Kurz)

**App-Entry (JSON):**

```json
{
  "Id":"guid",
  "DisplayName":"7-Zip",
  "Publisher":"Igor Pavlov",
  "InstallLocation":"C:\\Program Files\\7-Zip",
  "UninstallString":"\"C:\\Program Files\\7-Zip\\uninstall.exe\"",
  "InstallType":"msi|exe|portable|store",
  "Services":["7zSvc"],
  "HasSysFiles":false,
  "FilesCount":123,
  "TotalSizeBytes":12345678,
  "Score":85,
  "Category":"MoveableAuto",
  "SuggestedAction":"Robocopy+Junction",
  "LastScan":"1.1.2025T12:00:00+01:00"
}
```

---

## Schnittstellen intern / Logs

* **Plan/Execution Logs:** JSONL in `D:\mover\logs\` (Fallback `C:\mover\logs\` wenn D: nicht vorhanden).
* **Export/Import:** `inventory.json`, `plan.json`, `execution_report.json` für Audits.
* **Error Codes:** Standardisierte Exitcodes (0 OK, 1 UserErr, 2 NotMoveable, 3 CopyError, 4 VerificationError, 5 RollbackSuccess, 6 RollbackFailed).

---

## Sicherheitsregeln / harte Verbote

* **Nie** versuchen, `C:\Windows` oder Inhalte aus `C:\Windows\System32` zu verschieben.
* **Keine automatische Verschiebung** für `NotMoveable` oder `MoveableCaution` ohne explizite Admin-Bestätigung.
* **Anti-Cheat / EDR / Virenschutz:** Vor der Ausführung prüfen; wenn Prozesse mit verdächtigem Verhalten vorhanden sind, Warnung und Abbruch.
* **Backup:** Immer kopieren (nie verschieben). `.old` Versionen mindestens bis Monitoring-Phase aufbewahren.