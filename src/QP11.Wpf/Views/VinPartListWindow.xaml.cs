using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QP11.Core.Entities;
using QP11.Core.Interfaces;

namespace QP11.Wpf.Views;

public partial class VinPartListWindow : Window
{
    private readonly VinQueryWindow _owner;

    public VinPartListWindow(List<VinPartCategoryGroup> categories, VinQueryWindow owner)
    {
        InitializeComponent();
        _owner = owner;

        var all = categories.SelectMany(c => c.Products).ToList();
        var allMatched = all.Count(p => p.IsLocalMatched);

        // 添加"全部"分类（显示匹配数/总数）
        lstCategories.Items.Add(new VinCategoryNavItem
        {
            DisplayName = $"全部 ({allMatched}/{all.Count})",
            Products = all
        });
        foreach (var cat in categories)
        {
            var matched = cat.Products.Count(p => p.IsLocalMatched);
            lstCategories.Items.Add(new VinCategoryNavItem
            {
                DisplayName = $"{cat.CategoryName} ({matched}/{cat.Products.Count})",
                Products = cat.Products
            });
        }
        lstCategories.SelectedIndex = 0;
        lstCategories.DisplayMemberPath = "DisplayName";

        // 统计
        txtSummary.Text = $"共 {all.Count} 个配件，已匹配 {allMatched} 个本地库存";
    }

    private void LstCategories_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (lstCategories.SelectedItem is VinCategoryNavItem item)
        {
            // 显示所有配件，已匹配排前面
            icParts.ItemsSource = item.Products
                .OrderByDescending(p => p.IsLocalMatched)
                .ToList();
        }
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is VinPartCard card)
        {
            _owner.AddPartToSellDetail(card);
        }
    }

    private void Img_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is VinPartCard card && card.ImgUrlList.Count > 0)
        {
            e.Handled = true;
            var viewer = new VinImageViewerWindow(card.ImgUrlList);
            viewer.Owner = this;
            viewer.Show();
        }
    }

    /// <summary>刷新数据（复用窗口时调用）</summary>
    public void RefreshData(List<VinPartCategoryGroup> categories)
    {
        lstCategories.Items.Clear();
        var all = categories.SelectMany(c => c.Products).ToList();
        var allMatched = all.Count(p => p.IsLocalMatched);

        lstCategories.Items.Add(new VinCategoryNavItem
        {
            DisplayName = $"全部 ({allMatched}/{all.Count})",
            Products = all
        });
        foreach (var cat in categories)
        {
            var matched = cat.Products.Count(p => p.IsLocalMatched);
            lstCategories.Items.Add(new VinCategoryNavItem
            {
                DisplayName = $"{cat.CategoryName} ({matched}/{cat.Products.Count})",
                Products = cat.Products
            });
        }
        lstCategories.SelectedIndex = 0;

        txtSummary.Text = $"共 {all.Count} 个配件，已匹配 {allMatched} 个本地库存";
    }
}
