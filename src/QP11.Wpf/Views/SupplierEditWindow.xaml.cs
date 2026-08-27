using System;
using System.Windows;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using QP11.Core.Entities;
using QP11.Core.Interfaces;

namespace QP11.Wpf.Views;

public partial class SupplierEditWindow : Window
{
    private readonly ISupplierRepository _supplierRepo = App.ServiceProvider.GetRequiredService<ISupplierRepository>();
    private readonly SupplierInfor? _editEntity;
    private readonly bool _isEdit;

    public SupplierEditWindow() : this(null) { }

    public SupplierEditWindow(SupplierInfor? entity)
    {
        InitializeComponent();
        _editEntity = entity;
        _isEdit = entity != null;

        if (_isEdit && entity != null)
        {
            txtSid.Text = entity.Sid;
            txtSid.IsReadOnly = true;
            txtSid.Background = SystemColors.ControlBrush;
            txtName.Text = entity.Name;
            txtTel.Text = entity.Tel;
            txtMobile.Text = entity.Mobile;
            txtAddress.Text = entity.Address;
            txtLinkman.Text = entity.Linkman;
        }
        else
        {
            // 新增时自动获取最大编号并+1
            GenerateNextSid();
        }
    }

    /// <summary>自动生成下一个供应商编号</summary>
    private async void GenerateNextSid()
    {
        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            var maxSid = await db.ExecuteScalarAsync<string?>(
                "SELECT MAX(sid) FROM supplier_infor");
            if (int.TryParse(maxSid, out var num))
                txtSid.Text = (num + 1).ToString("D5");
            else
                txtSid.Text = "00001";
            txtSid.IsReadOnly = true;
            txtSid.Background = SystemColors.ControlBrush;
        }
        catch
        {
            txtSid.Text = "00001";
        }
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtSid.Text) || string.IsNullOrWhiteSpace(txtName.Text))
        {
            MessageBox.Show("编号和名称不能为空", "提示");
            return;
        }

        var entity = new SupplierInfor
        {
            Sid = txtSid.Text.Trim(),
            Name = txtName.Text.Trim(),
            Tel = txtTel.Text.Trim(),
            Mobile = txtMobile.Text.Trim(),
            Address = txtAddress.Text.Trim(),
            Linkman = txtLinkman.Text.Trim()
        };

        try
        {
            if (_isEdit)
                await _supplierRepo.UpdateAsync(entity);
            else
                await _supplierRepo.InsertAsync(entity);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败: {ex.Message}", "错误");
        }
    }
}
