using System.IO;
using System.Windows;
using QP11.Services.Update;

namespace QP11.Wpf.Views;

public partial class UpdateWindow : Window
{
    private readonly UpdateInfo _updateInfo;
    private readonly UpdateService _updateService;

    /// <summary>跳过的版本号文件路径（用户级持久化，随重装重置）</summary>
    private static string SkippedVersionFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "QP11", "skipped_version.txt");

    public UpdateWindow(UpdateInfo updateInfo, UpdateService updateService)
    {
        InitializeComponent();
        _updateInfo = updateInfo;
        _updateService = updateService;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        txtVersion.Text = $"v{_updateInfo.Version}";
        txtCurrentVersion.Text = $"当前版本：v{UpdateService.GetCurrentVersion()}";
        txtChangelog.Text = string.IsNullOrEmpty(_updateInfo.Changelog)
            ? "暂无更新说明"
            : _updateInfo.Changelog;
        txtFileSize.Text = _updateInfo.FileSize > 0
            ? $"安装包大小：{FormatFileSize(_updateInfo.FileSize)}"
            : "";

        // 强制更新时隐藏跳过和稍后提醒按钮
        if (_updateInfo.Mandatory)
        {
            btnRemindLater.Visibility = Visibility.Collapsed;
            btnSkip.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>检查指定版本是否已被用户跳过</summary>
    public static bool IsVersionSkipped(Version version)
    {
        try
        {
            if (!File.Exists(SkippedVersionFile)) return false;
            var skipped = File.ReadAllText(SkippedVersionFile, System.Text.Encoding.UTF8).Trim();
            return skipped == version.ToString();
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "检查跳过版本失败"); return false; }
    }

    /// <summary>清除跳过的版本记录（可用于设置页手动重置）</summary>
    public static void ClearSkippedVersion()
    {
        try
        {
            if (File.Exists(SkippedVersionFile))
                File.Delete(SkippedVersionFile);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "清除跳过版本记录失败");
        }
    }

    private void BtnRemindLater_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BtnSkip_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dir = Path.GetDirectoryName(SkippedVersionFile)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(SkippedVersionFile, _updateInfo.Version.ToString(), System.Text.Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "保存跳过版本失败");
        }
        Close();
    }

    private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
    {
        btnUpdate.IsEnabled = false;
        btnRemindLater.IsEnabled = false;
        btnSkip.IsEnabled = false;
        progressPanel.Visibility = Visibility.Visible;

        try
        {
            await _updateService.DownloadAndInstallAsync(_updateInfo, new Progress<(long downloaded, long total)>(p =>
            {
                Dispatcher.Invoke(() =>
                {
                    var percent = p.total > 0 ? (int)(p.downloaded * 100 / p.total) : 0;
                    progressBar.Value = percent;
                    txtProgress.Text = $"{FormatFileSize(p.downloaded)} / {FormatFileSize(p.total)}  ({percent}%)";
                });
            }));
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "更新下载失败");
            MessageBox.Show($"更新失败：\n{ex.Message}", "更新错误", MessageBoxButton.OK, MessageBoxImage.Error);
            btnUpdate.IsEnabled = true;
            btnRemindLater.IsEnabled = true;
            btnSkip.IsEnabled = true;
            progressPanel.Visibility = Visibility.Collapsed;
        }
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
