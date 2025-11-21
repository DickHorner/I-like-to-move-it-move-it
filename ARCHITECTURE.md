# ProgramMover - Architektur & Technische Dokumentation

## Überblick

ProgramMover ist eine modulare WinForms-Anwendung, die nach dem Agent-Muster aufgebaut ist. Jeder Agent ist für einen spezifischen Aspekt der Migration verantwortlich.

## Architektur-Diagramm

```
┌─────────────────────────────────────────────────────────────┐
│                         MainForm (UI)                        │
│                      Wizard-Style GUI                        │
└────────────────────────────┬────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────┐
│                   OrchestratorAgent                          │
│                 (Koordiniert alle Agenten)                   │
└─┬───────┬───────┬────────┬────────┬────────┬────────┬───────┘
  │       │       │        │        │        │        │
  ▼       ▼       ▼        ▼        ▼        ▼        ▼
┌───┐   ┌───┐   ┌───┐   ┌───┐   ┌───┐   ┌───┐   ┌───┐
│Sec│   │Scn│   │Ana│   │Pln│   │Exe│   │Rol│   │Mon│
│uri│   │ner│   │lyz│   │ner│   │cut│   │lbk│   │ito│
│ty │   │   │   │er │   │   │   │or │   │   │   │r  │
└───┘   └───┘   └───┘   └───┘   └───┘   └───┘   └───┘
```

## Komponenten

### 1. Models (Datenmodelle)

#### AppEntry.cs
Repräsentiert eine installierte Anwendung mit allen Metadaten.

**Properties:**
- `Id`: Eindeutige GUID
- `DisplayName`: Anzeigename
- `Publisher`: Herausgeber
- `InstallLocation`: Installationspfad
- `InstallType`: MSI, EXE, Portable, Store, Service
- `Services`: Liste von Windows-Services
- `Score`: Bewertung 0-100
- `Category`: MoveableAuto, MoveableCaution, NotMoveable

#### MigrationPlan.cs & MigrationStep.cs
Definiert den Ablaufplan für die Migration.

**Step Types:**
- PreCheck
- StopService
- BackupRegistry
- RobocopyFiles
- VerifyFiles
- RenameSource
- CreateJunction
- StartService
- VerifyService
- SmokeTest
- Cleanup

#### Common.cs
Gemeinsame Modelle wie `ExecutionLog`, `AppConfiguration`, Exit-Codes.

### 2. Agents (Geschäftslogik)

#### SecurityAgent
**Verantwortlichkeiten:**
- Admin-Rechte prüfen
- Ziellaufwerk validieren
- Wiederherstellungspunkt erstellen
- Problematische Software erkennen
- Prozess-Konflikte identifizieren

**Key Methods:**
```csharp
bool IsAdministrator()
(bool, string) ValidateTargetDrive(string targetDrive)
(bool, string) CreateRestorePoint()
SecurityCheckResult PerformSecurityChecks(...)
```

#### ScannerAgent
**Verantwortlichkeiten:**
- Registry-Scan (Uninstall Keys)
- Service-Erkennung
- Start-Menü Analyse
- Dateisystem-Scan
- Store-App-Erkennung

**Quellen:**
- `HKLM:\SOFTWARE\...\Uninstall`
- `HKCU:\SOFTWARE\...\Uninstall`
- `HKLM:\SYSTEM\CurrentControlSet\Services`
- `%ProgramData%\Microsoft\Windows\Start Menu`
- `Get-AppxPackage` (PowerShell)

**Key Methods:**
```csharp
List<AppEntry> ScanSystem()
List<AppEntry> ScanRegistryUninstallKeys()
List<AppEntry> ScanServices()
```

#### AnalyzerAgent
**Verantwortlichkeiten:**
- Scoring-System
- Kategorisierung
- Heuristiken anwenden

**Scoring-Logik:**
```
Base Score: 50

+15: InstallLocation vorhanden
+10: Standard Program Files
+20: Portable
+10: EXE-Installer
-15: MSI
-10: Services
-30: .sys-Dateien
-25: Problematische Keywords
= 0: Windows-Verzeichnis (BLOCKED)
```

**Key Methods:**
```csharp
List<AppEntry> AnalyzeApps(List<AppEntry> apps)
void AnalyzeApp(AppEntry app)
```

#### PlannerAgent
**Verantwortlichkeiten:**
- Migrationsplan erstellen
- Schritte generieren
- Reihenfolge festlegen
- Zeitschätzung

**Plan-Struktur:**
```
Für jede App:
1. PreCheck
2. StopServices (falls vorhanden)
3. BackupRegistry (für MSI)
4. RobocopyFiles
5. VerifyFiles
6. RenameSource (.old)
7. CreateJunction
8. StartServices
9. VerifyService
10. SmokeTest
```

**Key Methods:**
```csharp
MigrationPlan CreatePlan(List<AppEntry> apps, bool isDryRun)
List<MigrationStep> CreateStepsForApp(AppEntry app, ref int stepOrder)
```

#### ExecutorAgent
**Verantwortlichkeiten:**
- Plan ausführen
- Robocopy aufrufen
- Junctions erstellen
- Services steuern
- Fehlerbehandlung

