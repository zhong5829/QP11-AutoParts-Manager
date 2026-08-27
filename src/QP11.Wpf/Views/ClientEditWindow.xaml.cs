using System;
using System.Windows;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using QP11.Core.Entities;
using QP11.Core.Interfaces;

namespace QP11.Wpf.Views;

public partial class ClientEditWindow : Window
{
    private readonly IClientRepository _clientRepo = App.ServiceProvider.GetRequiredService<IClientRepository>();
    private readonly ClientInfor? _editEntity;
    private readonly bool _isEdit;

    public ClientEditWindow() : this(null) { }

    public ClientEditWindow(ClientInfor? entity)
    {
        InitializeComponent();
        _editEntity = entity;
        _isEdit = entity != null;

        if (_isEdit && entity != null)
        {
            txtCid.Text = entity.Cid;
            txtCid.IsReadOnly = true;
            txtCid.Background = SystemColors.ControlBrush;
            txtName.Text = entity.Name;
            txtTel.Text = entity.Tel;
            txtMobile.Text = entity.Mobile;
            txtAddress.Text = entity.Address;
            cboLevel.Text = entity.Level;
            txtCredit.Text = entity.Credit?.ToString() ?? "0";
            txtLinkman.Text = entity.Linkman;
            txtNote.Text = entity.Note;
        }
        else
        {
            // 新增时自动获取最大编号并+1
            GenerateNextCid();
        }
    }

    /// <summary>自动生成下一个客户编号</summary>
    private async void GenerateNextCid()
    {
        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            var maxCid = await db.ExecuteScalarAsync<string?>(
                "SELECT MAX(cid) FROM client_infor");
            if (int.TryParse(maxCid, out var num))
                txtCid.Text = (num + 1).ToString("D5");
            else
                txtCid.Text = "00001";
            txtCid.IsReadOnly = true;
            txtCid.Background = SystemColors.ControlBrush;
        }
        catch
        {
            txtCid.Text = "00001";
        }
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtCid.Text) || string.IsNullOrWhiteSpace(txtName.Text))
        {
            MessageBox.Show("编号和名称不能为空", "提示");
            return;
        }

        var entity = new ClientInfor
        {
            Cid = txtCid.Text.Trim(),
            Name = txtName.Text.Trim(),
            Tel = txtTel.Text.Trim(),
            Mobile = txtMobile.Text.Trim(),
            Address = txtAddress.Text.Trim(),
            Level = cboLevel.Text,
            Credit = decimal.TryParse(txtCredit.Text, out var cr) ? cr : 0,
            Linkman = txtLinkman.Text.Trim(),
            Note = txtNote.Text.Trim()
        };

        try
        {
            if (_isEdit)
                await _clientRepo.UpdateAsync(entity);
            else
                await _clientRepo.InsertAsync(entity);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败: {ex.Message}", "错误");
        }
    }
}
