using ProgramMover.Agents;
using ProgramMover.Models;

namespace ProgramMover;

/// <summary>
/// Main form - Wizard-style interface for program migration
/// </summary>
public partial class MainForm : Form
{
    private readonly OrchestratorAgent _orchestrator;
    private List<AppEntry> _scannedApps = new();
    private List<AppEntry> _selectedApps = new();
    private MigrationPlan? _currentPlan;
    private WizardStep _currentStep = WizardStep.Welcome;

    // UI Controls
    private Panel pnlContent = new();
    private Panel pnlButtons = new();
    private Button btnNext = new();
    private Button btnBack = new();
    private Button btnCancel = new();
    private ProgressBar progressBar = new();
    private Label lblStatus = new();

    public MainForm()
    {
        _orchestrator = new OrchestratorAgent();
        InitializeComponent();
        SetupUI();
        ShowWelcome();
    }

    private void InitializeComponent()
    {
        Text = "ProgramMover - I like to move it, move it!";
        Size = new Size(900, 700);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(800, 600);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
    }

    private void SetupUI()
    {
        // Content panel
        pnlContent.Dock = DockStyle.Fill;
        pnlContent.Padding = new Padding(20);
        Controls.Add(pnlContent);

        // Button panel
        pnlButtons.Dock = DockStyle.Bottom;
        pnlButtons.Height = 60;
        pnlButtons.Padding = new Padding(10);
        Controls.Add(pnlButtons);

        // Status label
        lblStatus.Dock = DockStyle.Fill;
        lblStatus.TextAlign = ContentAlignment.MiddleLeft;
        pnlButtons.Controls.Add(lblStatus);

        // Buttons
        btnCancel.Text = "Abbrechen";
        btnCancel.Size = new Size(100, 35);
        btnCancel.Location = new Point(pnlButtons.Width - 120, 12);
        btnCancel.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        btnCancel.Click += (s, e) => Close();
        pnlButtons.Controls.Add(btnCancel);

        btnNext.Text = "Weiter >";
        btnNext.Size = new Size(100, 35);
        btnNext.Location = new Point(btnCancel.Left - 110, 12);
        btnNext.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        btnNext.Click += BtnNext_Click;
        pnlButtons.Controls.Add(btnNext);

        btnBack.Text = "< Zurück";
        btnBack.Size = new Size(100, 35);
        btnBack.Location = new Point(btnNext.Left - 110, 12);
        btnBack.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        btnBack.Click += BtnBack_Click;
        btnBack.Enabled = false;
        pnlButtons.Controls.Add(btnBack);

        // Progress bar
        progressBar.Dock = DockStyle.Bottom;
        progressBar.Height = 25;
        progressBar.Visible = false;
        pnlContent.Controls.Add(progressBar);
    }

    private void BtnNext_Click(object? sender, EventArgs e)
    {
        switch (_currentStep)
        {
            case WizardStep.Welcome:
                ShowSecurityChecks();
                break;
            case WizardStep.SecurityChecks:
                ShowScanning();
                break;
            case WizardStep.Scanning:
                ShowAnalysis();
                break;
            case WizardStep.Analysis:
                ShowSelection();
                break;
            case WizardStep.Selection:
                ShowPlan();
                break;
            case WizardStep.Plan:
                ShowDryRun();
                break;
            case WizardStep.DryRun:
                ShowExecution();
                break;
            case WizardStep.Execution:
                ShowMonitoring();
                break;
            case WizardStep.Monitoring:
                ShowComplete();
                break;
        }
    }

    private void BtnBack_Click(object? sender, EventArgs e)
    {
        switch (_currentStep)
        {
            case WizardStep.SecurityChecks:
                ShowWelcome();
                break;
            case WizardStep.Analysis:
                ShowSecurityChecks();
                break;
            case WizardStep.Selection:
                ShowAnalysis();
                break;
            case WizardStep.Plan:
                ShowSelection();
                break;
            case WizardStep.DryRun:
                ShowPlan();
                break;
        }
    }

    private void ClearContent()
    {
        pnlContent.Controls.Clear();
        progressBar.Visible = false;
        pnlContent.Controls.Add(progressBar);
    }

    private void ShowWelcome()
    {
        _currentStep = WizardStep.Welcome;
        ClearContent();

        var lblTitle = new Label
        {
            Text = "Willkommen beim ProgramMover!",
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 20)
        };
        pnlContent.Controls.Add(lblTitle);

