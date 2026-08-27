using System;
using System.Collections.ObjectModel;
using System.Windows;
using QP11.Core.Entities;
using QP11.Core.Interfaces;

namespace QP11.Wpf.Views;

public partial class MemberCardWindow : Window
{
    private readonly IMemberCardRepository _memberRepo;
    public ObservableCollection<MemberCard> Members { get; } = new();

    public MemberCardWindow(IMemberCardRepository memberRepo)
    {
        _memberRepo = memberRepo;
        InitializeComponent();
        dgMembers.ItemsSource = Members;
        LoadMembers();
    }

    private async void LoadMembers(string? keyword = null)
    {
        try
        {
            Members.Clear();
            var data = string.IsNullOrEmpty(keyword)
                ? await _memberRepo.GetAllAsync()
                : await _memberRepo.SearchAsync(keyword);
            foreach (var m in data) Members.Add(m);
            txtCount.Text = $"共 {Members.Count} 条记录";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载会员失败: {ex.Message}", "错误");
        }
    }

    private void TxtSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var kw = txtSearch.Text.Trim();
        if (kw.Length >= 2) LoadMembers(kw);
        else if (kw.Length == 0) LoadMembers();
    }

    private void BtnSearch_Click(object sender, RoutedEventArgs e) => LoadMembers(txtSearch.Text.Trim());

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new MemberEditWindow { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true) LoadMembers();
    }

    private void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        if (dgMembers.SelectedItem is not MemberCard member)
        {
            MessageBox.Show("请选择要编辑的会员", "提示");
            return;
        }
        var dlg = new MemberEditWindow(member) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true) LoadMembers();
    }

    private async void BtnRecharge_Click(object sender, RoutedEventArgs e)
    {
        if (dgMembers.SelectedItem is not MemberCard member) return;
        var input = InputBoxDialog.Show($"当前余额: {member.Je:C2}\n请输入充值金额:", "会员充值", "100");
        if (decimal.TryParse(input, out var amount) && amount > 0)
        {
            try
            {
                await _memberRepo.RechargeAsync(member.Kh!, amount);
                LoadMembers();
                MessageBox.Show($"充值成功! 金额: {amount:C2}", "提示");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"充值失败: {ex.Message}", "错误");
            }
        }
    }

    private async void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (dgMembers.SelectedItem is not MemberCard member) return;
        if (MessageBox.Show($"确定删除会员 [{member.Khmc}]?", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        try
        {
            await _memberRepo.LogicDeleteAsync(member.Kh!);
            LoadMembers();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"删除失败: {ex.Message}", "错误");
        }
    }

    private void DgMembers_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        BtnEdit_Click(sender, e);
    }
}
