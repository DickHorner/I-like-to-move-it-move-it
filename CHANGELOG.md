# ProgramMover - Changelog

## Version 1.0.0 (Initial Release)

### Features

#### Core Functionality
- **System Scanner**: Erkennt installierte Programme aus Registry, Services, Start-Menü und Dateisystem
- **Intelligent Analyzer**: Bewertet Programme nach Verschiebbarkeit mit Scoring-System (0-100)
- **Migration Planner**: Erstellt detaillierte Ablaufpläne mit Rollback-Fähigkeit
- **Safe Executor**: Führt Migration mit robocopy und Junction-Links durch
- **Automatic Rollback**: Stellt System bei Fehlern automatisch wieder her
- **Monitoring**: Überwacht migrierte Programme auf Funktionalität

#### Security
- Administrator-Rechte-Prüfung beim Start
- System-Wiederherstellungspunkt (optional)
- Ziellaufwerk-Validierung (NTFS, Speicherplatz, Schreibrechte)
- Erkennung problematischer Software (Anti-Cheat, EDR, Antivirus)
- Laufende Prozess-Konflikte werden erkannt

#### User Interface
- Wizard-Style GUI für einfache Bedienung
- Schritt-für-Schritt Anleitung: Welcome → Security → Scan → Analysis → Selection → Plan → DryRun → Execution → Monitoring
- DryRun-Modus zum Testen ohne Änderungen
- Echtzeit-Fortschrittsanzeige
- Detaillierte Logs und Statusmeldungen

#### Categories
- **MoveableAuto**: Automatisch sicher verschiebbar (Score ≥ 75)
- **MoveableCaution**: Mit Vorsicht verschiebbar (Score 40-74)
- **NotMoveable**: Nicht empfohlen oder technisch unmöglich

#### Technical
- Single-File Executable (portable, keine Installation)
- Admin-Manifest für UAC-Prompt
- JSONL-Logging für Audit-Trail
- Export von Inventory, Plan und Execution Reports
- Unterstützung für Services (Stop/Start)
- ACL-Erhaltung durch robocopy /COPYALL

### Known Limitations

- MSI-Installationen können Registry-Probleme verursachen
- Store-Apps müssen manuell über Windows-Einstellungen verschoben werden
- Programme mit Kernel-Treibern werden blockiert
- Anti-Cheat-Software sollte nicht verschoben werden

### System Requirements

- Windows 10/11 (64-bit)
- Administrator-Rechte
- .NET 7.0 Runtime (bei self-contained Build nicht erforderlich)
- NTFS-Ziellaufwerk mit mindestens 10 GB freiem Speicher

### Installation

Keine Installation erforderlich. Einfach `ProgramMover.exe` als Administrator ausführen.

### Build

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true
```

### Security Notes

⚠️ **WICHTIG**: Erstellen Sie vor der Nutzung ein vollständiges System-Backup!

### License

MIT License - See LICENSE file

### Contributors

ProgramMover Team

---

## Planned Features (Future Versions)

- [ ] CLI-Modus für automatisierte Deployments
- [ ] Scheduler für zeitgesteuerte Migrationen
- [ ] Launcher-Integration (Steam, Epic Games, etc.)
- [ ] MSI-Repair-Mechanismus
- [ ] Mehrstufiges Monitoring über 72 Stunden
- [ ] Telemetrie (Opt-in)
- [ ] Auto-Update-Mechanismus
- [ ] Gruppenrichtlinien-kompatible Reports
- [ ] Multi-Drive-Support (E:, F:, etc.)
