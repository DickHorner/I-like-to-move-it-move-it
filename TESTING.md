# ProgramMover - Test & Deployment Guide

## Build Status

✅ Build erfolgreich abgeschlossen
- Output: `bin/Release/net8.0-windows/win-x64/publish/ProgramMover.exe`
- Größe: ~69 MB (Single-File, Self-Contained)
- Target: .NET 8.0 Windows x64

## Deployment auf Windows

### Voraussetzungen
- Windows 10/11 (64-bit)
- **Keine** .NET Runtime Installation erforderlich (self-contained)
- Administrator-Rechte zum Ausführen

### Installation
1. Kopieren Sie `ProgramMover.exe` auf einen Windows-Rechner
2. Rechtsklick → "Als Administrator ausführen"
3. UAC-Prompt bestätigen

## Testing auf Windows

### Schnelltest
```cmd
# Als Administrator ausführen
ProgramMover.exe
```

### Erwartetes Verhalten
1. **Welcome Screen** erscheint mit Sicherheitshinweisen
2. Checkbox "Ich habe ein Backup erstellt" muss aktiviert werden
3. Button "Weiter >" wird aktiv
4. Security Checks laufen automatisch
5. System-Scan startet

### Bekannte Einschränkungen
Da das Programm auf **Linux cross-compiled** wurde:

#### ✅ Funktioniert:
- EXE startet auf Windows
- GUI wird angezeigt
- Grundlegende .NET Funktionalität

#### ⚠️ Zu testen auf Windows:
- **Registry-Zugriff** (Microsoft.Win32)
- **Service-Management** (System.ServiceProcess)
- **WMI-Zugriffe** (System.Management) - nur wenn verwendet
- **Admin-Rechte-Erkennung** (WindowsIdentity)
- **Robocopy/mklink Prozessaufrufe**

## Debugging

### Wenn die EXE nicht startet:

1. **Event Viewer prüfen** (Windows Logs → Application)
   ```
   eventvwr.msc
   ```

2. **Dependency Walker verwenden** (für DLL-Fehler)
   - Download: http://www.dependencywalker.com/

3. **Mit Konsole ausführen** (für Fehlerausgabe)
   ```cmd
   # PowerShell als Administrator
   & ".\ProgramMover.exe"
   ```

4. **Logs prüfen**
   - Standardpfad: `D:\mover\logs\` 
   - Fallback: `C:\mover\logs\`

### Wenn Admin-Rechte nicht erkannt werden:

Manifest prüfen:
```cmd
sigcheck.exe -m ProgramMover.exe
```
Sollte zeigen: `requestedExecutionLevel: requireAdministrator`

### Wenn GUI nicht erscheint:

Compatibility Mode testen:
```
Rechtsklick → Eigenschaften → Kompatibilität → "Programm als Administrator ausführen"
```

## Troubleshooting: Cross-Compile Issues

Falls spezifische Windows-APIs nicht funktionieren:

### Option 1: Native Build auf Windows
```cmd
# Auf Windows-Rechner ausführen
git clone <repo>
cd I-like-to-move-it-move-it
dotnet restore ProgramMover.csproj
dotnet publish ProgramMover.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

### Option 2: Trimming deaktivieren
Wenn Reflection-basierte Features fehlen:
```bash
dotnet publish ProgramMover.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false
```

### Option 3: Größe reduzieren (nach Tests)
```bash
# ReadyToRun für schnelleren Start
dotnet publish ProgramMover.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true
```

## Test-Checkliste

- [ ] EXE startet ohne Fehler
- [ ] UAC Prompt erscheint
- [ ] Welcome Screen wird angezeigt
- [ ] Security Checks laufen durch
- [ ] Admin-Rechte werden erkannt
- [ ] Laufwerk D: Validierung funktioniert
- [ ] System-Scan findet installierte Programme
- [ ] Analyzer kategorisiert Programme korrekt
- [ ] Programmauswahl funktioniert
- [ ] DryRun läuft ohne Fehler
- [ ] Plan-Anzeige ist vollständig

## Produktiv-Test (VORSICHT!)

⚠️ **NUR auf Test-VM durchführen!**

- [ ] VM Snapshot erstellen
- [ ] Backup-Test: Portable App (z.B. 7-Zip)
- [ ] DryRun durchführen
- [ ] Live-Migration einer kleinen App
- [ ] Junction-Prüfung: `dir /AL C:\Program Files\`
- [ ] App-Start testen
- [ ] Rollback testen (bei Fehler)

## Support

Bei Problemen:
1. Log-Dateien sammeln aus `D:\mover\logs\` oder `C:\mover\logs\`
2. Event Viewer Export (Application + System)
3. Screenshot von Fehlermeldungen
4. GitHub Issue erstellen mit allen Informationen

## Known Issues & Workarounds

### System.Management auf non-Windows
- Cross-compile funktioniert
- Runtime-Test nur auf Windows möglich

### WinForms Designer
- Designer funktioniert nur auf Windows
- Code wurde manuell erstellt (kein Designer verwendet)

### Performance
- First-run ist langsamer (Self-Contained Unpacking)
- Nachfolgende Starts sind schneller
- Bei großen Installationen kann Scan 1-2 Minuten dauern
