using System.Collections.Specialized;
using System.Windows.Controls;
using System.Windows.Input;
using QP11.Wpf.ViewModels;

namespace QP11.Wpf.Views;

public partial class AgnesAssistantTab : UserControl
{
    public AgnesAssistantTab(AgnesChatViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        if (viewModel.Messages is INotifyCollectionChanged cc)
            cc.CollectionChanged += (_, _) => ScrollToEnd();

        Loaded += (_, _) => inputBox.Focus();
    }

    private void ScrollToEnd()
    {
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
            () => msgScroll?.ScrollToBottom());
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            if (DataContext is AgnesChatViewModel vm && vm.SendCommand.CanExecute(null))
                vm.SendCommand.Execute(null);
        }
    }
}
