using System.Windows;

namespace QP11.Wpf.Views;

public partial class InputBoxDialog : Window
{
    public string InputText { get; private set; } = string.Empty;

    public InputBoxDialog(string prompt, string title = "输入", string defaultValue = "")
    {
        InitializeComponent();
        Title = title;
        txtPrompt.Text = prompt;
        txtInput.Text = defaultValue;
        txtInput.SelectAll();
        txtInput.Focus();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        InputText = txtInput.Text;
        DialogResult = true;
    }

    public static string? Show(string prompt, string title = "输入", string defaultValue = "")
    {
        var dlg = new InputBoxDialog(prompt, title, defaultValue);
        return dlg.ShowDialog() == true ? dlg.InputText : null;
    }
}
