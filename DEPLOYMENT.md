# Deployment Anleitung

## Schritt 1: EXE auf Windows kopieren

Die fertige EXE befindet sich hier:
```
bin/Release/net8.0-windows/win-x64/publish/ProgramMover.exe
```

**Empfohlene Transfer-Methoden:**
- USB-Stick
- Netzwerk-Share
- Cloud (OneDrive, Dropbox)
- Git LFS (für größere Binaries)

## Schritt 2: Auf Windows ausführen

### Erste Ausführung:
```
1. Rechtsklick auf ProgramMover.exe
2. "Als Administrator ausführen" wählen
3. UAC Prompt mit "Ja" bestätigen
```

### Alternativer Start (PowerShell):
```powershell
# PowerShell als Administrator öffnen
Set-Location "C:\Pfad\zu\ProgramMover"
.\ProgramMover.exe
```

## Schritt 3: Wizard durchlaufen

1. **Welcome** - Hinweise lesen, Backup-Checkbox aktivieren
2. **Security Checks** - Automatische Prüfungen
3. **Scanning** - System-Scan läuft
4. **Analysis** - Programme werden kategorisiert
5. **Selection** - Programme auswählen
6. **Plan** - Migrationsplan prüfen
7. **DryRun** - Simulation (empfohlen!)
8. **Execution** - Echte Migration
9. **Monitoring** - Überwachung
10. **Complete** - Abschluss

## Fehlerbehandlung

### "Kein Administrator"
- Programm muss als Administrator gestartet werden
- UAC muss aktiviert sein

### "Laufwerk D: nicht gefunden"
- Ziellaufwerk muss NTFS sein
- Mindestens 10 GB frei
- Schreibrechte erforderlich

### "Programme werden nicht gefunden"
- Warten bis Scan komplett ist
- Manche Programme sind in Registry nicht sichtbar
- Store-Apps werden separat behandelt

## Sicherheit

### Vor dem Start:
- ✅ Vollständiges System-Backup
- ✅ VM Snapshot (wenn VM)
- ✅ Wichtige Daten extern sichern

### Während der Nutzung:
- ⚠️ Keine anderen Programme ausführen
- ⚠️ Nicht unterbrechen während Migration
- ⚠️ System nicht herunterfahren

### Nach der Migration:
- ✓ .old Verzeichnisse bleiben als Backup
- ✓ Erst nach 7 Tagen löschen
- ✓ Monitoring-Phase abwarten

## Logs & Debugging

Alle Logs werden gespeichert in:
- `D:\mover\logs\` (primär)
- `C:\mover\logs\` (fallback)

Dateien:
- `inventory.json` - Gefundene Programme
- `inventory_scored.json` - Mit Bewertung
- `plan.json` - Migrationsplan
- `execution_report.json` - Ausführungsbericht
- `log_*.jsonl` - Detaillierte Logs

## Support

Bei Problemen:
1. Screenshot von Fehlermeldung
2. Log-Dateien aus `C:\mover\logs\` oder `D:\mover\logs\`
3. Event Viewer (eventvwr.msc) → Application Logs
4. GitHub Issue mit allen Infos erstellen

## Empfohlene Test-Umgebung

Ideal für ersten Test:
- Windows 10/11 VM (VirtualBox, VMware, Hyper-V)
- Snapshot vor Test erstellen
- Kleine portable App installieren (z.B. 7-Zip, Notepad++)
- Migration testen
- Bei Erfolg: Snapshot löschen und für echte Nutzung vorbereiten
