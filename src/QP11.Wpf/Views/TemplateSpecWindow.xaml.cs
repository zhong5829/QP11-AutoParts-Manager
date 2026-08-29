using System.Windows;

namespace QP11.Wpf.Views;

/// <summary>新建标签模板对话框：设置名称与纸张大小（宽×高 mm）</summary>
public partial class TemplateSpecWindow : Window
{
    /// <summary>模板名称</summary>
    public string TemplateName { get; private set; } = "";
    /// <summary>标签宽度（mm）</summary>
    public double WidthMm { get; private set; }
    /// <summary>标签高度（mm）</summary>
    public double HeightMm { get; private set; }

    public TemplateSpecWindow(string defaultName = "", double defaultWidth = 50, double defaultHeight = 30)
    {
        InitializeComponent();
        txtName.Text = defaultName;
        txtWidth.Text = defaultWidth.ToString();
        txtHeight.Text = defaultHeight.ToString();
        txtName.Focus();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        var name = txtName.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            txtHint.Text = "请输入模板名称";
            return;
        }
        if (!double.TryParse(txtWidth.Text.Trim(), out var w) || w <= 0 || w > 300)
        {
            txtHint.Text = "宽度须为 1~300 之间的数字（mm）";
            return;
        }
        if (!double.TryParse(txtHeight.Text.Trim(), out var h) || h <= 0 || h > 500)
        {
            txtHint.Text = "高度须为 1~500 之间的数字（mm）";
            return;
        }

        TemplateName = name;
        WidthMm = w;
        HeightMm = h;
        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}