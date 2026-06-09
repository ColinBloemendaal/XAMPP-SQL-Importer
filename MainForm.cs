using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SqlImporter
{
    // ── Persisted config ──────────────────────────────────────────────────────
    public class AppConfig
    {
        public string MysqlPath { get; set; } = "";
    }

    public class MainForm : Form
    {
        // ── Controls ──────────────────────────────────────────────────────────
        private TextBox     txtMysqlPath = null!;
        private ComboBox    cboDatabase  = null!;
        private TextBox     txtUsername  = null!;
        private TextBox     txtPassword  = null!;
        private TextBox     txtSqlFile   = null!;
        private Button      btnImport        = null!;
        private Button      btnRefreshDb     = null!;
        private CheckBox    chkSelfContained = null!;
        private RichTextBox rtbLog           = null!;
        private Label       lblStatus        = null!;

        // ── Colours ───────────────────────────────────────────────────────────
        private static readonly Color BgDark       = Color.FromArgb( 18,  18,  24);
        private static readonly Color BgPanel      = Color.FromArgb( 28,  28,  36);
        private static readonly Color BgInput      = Color.FromArgb( 38,  38,  50);
        private static readonly Color AccentOrange = Color.FromArgb(255, 140,  50);
        private static readonly Color AccentGreen  = Color.FromArgb( 80, 220, 130);
        private static readonly Color AccentRed    = Color.FromArgb(255,  85,  85);
        private static readonly Color TextPrimary  = Color.FromArgb(230, 230, 240);
        private static readonly Color TextMuted    = Color.FromArgb(140, 140, 160);
        private static readonly Color BorderColor  = Color.FromArgb( 55,  55,  72);

        // ── Config path — stored next to the EXE ─────────────────────────────
        private static readonly string ConfigPath = Path.Combine(
            AppContext.BaseDirectory, "config.json");

        public MainForm()
        {
            BuildUI();
            Shown += async (s, e) =>
            {
                LoadConfig();
                AutoDetectMysql();
                await LoadDatabasesAsync();
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Config load / save
        // ─────────────────────────────────────────────────────────────────────
        private void LoadConfig()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return;
                var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath));
                if (cfg == null) return;

                if (!string.IsNullOrWhiteSpace(cfg.MysqlPath) && File.Exists(cfg.MysqlPath))
                {
                    txtMysqlPath.Text = cfg.MysqlPath;
                    LogLine($"[CONFIG] Loaded saved path: {cfg.MysqlPath}", AccentGreen);
                }
            }
            catch { /* ignore corrupt config */ }
        }

        private void SaveConfig(string mysqlPath)
        {
            try
            {
                var cfg = new AppConfig { MysqlPath = mysqlPath };
                File.WriteAllText(ConfigPath, JsonSerializer.Serialize(cfg,
                    new JsonSerializerOptions { WriteIndented = true }));
                LogLine($"[CONFIG] Saved path to config.json", TextMuted);
            }
            catch (Exception ex)
            {
                LogLine($"[CONFIG] Could not save config: {ex.Message}", AccentRed);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Auto-detect mysql.exe
        // ─────────────────────────────────────────────────────────────────────
        private void AutoDetectMysql()
        {
            // Already set and valid (from config or default) — skip scan
            if (File.Exists(txtMysqlPath.Text.Trim()))
            {
                LogLine($"[INFO]  Using mysql.exe at: {txtMysqlPath.Text.Trim()}", TextMuted);
                return;
            }

            LogLine("[INFO]  Auto-detecting mysql.exe…", TextMuted);

            var drives = new[] { "C", "D", "E", "F", "G" };
            var subPaths = new[]
            {
                @"xampp\mysql\bin\mysql.exe",
                @"xampp\bin\mysql.exe",
                @"XAMPP\mysql\bin\mysql.exe",
                @"Development\XAMPP\mysql\bin\mysql.exe",
                @"Development\xampp\mysql\bin\mysql.exe",
                @"Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe",
                @"Program Files\MySQL\MySQL Server 5.7\bin\mysql.exe",
                @"Program Files (x86)\MySQL\MySQL Server 8.0\bin\mysql.exe",
                @"Program Files\MariaDB 10.11\bin\mysql.exe",
                @"Program Files\MariaDB 10.6\bin\mysql.exe",
            };

            var xamppEnv = Environment.GetEnvironmentVariable("XAMPP_HOME");

            foreach (var drive in drives)
            {
                foreach (var sub in subPaths)
                {
                    var candidate = $@"{drive}:\{sub}";
                    if (File.Exists(candidate))
                    {
                        txtMysqlPath.Text = candidate;
                        LogLine($"[FOUND] {candidate}", AccentGreen);
                        SetStatus($"Auto-detected: {candidate}", AccentGreen);
                        return;
                    }
                }
            }

            if (!string.IsNullOrEmpty(xamppEnv))
            {
                var candidate = Path.Combine(xamppEnv, @"mysql\bin\mysql.exe");
                if (File.Exists(candidate))
                {
                    txtMysqlPath.Text = candidate;
                    LogLine($"[FOUND] {candidate} (via XAMPP_HOME)", AccentGreen);
                    SetStatus($"Auto-detected: {candidate}", AccentGreen);
                    return;
                }
            }

            LogLine("[WARN]  Could not auto-detect mysql.exe. Use Browse… to locate it.", Color.FromArgb(255, 200, 80));
            SetStatus("mysql.exe not found — use Browse… to locate it.", AccentRed);
        }

        // ─────────────────────────────────────────────────────────────────────
        // UI construction
        // ─────────────────────────────────────────────────────────────────────
        private void BuildUI()
        {
            Text            = "XAMPP SQL Importer";
            ClientSize      = new Size(680, 646);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            MinimumSize     = new Size(680, 646);
            BackColor       = BgDark;
            ForeColor       = TextPrimary;
            Font            = new Font("Segoe UI", 9.5f);
            StartPosition   = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox     = false;

            // ── Header ───────────────────────────────────────────────────────
            var pnlTop = new Panel { Location = new Point(0, 0), Size = new Size(680, 64), BackColor = BgPanel };
            pnlTop.Controls.Add(new Label { Text = "⚡  XAMPP SQL Importer", Font = new Font("Segoe UI", 14f, FontStyle.Bold), ForeColor = AccentOrange, Location = new Point(20, 12), AutoSize = true });
            pnlTop.Controls.Add(new Label { Text = "Fast command-line import for XAMPP MySQL", Font = new Font("Segoe UI", 8.5f), ForeColor = TextMuted, Location = new Point(22, 40), AutoSize = true });
            Controls.Add(pnlTop);

            // ── Settings card ────────────────────────────────────────────────
            var card = MakeCard(new Point(20, 82), new Size(640, 334));
            int cx = 18;

            // Row 0: MySQL path
            AddFieldLabel(card, "MySQL Executable", cx, 18);
            txtMysqlPath = AddTextBox(card, cx, 36, 490, @"C:\xampp\mysql\bin\mysql.exe");
            var btnBrowseMysql = AddSmallButton(card, cx + 496, 35, "Browse…");
            btnBrowseMysql.Click += async (s, e) =>
            {
                using var dlg = new OpenFileDialog { Filter = "MySQL Executable|mysql.exe|All Files|*.*" };
                if (dlg.ShowDialog() != DialogResult.OK) return;

                txtMysqlPath.Text = dlg.FileName;

                // Test it works before saving
                SetStatus("Testing selected mysql.exe…", TextMuted);
                bool ok = await TestMysqlPathAsync(dlg.FileName);
                if (ok)
                {
                    SaveConfig(dlg.FileName);
                    SetStatus($"✔  Saved as default: {dlg.FileName}", AccentGreen);
                    LogLine($"[CONFIG] Path verified and saved as default.", AccentGreen);
                    await LoadDatabasesAsync();
                }
                else
                {
                    SetStatus("⚠  Path selected but MySQL didn't respond — not saved.", AccentRed);
                    LogLine("[WARN]  Selected mysql.exe did not respond. Check that XAMPP is running.", Color.FromArgb(255, 200, 80));
                }
            };

            // Row 1: Username + Password
            AddFieldLabel(card, "Username", cx, 80);
            txtUsername = AddTextBox(card, cx, 98, 150, "root");

            AddFieldLabel(card, "Password", cx + 160, 80);
            txtPassword = AddTextBox(card, cx + 160, 98, 150, "");
            txtPassword.UseSystemPasswordChar = true;

            // Row 2: Database dropdown + self-contained toggle
            var lblDb = AddFieldLabel(card, "Database", cx, 142);
            chkSelfContained = new CheckBox
            {
                Text      = "SQL file creates its own database",
                Location  = new Point(cx, 192),
                AutoSize  = true,
                ForeColor = TextMuted,
                BackColor = Color.Transparent,
                Font      = new Font("Segoe UI", 8.5f),
                Cursor    = Cursors.Hand
            };
            chkSelfContained.CheckedChanged += (s, e) =>
            {
                bool self = chkSelfContained.Checked;
                cboDatabase.Enabled  = !self;
                btnRefreshDb.Enabled = !self;
                cboDatabase.BackColor  = self ? Color.FromArgb(28, 28, 36) : BgInput;
                chkSelfContained.ForeColor = self ? AccentOrange : TextMuted;
            };
            card.Controls.Add(chkSelfContained);

            cboDatabase = new ComboBox
            {
                Location      = new Point(cx, 160),
                Size          = new Size(490, 28),
                BackColor     = BgInput,
                ForeColor     = TextPrimary,
                FlatStyle     = FlatStyle.Flat,
                Font          = new Font("Segoe UI", 9.5f),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            card.Controls.Add(cboDatabase);

            btnRefreshDb = AddSmallButton(card, cx + 496, 159, "↻  Refresh");
            btnRefreshDb.Click += async (s, e) => await LoadDatabasesAsync();

            // Row 3: SQL File
            AddFieldLabel(card, "SQL File", cx, 220);
            txtSqlFile = AddTextBox(card, cx, 238, 490, "");
            txtSqlFile.PlaceholderText = "Browse… to select a .sql file";
            var btnBrowseSql = AddSmallButton(card, cx + 496, 237, "Browse…");
            btnBrowseSql.Click += (s, e) => BrowseFile(txtSqlFile, "SQL Files|*.sql|All Files|*.*");

            // Import button
            btnImport = new Button
            {
                Text      = "▶   Run Import",
                Location  = new Point(cx, 280),
                Size      = new Size(604, 44),
                BackColor = AccentOrange,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                Cursor    = Cursors.Hand
            };
            btnImport.FlatAppearance.BorderSize = 0;
            btnImport.Click += BtnImport_Click;
            HoverColor(btnImport, AccentOrange, Color.FromArgb(255, 165, 80));
            card.Controls.Add(btnImport);
            Controls.Add(card);

            // ── Status label ─────────────────────────────────────────────────
            lblStatus = new Label
            {
                Location  = new Point(22, 426),
                Size      = new Size(640, 20),
                ForeColor = TextMuted,
                Text      = "Starting…",
                Font      = new Font("Segoe UI", 8.5f)
            };
            Controls.Add(lblStatus);

            // ── Log card ─────────────────────────────────────────────────────
            var pnlLog = MakeCard(new Point(20, 452), new Size(640, 172));
            pnlLog.Controls.Add(new Label { Text = "OUTPUT", Location = new Point(14, 10), AutoSize = true, Font = new Font("Consolas", 7.5f, FontStyle.Bold), ForeColor = TextMuted });
            rtbLog = new RichTextBox
            {
                Location    = new Point(10, 30),
                Size        = new Size(620, 130),
                BackColor   = BgInput,
                ForeColor   = TextPrimary,
                BorderStyle = BorderStyle.None,
                Font        = new Font("Consolas", 9f),
                ReadOnly    = true,
                ScrollBars  = RichTextBoxScrollBars.Vertical
            };
            pnlLog.Controls.Add(rtbLog);
            Controls.Add(pnlLog);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Test if a mysql.exe path actually responds
        // ─────────────────────────────────────────────────────────────────────
        private async System.Threading.Tasks.Task<bool> TestMysqlPathAsync(string mysqlPath)
        {
            try
            {
                string username    = txtUsername.Text.Trim();
                string password    = txtPassword.Text;
                string passwordArg = string.IsNullOrWhiteSpace(password) ? "" : $"-p{password}";
                string innerCmd    = $"\"{mysqlPath}\" -u {username} {passwordArg} --batch --skip-column-names -e \"SELECT 1;\"";
                string cmdArgs     = $"/c \"{innerCmd}\"";

                var psi = new ProcessStartInfo
                {
                    FileName               = "cmd.exe",
                    Arguments              = cmdArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var proc = new Process { StartInfo = psi };
                proc.Start();
                await proc.StandardOutput.ReadToEndAsync();
                await proc.WaitForExitAsync();
                return proc.ExitCode == 0;
            }
            catch { return false; }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Load databases
        // ─────────────────────────────────────────────────────────────────────
        private async System.Threading.Tasks.Task LoadDatabasesAsync()
        {
            string mysqlPath = txtMysqlPath.Text.Trim();

            if (!File.Exists(mysqlPath))
            {
                SetStatus($"mysql.exe not found at: {mysqlPath}", AccentRed);
                return;
            }

            string username    = txtUsername.Text.Trim();
            string password    = txtPassword.Text;
            string passwordArg = string.IsNullOrWhiteSpace(password) ? "" : $"-p{password}";
            string innerCmd    = $"\"{mysqlPath}\" -u {username} {passwordArg} --batch --skip-column-names -e \"SHOW DATABASES;\"";
            string cmdArgs     = $"/c \"{innerCmd}\"";

            btnRefreshDb.Enabled = false;
            SetStatus("Connecting to MySQL…", TextMuted);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = "cmd.exe",
                    Arguments              = cmdArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var proc = new Process { StartInfo = psi };
                proc.Start();
                string stdout = await proc.StandardOutput.ReadToEndAsync();
                string stderr = await proc.StandardError.ReadToEndAsync();
                await proc.WaitForExitAsync();

                if (!string.IsNullOrWhiteSpace(stderr))
                    LogLine($"[MYSQL] {stderr.Trim()}", Color.FromArgb(255, 200, 80));

                if (proc.ExitCode != 0)
                {
                    SetStatus($"MySQL error (exit {proc.ExitCode}). Is XAMPP running?", AccentRed);
                    return;
                }

                var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    { "information_schema", "performance_schema", "mysql", "sys" };

                var dbs = new List<string>();
                foreach (var line in stdout.Split('\n'))
                {
                    var db = line.Trim().Trim('\r');
                    if (!string.IsNullOrEmpty(db) && !skip.Contains(db))
                        dbs.Add(db);
                }

                cboDatabase.Items.Clear();
                foreach (var db in dbs) cboDatabase.Items.Add(db);

                if (dbs.Count > 0)
                {
                    cboDatabase.SelectedIndex = 0;
                    SetStatus($"Found {dbs.Count} database(s). Ready.", AccentGreen);
                    LogLine($"[OK]    Loaded {dbs.Count} database(s): {string.Join(", ", dbs)}", AccentGreen);

                    // Auto-save if this path isn't in config yet
                    AutoSavePathIfNew(mysqlPath);
                }
                else
                {
                    SetStatus("Connected — no user databases found.", TextMuted);
                    LogLine("[WARN]  No user databases found.", Color.FromArgb(255, 200, 80));
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Exception: {ex.Message}", AccentRed);
                LogLine($"[EXCEPTION] {ex.Message}", AccentRed);
            }
            finally
            {
                btnRefreshDb.Enabled = true;
            }
        }

        // Save the path automatically if it's not already the saved one
        private void AutoSavePathIfNew(string mysqlPath)
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var existing = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath));
                    if (existing?.MysqlPath == mysqlPath) return; // already saved
                }
                SaveConfig(mysqlPath);
            }
            catch { /* non-critical */ }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Import
        // ─────────────────────────────────────────────────────────────────────
        private async void BtnImport_Click(object? sender, EventArgs e)
        {
            string mysqlPath   = txtMysqlPath.Text.Trim();
            string database    = cboDatabase.SelectedItem?.ToString() ?? "";
            string username    = txtUsername.Text.Trim();
            string password    = txtPassword.Text;
            string sqlFile     = txtSqlFile.Text.Trim();
            string passwordArg = string.IsNullOrWhiteSpace(password) ? "" : $"-p{password}";

            if (!File.Exists(mysqlPath))             { SetStatus("mysql.exe not found.",     AccentRed); return; }
            if (!chkSelfContained.Checked && string.IsNullOrWhiteSpace(database)) { SetStatus("Please select a database, or tick 'SQL file creates its own database'.", AccentRed); return; }
            if (!File.Exists(sqlFile))               { SetStatus("SQL file not found.",       AccentRed); return; }

            // If self-contained, omit the database name from the command
            string dbArg   = chkSelfContained.Checked ? "" : database;
            string cmdArgs = $"/c \"\"{mysqlPath}\" -u {username} {passwordArg} {dbArg} < \"{sqlFile}\"\"";

            btnImport.Enabled = false;
            rtbLog.Clear();
            SetStatus("Importing…", AccentOrange);
            LogLine($"[INFO]  {DateTime.Now:HH:mm:ss}  {(chkSelfContained.Checked ? "Importing (script creates its own database)" : $"Importing into [{database}]")}", TextMuted);
            LogLine($"[FILE]  {sqlFile}", TextMuted);
            LogLine("", TextMuted);

            var psi = new ProcessStartInfo
            {
                FileName               = "cmd.exe",
                Arguments              = cmdArgs,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };

            try
            {
                var sw = Stopwatch.StartNew();
                using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
                proc.OutputDataReceived += (s, ev) => { if (ev.Data != null) BeginInvoke(() => LogLine(ev.Data, TextPrimary)); };
                proc.ErrorDataReceived  += (s, ev) => { if (ev.Data != null) BeginInvoke(() => LogLine(ev.Data, Color.FromArgb(255, 200, 80))); };
                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                await System.Threading.Tasks.Task.Run(() => proc.WaitForExit());
                sw.Stop();

                string elapsed = $"{sw.Elapsed.TotalSeconds:F2}s";
                if (proc.ExitCode == 0)
                {
                    LogLine($"[OK]    Import completed in {elapsed}", AccentGreen);
                    SetStatus($"✔  Import successful  ({elapsed})", AccentGreen);
                }
                else
                {
                    LogLine($"[FAIL]  Exit code {proc.ExitCode} after {elapsed}", AccentRed);
                    SetStatus($"✘  Import failed  (exit code {proc.ExitCode})", AccentRed);
                }
            }
            catch (Exception ex)
            {
                LogLine($"[EXCEPTION] {ex.Message}", AccentRed);
                SetStatus("✘  Exception occurred.", AccentRed);
            }
            finally
            {
                btnImport.Enabled = true;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────
        private void BrowseFile(TextBox target, string filter)
        {
            using var dlg = new OpenFileDialog { Filter = filter };
            if (dlg.ShowDialog() == DialogResult.OK) target.Text = dlg.FileName;
        }

        private void SetStatus(string msg, Color colour)
        {
            if (InvokeRequired) { BeginInvoke(() => SetStatus(msg, colour)); return; }
            lblStatus.ForeColor = colour;
            lblStatus.Text = msg;
        }

        private void LogLine(string text, Color colour)
        {
            if (InvokeRequired) { BeginInvoke(() => LogLine(text, colour)); return; }
            rtbLog.SelectionStart  = rtbLog.TextLength;
            rtbLog.SelectionLength = 0;
            rtbLog.SelectionColor  = colour;
            rtbLog.AppendText(text + "\n");
            rtbLog.ScrollToCaret();
        }

        private Panel MakeCard(Point location, Size size)
        {
            var p = new Panel { Location = location, Size = size, BackColor = BgPanel };
            p.Paint += (s, e) => { using var pen = new Pen(BorderColor, 1); e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1); };
            return p;
        }

        private static Label AddFieldLabel(Control parent, string text, int x, int y)
        {
            var lbl = new Label { Text = text.ToUpperInvariant(), Location = new Point(x, y), AutoSize = true, Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), ForeColor = TextMuted };
            parent.Controls.Add(lbl);
            return lbl;
        }

        private static TextBox AddTextBox(Control parent, int x, int y, int w, string defVal)
        {
            var tb = new TextBox { Location = new Point(x, y), Size = new Size(w, 28), Text = defVal, BackColor = BgInput, ForeColor = TextPrimary, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 9.5f) };
            parent.Controls.Add(tb);
            return tb;
        }

        private static Button AddSmallButton(Control parent, int x, int y, string label)
        {
            var btn = new Button { Text = label, Location = new Point(x, y), Size = new Size(126, 28), BackColor = Color.FromArgb(55, 55, 70), ForeColor = TextPrimary, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9f), Cursor = Cursors.Hand };
            btn.FlatAppearance.BorderColor = Color.FromArgb(55, 55, 72);
            btn.FlatAppearance.BorderSize  = 1;
            parent.Controls.Add(btn);
            return btn;
        }

        private static void HoverColor(Button btn, Color normal, Color hover)
        {
            btn.MouseEnter += (s, e) => btn.BackColor = hover;
            btn.MouseLeave += (s, e) => btn.BackColor = normal;
        }
    }

    static class Program
    {
        private static readonly string InstallDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "XAMPP SQL Importer");

        private static readonly string InstallExe = Path.Combine(InstallDir, "SqlImporter.exe");

        private const string UninstallRegKey =
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall\XamppSqlImporter";

        // Stable identity that ties the running process to the pinned taskbar shortcut
        private const string AppId = "ColinBloemendaal.XamppSqlImporter";

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern void SetCurrentProcessExplicitAppUserModelID(string appId);

        [STAThread]
        static void Main(string[] args)
        {
            SetCurrentProcessExplicitAppUserModelID(AppId);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);

            if (Array.Exists(args, a => a.Equals("/uninstall", StringComparison.OrdinalIgnoreCase)))
            {
                RunUninstall();
                return;
            }

            string currentExe = Process.GetCurrentProcess().MainModule!.FileName;
            bool runningFromInstallDir = string.Equals(
                Path.GetFullPath(currentExe), Path.GetFullPath(InstallExe),
                StringComparison.OrdinalIgnoreCase);

            if (!runningFromInstallDir)
            {
                bool alreadyInstalled = File.Exists(InstallExe);
                string msg = alreadyInstalled
                    ? "Update the existing XAMPP SQL Importer installation?"
                    : "Install XAMPP SQL Importer to your Programs folder?\n\nThe app will be available from Start and can be uninstalled from Settings.";

                var answer = MessageBox.Show(msg, "XAMPP SQL Importer Setup",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button1);

                if (answer == DialogResult.Yes)
                {
                    RunInstall(currentExe);
                    return;
                }
            }

            Application.Run(new MainForm());
        }

        // ── Install ───────────────────────────────────────────────────────────
        private static void RunInstall(string sourceExe)
        {
            try
            {
                Directory.CreateDirectory(InstallDir);
                File.Copy(sourceExe, InstallExe, overwrite: true);

                RegisterUninstaller();
                CreateStartMenuShortcut(InstallExe);

                Process.Start(new ProcessStartInfo(InstallExe) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Installation failed:\n{ex.Message}", "Setup Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void RegisterUninstaller()
        {
            using var key = Registry.CurrentUser.CreateSubKey(UninstallRegKey);
            key.SetValue("DisplayName",          "XAMPP SQL Importer");
            key.SetValue("DisplayIcon",          $"{InstallExe},0");
            key.SetValue("UninstallString",      $"\"{InstallExe}\" /uninstall");
            key.SetValue("QuietUninstallString", $"\"{InstallExe}\" /uninstall /quiet");
            key.SetValue("Publisher",            "ColinBloemendaal");
            key.SetValue("DisplayVersion",       "1.0.0");
            key.SetValue("InstallDate",          DateTime.Now.ToString("yyyyMMdd"));
            key.SetValue("InstallLocation",      InstallDir);
            key.SetValue("NoModify",  1, RegistryValueKind.DWord);
            key.SetValue("NoRepair",  1, RegistryValueKind.DWord);
            key.SetValue("EstimatedSize",
                (int)(new FileInfo(InstallExe).Length / 1024), RegistryValueKind.DWord);
        }

        // Creates the .lnk with an AppUserModelID so the taskbar icon matches the running process
        private static void CreateStartMenuShortcut(string exePath)
        {
            string shortcutPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                "XAMPP SQL Importer.lnk");

            var link = (IShellLinkW)new CShellLink();
            link.SetPath(exePath);
            link.SetWorkingDirectory(InstallDir);
            link.SetDescription("Fast command-line SQL import tool for XAMPP MySQL");
            link.SetIconLocation(exePath, 0);

            // Stamp the same AppId so Windows groups the shortcut with the running process
            var store = (IPropertyStore)link;
            var key   = new PROPERTYKEY
            {
                fmtid = new Guid("{9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3}"),
                pid   = 5
            };
            IntPtr pStr = Marshal.StringToCoTaskMemUni(AppId);
            try
            {
                var pv = new PROPVARIANT { vt = 31 /* VT_LPWSTR */, pwszVal = pStr };
                store.SetValue(ref key, ref pv);
                store.Commit();
            }
            finally { Marshal.FreeCoTaskMem(pStr); }

            ((IPersistFile)link).Save(shortcutPath, true);
        }

        // ── Uninstall ─────────────────────────────────────────────────────────
        private static void RunUninstall()
        {
            var answer = MessageBox.Show(
                "Are you sure you want to uninstall XAMPP SQL Importer?",
                "Uninstall", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (answer != DialogResult.Yes) return;

            try
            {
                string shortcut = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                    "XAMPP SQL Importer.lnk");
                if (File.Exists(shortcut)) File.Delete(shortcut);

                Registry.CurrentUser.DeleteSubKey(UninstallRegKey, throwOnMissingSubKey: false);

                string bat = Path.Combine(Path.GetTempPath(), "xampp_sql_uninstall.bat");
                File.WriteAllText(bat,
                    $"@echo off\r\n" +
                    $"ping -n 3 127.0.0.1 >nul\r\n" +
                    $"rd /s /q \"{InstallDir}\"\r\n" +
                    $"del \"%~f0\"\r\n");

                Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{bat}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow  = true
                });

                MessageBox.Show("XAMPP SQL Importer has been uninstalled.",
                    "Uninstalled", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Uninstall error:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── COM interop ───────────────────────────────────────────────────────

        [ComImport, Guid("00021401-0000-0000-C000-000000000046"), ClassInterface(ClassInterfaceType.None)]
        class CShellLink { }

        [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        [ComImport, Guid("0000010b-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            [PreserveSig] int IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
            void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
        }

        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IPropertyStore
        {
            void GetCount(out uint cProps);
            void GetAt(uint iProp, out PROPERTYKEY pkey);
            void GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
            void SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
            void Commit();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        struct PROPERTYKEY
        {
            public Guid  fmtid;
            public uint  pid;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct PROPVARIANT
        {
            public ushort vt;
            ushort r1, r2, r3;  // reserved padding to reach offset 8
            public IntPtr pwszVal;
        }
    }
}