**Tools:**
- `robocopy.exe /MIR /COPYALL /XJ`
- `cmd.exe /C mklink /J`
- `ServiceController` (System.ServiceProcess)
- `reg.exe export` (für Registry-Backup)

**Key Methods:**
```csharp
Task<ExecutionResult> ExecutePlan(MigrationPlan plan, IProgress<>)
Task ExecuteStep(MigrationStep step)
Task ExecuteRobocopy(MigrationStep step)
Task ExecuteCreateJunction(MigrationStep step)
```

**Robocopy Exit Codes:**
```
0-7: Erfolg (verschiedene Stufen)
8+: Fehler
```

#### RollbackAgent
**Verantwortlichkeiten:**
- Rollback bei Fehler
- Schritt-Umkehrung
- .old-Verzeichnisse verwalten
- Cleanup nach Erfolg

**Rollback-Logik:**
```
Reverse Order der Schritte:
- Junction löschen
- .old zurück umbenennen
- Services wiederherstellen
- Logs schreiben
```

**Key Methods:**
```csharp
Task<RollbackResult> RollbackPlan(MigrationPlan plan, List<MigrationStep>)
Task RollbackStep(MigrationStep step)
Task<CleanupResult> CleanupOldDirectories(List<AppEntry>, bool confirm)
```

#### MonitorAgent
**Verantwortlichkeiten:**
- Post-Migration-Überwachung
- Service-Status prüfen
- Junction-Validität
- Prozess-Start-Tests
- EventLog-Analyse

**Checks:**
- Services laufen?
- Junction existiert und funktioniert?
- Executables erreichbar?
- Event-Log-Fehler?

**Key Methods:**
```csharp
Task<MonitoringResult> MonitorApps(List<AppEntry> apps)
Task<MonitoringResult> QuickHealthCheck(List<AppEntry> apps)
bool CheckJunction(AppEntry app)
```

#### OrchestratorAgent
**Verantwortlichkeiten:**
- Workflow-Koordination
- Agent-Verwaltung
- Logging aggregieren
- JSON-Serialisierung
- Exit-Code-Management

**Workflow:**
```
1. PerformSecurityChecks()
2. ScanSystem()
3. AnalyzeApps()
4. CreateMigrationPlan()
5. ExecuteMigration()
   - Bei Fehler: RollbackMigration()
6. MonitorApps()
7. CleanupOldDirectories()
```

**Key Methods:**
```csharp
SecurityCheckResult PerformSecurityChecks()
List<AppEntry> ScanSystem()
MigrationPlan CreateMigrationPlan(List<AppEntry>, bool isDryRun)
Task<ExecutionResult> ExecuteMigration(MigrationPlan, IProgress<>)
```

### 3. UI (MainForm.cs)

**Wizard-Steps:**
```
Welcome
  ↓
SecurityChecks
  ↓
Scanning
  ↓
Analysis
  ↓
Selection
  ↓
Plan
  ↓
DryRun
  ↓
Execution
  ↓
Monitoring
  ↓
Complete
```

**Features:**
- Schritt-Navigation (Vor/Zurück)
- Fortschrittsbalken
- Echtzeit-Logs
- DataGridView für Auswahl
- Async/Await für Background-Tasks

**Key UI Elements:**
- `pnlContent`: Hauptinhalt
- `pnlButtons`: Navigation
- `progressBar`: Fortschritt
- `lblStatus`: Statustext
- `DataGridView`: Programmauswahl

## Datenfluss

### 1. Scan-Phase
```
ScannerAgent → List<AppEntry> (roh)
  ↓
SaveInventory("inventory.json")
```

### 2. Analyse-Phase
```
AnalyzerAgent → List<AppEntry> (mit Score & Category)
  ↓
SaveInventory("inventory_scored.json")
```

### 3. Planung-Phase
```
PlannerAgent → MigrationPlan
  ↓
SavePlan("plan.json")
```

### 4. Ausführung-Phase
```
ExecutorAgent → ExecutionResult
  ↓
SaveExecutionReport("execution_report.json")
  ↓
SaveLogs("log_YYYYMMDD_HHMMSS.jsonl")
```

## Logging

### Log-Struktur (JSONL)
```json
{
  "Timestamp": "2025-01-21T12:34:56.789+01:00",
  "Level": "Info",
  "Category": "Executor",
  "Message": "Starting step: Robocopy files",
  "AppId": "guid-here",
  "StepId": "guid-here",
  "ExitCode": null,
  "Exception": null,
  "Data": {}
}
```

### Log-Levels
- Trace: Sehr detailliert
- Debug: Entwickler-Info
- Info: Normale Operationen
- Warning: Potenzielle Probleme
- Error: Fehler, aber fortfahrbar
- Critical: Schwerwiegende Fehler

### Log-Dateien
```
D:\mover\logs\
├── inventory.json              (Scan-Ergebnis)
├── inventory_scored.json       (Mit Analyse)
├── plan.json                   (Migrationsplan)
├── execution_report.json       (Ergebnis)
├── log_20250121_123456.jsonl   (JSONL-Logs)
├── robocopy_guid_timestamp.log (Robocopy Details)
└── registry_backup_guid.reg    (Registry Backup)
```

