using System.Collections.Generic;
using System.Windows;
using QP11.Core.Entities;

namespace QP11.Wpf.Views;

public partial class VinCandidateSelectWindow : Window
{
    public VinLocalMatch? SelectedItem { get; private set; }

    public VinCandidateSelectWindow(List<VinLocalMatch> candidates, string model)
    {
        InitializeComponent();
        txtModel.Text = model;
        dgCandidates.ItemsSource = candidates;
        if (candidates.Count > 0)
            dgCandidates.SelectedIndex = 0;
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (dgCandidates.SelectedItem is VinLocalMatch item)
        {
            SelectedItem = item;
            DialogResult = true;
        }
        else
        {
            MessageBox.Show("请选择一条配件记录", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
