using System.Windows.Media;

namespace QP11.Wpf.ViewModels;

public sealed class AgnesMessageItem : BaseViewModel
{
    private string _role = "";
    private string _text = "";
    private string _toolName = "";
    private bool _success = true;

    public string Role
    {
        get => _role;
        set => SetProperty(ref _role, value);
    }

    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }

    public string ToolName
    {
        get => _toolName;
        set => SetProperty(ref _toolName, value);
    }

    public bool Success
    {
        get => _success;
        set => SetProperty(ref _success, value);
    }

    public bool IsUser => Role == "user";
    public bool IsAssistant => Role == "assistant";
    public bool IsTool => Role == "tool";
    public bool IsSystem => Role == "system";

    public Brush RoleBrush => Role switch
    {
        "user" => new SolidColorBrush(Color.FromRgb(0x1E, 0x90, 0xFF)),
        "assistant" => new SolidColorBrush(Color.FromRgb(0x21, 0x21, 0x21)),
        "tool" => Success
            ? new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66))
            : new SolidColorBrush(Color.FromRgb(0xCC, 0x00, 0x00)),
        _ => new SolidColorBrush(Color.FromRgb(0xAA, 0x55, 0x00))
    };

    public void AppendText(string delta)
    {
        Text += delta;
    }
}