## Fehlerbehandlung

### Fehler-Kategorien

**Kritische Fehler (Abort + Rollback):**
- Robocopy-Fehler (ExitCode ≥ 8)
- Verifikation fehlgeschlagen
- Junction-Erstellung fehlgeschlagen

**Nicht-kritische Fehler (Warning + Continue):**
- Service-Stop-Timeout
- Registry-Backup fehlgeschlagen
- EventLog nicht lesbar

### Rollback-Prozess
```
1. Fehler erkannt
2. ExecutorAgent stoppt
3. RollbackAgent gestartet
4. Schritte in umgekehrter Reihenfolge
5. Junction entfernen
6. .old zurück umbenennen
7. Services wiederherstellen
8. Logs schreiben
9. Exit mit Code 5 (RollbackSuccess) oder 6 (RollbackFailed)
```

## Sicherheitsmechanismen

### 1. Prävention
- Admin-Check vor Start
- Ziellaufwerk-Validierung
- Problematische Software-Erkennung
- Prozess-Konflikt-Check
- Backup-Bestätigung (UI)

### 2. Während Migration
- DryRun-Modus
- .old-Backups
- Datei-Verifikation (Count + Size)
- Service-Status-Checks
- Schritt-für-Schritt Logging

### 3. Nach Migration
- Monitoring
- Junction-Validierung
- Service-Health-Checks
- EventLog-Analyse
- 72h Beobachtungszeit (empfohlen)

### 4. Wiederherstellung
- Automatischer Rollback
- Manueller Restore via .old
- System-Wiederherstellungspunkt
- Komplette Logs für Forensik

## Performance-Überlegungen

### Robocopy-Parameter
```
/MIR      - Mirror (effiziente Synchronisation)
/COPYALL  - Alle Attribute + ACLs
/XJ       - Junctions überspringen (keine Rekursion)
/R:3      - 3 Wiederholungen
/W:5      - 5 Sekunden Wartezeit
/LOG      - Detailliertes Log
/NP       - Kein Prozent (weniger Output)
/NDL /NFL - Keine Verzeichnis-/Datei-Listen (schneller)
```

### Schätzungen
- 10 MB/s: Durchschnittliche Kopiergeschwindigkeit
- 2 Min/App: Overhead pro Programm
- 10 Schritte/App: Standard-Schrittanzahl

## Erweiterbarkeit

### Neue Step-Types hinzufügen
1. Enum `StepType` erweitern
2. `ExecutorAgent.ExecuteStep()` switch erweitern
3. `RollbackAgent.RollbackStep()` switch erweitern
4. Neue Methoden implementieren

### Neues Ziellaufwerk
`AppConfiguration.TargetDrive` ändern (Standard: "D:")

### Zusätzliche Scanner-Quellen
In `ScannerAgent` neue Methode:
```csharp
private List<AppEntry> ScanCustomSource()
{
    // Implementierung
}
```
Dann in `ScanSystem()` aufrufen und mergen.

### Erweiterte Analyse-Heuristiken
In `AnalyzerAgent.AnalyzeApp()`:
```csharp
// Neue Regel hinzufügen
if (app.CustomCondition)
    score += 15;
```

## Build & Deployment

### Build-Kommando
```bash
dotnet publish -c Release -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=true \
  -p:EnableCompressionInSingleFile=true
```

### Output
```
bin/Release/net7.0-windows/win-x64/publish/
└── ProgramMover.exe (ca. 80-120 MB)
```

### Code-Signierung (Optional)
```powershell
signtool sign /f cert.pfx /p password /t http://timestamp.digicert.com ProgramMover.exe
```

## Testing

### Unit Tests (Geplant)
- ScannerAgent.ScanRegistryUninstallKeys()
- AnalyzerAgent.AnalyzeApp()
- PlannerAgent.CreateStepsForApp()

### Integration Tests (VM)
```powershell
# Test-Skript
1. Snapshot erstellen
2. ProgramMover ausführen
3. DryRun testen
4. Live-Migration (Test-App)
5. Rollback testen
6. Snapshot wiederherstellen
```

### Testfälle
1. Portable App (keine Services)
2. App mit Service
3. MSI-Installation
4. Fehlersimulation (Kill während Robocopy)
5. Rollback-Test

## Bekannte Einschränkungen

1. **MSI**: Registry-Tracking kann Probleme verursachen
2. **Store-Apps**: Nicht verschiebbar (Windows-API-Beschränkung)
3. **Anti-Cheat**: Kernel-Level, wird blockiert
4. **Symbolische Links**: Nur auf NTFS
5. **Laufwerk-Buchstabe**: Nur D: (konfigurierbar)

## Zukünftige Erweiterungen

- CLI-Modus für Automation
- Multi-Drive-Support
- Launcher-Integration (Steam, Epic)
- MSI-Repair-Mechanismus
- Telemetrie (Opt-in)
- Auto-Update
- Geplante Migrationen

---

**Version**: 1.0.0  
**Letzte Aktualisierung**: 2025-01-21  
**Autor**: ProgramMover Team
