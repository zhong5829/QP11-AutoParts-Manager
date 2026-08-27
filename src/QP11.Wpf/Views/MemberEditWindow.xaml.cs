using System;
using System.Windows;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace QP11.Wpf.Views;

public partial class MemberEditWindow : Window
{
    private readonly IMemberCardRepository _memberRepo = App.ServiceProvider.GetRequiredService<IMemberCardRepository>();
    private readonly MemberCard? _editEntity;
    private readonly bool _isEdit;

    public MemberEditWindow() : this(null) { }

    public MemberEditWindow(MemberCard? entity)
    {
        InitializeComponent();
        _editEntity = entity;
        _isEdit = entity != null;

        if (_isEdit && entity != null)
        {
            txtKh.Text = entity.Kh;
            txtKh.IsReadOnly = true;
            txtKh.Background = SystemColors.ControlBrush;
            txtName.Text = entity.Khmc;
            txtPhone.Text = entity.Tel;
            cboType.Text = entity.Klb;
            txtZkl.Text = entity.Zkl?.ToString() ?? "1";
            cboZt.Text = entity.Zt;
        }
        else
        {
            cboZt.SelectedIndex = 0;
        }
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtKh.Text) || string.IsNullOrWhiteSpace(txtName.Text))
        {
            MessageBox.Show("卡号和姓名不能为空", "提示");
            return;
        }

        var entity = new MemberCard
        {
            Kh = txtKh.Text.Trim(),
            Khmc = txtName.Text.Trim(),
            Tel = txtPhone.Text.Trim(),
            Klb = cboType.Text,
            Zkl = decimal.TryParse(txtZkl.Text, out var zkl) ? zkl : 1m,
            Zt = cboZt.Text,
            Kmm = txtKmm.Password,
            Je = 0
        };

        try
        {
            if (_isEdit)
                await _memberRepo.UpdateAsync(entity);
            else
                await _memberRepo.InsertAsync(entity);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败: {ex.Message}", "错误");
        }
    }
}
