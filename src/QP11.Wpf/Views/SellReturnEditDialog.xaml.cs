using System.Windows;

namespace QP11.Wpf.Views;

public partial class SellReturnEditDialog : Window
{
    private readonly long? _partId;
    private readonly int _maxAmount;

    public bool IsConfirmed { get; private set; }
    public int ReturnAmount { get; private set; }
    public decimal ReturnPrice { get; private set; }
    public bool ToWaste { get; private set; }

    public SellReturnEditDialog(long? partId, string partNo, string partName,
        string cartype, decimal origPrice, int maxAmount)
    {
        InitializeComponent();

        _partId = partId;
        _maxAmount = maxAmount;

        txtPartNo.Text = partNo;
        txtPartName.Text = partName;
        txtCartype.Text = cartype;
        txtOrigPrice.Text = $"¥{origPrice:N2}";
        txtOrigAmount.Text = maxAmount.ToString();
        txtPrice.Text = origPrice.ToString();
        txtAmount.Text = "1";
    }

    /// <summary>预填当前退货数量和单价（用于编辑已有明细行）</summary>
    public void SetCurrentValues(int amount, decimal price, bool toWaste)
    {
        txtAmount.Text = amount.ToString();
        txtPrice.Text = price.ToString("N2");
        chkWaste.IsChecked = toWaste;
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(txtAmount.Text, out var amount) || amount <= 0)
        {
            MessageBox.Show("请输入有效的退货数量", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            txtAmount.Focus();
            return;
        }

        if (amount > _maxAmount)
        {
            MessageBox.Show($"退货数量不能超过已购数量({_maxAmount})", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            txtAmount.Focus();
            return;
        }

        if (!decimal.TryParse(txtPrice.Text, out var price) || price < 0)
        {
            MessageBox.Show("请输入有效的退货单价", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            txtPrice.Focus();
            return;
        }

        ReturnAmount = amount;
        ReturnPrice = price;
        ToWaste = chkWaste.IsChecked == true;
        IsConfirmed = true;

        DialogResult = true;
        Close();
    }
}
