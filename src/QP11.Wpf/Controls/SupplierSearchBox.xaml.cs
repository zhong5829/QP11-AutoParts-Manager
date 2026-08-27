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
/// 供应商搜索输入框：TextBox + Popup + ListBox，支持拼音搜索
/// </summary>
public partial class SupplierSearchBox : UserControl
{
    private List<SupplierInfor> _allSuppliers = new();
    private Dictionary<string, string> _supplierPyCache = new();
    private readonly DispatcherTimer _filterTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private string _pendingFilter = "";
    private bool _isSelecting;

    /// <summary>用户从下拉列表选中供应商时触发</summary>
    public event EventHandler? SupplierSelected;

    /// <summary>当前选中的供应商</summary>
    public SupplierInfor? SelectedSupplier { get; private set; }

    /// <summary>选中供应商的编号</summary>
    public string? SelectedSupplierId => SelectedSupplier?.Sid;

    /// <summary>输入框文本</summary>
    public string SearchText
    {
        get => txtInput.Text;
        set { _isSelecting = true; txtInput.Text = value; _isSelecting = false; }
    }

    public SupplierSearchBox()
    {
        InitializeComponent();
        _filterTimer.Tick += (_, _) => { _filterTimer.Stop(); FilterSuppliersCore(_pendingFilter); };
    }

    /// <summary>设置供应商数据源（全量），在页面初始化时调用一次</summary>
    public void SetSuppliers(List<SupplierInfor> suppliers)
    {
        _allSuppliers = suppliers ?? new();
        _supplierPyCache = _allSuppliers.Where(s => !string.IsNullOrEmpty(s.Name))
            .ToDictionary(s => s.Sid ?? "", s => PinyinHelper.GetPinyinInitials(s.Name!));
    }

    /// <summary>清空选择和文本</summary>
    public void ClearSelection()
    {
        _isSelecting = true;
        SelectedSupplier = null;
        txtInput.Text = "";
        _isSelecting = false;
        popup.IsOpen = false;
    }

    /// <summary>编程式设置选中的供应商（不触发 SupplierSelected 事件）</summary>
    public void SetSupplier(SupplierInfor? supplier)
    {
        _isSelecting = true;
        SelectedSupplier = supplier;
        txtInput.Text = supplier?.Name ?? "";
        _isSelecting = false;
        popup.IsOpen = false;
    }

    private void TxtInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSelecting) return;
        SelectedSupplier = null;
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
        else if (e.Key == Key.Enter && lstResults.SelectedItem is SupplierInfor supplier)
        {
            SelectSupplier(supplier);
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
        if (item != null && item.Content is SupplierInfor supplier)
        {
            SelectSupplier(supplier);
            e.Handled = true;
        }
    }

    private void SelectSupplier(SupplierInfor supplier)
    {
        _isSelecting = true;
        SelectedSupplier = supplier;
        txtInput.Text = supplier.Name ?? "";
        _isSelecting = false;
        popup.IsOpen = false;
        SupplierSelected?.Invoke(this, EventArgs.Empty);
    }

    private void FilterSuppliersCore(string keyword)
    {
        try
        {
            List<SupplierInfor> filtered;
            if (string.IsNullOrWhiteSpace(keyword))
            {
                filtered = _allSuppliers;
            }
            else
            {
                var keywordLower = keyword.ToLower();
                filtered = _allSuppliers.Where(s => MatchSupplier(s, keyword, keywordLower)).ToList();
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

    private bool MatchSupplier(SupplierInfor s, string keyword, string keywordLower)
    {
        if (s.Name?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true) return true;
        if (s.Sid?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true) return true;
        if (s.Mobile?.Contains(keyword) == true) return true;
        if (s.Tel?.Contains(keyword) == true) return true;
        if (s.Linkman?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true) return true;
        if (_supplierPyCache.TryGetValue(s.Sid ?? "", out var py) && py.StartsWith(keywordLower, StringComparison.OrdinalIgnoreCase)) return true;
        if (!string.IsNullOrEmpty(s.NamePy) && s.NamePy.StartsWith(keywordLower, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
