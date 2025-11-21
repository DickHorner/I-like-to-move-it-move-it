# ProgramMover - I like to move it, move it!

Your C: drive is full again? 🚀

## Überblick

ProgramMover ist eine Single-EXE Anwendung, die Programme von der Systempartition auf ein anderes Laufwerk verschiebt, ohne die Funktionalität zu beeinträchtigen. Das Tool nutzt Junctions (symbolische Links), um Kompatibilität zu wahren.

## Hauptfunktionen

- **Automatischer Scan**: Erkennt installierte Programme aus Registry, Startmenü, Services und Dateisystem
- **Intelligente Analyse**: Kategorisiert Programme nach Verschiebbarkeit (MoveableAuto, MoveableCaution, NotMoveable)
- **Sicheres Verschieben**: Nutzt Robocopy für ACL-Erhalt und erstellt Junctions
- **DryRun-Modus**: Zeigt geplante Aktionen ohne Ausführung
- **Rollback**: Automatische Wiederherstellung bei Fehlern
- **Monitoring**: Überwacht Programme nach der Migration

## Systemanforderungen

- Windows 10/11 (64-bit)
- Administrator-Rechte erforderlich
- NTFS-Ziellaufwerk (empfohlen: D:)
- .NET 7.0 Runtime (bei self-contained Build nicht erforderlich)

## Installation

Keine Installation erforderlich! Einfach `ProgramMover.exe` als Administrator ausführen.

## Verwendung

1. **Backup erstellen** (wichtig!)
2. Programm als Administrator starten
3. Scan durchführen
4. Programme auswählen
5. DryRun ausführen (optional, empfohlen)
6. Migration starten
7. Monitoring-Phase abwarten

## Sicherheitshinweise

⚠️ **Wichtig:**
- Erstellen Sie vor der Nutzung ein vollständiges System-Backup
- Das Tool erfordert Administrator-Rechte
- Programme mit Treibern, Anti-Cheat oder MSI-Repair können Probleme verursachen
- Niemals System-Komponenten aus `C:\Windows` verschieben

## Architektur

Das Programm ist in folgende Module (Agenten) unterteilt:

- **Scanner Agent**: Erkennt installierte Programme
- **Analyzer Agent**: Bewertet Verschiebbarkeit
- **Planner Agent**: Erstellt Ausführungspläne
- **Executor Agent**: Führt Migration durch
- **Rollback Agent**: Stellt bei Fehlern wieder her
- **Monitor Agent**: Überwacht migirierte Programme
- **Security Agent**: Prüft Berechtigungen und Sicherheit
- **Orchestrator Agent**: Koordiniert alle Prozesse

## Build

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true
```

## Lizenz

Siehe LICENSE Datei

## Haftungsausschluss

Die Nutzung erfolgt auf eigene Gefahr. Erstellen Sie vor der Verwendung immer ein Backup!
