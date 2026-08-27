using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using QP11.Core.Entities;
using QP11.Wpf.Helpers;

namespace QP11.Wpf.Controls;

/// <summary>
/// 客户搜索输入框：TextBox + Popup + ListBox，彻底避免 WPF 可编辑 ComboBox 的级联事件问题
/// </summary>
public partial class ClientSearchBox : UserControl
{
    private List<ClientInfor> _allClients = new();
    private Dictionary<string, string> _clientPyCache = new();
    private readonly DispatcherTimer _filterTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private string _pendingFilter = "";
    private bool _isSelecting;

    /// <summary>用户从下拉列表选中客户时触发</summary>
    public event EventHandler? ClientSelected;

    /// <summary>当前选中的客户</summary>
    public ClientInfor? SelectedClient { get; private set; }

    /// <summary>选中客户的编号</summary>
    public string? SelectedClientId => SelectedClient?.Cid;

    /// <summary>输入框文本</summary>
    public string SearchText
    {
        get => txtInput.Text;
        set { _isSelecting = true; txtInput.Text = value; _isSelecting = false; }
    }

    public ClientSearchBox()
    {
        InitializeComponent();
        _filterTimer.Tick += (_, _) => { _filterTimer.Stop(); FilterClientsCore(_pendingFilter); };
    }

    /// <summary>设置客户数据源（全量），在页面初始化时调用一次</summary>
    public void SetClients(List<ClientInfor> clients)
    {
        _allClients = clients ?? new();
        _clientPyCache = _allClients.Where(c => !string.IsNullOrEmpty(c.Name))
            .ToDictionary(c => c.Cid ?? "", c => PinyinHelper.GetPinyinInitials(c.Name!));
    }

    /// <summary>清空选择和文本</summary>
    public void ClearSelection()
    {
        _isSelecting = true;
        SelectedClient = null;
        txtInput.Text = "";
        _isSelecting = false;
        popup.IsOpen = false;
    }

    /// <summary>编程式设置选中的客户（不触发 ClientSelected 事件）</summary>
    public void SetClient(ClientInfor? client)
    {
        _isSelecting = true;
        SelectedClient = client;
        txtInput.Text = client?.Name ?? "";
        _isSelecting = false;
        popup.IsOpen = false;
    }

    private void TxtInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSelecting) return;
        SelectedClient = null;
        _pendingFilter = txtInput.Text;
        _filterTimer.Stop();
        _filterTimer.Start();
    }

    private void TxtInput_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!popup.IsOpen) return;

        if (e.Key == Key.Down && lstResults.Items.Count > 0)
        {
            // 焦点始终留在 TextBox，直接操作 SelectedIndex
            if (lstResults.SelectedIndex < 0)
                lstResults.SelectedIndex = 0;
            else if (lstResults.SelectedIndex < lstResults.Items.Count - 1)
                lstResults.SelectedIndex++;
            e.Handled = true;
        }
        else if (e.Key == Key.Up && lstResults.Items.Count > 0)
        {
            if (lstResults.SelectedIndex > 0)
                lstResults.SelectedIndex--;
            // 在第一项按 Up 不做操作（焦点已在输入框）
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && lstResults.SelectedItem is ClientInfor client)
        {
            SelectClient(client);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            popup.IsOpen = false;
            e.Handled = true;
        }
    }

    private void LstResults_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        var item = ItemsControl.ContainerFromElement(lstResults, (DependencyObject)e.OriginalSource) as ListBoxItem;
        if (item != null && item.Content is ClientInfor client)
        {
            SelectClient(client);
            e.Handled = true;
        }
    }

    private void SelectClient(ClientInfor client)
    {
        _isSelecting = true;
        SelectedClient = client;
        txtInput.Text = client.Name ?? "";
        _isSelecting = false;
        popup.IsOpen = false;
        ClientSelected?.Invoke(this, EventArgs.Empty);
    }

    private void FilterClientsCore(string keyword)
    {
        try
        {
            List<ClientInfor> filtered;
            if (string.IsNullOrWhiteSpace(keyword))
            {
                filtered = _allClients;
            }
            else
            {
                var keywordLower = keyword.ToLower();
                filtered = _allClients.Where(c => MatchClient(c, keyword, keywordLower)).ToList();
            }

            lstResults.ItemsSource = filtered;

            if (filtered.Count > 0)
            {
                popupBorder.Width = Math.Max(txtInput.ActualWidth, 150);
                popup.IsOpen = true;
                lstResults.SelectedIndex = -1;
            }
            else
            {
                popup.IsOpen = false;
            }
        }
        catch { }
    }

    private bool MatchClient(ClientInfor c, string keyword, string keywordLower)
    {
        if (c.Name?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true) return true;
        if (c.Cid?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true) return true;
        if (c.Mobile?.Contains(keyword) == true) return true;
        if (c.Tel?.Contains(keyword) == true) return true;
        if (c.Linkman?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true) return true;
        if (_clientPyCache.TryGetValue(c.Cid ?? "", out var py) && py.StartsWith(keywordLower, StringComparison.OrdinalIgnoreCase)) return true;
        if (!string.IsNullOrEmpty(c.NamePy) && c.NamePy.StartsWith(keywordLower, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
