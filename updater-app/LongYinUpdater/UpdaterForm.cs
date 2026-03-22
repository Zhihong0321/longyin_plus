using System.Diagnostics;
using System.Text;

namespace LongYinUpdater;

internal sealed class UpdaterForm : Form
{
    private readonly UpdateOptions _options;
    private readonly Label _statusLabel;
    private readonly ProgressBar _progressBar;
    private readonly TextBox _detailsBox;
    private readonly Button _openLogButton;
    private readonly Button _closeButton;

    public UpdaterForm(UpdateOptions options)
    {
        _options = options;

        Text = "龙胤立志传 Pro Max 更新器";
        Width = 720;
        Height = 420;
        MinimumSize = new Size(680, 380);
        StartPosition = FormStartPosition.CenterScreen;

        var titleLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 52,
            Padding = new Padding(18, 18, 18, 0),
            Text = $"正在更新 龙胤立志传 Pro Max 到 {_options.Version}",
            Font = new Font(Font, FontStyle.Bold)
        };

        _statusLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 38,
            Padding = new Padding(18, 4, 18, 0),
            Text = "准备启动更新器..."
        };

        _progressBar = new ProgressBar
        {
            Dock = DockStyle.Top,
            Height = 20,
            Margin = new Padding(18),
            Minimum = 0,
            Maximum = 100,
            Value = 0
        };

        _detailsBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = SystemColors.Window,
            Font = new Font("Consolas", 9f),
            Margin = new Padding(18)
        };

        _openLogButton = new Button
        {
            Text = "打开日志",
            AutoSize = true,
            Enabled = false
        };
        _openLogButton.Click += (_, _) => OpenLogPath();

        _closeButton = new Button
        {
            Text = "关闭",
            AutoSize = true,
            Enabled = false
        };
        _closeButton.Click += (_, _) => Close();

        var buttonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            Padding = new Padding(18, 8, 18, 8),
            FlowDirection = FlowDirection.RightToLeft
        };
        buttonsPanel.Controls.Add(_closeButton);
        buttonsPanel.Controls.Add(_openLogButton);

        var contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18, 0, 18, 18)
        };
        contentPanel.Controls.Add(_detailsBox);
        contentPanel.Controls.Add(_progressBar);
        contentPanel.Controls.Add(_statusLabel);
        contentPanel.Controls.Add(titleLabel);

        Controls.Add(contentPanel);
        Controls.Add(buttonsPanel);

        Shown += async (_, _) => await RunUpdateAsync();
    }

    private async Task RunUpdateAsync()
    {
        try
        {
            await UpdateRunner.RunAsync(_options, ReportProgress);
            ReportProgress("更新完成，正在重新启动应用...", 100);
            await Task.Delay(1200);
            Close();
        }
        catch (Exception ex)
        {
            ReportProgress($"更新失败：{ex.Message}", Math.Max(_progressBar.Value, 1));
            AppendDetail($"ERROR: {ex}");
            _openLogButton.Enabled = File.Exists(_options.LogPath);
            _closeButton.Enabled = true;
            MessageBox.Show(
                $"更新失败：{ex.Message}\n\n你可以点击“打开日志”查看详细信息。",
                "龙胤立志传 Pro Max 更新器",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ReportProgress(string message, int percent)
    {
        var clampedPercent = Math.Max(0, Math.Min(100, percent));
        _statusLabel.Text = message;
        _progressBar.Value = clampedPercent;
        AppendDetail($"[{DateTime.Now:HH:mm:ss}] {message}");
    }

    private void AppendDetail(string message)
    {
        if (_detailsBox.TextLength > 0)
        {
            _detailsBox.AppendText(Environment.NewLine);
        }

        _detailsBox.AppendText(message);
    }

    private void OpenLogPath()
    {
        if (!File.Exists(_options.LogPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = _options.LogPath,
            UseShellExecute = true
        });
    }

    private static class UpdateRunner
    {
        private static readonly string[] ObsoleteRelativePaths =
        {
            Path.Combine("resources", "updater", "apply-ota-update.cmd")
        };

        public static async Task RunAsync(UpdateOptions options, Action<string, int> report)
        {
            EnsureLogDirectory(options.LogPath);
            Log(options.LogPath, $"Updater started. version={options.Version} waitPid={options.WaitPid}");
            report("等待旧版本退出...", 3);

            await WaitForProcessExitAsync(options, report);

            if (!Directory.Exists(options.SourceRoot))
            {
                throw new DirectoryNotFoundException($"未找到更新暂存目录：{options.SourceRoot}");
            }

            if (!Directory.Exists(options.TargetRoot))
            {
                throw new DirectoryNotFoundException($"未找到目标目录：{options.TargetRoot}");
            }

            RemoveObsoletePaths(options.TargetRoot, options.LogPath);
            report("正在扫描待替换文件...", 12);
            var files = Directory.GetFiles(options.SourceRoot, "*", SearchOption.AllDirectories);
            Log(options.LogPath, $"Stage file count={files.Length}");

            for (var index = 0; index < files.Length; index++)
            {
                var sourceFile = files[index];
                var relativePath = Path.GetRelativePath(options.SourceRoot, sourceFile);
                var targetFile = Path.Combine(options.TargetRoot, relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
                await CopyWithRetryAsync(sourceFile, targetFile, options.LogPath);

                var percent = 15 + (int)Math.Round(((index + 1d) / Math.Max(1, files.Length)) * 70d);
                report($"正在替换文件：{relativePath}", percent);
            }

            report("正在清理更新暂存目录...", 88);
            TryDeleteStageRoot(options.SourceRoot, options.LogPath);

            report("正在重新启动主程序...", 94);
            StartMainApp(options);
            Log(options.LogPath, $"Relaunched app: {options.TargetExecutablePath}");
        }

        private static void RemoveObsoletePaths(string targetRoot, string logPath)
        {
            foreach (var relativePath in ObsoleteRelativePaths)
            {
                var targetPath = Path.Combine(targetRoot, relativePath);
                try
                {
                    if (File.Exists(targetPath))
                    {
                        File.Delete(targetPath);
                        Log(logPath, $"Removed obsolete file: {targetPath}");
                    }
                }
                catch (Exception ex)
                {
                    Log(logPath, $"Skipped obsolete file cleanup for {targetPath}: {ex.Message}");
                }
            }
        }

        private static async Task WaitForProcessExitAsync(UpdateOptions options, Action<string, int> report)
        {
            if (options.WaitPid <= 0)
            {
                return;
            }

            for (var attempt = 0; attempt < 240; attempt++)
            {
                if (!IsProcessAlive(options.WaitPid))
                {
                    await Task.Delay(800);
                    return;
                }

                if (attempt % 4 == 0)
                {
                    var elapsedSeconds = attempt / 2;
                    report($"等待旧版本退出... {elapsedSeconds}s", Math.Min(10, 3 + attempt / 8));
                }

                await Task.Delay(500);
            }

            throw new TimeoutException("主程序长时间未退出，无法继续替换文件。");
        }

        private static bool IsProcessAlive(int pid)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        private static async Task CopyWithRetryAsync(string sourceFile, string targetFile, string logPath)
        {
            const int maxAttempts = 120;
            Exception? lastError = null;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    File.Copy(sourceFile, targetFile, overwrite: true);
                    return;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    lastError = ex;
                    if (attempt == 1 || attempt % 10 == 0)
                    {
                        Log(logPath, $"Copy retry {attempt}/{maxAttempts} for {targetFile}: {ex.Message}");
                    }

                    await Task.Delay(500);
                }
            }

            throw new IOException($"替换文件失败：{targetFile}", lastError);
        }

        private static void TryDeleteStageRoot(string stageRoot, string logPath)
        {
            try
            {
                if (Directory.Exists(stageRoot))
                {
                    Directory.Delete(stageRoot, recursive: true);
                }
            }
            catch (Exception ex)
            {
                Log(logPath, $"Stage cleanup skipped: {ex.Message}");
            }
        }

        private static void StartMainApp(UpdateOptions options)
        {
            if (!File.Exists(options.TargetExecutablePath))
            {
                throw new FileNotFoundException($"更新后的主程序不存在：{options.TargetExecutablePath}");
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = options.TargetExecutablePath,
                WorkingDirectory = options.TargetRoot,
                UseShellExecute = true
            });
        }

        private static void EnsureLogDirectory(string logPath)
        {
            var directory = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static void Log(string logPath, string message)
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
            File.AppendAllText(logPath, line, Encoding.UTF8);
        }
    }
}
