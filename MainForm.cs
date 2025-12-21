using ProgramMover.Agents;
using ProgramMover.Models;
using System.Linq;

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
    private DataGridView? _selectionGrid;
    private EventHandler? _currentNextHandler;

    // UI Controls
    private Panel pnlContent = new();
    private Panel pnlButtons = new();
    private Button btnNext = new();
    private Button btnBack = new();
    private Button btnCancel = new();
    private ProgressBar progressBar = new();
    private Label lblStatus = new();

    private TableLayoutPanel CreateStandardLayout(bool includeFooter = false)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = includeFooter ? 3 : 2,
            AutoSize = false,
            Padding = new Padding(10),
        };

        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        if (includeFooter)
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        pnlContent.Controls.Add(layout);
        pnlContent.Controls.SetChildIndex(progressBar, pnlContent.Controls.Count - 1);

        return layout;
    }

    private static TextBox CreateReadOnlyMultiline()
    {
        return new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
        };
    }

    private void ResetNextButtonHandlers()
    {
        // Remove any custom handler we previously set
        if (_currentNextHandler != null)
        {
            btnNext.Click -= _currentNextHandler;
            _currentNextHandler = null;
        }

        // Ensure the default handler is attached (idempotent operation)
        btnNext.Click -= BtnNext_Click;
        btnNext.Click += BtnNext_Click;
    }

    private void SetNextButtonHandler(EventHandler handler)
    {
        // Remove the default handler
        btnNext.Click -= BtnNext_Click;
        
        // Remove any previous custom handler
        if (_currentNextHandler != null)
        {
            btnNext.Click -= _currentNextHandler;
        }

        // Set the new custom handler
        _currentNextHandler = handler;
        btnNext.Click += _currentNextHandler;
    }

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
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
    }

    private void SetupUI()
    {
        // Button panel - Add FIRST so it stays on top when content panel fills
        pnlButtons.Dock = DockStyle.Bottom;
        pnlButtons.Height = 60;
        pnlButtons.Padding = new Padding(10);
        Controls.Add(pnlButtons);

        // Content panel - Add SECOND so it fills remaining space
        pnlContent.Dock = DockStyle.Fill;
        pnlContent.Padding = new Padding(20);
        Controls.Add(pnlContent);

        var buttonsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        buttonsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        buttonsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pnlButtons.Controls.Add(buttonsLayout);

        // Status label
        lblStatus.Dock = DockStyle.Fill;
        lblStatus.TextAlign = ContentAlignment.MiddleLeft;
        buttonsLayout.Controls.Add(lblStatus, 0, 0);

        // Button row uses flow layout to stay visible on resize
        var buttonFlow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        buttonsLayout.Controls.Add(buttonFlow, 1, 0);

        // Buttons
        btnCancel.Text = "Abbrechen";
        btnCancel.Size = new Size(100, 35);
        btnCancel.Margin = new Padding(5, 0, 0, 0);
        btnCancel.Click += (s, e) => Close();
        buttonFlow.Controls.Add(btnCancel);

        btnNext.Text = "Weiter >";
        btnNext.Size = new Size(100, 35);
        btnNext.Margin = new Padding(5, 0, 0, 0);
        btnNext.Click += BtnNext_Click;
        buttonFlow.Controls.Add(btnNext);

        btnBack.Text = "< Zurück";
        btnBack.Size = new Size(100, 35);
        btnBack.Margin = new Padding(5, 0, 0, 0);
        btnBack.Click += BtnBack_Click;
        btnBack.Enabled = false;
        buttonFlow.Controls.Add(btnBack);

        ResetNextButtonHandlers();

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
        pnlContent.AutoScroll = true;
        progressBar.Visible = false;
        pnlContent.Controls.Add(progressBar);
    }

    private int FilterAppsOnTargetDrive()
    {
        var initialCount = _scannedApps.Count;
        _scannedApps = _scannedApps
            .Where(a => string.IsNullOrWhiteSpace(a.InstallLocation) ||
                        !a.InstallLocation.TrimStart().StartsWith(@"D:\", StringComparison.OrdinalIgnoreCase))
            .ToList();
        return initialCount - _scannedApps.Count;
    }

    private void ShowWelcome()
    {
        _currentStep = WizardStep.Welcome;
        ClearContent();
        var layout = CreateStandardLayout(includeFooter: false);

        layout.RowCount = 4;
        layout.RowStyles[0] = new RowStyle(SizeType.AutoSize);     // Title
        layout.RowStyles[1] = new RowStyle(SizeType.AutoSize);     // Warning panel - size to content
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // Verbose logging checkbox
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // Backup checkbox

        var lblTitle = new Label
        {
            Text = "Willkommen beim ProgramMover!",
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        };
        layout.Controls.Add(lblTitle, 0, 0);

        var warningPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.None,
            FlowDirection = FlowDirection.TopDown,
            AutoSize = false,
            Width = 700,
            Height = 250,
            Margin = new Padding(0, 0, 0, 10)
        };

        var lblWarning = new Label
        {
            Text = "⚠️ WICHTIGE SICHERHEITSHINWEISE ⚠️",
            Font = new Font(Font.FontFamily, 12, FontStyle.Bold),
            ForeColor = Color.Red,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        };
        warningPanel.Controls.Add(lblWarning);

        var txtInfo = CreateReadOnlyMultiline();
        txtInfo.Dock = DockStyle.None;
        txtInfo.Width = 700;
        txtInfo.Height = 220;
        txtInfo.Margin = new Padding(0, 0, 0, 10);
        txtInfo.Text = @"Dieses Tool verschiebt installierte Programme von C:\ nach D:\ unter Verwendung von Junctions (symbolischen Links).

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
3. Auf eigene Verantwortung handeln";
        warningPanel.Controls.Add(txtInfo);

        layout.Controls.Add(warningPanel, 0, 1);

        var chkVerboseLogging = new CheckBox
        {
            Text = "Optionale Detail-Logs für Debugging aktivieren",
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 0),
            Checked = LoggingOptions.EnableDebugLogs
        };
        chkVerboseLogging.CheckedChanged += (s, e) =>
        {
            LoggingOptions.EnableDebugLogs = chkVerboseLogging.Checked;
            lblStatus.Text = chkVerboseLogging.Checked
                ? "Detail-Logging aktiviert. Debug-Informationen werden gesammelt."
                : "Detail-Logging deaktiviert.";
        };
        layout.Controls.Add(chkVerboseLogging, 0, 2);

        var chkBackup = new CheckBox
        {
            Text = "Ich habe ein Backup erstellt und die Hinweise gelesen",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 10, FontStyle.Bold),
            Margin = new Padding(0, 10, 0, 0)
        };
        chkBackup.CheckedChanged += (s, e) => btnNext.Enabled = chkBackup.Checked;
        layout.Controls.Add(chkBackup, 0, 3);

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
        var layout = CreateStandardLayout();

        var lblTitle = new Label
        {
            Text = "Sicherheitsprüfungen",
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        };
        layout.Controls.Add(lblTitle, 0, 0);

        var txtResults = CreateReadOnlyMultiline();
        layout.Controls.Add(txtResults, 0, 1);

        lblStatus.Text = "Führe Sicherheitsprüfungen durch...";
        Application.DoEvents();

        Task.Run(() =>
        {
            var result = _orchestrator.PerformSecurityChecks();
            var recoveryReport = _orchestrator.DetectAndFixPreviousRuns();

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

                if (recoveryReport.HasFindings)
                {
                    sb.AppendLine("AUTOMATISCHE WIEDERHERSTELLUNG VON FRÜHEREN LÄUFEN:");

                    if (recoveryReport.RestoredPaths.Any())
                        sb.AppendLine($"  • {recoveryReport.RestoredPaths.Count} Installationen wurden zurückkopiert (\".old\" -> Originalpfad)");

                    if (recoveryReport.NeedsManualReview.Any())
                        sb.AppendLine($"  • {recoveryReport.NeedsManualReview.Count} Ordner benötigen manuelle Prüfung (\".old\" ohne Junction)");

                    if (recoveryReport.Errors.Any())
                        sb.AppendLine($"  • Fehler bei der Wiederherstellung: {string.Join(", ", recoveryReport.Errors.Take(3))}");

                    sb.AppendLine();
                }

                txtResults.Text = sb.ToString();

                if (result.IsValid)
                {
                    var recoveryNote = recoveryReport.RestoredPaths.Any()
                        ? " Frühere fehlerhafte Migrationen wurden repariert."
                        : string.Empty;

                    var manualReviewNote = recoveryReport.NeedsManualReview.Any()
                        ? " Bitte prüfen Sie die gefundenen .old-Ordner manuell, bevor Sie fortfahren."
                        : string.Empty;

                    lblStatus.Text = "Sicherheitsprüfungen erfolgreich abgeschlossen." + recoveryNote + manualReviewNote;
                    btnNext.Enabled = true;

                    if (recoveryReport.NeedsManualReview.Any())
                    {
                        SetNextButtonHandler((s, e) =>
                        {
                            var confirmation = MessageBox.Show(
                                "Es wurden Ordner gefunden, die eine manuelle Überprüfung erfordern (\".old\" ohne Junction).\n\n" +
                                "Bitte stellen Sie sicher, dass Sie diese Ordner geprüft und ggf. bereinigt haben, bevor Sie fortfahren.\n\n" +
                                "Möchten Sie trotzdem mit der Migration fortfahren?",
                                "Warnung – manuelle Überprüfung empfohlen",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Warning,
                                MessageBoxDefaultButton.Button2);

                            if (confirmation == DialogResult.Yes)
                            {
                                ShowScanning();
                            }
                        });
                    }
                    else
                    {
                        SetNextButtonHandler((s, e) => ShowScanning());
                    }
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
        ResetNextButtonHandlers();
        btnNext.Enabled = false;
        btnBack.Enabled = false;
        var layout = CreateStandardLayout();

        var lblTitle = new Label
        {
            Text = "System-Scan",
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        };
        layout.Controls.Add(lblTitle, 0, 0);

        var txtLog = CreateReadOnlyMultiline();
        layout.Controls.Add(txtLog, 0, 1);

        progressBar.Visible = true;
        progressBar.Style = ProgressBarStyle.Marquee;
        lblStatus.Text = "Scanne System nach installierten Programmen...";

        Task.Run(() =>
        {
            _scannedApps = _orchestrator.ScanSystem();
            
            Invoke(() =>
            {
                var skipped = FilterAppsOnTargetDrive();

                var logs = _orchestrator.GetAllLogs()
                    .Where(l => l.Category == "Scanner")
                    .Select(l => l.ToString());
                var logText = string.Join(Environment.NewLine, logs);
                if (skipped > 0)
                    logText += $"{Environment.NewLine}{Environment.NewLine}Hinweis: {skipped} Programme bereits auf D:\\ erkannt und übersprungen.";

                txtLog.Text = logText;

                progressBar.Style = ProgressBarStyle.Continuous;
                progressBar.Visible = false;
                lblStatus.Text = $"Scan abgeschlossen. {_scannedApps.Count} Programme gefunden." + (skipped > 0 ? $" {skipped} bereits auf D: übersprungen." : string.Empty);
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
        var layout = CreateStandardLayout();

        var lblTitle = new Label
        {
            Text = "Analyse",
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        };
        layout.Controls.Add(lblTitle, 0, 0);

        var txtLog = CreateReadOnlyMultiline();
        layout.Controls.Add(txtLog, 0, 1);

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
        var layout = CreateStandardLayout(includeFooter: true);

        var lblTitle = new Label
        {
            Text = "Programmauswahl",
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        };
        layout.Controls.Add(lblTitle, 0, 0);

        var dataGridView = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = true,
            ReadOnly = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
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

        layout.Controls.Add(dataGridView, 0, 1);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 10, 0, 0)
        };

        var btnSelectAll = new Button
        {
            Text = "Alle 'MoveableAuto' auswählen",
            AutoSize = true
        };
        btnSelectAll.Click += (s, e) =>
        {
            foreach (DataGridViewRow row in dataGridView.Rows)
            {
                var app = row.Tag as AppEntry;
                row.Cells[0].Value = app?.Category == MoveCategory.MoveableAuto;
            }
        };
        footer.Controls.Add(btnSelectAll);

        layout.Controls.Add(footer, 0, 2);

        _selectionGrid = dataGridView;

        SetNextButtonHandler(SelectionNextClicked);

        lblStatus.Text = $"{_scannedApps.Count} Programme gefunden. Wählen Sie Programme zum Verschieben aus.";
    }

    private void SelectionNextClicked(object? sender, EventArgs e)
    {
        if (_selectionGrid == null)
            return;

        _selectedApps = _selectionGrid.Rows.Cast<DataGridViewRow>()
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

        ResetNextButtonHandlers();
        ShowPlan();
    }

    private void ShowPlan()
    {
        _currentStep = WizardStep.Plan;
        ClearContent();
        btnNext.Text = "DryRun starten";
        btnNext.Enabled = true;
        btnBack.Enabled = true;

        var layout = CreateStandardLayout();

        var lblTitle = new Label
        {
            Text = "Migrationsplan",
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        };
        layout.Controls.Add(lblTitle, 0, 0);

        lblStatus.Text = "Erstelle Migrationsplan...";
        Application.DoEvents();

        _currentPlan = _orchestrator.CreateMigrationPlan(_selectedApps, false);

        var txtPlan = CreateReadOnlyMultiline();

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
        layout.Controls.Add(txtPlan, 0, 1);

        lblStatus.Text = $"Plan erstellt: {_currentPlan.Steps.Count} Schritte für {_currentPlan.Apps.Count} Programme";
    }

    private void ShowDryRun()
    {
        _currentStep = WizardStep.DryRun;
        ClearContent();
        btnNext.Text = "Live ausführen";
        btnNext.Enabled = false;
        btnBack.Enabled = false;
        var layout = CreateStandardLayout();

        var lblTitle = new Label
        {
            Text = "DryRun - Simulation",
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        };
        layout.Controls.Add(lblTitle, 0, 0);

        var txtLog = CreateReadOnlyMultiline();
        layout.Controls.Add(txtLog, 0, 1);

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
                
                SetNextButtonHandler((s, e) => ShowExecution());
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
        var layout = CreateStandardLayout();

        var lblTitle = new Label
        {
            Text = "Migration läuft...",
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        };
        layout.Controls.Add(lblTitle, 0, 0);

        var txtLog = CreateReadOnlyMultiline();
        layout.Controls.Add(txtLog, 0, 1);

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

                var logs = _orchestrator.GetAllLogs()
                    .Where(l => l.Category == "Executor")
                    .Select(l => l.ToString());
                txtLog.Text = string.Join(Environment.NewLine, logs);

                if (result.Success.GetValueOrDefault())
                {
                    lblStatus.Text = $"Migration erfolgreich abgeschlossen!";
                    MessageBox.Show($"Migration erfolgreich!\n\n{result.SuccessfulSteps.Count} Schritte abgeschlossen.\n\nDauer: {result.Duration:mm\\:ss}",
                        "Erfolg", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    lblStatus.Text = $"Migration mit Fehlern abgeschlossen: {result.SuccessfulSteps.Count} erfolgreich, {result.FailedSteps.Count} fehlgeschlagen";
                    var failureDetail = result.FailedSteps.Any() 
                        ? $"\n\nFehlerhafte Schritte:\n{string.Join("\n", result.FailedSteps.Take(10).Select(s => $"• {s.AppName}: {s.Description} - {s.ErrorMessage}"))}"
                        : "";
                    MessageBox.Show($"{result.Message}\n\nErfolgreich: {result.SuccessfulSteps.Count}\nFehlgeschlagen: {result.FailedSteps.Count}\nDauer: {result.Duration:mm\\:ss}{failureDetail}",
                        "Migration abgeschlossen (mit Fehlern)", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
        var layout = CreateStandardLayout();

        var lblTitle = new Label
        {
            Text = "Überwachung",
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        };
        layout.Controls.Add(lblTitle, 0, 0);

        var txtStatus = CreateReadOnlyMultiline();
        layout.Controls.Add(txtStatus, 0, 1);

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
        var layout = CreateStandardLayout(includeFooter: true);

        var lblTitle = new Label
        {
            Text = "Migration abgeschlossen!",
            Font = new Font(Font.FontFamily, 16, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        };
        layout.Controls.Add(lblTitle, 0, 0);

        var txtInfo = CreateReadOnlyMultiline();
        txtInfo.Text = @"Die Migration wurde erfolgreich abgeschlossen!

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

Vielen Dank für die Nutzung von ProgramMover!";
        layout.Controls.Add(txtInfo, 0, 1);

        var btnCleanup = new Button
        {
            Text = "Cleanup: .old-Verzeichnisse anzeigen",
            AutoSize = true
        };
        btnCleanup.Click += async (s, e) =>
        {
            var result = await _orchestrator.CleanupOldDirectories(_selectedApps, false);
            MessageBox.Show($"Gefundene .old-Verzeichnisse: {result.PendingDirectories.Count}\n\n" +
                          string.Join("\n", result.PendingDirectories.Take(10)) +
                          (result.PendingDirectories.Count > 10 ? $"\n... und {result.PendingDirectories.Count - 10} weitere" : ""),
                          "Cleanup-Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 10, 0, 0)
        };
        footer.Controls.Add(btnCleanup);
        layout.Controls.Add(footer, 0, 2);

        SetNextButtonHandler((s, e) => Close());

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
