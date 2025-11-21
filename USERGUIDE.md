# ProgramMover - Benutzerhandbuch

## Inhaltsverzeichnis

1. [Einführung](#einführung)
2. [Vor dem Start](#vor-dem-start)
3. [Installation](#installation)
4. [Schritt-für-Schritt Anleitung](#schritt-für-schritt-anleitung)
5. [Kategorien und Scoring](#kategorien-und-scoring)
6. [DryRun vs. Live Migration](#dryrun-vs-live-migration)
7. [Troubleshooting](#troubleshooting)
8. [FAQ](#faq)

---

## Einführung

ProgramMover ist ein Werkzeug zur sicheren Migration von installierten Programmen von Laufwerk C: nach D:. Das Tool nutzt Junction-Links (symbolische Links), um die Kompatibilität zu wahren, während die tatsächlichen Dateien auf dem Ziellaufwerk liegen.

### Warum ProgramMover?

- **Automatisch**: Scannt und analysiert automatisch installierte Programme
- **Sicher**: DryRun-Modus, automatisches Rollback, Backup-Verzeichnisse
- **Intelligent**: Scoring-System bewertet Verschiebbarkeit
- **Transparent**: Detaillierte Logs und Berichte

---

## Vor dem Start

### Systemanforderungen

✅ **Erforderlich:**
- Windows 10 oder Windows 11 (64-bit)
- Administrator-Rechte
- Ziellaufwerk D: mit NTFS-Dateisystem
- Mindestens 10 GB freier Speicherplatz auf D:

### Backup erstellen

⚠️ **WICHTIG**: Erstellen Sie VOR der Nutzung ein vollständiges System-Backup!

**Empfohlene Methoden:**
1. Windows System-Image (Systemsteuerung → Sichern und Wiederherstellen)
2. Externe Backup-Software (Acronis, Macrium Reflect, etc.)
3. VM-Snapshot (falls in virtueller Umgebung)

### Programme schließen

Schließen Sie alle laufenden Programme, besonders:
- Browser
- Office-Anwendungen
- Entwicklungsumgebungen
- Spiele

---

## Installation

**Keine Installation erforderlich!**

1. `ProgramMover.exe` herunterladen
2. Rechtsklick auf `ProgramMover.exe`
3. "Als Administrator ausführen" wählen
4. UAC-Dialog mit "Ja" bestätigen

---

## Schritt-für-Schritt Anleitung

### Schritt 1: Willkommen und Sicherheitshinweise

![Welcome Screen]

- Lesen Sie die Sicherheitshinweise sorgfältig
- Bestätigen Sie, dass Sie ein Backup erstellt haben
- Klicken Sie "Weiter"

### Schritt 2: Sicherheitsprüfungen

Das Tool prüft automatisch:

✓ **Administrator-Rechte**: Sind Sie als Admin angemeldet?
✓ **Ziellaufwerk D:**: Existiert es? Ist es NTFS? Genug Platz?
✓ **Wiederherstellungspunkt**: Wird versucht zu erstellen (optional)

**Mögliche Warnungen:**
- Problematische Software erkannt (Anti-Cheat, Antivirus, EDR)
- Laufende Prozesse, die gestoppt werden sollten
- Wiederherstellungspunkt konnte nicht erstellt werden (nicht kritisch)

### Schritt 3: System-Scan

Das Tool scannt folgende Quellen:

- Registry Uninstall Keys (HKLM, HKCU)
- Windows Services
- Start-Menü Verknüpfungen
- Program Files Verzeichnisse
- Windows Store Apps

**Dauer**: 30 Sekunden bis 2 Minuten (je nach Anzahl Programme)

### Schritt 4: Analyse

Programme werden kategorisiert und bewertet:

**Kategorien:**
- 🟢 **MoveableAuto**: Sicher automatisch verschiebbar (Score ≥ 75)
- 🟡 **MoveableCaution**: Mit Vorsicht verschiebbar (Score 40-74)
- 🔴 **NotMoveable**: Nicht empfohlen (Score < 40)

### Schritt 5: Programmauswahl

![Selection Screen]

**Empfehlung**: Nur "MoveableAuto" auswählen!

**Spalten:**
- **Auswählen**: Checkbox zum Auswählen
- **Name**: Programmname
- **Kategorie**: MoveableAuto / MoveableCaution / NotMoveable
- **Score**: Bewertung 0-100
- **Größe**: Speicherplatzbedarf
- **Pfad**: Installationsverzeichnis

**Buttons:**
- "Alle 'MoveableAuto' auswählen": Wählt alle sicheren Programme
- Einzelne Programme können manuell an-/abgewählt werden

### Schritt 6: Migrationsplan

Zeigt detaillierte Informationen:

- Anzahl ausgewählter Programme
- Anzahl geplanter Schritte
- Geschätzte Dauer
- Gesamtgröße

**Typische Schritte pro Programm:**
1. PreCheck (Vorprüfung)
2. StopService (falls Services vorhanden)
3. BackupRegistry (für MSI)
4. RobocopyFiles (Dateien kopieren)
5. VerifyFiles (Überprüfung)
6. RenameSource (Original → .old)
7. CreateJunction (Symbolischer Link)
8. StartService (Services neu starten)
9. VerifyService (Service-Check)
10. SmokeTest (Funktionstest)

### Schritt 7: DryRun

**Wichtig**: Führen Sie IMMER zuerst einen DryRun durch!

- Simuliert alle Schritte
- Macht KEINE Änderungen
- Zeigt potenzielle Probleme
- Dauer: ~1-2 Minuten

**Nach erfolgreichem DryRun**: "Live ausführen" wird aktiviert

### Schritt 8: Live-Migration

⚠️ **Letzte Warnung**: Echte Änderungen werden durchgeführt!

**Während der Migration:**
- Fortschrittsbalken zeigt Prozentsatz
- Aktuelle Aktion wird angezeigt
- Logs werden in Echtzeit aktualisiert
- **NICHT** abbrechen (nur im Notfall)

**Bei Fehler:**
- Automatischer Rollback wird gestartet
- Original-Zustand wird wiederhergestellt
- .old-Verzeichnisse bleiben erhalten

**Bei Erfolg:**
- Original-Verzeichnisse → .old umbenannt
- Junctions zeigen auf D:\
- Programme sollten funktionieren

### Schritt 9: Überwachung

Quick Health Check:

✓ **Junction-Gültigkeit**: Sind Links korrekt?
✓ **Service-Status**: Laufen Services?
✓ **Prozess-Start**: Sind Executables erreichbar?

**Gesamtstatus:**
- 🟢 **OK**: Alles funktioniert
- 🟡 **Degraded**: Kleine Probleme, aber nutzbar
- 🔴 **Error**: Schwerwiegende Probleme

### Schritt 10: Abschluss

**Nächste Schritte:**

1. **Testen Sie Programme**: Starten Sie jedes verschobene Programm
2. **Überwachen Sie 72 Stunden**: Achten Sie auf Fehler
3. **Event-Logs prüfen**: Windows Event Viewer → Application
4. **.old-Verzeichnisse behalten**: Mindestens 1 Woche!

**Aufräumen (nach Testphase):**
- Button "Cleanup: .old-Verzeichnisse anzeigen"
- Zeigt alle Backup-Verzeichnisse
- Löschen Sie manuell bei Bedarf

---

## Kategorien und Scoring

### Scoring-Faktoren

**Positive Faktoren (+Punkte):**
- ✓ Installationsverzeichnis vorhanden: +15
- ✓ Standard Program Files Pfad: +10
- ✓ Portable Installation: +20
- ✓ EXE-Installer: +10
- ✓ Bekannter guter Publisher: +10
- ✓ Kleine Größe (<1 GB): +5

**Negative Faktoren (-Punkte):**
- ✗ System-Dateien (.sys): -30
- ✗ Services vorhanden: -10
- ✗ MSI-Installation: -15
- ✗ Microsoft Publisher: -15
- ✗ Sehr groß (>100 GB): -10
- ✗ Problematische Keywords: -25

**Blockiert (Score = 0):**
- 🚫 C:\Windows Pfad
- 🚫 System32-Dateien
- 🚫 Kritische Services
- 🚫 Store-Apps

### Kategorie-Schwellwerte

```
Score 75-100 → MoveableAuto    (Grün)
Score 40-74  → MoveableCaution (Gelb)
Score 0-39   → NotMoveable     (Rot)
```

### Beispiele

**MoveableAuto (Score 85):**
- 7-Zip
- Notepad++
- VLC Media Player
- FileZilla
- Paint.NET

**MoveableCaution (Score 55):**
- Adobe Reader
- Größere Entwicklungstools
- Programme mit Services (nicht-kritisch)

**NotMoveable (Score 15):**
- Microsoft Office (MSI + komplexe Abhängigkeiten)
- Visual Studio (zu viele System-Integrationen)
- Antivirus-Software
- Virtual Machine Software (VMware, VirtualBox)

---

## DryRun vs. Live Migration

### DryRun (Simulation)

**Zweck:**
- Testen ohne Risiko
- Potenzielle Probleme erkennen
- Plan überprüfen

**Was passiert:**
- Alle Schritte werden protokolliert
- KEINE Dateien verschoben
- KEINE Junctions erstellt
- KEINE Services gestoppt

**Ausgabe:**
- Detaillierte Logs
- Schritt-für-Schritt Simulation
- Geschätzte Dauer

### Live Migration

**Was passiert:**
- Echte Dateioperationen mit robocopy
- Services werden gestoppt/gestartet
- Original-Verzeichnisse → .old
- Junctions werden erstellt

**Sicherheitsmechanismen:**
- .old-Backups bleiben erhalten
- Automatischer Rollback bei Fehler
- Datei-Verifikation nach Copy
- Service-Status-Checks

---

## Troubleshooting

### Problem: "Administrator-Rechte fehlen"

**Lösung:**
1. Rechtsklick auf ProgramMover.exe
2. "Als Administrator ausführen"
3. UAC-Dialog mit "Ja" bestätigen

### Problem: "Laufwerk D: nicht vorhanden"

**Lösungen:**
- Partition erstellen in Datenträgerverwaltung
- Externes Laufwerk anschließen und als D: mounten
- Im Code Ziellaufwerk ändern (für erfahrene Nutzer)

### Problem: "Nicht genug Speicherplatz"

**Berechnung:**
- Benötigt: Summe aller ausgewählten Programme
- Reserve: +20% für Overhead
- Minimum: 10 GB

**Lösung:**
- Weniger Programme auswählen
- Ziellaufwerk aufräumen
- Größeres Laufwerk verwenden

### Problem: "Service lässt sich nicht stoppen"

**Mögliche Ursachen:**
- Service ist kritisch
- Abhängigkeiten existieren
- Keine Berechtigung

**Lösung:**
- Dieses Programm abwählen
- Service manuell stoppen (services.msc)
- Im Safe Mode ausführen (nicht empfohlen)

### Problem: "Programm funktioniert nach Migration nicht"

**Erste Hilfe:**
1. Rollback ausführen (falls verfügbar)
2. .old-Verzeichnis zurück umbenennen
3. Junction löschen (cmd: `rmdir /S ProgramPath`)
4. .old umbenennen zu Original

**Manuelle Schritte:**
```cmd
cd "C:\Program Files"
rmdir "7-Zip"           # Junction löschen
ren "7-Zip.old" "7-Zip" # Restore
```

### Problem: "Migration hängt bei einem Schritt"

**Beobachten:**
- Warten Sie 2x Timeout (z.B. 10 Minuten)
- Prüfen Sie Task Manager → Prozesse

**Notfall-Abbruch:**
- Nur im äußersten Notfall!
- Task Manager → ProgramMover beenden
- System neu starten
- .old-Verzeichnisse manuell zurück

### Problem: "Logs zeigen Fehler"

**Log-Dateien:**
```
D:\mover\logs\
- inventory.json         (Gescannte Programme)
- inventory_scored.json  (Mit Scores)
- plan.json             (Migrationsplan)
- execution_report.json (Ergebnis)
- log_YYYYMMDD_HHMMSS.jsonl (Detaillierte Logs)
- robocopy_*.log        (Kopier-Details)
```

**Analysieren:**
- Öffnen mit Texteditor
- Suchen nach "Error" oder "Exception"
- Timestamp beachten
- AppId/StepId notieren

---

## FAQ

### F: Kann ich mehrere Laufwerke als Ziel nutzen?

**A:** Aktuell nur D: unterstützt. Änderung des Ziellaufwerks erfordert Code-Anpassung in `AppConfiguration`.

### F: Was passiert mit Registry-Einträgen?

**A:** Registry wird NICHT geändert. Programme nutzen den Original-Pfad, der durch Junction auf neuen Speicherort zeigt.

### F: Funktionieren Updates nach Migration?

**A:** Ja, für die meisten Programme. Updates schreiben über Junction in Zielverzeichnis.

### F: Kann ich einzelne Dateien/Ordner zurück auf C: verschieben?

**A:** Ja, manuell:
1. Junction löschen
2. Benötigte Dateien von D: nach C: kopieren
3. Rest auf D: belassen (nicht empfohlen, komplex)

### F: Unterstützt das Tool auch Linux/Mac?

**A:** Nein, nur Windows. Junction-Links sind Windows-spezifisch.

### F: Wie lange dauert die Migration?

**A:** Abhängig von Größe:
- <10 GB: 5-15 Minuten
- 10-50 GB: 15-45 Minuten
- 50-100 GB: 45-90 Minuten
- >100 GB: 1.5+ Stunden

**Faktoren:**
- Festplatten-Geschwindigkeit (SSD vs. HDD)
- Anzahl Dateien
- Services (Stop/Start dauert)

### F: Kann ich die .old-Verzeichnisse sofort löschen?

**A:** NEIN! Warten Sie mindestens:
- 1 Woche: Bei unkritischen Programmen
- 1 Monat: Bei wichtigen Anwendungen
- Nach erfolgreichen Windows-Updates

### F: Was wenn Windows Updates installiert werden?

**A:** Meist kein Problem. Windows erkennt Junctions. Bei Problemen:
- .old-Verzeichnisse verfügbar für Restore
- System-Wiederherstellung nutzen

### F: Unterstützt das Tool Spiele (Steam, Epic)?

**A:** Teilweise:
- ✓ Portable Spiele: Ja
- ✓ Standalone: Ja
- ✗ Steam/Epic: Besser Launcher-eigene Move-Funktion nutzen
- ✗ Anti-Cheat: NICHT verschieben!

### F: Kann ich MSI-Programme verschieben?

**A:** Mit Vorsicht:
- MSI trackt Installationspfade in Registry
- Junctions funktionieren meist
- Repair/Modify könnte Probleme verursachen
- Empfehlung: Deinstall → Reinstall nach D:

### F: Wie mache ich Rollback?

**A:** Automatisch bei Fehler, manuell:

```cmd
# Beispiel für 7-Zip
cd "C:\Program Files"
rmdir "7-Zip"              # Junction entfernen
ren "7-Zip.old" "7-Zip"    # Backup wiederherstellen
rd /s /q "D:\Program Files\7-Zip"  # Optional: Aufräumen
```

---

## Support

### Logs sammeln

Vor Support-Anfrage:

1. Alle Logs aus `D:\mover\logs\` sammeln
2. Screenshots von Fehlermeldungen
3. Systeminfo (Windows-Version, RAM, etc.)

### Bekannte Einschränkungen

- Keine Store-App-Migration (Windows 10/11 Einstellungen nutzen)
- MSI-Programme können Registry-Probleme verursachen
- Anti-Cheat wird blockiert
- Kernel-Treiber werden blockiert

### Community

- GitHub Issues: [Repository-URL]
- Diskussionen: [Forum-URL]

---

**Version**: 1.0.0  
**Letztes Update**: 2025-01-21  
**Autor**: ProgramMover Team
