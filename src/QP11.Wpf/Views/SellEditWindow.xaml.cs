using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Dapper;
using QP11.Core.Constants;
using QP11.Core.Entities;
using QP11.Core.Interfaces;

namespace QP11.Wpf.Views;

public partial class SellEditWindow : Window
{
    private readonly ISellRepository _sellRepo;
    private readonly IPartRepository _partRepo;
    private readonly IClientRepository _clientRepo;
    private readonly IUserRepository _userRepo;
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IUnitOfWorkFactory _uowFactory;
    private readonly IArrearageRepository _arrearRepo;
    public ObservableCollection<DetailSell> Details { get; } = new();
    private BillSell? _currentBill;
    private List<ClientInfor> _allClients = new();

    private bool _isNewBill = true;

    public SellEditWindow(
        ISellRepository sellRepo,
        IPartRepository partRepo,
        IClientRepository clientRepo,
        IUserRepository userRepo,
        IDbConnectionFactory dbFactory,
        IUnitOfWorkFactory uowFactory,
        IArrearageRepository arrearRepo)
    {
        InitializeComponent();
        _sellRepo = sellRepo;
        _partRepo = partRepo;
        _clientRepo = clientRepo;
        _userRepo = userRepo;
        _dbFactory = dbFactory;
        _uowFactory = uowFactory;
        _arrearRepo = arrearRepo;
        dgDetails.ItemsSource = Details;
        Loaded += async (_, _) => await LoadDropdownsAsync();
    }

    public SellEditWindow(
        string sn,
        ISellRepository sellRepo,
        IPartRepository partRepo,
        IClientRepository clientRepo,
        IUserRepository userRepo,
        IDbConnectionFactory dbFactory,
        IUnitOfWorkFactory uowFactory,
        IArrearageRepository arrearRepo) : this(sellRepo, partRepo, clientRepo, userRepo, dbFactory, uowFactory, arrearRepo)
    {
        _pendingSn = sn;
    }

    private string? _pendingSn;

    private async Task LoadDropdownsAsync()
    {
        try
        {
            var clients = await _clientRepo.GetAllAsync();
            _allClients = clients.ToList();
            cboClient.SetClients(_allClients);

            var users = await _userRepo.GetAllAsync();
            cboWorker.ItemsSource = users;
            cboWorker.DisplayMemberPath = "Name";
            cboWorker.SelectedValuePath = "Username";

            // 下拉框加载完成后，如果有待加载的单据号，自动加载
            if (!string.IsNullOrEmpty(_pendingSn))
            {
                txtSn.Text = _pendingSn;
                _pendingSn = null;
                BtnLoad_Click(this, new RoutedEventArgs());
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "LoadDropdownsAsync 失败");
        }
    }

