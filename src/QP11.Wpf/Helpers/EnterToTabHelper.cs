using System.Windows;
using System.Windows.Input;

namespace QP11.Wpf.Helpers
{
    public static class EnterToTabHelper
    {
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(EnterToTabHelper),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element)
            {
                if ((bool)e.NewValue)
                    element.PreviewKeyDown += OnPreviewKeyDown;
                else
                    element.PreviewKeyDown -= OnPreviewKeyDown;
            }
        }

        private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !(e.OriginalSource is System.Windows.Controls.TextBox)
                && !(e.OriginalSource is System.Windows.Controls.DataGridCell)
                && !(e.OriginalSource is System.Windows.Controls.DataGridRow))
            {
                e.Handled = true;
                (sender as FrameworkElement)?.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }
        }
    }
}