        var lblWarning = new Label
        {
            Text = "⚠️ WICHTIGE SICHERHEITSHINWEISE ⚠️",
            Font = new Font(Font.FontFamily, 12, FontStyle.Bold),
            ForeColor = Color.Red,
            AutoSize = true,
            Location = new Point(20, 80)
        };
        pnlContent.Controls.Add(lblWarning);

        var txtInfo = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Location = new Point(20, 120),
            Size = new Size(820, 300),
            Text = @"Dieses Tool verschiebt installierte Programme von C:\ nach D:\ unter Verwendung von Junctions (symbolischen Links).

WICHTIG VOR DEM START:
• Erstellen Sie ein vollständiges System-Backup!
• Schließen Sie alle laufenden Programme
• Stellen Sie sicher, dass Sie Administrator-Rechte haben
• Ziellaufwerk D: muss NTFS-formatiert sein
• Mindestens 10 GB freier Speicherplatz auf D: erforderlich

NICHT VERSCHIEBBAR:
• Windows-Systemkomponenten
• Antivirus/EDR-Software
• Anti-Cheat-Systeme
• Kritische System-Services
• Store-Apps (verwenden Sie Windows-Einstellungen)

EMPFOHLEN:
• DryRun-Modus zuerst testen
• Nur portable Programme automatisch verschieben
• MSI-Installationen mit Vorsicht behandeln

Durch Klicken auf 'Weiter' bestätigen Sie, dass Sie:
1. Ein Backup Ihres Systems erstellt haben
2. Die Risiken verstehen
3. Auf eigene Verantwortung handeln"
        };
        pnlContent.Controls.Add(txtInfo);

        var chkBackup = new CheckBox
        {
            Text = "Ich habe ein Backup erstellt und die Hinweise gelesen",
            Location = new Point(20, 440),
            Size = new Size(400, 30),
            Font = new Font(Font.FontFamily, 10, FontStyle.Bold)
        };
        chkBackup.CheckedChanged += (s, e) => btnNext.Enabled = chkBackup.Checked;
        pnlContent.Controls.Add(chkBackup);

        btnNext.Enabled = false;
        btnBack.Enabled = false;
        lblStatus.Text = "Bitte lesen Sie die Hinweise und bestätigen Sie, dass Sie ein Backup erstellt haben.";
    }

    private void ShowSecurityChecks()
    {
        _currentStep = WizardStep.SecurityChecks;
        ClearContent();
        btnNext.Enabled = false;
        btnBack.Enabled = true;

        var lblTitle = new Label
        {
            Text = "Sicherheitsprüfungen",
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 20)
        };
        pnlContent.Controls.Add(lblTitle);

        var txtResults = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Location = new Point(20, 80),
            Size = new Size(820, 450)
        };
        pnlContent.Controls.Add(txtResults);

        lblStatus.Text = "Führe Sicherheitsprüfungen durch...";
        Application.DoEvents();

        Task.Run(() =>
        {
            var result = _orchestrator.PerformSecurityChecks();
            
            Invoke(() =>
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("SICHERHEITSPRÜFUNGEN:\n");
                
                sb.AppendLine($"✓ Administrator-Rechte: {(result.IsAdministrator ? "OK" : "FEHLT")}");
                sb.AppendLine($"✓ Ziellaufwerk D: {(result.TargetDriveValid ? "OK" : "FEHLER")}");
                sb.AppendLine($"✓ Wiederherstellungspunkt: {(result.RestorePointCreated ? "Erstellt" : "Nicht erstellt")}");
                sb.AppendLine();
                
                if (result.Messages.Any())
                {
                    sb.AppendLine("INFORMATIONEN:");
                    foreach (var msg in result.Messages)
                        sb.AppendLine($"  • {msg}");
                    sb.AppendLine();
                }
                
                if (result.Warnings.Any())
                {
                    sb.AppendLine("WARNUNGEN:");
                    foreach (var warning in result.Warnings)
                        sb.AppendLine($"  ⚠ {warning}");
                    sb.AppendLine();
                }
                
                if (result.Errors.Any())
                {
                    sb.AppendLine("FEHLER:");
                    foreach (var error in result.Errors)
                        sb.AppendLine($"  ✗ {error}");
                    sb.AppendLine();
                }
                
                txtResults.Text = sb.ToString();
                
                if (result.IsValid)
                {
                    lblStatus.Text = "Sicherheitsprüfungen erfolgreich abgeschlossen.";
                    btnNext.Enabled = true;
                }
                else
                {
                    lblStatus.Text = "Sicherheitsprüfungen fehlgeschlagen. Bitte beheben Sie die Fehler.";
                    btnNext.Enabled = false;
                    MessageBox.Show("Sicherheitsprüfungen fehlgeschlagen!\n\nBitte beheben Sie die angezeigten Fehler und starten Sie das Programm als Administrator neu.",
                        "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            });
        });
    }

    private void ShowScanning()
    {
        _currentStep = WizardStep.Scanning;
        ClearContent();
        btnNext.Enabled = false;
        btnBack.Enabled = false;

        var lblTitle = new Label
        {
            Text = "System-Scan",
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 20)
        };
        pnlContent.Controls.Add(lblTitle);

        var txtLog = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Location = new Point(20, 80),
            Size = new Size(820, 450)
        };
        pnlContent.Controls.Add(txtLog);

        progressBar.Visible = true;
        progressBar.Style = ProgressBarStyle.Marquee;
        lblStatus.Text = "Scanne System nach installierten Programmen...";

        Task.Run(() =>
        {
            _scannedApps = _orchestrator.ScanSystem();
            
            Invoke(() =>
            {
                var logs = _orchestrator.GetAllLogs()
                    .Where(l => l.Category == "Scanner")
                    .Select(l => l.ToString());
                
                txtLog.Text = string.Join(Environment.NewLine, logs);
                
                progressBar.Style = ProgressBarStyle.Continuous;
                progressBar.Visible = false;
                lblStatus.Text = $"Scan abgeschlossen. {_scannedApps.Count} Programme gefunden.";
                btnNext.Enabled = true;
            });
        });
    }

    private void ShowAnalysis()
    {
        _currentStep = WizardStep.Analysis;
        ClearContent();
        btnNext.Enabled = false;
        btnBack.Enabled = true;

        var lblTitle = new Label
        {
            Text = "Analyse",
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 20)
        };
        pnlContent.Controls.Add(lblTitle);

        var txtLog = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Location = new Point(20, 80),
            Size = new Size(820, 450)
        };
        pnlContent.Controls.Add(txtLog);

        progressBar.Visible = true;
        progressBar.Style = ProgressBarStyle.Marquee;
        lblStatus.Text = "Analysiere Programme...";

        Task.Run(() =>
        {
            _scannedApps = _orchestrator.AnalyzeApps(_scannedApps);
            
            Invoke(() =>
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("ANALYSE-ERGEBNISSE:\n");
                
                var autoCount = _scannedApps.Count(a => a.Category == MoveCategory.MoveableAuto);
                var cautionCount = _scannedApps.Count(a => a.Category == MoveCategory.MoveableCaution);
                var notMoveableCount = _scannedApps.Count(a => a.Category == MoveCategory.NotMoveable);
                
                sb.AppendLine($"✓ Automatisch verschiebbar (MoveableAuto): {autoCount}");
                sb.AppendLine($"⚠ Mit Vorsicht verschiebbar (MoveableCaution): {cautionCount}");
                sb.AppendLine($"✗ Nicht verschiebbar (NotMoveable): {notMoveableCount}");
                sb.AppendLine();
                
                txtLog.Text = sb.ToString();
                
                progressBar.Style = ProgressBarStyle.Continuous;
                progressBar.Visible = false;
                lblStatus.Text = "Analyse abgeschlossen.";
                btnNext.Enabled = true;
            });
        });
    }

    private void ShowSelection()
    {
        _currentStep = WizardStep.Selection;
        ClearContent();
        btnNext.Enabled = true;
        btnBack.Enabled = true;

        var lblTitle = new Label
        {
            Text = "Programmauswahl",
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 20)
        };
        pnlContent.Controls.Add(lblTitle);

        var dataGridView = new DataGridView
        {
            Location = new Point(20, 80),
            Size = new Size(820, 400),
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = true,
            ReadOnly = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        dataGridView.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Select", HeaderText = "Auswählen", Width = 80 });
        dataGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Name", ReadOnly = true });
        dataGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "Category", HeaderText = "Kategorie", ReadOnly = true, Width = 120 });
        dataGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "Score", HeaderText = "Score", ReadOnly = true, Width = 60 });
        dataGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "Size", HeaderText = "Größe", ReadOnly = true, Width = 100 });
        dataGridView.Columns.Add(new DataGridViewTextBoxColumn { Name = "Location", HeaderText = "Pfad", ReadOnly = true });

        foreach (var app in _scannedApps.OrderByDescending(a => a.Score))
        {
            var sizeGB = app.TotalSizeBytes / (1024.0 * 1024.0 * 1024.0);
            var row = dataGridView.Rows.Add(
                app.Category == MoveCategory.MoveableAuto,
                app.DisplayName,
                app.Category.ToString(),
                app.Score,
                $"{sizeGB:F2} GB",
                app.InstallLocation ?? "N/A"
            );
            dataGridView.Rows[row].Tag = app;
        }

        pnlContent.Controls.Add(dataGridView);

        var btnSelectAll = new Button
        {
            Text = "Alle 'MoveableAuto' auswählen",
            Location = new Point(20, 490),
            Size = new Size(200, 30)
        };
        btnSelectAll.Click += (s, e) =>
        {
            foreach (DataGridViewRow row in dataGridView.Rows)
            {
                var app = row.Tag as AppEntry;
                row.Cells[0].Value = app?.Category == MoveCategory.MoveableAuto;
            }
        };
        pnlContent.Controls.Add(btnSelectAll);

        btnNext.Click -= BtnNext_Click;
        btnNext.Click += (s, e) =>
        {
            _selectedApps = dataGridView.Rows.Cast<DataGridViewRow>()
                .Where(r => r.Cells[0].Value is bool b && b)
                .Select(r => r.Tag as AppEntry)
                .Where(a => a != null)
                .Cast<AppEntry>()
                .ToList();

            if (!_selectedApps.Any())
            {
                MessageBox.Show("Bitte wählen Sie mindestens ein Programm aus.", "Hinweis", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            btnNext.Click -= BtnNext_Click;
            btnNext.Click += BtnNext_Click;
            ShowPlan();
        };

        lblStatus.Text = $"{_scannedApps.Count} Programme gefunden. Wählen Sie Programme zum Verschieben aus.";
    }

    private void ShowPlan()
    {
        _currentStep = WizardStep.Plan;
        ClearContent();
        btnNext.Text = "DryRun starten";
        btnNext.Enabled = true;
        btnBack.Enabled = true;

        var lblTitle = new Label
        {
            Text = "Migrationsplan",
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 20)
        };
        pnlContent.Controls.Add(lblTitle);

        lblStatus.Text = "Erstelle Migrationsplan...";
        Application.DoEvents();

        _currentPlan = _orchestrator.CreateMigrationPlan(_selectedApps, false);

        var txtPlan = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Location = new Point(20, 80),
            Size = new Size(820, 450)
        };

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"MIGRATIONSPLAN");
        sb.AppendLine($"Anzahl Programme: {_currentPlan.Apps.Count}");
        sb.AppendLine($"Anzahl Schritte: {_currentPlan.Steps.Count}");
        sb.AppendLine($"Geschätzte Dauer: {_currentPlan.EstimatedDurationMinutes} Minuten");
        sb.AppendLine($"Gesamtgröße: {_currentPlan.TotalSizeBytes / (1024.0 * 1024.0 * 1024.0):F2} GB");
        sb.AppendLine();
        sb.AppendLine("SCHRITTE:");
        
        foreach (var step in _currentPlan.Steps.Take(20))
        {
            sb.AppendLine($"  {step.Order + 1}. [{step.StepType}] {step.Description}");
        }
        
        if (_currentPlan.Steps.Count > 20)
            sb.AppendLine($"  ... und {_currentPlan.Steps.Count - 20} weitere Schritte");

        txtPlan.Text = sb.ToString();
        pnlContent.Controls.Add(txtPlan);

        lblStatus.Text = $"Plan erstellt: {_currentPlan.Steps.Count} Schritte für {_currentPlan.Apps.Count} Programme";
    }

    private void ShowDryRun()
    {
        _currentStep = WizardStep.DryRun;
        ClearContent();
        btnNext.Text = "Live ausführen";
        btnNext.Enabled = false;
        btnBack.Enabled = false;

        var lblTitle = new Label
        {
            Text = "DryRun - Simulation",
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 20)
        };
        pnlContent.Controls.Add(lblTitle);

        var txtLog = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Location = new Point(20, 80),
            Size = new Size(820, 450)
        };
        pnlContent.Controls.Add(txtLog);

        progressBar.Visible = true;
        lblStatus.Text = "Führe DryRun durch...";

        if (_currentPlan == null) return;

        var dryRunPlan = _orchestrator.CreateMigrationPlan(_selectedApps, true);

        Task.Run(async () =>
        {
            var progress = new Progress<ExecutionProgress>(p =>
            {
                Invoke(() =>
                {
                    progressBar.Value = p.PercentComplete;
                    lblStatus.Text = $"Schritt {p.CurrentStep}/{p.TotalSteps}: {p.StepDescription}";
                });
            });

            var result = await _orchestrator.ExecuteMigration(dryRunPlan, progress);

            Invoke(() =>
            {
                var logs = _orchestrator.GetAllLogs()
                    .Where(l => l.Category == "Executor")
                    .Select(l => l.ToString());

                txtLog.Text = string.Join(Environment.NewLine, logs);

                progressBar.Visible = false;
                lblStatus.Text = $"DryRun abgeschlossen: {result.Message}";
                btnNext.Enabled = true;
                btnBack.Enabled = true;

                MessageBox.Show($"DryRun erfolgreich abgeschlossen!\n\n{result.SuccessfulSteps.Count} Schritte simuliert.\n\nSie können jetzt die echte Migration starten.",
                    "DryRun abgeschlossen", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        });
    }

    private void ShowExecution()
    {
        var confirmResult = MessageBox.Show(
            "Sie sind dabei, die echte Migration zu starten!\n\n" +
            "Dies wird die ausgewählten Programme von C:\\ nach D:\\ verschieben.\n\n" +
            "Haben Sie ein Backup erstellt?\n\n" +
            "Möchten Sie fortfahren?",
            "Migration starten",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirmResult != DialogResult.Yes)
            return;

        _currentStep = WizardStep.Execution;
        ClearContent();
        btnNext.Enabled = false;
        btnBack.Enabled = false;
        btnCancel.Enabled = false;

        var lblTitle = new Label
        {
            Text = "Migration läuft...",
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 20)
        };
        pnlContent.Controls.Add(lblTitle);

        var txtLog = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Location = new Point(20, 80),
            Size = new Size(820, 450)
        };
        pnlContent.Controls.Add(txtLog);

        progressBar.Visible = true;
        lblStatus.Text = "Migration wird ausgeführt...";

        if (_currentPlan == null) return;

        Task.Run(async () =>
        {
            var progress = new Progress<ExecutionProgress>(p =>
            {
                Invoke(() =>
                {
                    progressBar.Value = p.PercentComplete;
                    lblStatus.Text = $"Schritt {p.CurrentStep}/{p.TotalSteps}: {p.StepDescription}";
                    
                    var logs = _orchestrator.GetAllLogs()
                        .Where(l => l.Category == "Executor")
                        .TakeLast(50)
                        .Select(l => l.ToString());
                    txtLog.Text = string.Join(Environment.NewLine, logs);
                    txtLog.SelectionStart = txtLog.Text.Length;
                    txtLog.ScrollToCaret();
                });
            });

            var result = await _orchestrator.ExecuteMigration(_currentPlan, progress);

            Invoke(() =>
            {
                progressBar.Visible = false;
                btnNext.Enabled = true;
                btnCancel.Enabled = true;

                if (result.Success.GetValueOrDefault())
                {
                    lblStatus.Text = $"Migration erfolgreich abgeschlossen!";
                    MessageBox.Show($"Migration erfolgreich!\n\n{result.SuccessfulSteps.Count} Schritte abgeschlossen.\n\nDauer: {result.Duration:mm\\:ss}",
                        "Erfolg", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    lblStatus.Text = $"Migration fehlgeschlagen: {result.Message}";
                    MessageBox.Show($"Migration fehlgeschlagen!\n\n{result.Message}\n\nRollback wurde ausgeführt.",
                        "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            });
        });
    }

    private void ShowMonitoring()
    {
        _currentStep = WizardStep.Monitoring;
        ClearContent();
        btnNext.Text = "Abschließen";
        btnNext.Enabled = false;
        btnBack.Enabled = false;

        var lblTitle = new Label
        {
            Text = "Überwachung",
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 20)
        };
        pnlContent.Controls.Add(lblTitle);

        var txtStatus = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Location = new Point(20, 80),
            Size = new Size(820, 450)
        };
        pnlContent.Controls.Add(txtStatus);

        progressBar.Visible = true;
        progressBar.Style = ProgressBarStyle.Marquee;
        lblStatus.Text = "Überprüfe migrierte Programme...";

        Task.Run(async () =>
        {
            var result = await _orchestrator.MonitorApps(_selectedApps);

            Invoke(() =>
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"ÜBERWACHUNGSERGEBNIS\n");
                sb.AppendLine($"Gesamtstatus: {result.OverallStatus}\n");
                sb.AppendLine("PROGRAMME:\n");

                foreach (var appStatus in result.AppStatuses)
                {
                    sb.AppendLine($"• {appStatus.AppName}");
                    sb.AppendLine($"  Status: {appStatus.Status}");
                    sb.AppendLine($"  Junction gültig: {(appStatus.JunctionValid ? "Ja" : "Nein")}");
                    sb.AppendLine($"  Prozesse startbar: {(appStatus.CanStartProcess ? "Ja" : "Nein")}");
                    
                    if (appStatus.ServiceChecks.Any())
                    {
                        var lastCheck = appStatus.ServiceChecks.Last();
                        sb.AppendLine($"  Services: {(lastCheck.AllRunning ? "Alle laufen" : "Probleme")}");
                    }
                    
                    if (!string.IsNullOrEmpty(appStatus.ErrorMessage))
                        sb.AppendLine($"  Fehler: {appStatus.ErrorMessage}");
                    
                    sb.AppendLine();
                }

                txtStatus.Text = sb.ToString();
                progressBar.Visible = false;
                lblStatus.Text = $"Überwachung abgeschlossen. Status: {result.OverallStatus}";
                btnNext.Enabled = true;
            });
        });
    }

    private void ShowComplete()
    {
        _currentStep = WizardStep.Complete;
        ClearContent();
        btnNext.Text = "Beenden";
        btnBack.Enabled = false;

        var lblTitle = new Label
        {
            Text = "Migration abgeschlossen!",
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 20)
        };
        pnlContent.Controls.Add(lblTitle);

        var txtInfo = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Location = new Point(20, 80),
            Size = new Size(820, 400),
            Text = @"Die Migration wurde erfolgreich abgeschlossen!

NÄCHSTE SCHRITTE:

1. Testen Sie die verschobenen Programme
2. Überwachen Sie die Systemstabilität für 72 Stunden
3. Überprüfen Sie Event-Logs auf Fehler

AUFRÄUMEN:
Die Original-Verzeichnisse wurden in .old umbenannt und bleiben als Backup erhalten.

Sie können diese nach erfolgreicher Testphase löschen, um Speicherplatz freizugeben.

EMPFEHLUNG:
Warten Sie mindestens 1 Woche, bevor Sie die .old-Verzeichnisse löschen.

Alle Logs wurden gespeichert in:
D:\mover\logs\

Bei Problemen:
- Überprüfen Sie die Log-Dateien
- Nutzen Sie die Rollback-Funktion falls nötig
- Kontaktieren Sie den Support

Vielen Dank für die Nutzung von ProgramMover!"
        };
        pnlContent.Controls.Add(txtInfo);

        var btnCleanup = new Button
        {
            Text = "Cleanup: .old-Verzeichnisse anzeigen",
            Location = new Point(20, 500),
            Size = new Size(250, 30)
        };
        btnCleanup.Click += async (s, e) =>
        {
            var result = await _orchestrator.CleanupOldDirectories(_selectedApps, false);
            MessageBox.Show($"Gefundene .old-Verzeichnisse: {result.PendingDirectories.Count}\n\n" +
                          string.Join("\n", result.PendingDirectories.Take(10)) +
                          (result.PendingDirectories.Count > 10 ? $"\n... und {result.PendingDirectories.Count - 10} weitere" : ""),
                          "Cleanup-Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        pnlContent.Controls.Add(btnCleanup);

        btnNext.Click -= BtnNext_Click;
        btnNext.Click += (s, e) => Close();

        lblStatus.Text = "Migration erfolgreich abgeschlossen!";
    }

    private enum WizardStep
    {
        Welcome,
        SecurityChecks,
        Scanning,
        Analysis,
        Selection,
        Plan,
        DryRun,
        Execution,
        Monitoring,
        Complete
    }
}