    private async void BtnLoad_Click(object sender, RoutedEventArgs e)
    {
        var sn = txtSn.Text.Trim();
        if (string.IsNullOrEmpty(sn)) { MessageBox.Show("请输入单号", "提示"); return; }

        try
        {
            _currentBill = await _sellRepo.GetBySnAsync(sn);
            if (_currentBill == null) { MessageBox.Show("未找到该销售单", "提示"); return; }

            _isNewBill = false;

            txtBillNo.Text = _currentBill.Sn;
            dtBillDate.SelectedDate = _currentBill.Datetime;
            // 通过客户ID查找客户名称并设置
            if (!string.IsNullOrEmpty(_currentBill.Client))
            {
                var client = _allClients.FirstOrDefault(c => c.Cid == _currentBill.Client);
                cboClient.SetClient(client);
            }
            else
            {
                cboClient.ClearSelection();
            }
            // 业务员：按workid查名字显示
            if (!string.IsNullOrEmpty(_currentBill.Worker))
            {
                try
                {
                    using var wdb = await _dbFactory.CreateAsync();
                    var wn = await wdb.QueryFirstOrDefaultAsync<string>("SELECT name FROM work_infor WHERE workid=@W", new { W = _currentBill.Worker });
                    if (!string.IsNullOrEmpty(wn))
                    {
                        var m = cboWorker.Items.Cast<UserInfor>().FirstOrDefault(u => u.Name == wn);
                        if (m != null) cboWorker.SelectedItem = m; else cboWorker.Text = wn;
                    }
                }
                catch (Exception ex) { Serilog.Log.Warning(ex, "匹配业务员失败"); }
            }
            txtCheckno.Text = _currentBill.Checkno ?? "";
            txtDiscountRate.Text = _currentBill.DiscountRate?.ToString() ?? "1";
            txtTotal.Text = _currentBill.Total?.ToString("N2") ?? "0.00";
            txtBillTotal.Text = _currentBill.BillTotal?.ToString("N2") ?? "0.00";
            txtCash.Text = _currentBill.Cash?.ToString() ?? "0";
            txtChecks.Text = _currentBill.Checks?.ToString() ?? "0";
            txtArrear.Text = _currentBill.Arrear?.ToString() ?? "0";
            txtZhifubao.Text = _currentBill.Zhifubao?.ToString() ?? "0";
            txtWeixin.Text = _currentBill.Weixin?.ToString() ?? "0";
            txtMemo.Text = _currentBill.Memo ?? "";

            txtStatus.Text = $"状态: {BusinessConstants.GetFlagText(_currentBill.Flag ?? 0)} | 金额: {_currentBill.BillTotal:C2}";

            if (_currentBill.Flag == (int)BusinessConstants.BillFlag.Voided)
            {
                MessageBox.Show("该单据已作废，无法编辑", "提示");
                return;
            }

            Details.Clear();
            var details = await _sellRepo.GetDetailsAsync(sn);
            foreach (var d in details) Details.Add(d);
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "加载销售单失败"); MessageBox.Show($"加载失败: {ex.Message}", "错误"); }
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (_currentBill == null) return;
        try
        {
            _currentBill.Client = cboClient.SelectedClientId ?? cboClient.SearchText.Trim();
            _currentBill.Worker = cboWorker.Text.Trim();
            _currentBill.Checkno = txtCheckno.Text.Trim();
            _currentBill.DiscountRate = decimal.TryParse(txtDiscountRate.Text, out var dr) ? dr : 1m;
            _currentBill.Cash = decimal.TryParse(txtCash.Text, out var cash) ? cash : 0m;
            _currentBill.Checks = decimal.TryParse(txtChecks.Text, out var checks) ? checks : 0m;
            _currentBill.Arrear = decimal.TryParse(txtArrear.Text, out var arrear) ? arrear : 0m;
            _currentBill.Zhifubao = decimal.TryParse(txtZhifubao.Text, out var zfb) ? zfb : 0m;
            _currentBill.Weixin = decimal.TryParse(txtWeixin.Text, out var wx) ? wx : 0m;
            _currentBill.Memo = txtMemo.Text.Trim();
            if (_isNewBill)
                await _sellRepo.InsertBillAsync(_currentBill);
            else
                await _sellRepo.UpdateAsync(_currentBill);
            _isNewBill = false;
            MessageBox.Show("保存成功", "提示");
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "保存销售单失败"); MessageBox.Show($"保存失败: {ex.Message}", "错误"); }
    }

    private async void BtnConfirm_Click(object sender, RoutedEventArgs e)
    {
        if (_currentBill == null) return;
        if (MessageBox.Show("确认审核该销售单?", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        try
        {
            await _sellRepo.UpdateBillStatusAsync(_currentBill.Sn!, (int)BusinessConstants.BillFlag.Confirmed);
            _currentBill.Flag = (int)BusinessConstants.BillFlag.Confirmed;
            txtStatus.Text = $"状态: 已审核 | 金额: {_currentBill.BillTotal:C2}";
            MessageBox.Show("审核成功", "提示");
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "审核销售单失败"); MessageBox.Show($"审核失败: {ex.Message}", "错误"); }
    }

    private async void BtnVoid_Click(object sender, RoutedEventArgs e)
    {
        if (_currentBill == null) return;
        if (MessageBox.Show($"确定删除该销售单 [{_currentBill.Sn}]? 删除后不可恢复，库存将回补", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        try
        {
            using var uow = _uowFactory.Create();
            await uow.BeginTransactionAsync();
            var txn = uow.Transaction;
            var dbConn = uow.Connection;

            foreach (var detail in Details)
            {
                if (!detail.Partid.HasValue) continue;
                var amount = detail.Amount ?? 0m;
                if (amount == 0) continue;
                // 销售单作废 → 回补库存；退货单作废 → 扣减库存
                var isReturn = detail.Flag == (int)BusinessConstants.BillFlag.Returned;
                if (isReturn)
                    await _partRepo.DecreaseStockAsync(detail.Partid.Value, Math.Abs(amount), txn, dbConn);
                else
                    await _partRepo.IncreaseStockAsync(detail.Partid.Value, Math.Abs(amount), txn, dbConn);
            }

            // 物理删除单据（明细+头）并清除欠款
            await _sellRepo.DeleteDetailsAsync(_currentBill.Sn!, txn);
            await _sellRepo.DeleteBillAsync(_currentBill.Sn!, txn);
            await _arrearRepo.DeleteBySnAsync(_currentBill.Sn!, txn);

            await uow.CommitAsync();
            MessageBox.Show("单据已删除，库存已回补", "提示");
            Close();
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "删除销售单失败"); MessageBox.Show($"删除失败: {ex.Message}", "错误"); }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
