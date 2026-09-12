using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using Microsoft.Extensions.Configuration;
using QP11.Data.Infrastructure;

namespace QP11.Wpf.Views;

/// <summary>
/// 数据库连接配置窗口：数据库连接失败时弹出，允许修改连接信息并测试。
/// 保存成功后自动将新连接信息写入 appsettings.json（4 个连接串 + Provider），
/// 并重置 DatabaseFactory 缓存立即生效，无需重启程序。
/// </summary>
public partial class DatabaseConfigWindow : Window
{
    /// <summary>是否已完成保存（供调用方判断是否需要重新测试连接）</summary>
    public bool Saved { get; private set; }

    public DatabaseConfigWindow()
    {
        InitializeComponent();
        LoadCurrentSettings();
    }

    /// <summary>从 DatabaseFactory 读取当前连接信息回填界面</summary>
    private void LoadCurrentSettings()
    {
        try
        {
            var (server, port, database, uid, pwd, provider) = DatabaseFactory.GetConnectionInfo();
            txtServer.Text = server;
            txtPort.Text = port.ToString();
            txtDatabase.Text = database;
            txtUser.Text = uid;
            txtPassword.Password = pwd;

            var idx = 2; // 默认 SqlClient
            if (provider.Equals("OleDb", StringComparison.OrdinalIgnoreCase)) idx = 0;
            else if (provider.Equals("Odbc", StringComparison.OrdinalIgnoreCase)) idx = 1;
            cboProvider.SelectedIndex = idx;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "读取数据库配置失败");
        }
    }

    private void CboProvider_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // 仅为兼容绑定初始化触发，无需额外处理
    }

    private string SelectedProvider => (cboProvider.SelectedIndex == 0) ? "OleDb"
        : (cboProvider.SelectedIndex == 1) ? "Odbc" : "SqlClient";

    private bool TryCollect(out string server, out int port, out string database, out string uid, out string pwd)
    {
        server = txtServer.Text.Trim();
        port = 1433;
        database = txtDatabase.Text.Trim();
        uid = txtUser.Text.Trim();
        pwd = txtPassword.Password;

        if (string.IsNullOrEmpty(server))
        {
            MessageBox.Show("请输入服务器IP", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (!int.TryParse(txtPort.Text.Trim(), out port) || port <= 0 || port > 65535)
        {
            MessageBox.Show("端口格式不正确", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (string.IsNullOrEmpty(database))
        {
            MessageBox.Show("请输入数据库名", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        if (string.IsNullOrEmpty(uid))
        {
            MessageBox.Show("请输入用户名", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        return true;
    }

    private async void BtnTest_Click(object sender, RoutedEventArgs e)
    {
        if (!TryCollect(out var server, out var port, out var database, out var uid, out var pwd)) return;

        // 在 UI 线程取值，避免后台线程访问控件
        var provider = SelectedProvider;
        txtStatus.Text = "正在测试连接...";
        BtnTest.IsEnabled = false;
        BtnSave.IsEnabled = false;
        try
        {
            string msg = "";
            var success = await System.Threading.Tasks.Task.Run(() =>
                DatabaseFactory.TestConnectionInfo(server, port, database, uid, pwd, provider, out msg));
            txtStatus.Text = msg;
            txtStatus.Foreground = success
                ? System.Windows.Media.Brushes.Green
                : System.Windows.Media.Brushes.Red;
        }
        catch (Exception ex)
        {
            txtStatus.Text = $"连接测试异常: {ex.Message}";
            txtStatus.Foreground = System.Windows.Media.Brushes.Red;
        }
        finally
        {
            BtnTest.IsEnabled = true;
            BtnSave.IsEnabled = true;
        }
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (!TryCollect(out var server, out var port, out var database, out var uid, out var pwd)) return;

        // 在 UI 线程取值，避免后台线程访问控件
        var provider = SelectedProvider;
        // 先测试连接，通过才写入
        string msg = "";
        var success = await System.Threading.Tasks.Task.Run(() =>
            DatabaseFactory.TestConnectionInfo(server, port, database, uid, pwd, provider, out msg));
        if (!success)
        {
            txtStatus.Text = msg;
            txtStatus.Foreground = System.Windows.Media.Brushes.Red;
            return;
        }

        try
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            SaveToConfigFile(configPath, server, port, database, uid, pwd, SelectedProvider);

            // 重新构建配置并重置 DatabaseFactory，立即生效
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();
            DatabaseFactory.Reinitialize(configuration);

            Saved = true;
            txtStatus.Text = "保存成功，连接配置已生效";
            txtStatus.Foreground = System.Windows.Media.Brushes.Green;
            MessageBox.Show("数据库配置已保存并立即生效！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "保存数据库配置失败");
            txtStatus.Text = $"保存失败: {ex.Message}";
            txtStatus.Foreground = System.Windows.Media.Brushes.Red;
        }
    }

    /// <summary>将连接信息写入 appsettings.json：更新 ConnectionStrings 全部连接串与 Provider，保留其他配置节</summary>
    private static void SaveToConfigFile(string configPath, string server, int port, string database,
        string uid, string pwd, string provider)
    {
        var address = $"{server},{port}";
        var json = JsonNode.Parse(File.ReadAllText(configPath))?.AsObject()
            ?? new JsonObject();

        var connStr = json["ConnectionStrings"]?.AsObject();
        if (connStr == null)
        {
            connStr = new JsonObject();
            json["ConnectionStrings"] = connStr;
        }

        connStr["QipeiDb_OleDb"] = $"Provider=SQLOLEDB;Data Source={address};Initial Catalog={database};User ID={uid};Password={pwd};Connect Timeout=5;";
        connStr["QipeiDb_ODBC_DSN"] = $"DSN=qipei;Uid={uid};Pwd={pwd};";
        connStr["QipeiDb_ODBC_Driver"] = $"Driver={{SQL Server}};Server={address};Database={database};Uid={uid};Pwd={pwd};Connection Timeout=5;";
        connStr["QipeiDb_SqlClient"] = $"Server={address};Database={database};User Id={uid};Password={pwd};TrustServerCertificate=True;MultipleActiveResultSets=True;Max Pool Size=100;Connection Timeout=5;";
        connStr["Provider"] = provider;

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(configPath, json.ToJsonString(options), new UTF8Encoding(false));
        Serilog.Log.Information("数据库连接配置已写入: {Path}, Provider={Provider}, Server={Server}", configPath, provider, address);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}