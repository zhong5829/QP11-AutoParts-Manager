using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using QP11.Services;

namespace QP11.Wpf.Views;

public partial class MigrationWindow : Window
{
    private readonly MigrationService _migrationService;
    private bool _isMigrating;

    public MigrationWindow(MigrationService migrationService)
    {
        InitializeComponent();
        _migrationService = migrationService;

        _migrationService.LogMessage += OnLogMessage;
        _migrationService.ErrorOccurred += OnErrorOccurred;
        _migrationService.ProgressChanged += OnProgressChanged;

        Closed += MigrationWindow_Closed;
    }

    private void MigrationWindow_Closed(object? sender, EventArgs e)
    {
        _migrationService.LogMessage -= OnLogMessage;
        _migrationService.ErrorOccurred -= OnErrorOccurred;
        _migrationService.ProgressChanged -= OnProgressChanged;
    }

    private void BtnTestSource_Click(object sender, RoutedEventArgs e)
    {
        var server = txtSrcServer.Text.Trim();
        var db = txtSrcDb.Text.Trim();
        var user = txtSrcUser.Text.Trim();
        var pwd = txtSrcPwd.Password;

        if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(db) || string.IsNullOrEmpty(user))
        {
            MessageBox.Show("请填写完整的源库连接信息", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        AppendLog("正在测试源库连接...", Colors.Yellow);
        try
        {
            var ok = _migrationService.TestConnection(server, db, user, pwd);
            if (ok)
            {
                AppendLog("源库连接成功！", Colors.LimeGreen);
                MessageBox.Show("源数据库连接成功！", "连接测试", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                AppendLog("源库连接失败！", Colors.Red);
                MessageBox.Show("源数据库连接失败，请检查连接信息", "连接测试", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            AppendLog($"源库连接异常: {ex.Message}", Colors.Red);
            MessageBox.Show($"连接异常: {ex.Message}", "连接测试", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnTestTarget_Click(object sender, RoutedEventArgs e)
    {
        var server = txtTgtServer.Text.Trim();
        var db = txtTgtDb.Text.Trim();
        var user = txtTgtUser.Text.Trim();
        var pwd = txtTgtPwd.Password;

        if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(db) || string.IsNullOrEmpty(user))
        {
            MessageBox.Show("请填写完整的目标库连接信息", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        AppendLog("正在测试目标库连接...", Colors.Yellow);
        try
        {
            var ok = _migrationService.TestTargetConnection(server, db, user, pwd);
            if (ok)
            {
                AppendLog("目标库连接成功！", Colors.LimeGreen);
                MessageBox.Show("目标数据库连接成功！", "连接测试", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                AppendLog("目标库连接失败！", Colors.Red);
                MessageBox.Show("目标数据库连接失败，请检查连接信息", "连接测试", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            AppendLog($"目标库连接异常: {ex.Message}", Colors.Red);
            MessageBox.Show($"连接异常: {ex.Message}", "连接测试", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnMigrate_Click(object sender, RoutedEventArgs e)
    {
        if (_isMigrating)
        {
            MessageBox.Show("迁移正在进行中，请等待完成", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var srcServer = txtSrcServer.Text.Trim();
        var srcDb = txtSrcDb.Text.Trim();
        var srcUser = txtSrcUser.Text.Trim();
        var srcPwd = txtSrcPwd.Password;

        var tgtServer = txtTgtServer.Text.Trim();
        var tgtDb = txtTgtDb.Text.Trim();
        var tgtUser = txtTgtUser.Text.Trim();
        var tgtPwd = txtTgtPwd.Password;

        if (string.IsNullOrEmpty(srcServer) || string.IsNullOrEmpty(srcDb) ||
            string.IsNullOrEmpty(tgtServer) || string.IsNullOrEmpty(tgtDb))
        {
            MessageBox.Show("请填写完整的源库和目标库连接信息", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 确认迁移
        var result = MessageBox.Show(
            "即将开始数据迁移，请确认：\n\n" +
            $"源库: {srcServer}/{srcDb}\n" +
            $"目标库: {tgtServer}/{tgtDb}\n\n" +
            "迁移范围：\n" +
            "  - 系统设置（syscontrol、tbsysset）\n" +
            "  - 配件数据（tbprnoty → part_data）\n" +
            "  - 客户数据（tbgu → client_infor）\n" +
            "  - 供应商数据（tbgugys → supplier_infor）\n" +
            "  - 仓位数据（tbposi → part_place）\n" +
            "  - 库存数据（tbisto → part_stock）\n" +
            "  - 采购入库（tbistoed → bill_buy + detail_buy）\n" +
            "  - 销售出库（tbsada → bill_sell + detail_sell）\n" +
            "  - 财务数据（应收应付、收付款、账户流水）\n\n" +
            "注意：维修/保险/会员数据不迁移。\n" +
            "目标库中已存在的记录将被跳过。\n\n" +
            "确定开始迁移？",
            "确认迁移", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        _isMigrating = true;
        btnMigrate.IsEnabled = false;
        prgBar.Visibility = Visibility.Visible;
        txtProgress.Visibility = Visibility.Visible;
        txtStatus.Text = "正在迁移...";
        prgBar.Maximum = MigrationService.GetEstimatedTotalSteps();
        prgBar.Value = 0;

        AppendLog("", Colors.Gray);
        AppendLog("========== 开始数据迁移 ==========", Colors.Cyan);

        try
        {
            await Task.Run(() => _migrationService.RunMigration(
                srcServer, srcDb, srcUser, srcPwd,
                tgtServer, tgtDb, tgtUser, tgtPwd));

            AppendLog("========== 数据迁移完成！ ==========", Colors.LimeGreen);
            txtStatus.Text = "迁移完成";
            MessageBox.Show("数据迁移已完成！", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppendLog($"迁移失败: {ex.Message}", Colors.Red);
            txtStatus.Text = "迁移失败";
            MessageBox.Show($"迁移过程中发生错误:\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isMigrating = false;
            btnMigrate.IsEnabled = true;
        }
    }

    private void OnLogMessage(string msg)
    {
        Dispatcher.Invoke(() => AppendLog(msg, Colors.LightGray));
    }

    private void OnErrorOccurred(string msg)
    {
        Dispatcher.Invoke(() => AppendLog(msg, Colors.OrangeRed));
    }

    private void OnProgressChanged(string step, int current, int total)
    {
        Dispatcher.Invoke(() =>
        {
            // 根据步骤更新进度条
            var stepIndex = step switch
            {
                "系统设置" => 1,
                "配件数据" => 2,
                "客户数据" => 3,
                "供应商数据" => 4,
                "仓位数据" => 5,
                "库存数据" => 6,
                "采购入库" => 7,
                "销售出库" => 8,
                "应收应付" => 9,
                "付款记录" => 9,
                "账户流水" => 9,
                _ => prgBar.Value
            };

            if (stepIndex > prgBar.Value)
                prgBar.Value = stepIndex;

            txtProgress.Text = $"{step} ({current}/{total})";
            txtStatus.Text = $"正在迁移: {step}";
        });
    }

    private void AppendLog(string msg, Color color)
    {
        var paragraph = new Paragraph();
        var run = new Run(msg);
        run.Foreground = new SolidColorBrush(color);
        paragraph.Inlines.Add(run);
        rtbLog.Document.Blocks.Add(paragraph);
        rtbLog.ScrollToEnd();
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e)
    {
        rtbLog.Document.Blocks.Clear();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        if (_isMigrating)
        {
            MessageBox.Show("迁移正在进行中，请等待完成后再关闭", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Close();
    }
}