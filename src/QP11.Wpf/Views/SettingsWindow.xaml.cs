using System;
using System.Data;
using System.IO;
using System.Windows;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using QP11.Services.Update;

namespace QP11.Wpf.Views;

public partial class SettingsWindow : Window
{
    private readonly IDatabaseInfoService _dbInfoService;

    public SettingsWindow(IDatabaseInfoService dbInfoService)
    {
        _dbInfoService = dbInfoService;
        InitializeComponent();
        cboProvider.SelectedIndex = _dbInfoService.Provider.Equals("Odbc", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
        LoadCompanyInfo();
        txtCurrentVer.Text = $"当前版本：v{UpdateService.GetCurrentVersion()}";
    }

    private async void LoadCompanyInfo()
    {
        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            var row = await db.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT TOP 1 * FROM business_infor");
            if (row != null)
            {
                txtCompanyName.Text = row.qc?.ToString() ?? "";
                txtCompanyPhone.Text = row.tel?.ToString() ?? row.mobile?.ToString() ?? "";
                txtCompanyAddr.Text = row.address?.ToString() ?? "";
                txtTaxNo.Text = row.tax?.ToString() ?? "";
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "加载公司信息失败");
        }
    }

    private async void BtnSaveCompany_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            var existing = await db.QueryFirstOrDefaultAsync<int?>("SELECT TOP 1 1 FROM business_infor");
            if (existing.HasValue)
            {
                await db.ExecuteAsync(
                    "UPDATE business_infor SET qc=@Name, tel=@Phone, address=@Addr, tax=@TaxNo",
                    new { Name = txtCompanyName.Text, Phone = txtCompanyPhone.Text, Addr = txtCompanyAddr.Text, TaxNo = txtTaxNo.Text });
            }
            else
            {
                await db.ExecuteAsync(
                    "INSERT INTO business_infor (qc, tel, address, tax) VALUES (@Name, @Phone, @Addr, @TaxNo)",
                    new { Name = txtCompanyName.Text, Phone = txtCompanyPhone.Text, Addr = txtCompanyAddr.Text, TaxNo = txtTaxNo.Text });
            }
            MessageBox.Show("公司信息保存成功", "提示");
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "保存公司信息失败"); MessageBox.Show($"保存失败: {ex.Message}", "错误"); }
    }

    private async void BtnTestConnection_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            var result = await db.QueryFirstOrDefaultAsync<int>("SELECT 1");
            MessageBox.Show("数据库连接成功!", "测试结果", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "数据库连接测试失败"); MessageBox.Show($"连接失败:\n{ex.Message}", "测试结果", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async void BtnBackup_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "备份文件|*.bak|SQL脚本|*.sql",
            FileName = $"qipei_backup_{DateTime.Now:yyyyMMdd_HHmmss}.bak",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            txtBackupStatus.Text = "正在备份...";
            IsEnabled = false;

            // 远程SQL Server(ODBC模式): BACKUP DATABASE 只能写到服务端本地磁盘
            // 改用导出SQL脚本的方式备份到用户选择的本地路径
            if (_dbInfoService.Provider.Equals("Odbc", StringComparison.OrdinalIgnoreCase))
            {
                await BackupAsSqlScriptAsync(dlg.FileName);
                txtBackupStatus.Text = $"备份完成(SQL脚本): {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n路径: {dlg.FileName}";
                MessageBox.Show($"备份成功!\n已导出为SQL脚本文件。\n文件: {dlg.FileName}", "提示");
            }
            else
            {
                var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
                await db.ExecuteAsync($"BACKUP DATABASE qipei TO DISK = '{dlg.FileName}' WITH FORMAT");
                txtBackupStatus.Text = $"备份完成: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n路径: {dlg.FileName}";
                MessageBox.Show($"备份成功!\n文件: {dlg.FileName}", "提示");
            }
        }
        catch (Exception ex) { txtBackupStatus.Text = $"备份失败: {ex.Message}"; MessageBox.Show($"备份失败: {ex.Message}", "错误"); }
        finally { IsEnabled = true; }
    }

    /// <summary>将数据库所有表数据导出为SQL脚本（用于远程SQL Server的本地备份）</summary>
    private async Task BackupAsSqlScriptAsync(string filePath)
    {
        var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        using var db = await dbFactory.CreateAsync();
        var tables = await db.QueryAsync<string>(
            "SELECT name FROM sysobjects WHERE xtype='U' ORDER BY name");

        using var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8);
        writer.WriteLine("-- QP11 数据库备份脚本");
        writer.WriteLine("-- 生成时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        writer.WriteLine();

        foreach (var tableName in tables)
        {
            bool identityOn = false;
            try
            {
                writer.WriteLine($"-- ========== 表: {tableName} ==========");
                // 使用强类型查询替代 dynamic，避免 SQL Server 2000 ODBC 的 RuntimeBinderException
                var columnRows = await db.QueryAsync($@"
                    SELECT c.name AS colname, t.name AS typename
                    FROM syscolumns c JOIN systypes t ON c.xtype=t.xtype
                    WHERE c.id = OBJECT_ID('{tableName}')
                    ORDER BY c.colorder");

                var colNames = new List<string>();
                var colTypes = new List<string>();
                foreach (IDictionary<string, object> col in columnRows)
                {
                    colNames.Add((col["colname"] ?? "").ToString()!);
                    colTypes.Add((col["typename"] ?? "").ToString()!);
                }

                writer.WriteLine($"DELETE FROM [{tableName}];");
                writer.WriteLine($"SET IDENTITY_INSERT [{tableName}] ON;");
                identityOn = true;

                // 分批导出数据
                var rows = await db.QueryAsync($"SELECT * FROM [{tableName}]");
                foreach (IDictionary<string, object> row in rows)
                {
                    var values = new List<string>();
                    for (int i = 0; i < colNames.Count; i++)
                    {
                        var val = row.ContainsKey(colNames[i]) ? row[colNames[i]] : null;
                        var typeName = colTypes[i];
                        if (val == null || val == DBNull.Value)
                            values.Add("NULL");
                        else if (typeName.Contains("char") || typeName.Contains("text") || typeName.Contains("nvarchar") || typeName.Contains("varchar"))
                            values.Add("'" + val!.ToString()!.Replace("'", "''") + "'");
                        else if (typeName.Contains("datetime"))
                            values.Add("'" + Convert.ToDateTime(val).ToString("yyyy-MM-dd HH:mm:ss.fff") + "'");
                        else
                            values.Add(val!.ToString()!);
                    }
                    writer.WriteLine($"INSERT INTO [{tableName}] ({string.Join(", ", colNames)}) VALUES ({string.Join(", ", values)});");
                }
            }
            catch (Exception ex)
            {
                writer.WriteLine($"-- 导出表 [{tableName}] 失败: {ex.Message}");
            }
            finally
            {
                if (identityOn)
                    writer.WriteLine($"SET IDENTITY_INSERT [{tableName}] OFF;");
                writer.WriteLine();
            }
        }

        await writer.FlushAsync();
    }

    private async void BtnRestore_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "备份文件|*.bak;*.sql",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        };
        if (dlg.ShowDialog() != true) return;

        if (MessageBox.Show("恢复数据将覆盖当前数据库，确认继续?", "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        try
        {
            txtBackupStatus.Text = "正在恢复...";
            IsEnabled = false;
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();

            // ODBC远程模式：备份文件实际是SQL脚本（无论扩展名是什么），逐条执行恢复
            if (_dbInfoService.Provider.Equals("Odbc", StringComparison.OrdinalIgnoreCase)
                || dlg.FileName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            {
                var rawSql = File.ReadAllText(dlg.FileName);
                var sqlStatements = rawSql.Split(new[] { ";\r\n", ";\n", ";\r" }, StringSplitOptions.RemoveEmptyEntries);
                int count = 0, failCount = 0;
                var firstError = "";
                // 使用原生 ADO.NET 保持同一物理连接打开，确保 IDENTITY_INSERT 会话状态不丢失
                if (db.State != System.Data.ConnectionState.Open) await db.OpenAsync();
                using var cmd = db.CreateCommand();
                foreach (var sql in sqlStatements)
                {
                    var trimmed = sql.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("--")) continue;
                    try
                    {
                        cmd.CommandText = trimmed;
                        cmd.ExecuteNonQuery();
                        count++;
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        if (string.IsNullOrEmpty(firstError))
                            firstError = $"SQL: {trimmed.Substring(0, Math.Min(trimmed.Length, 80))}...\n错误: {ex.Message}";
                    }
                }
                txtBackupStatus.Text = $"恢复完成: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n成功 {count} 条, 失败 {failCount} 条";
                MessageBox.Show(failCount == 0
                    ? "数据恢复成功!"
                    : $"恢复完成（部分语句失败，已跳过）\n\n成功: {count} 条\n失败: {failCount} 条\n\n首条错误:\n{firstError}",
                    failCount > 0 ? "警告" : "提示",
                    MessageBoxButton.OK,
                    failCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
            }
            else
            {
                // .bak 文件 RESTORE（仅本地SqlClient模式）
                await db.ExecuteAsync($"USE master; ALTER DATABASE qipei SET SINGLE_USER WITH ROLLBACK IMMEDIATE; RESTORE DATABASE qipei FROM DISK = '{dlg.FileName}' WITH REPLACE; ALTER DATABASE qipei SET MULTI_USER;");
                txtBackupStatus.Text = $"恢复完成: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                MessageBox.Show("数据恢复成功!", "提示");
            }
        }
        catch (Exception ex) { txtBackupStatus.Text = $"恢复失败: {ex.Message}"; MessageBox.Show($"恢复失败: {ex.Message}", "错误"); }
        finally { IsEnabled = true; }
    }

    private async void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        var updateService = App.UpdateService;
        if (updateService == null)
        {
            MessageBox.Show("更新服务未初始化", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        btnCheckUpdate.IsEnabled = false;
        txtUpdateStatus.Text = "正在检查更新...";

        try
        {
            var update = await updateService.CheckUpdateAsync();
            if (update == null)
            {
                txtUpdateStatus.Text = "当前已是最新版本";
                return;
            }

            var dialog = new UpdateWindow(update, updateService);
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            txtUpdateStatus.Text = $"检查更新失败：{ex.Message}";
        }
        finally
        {
            btnCheckUpdate.IsEnabled = true;
        }
    }
}
