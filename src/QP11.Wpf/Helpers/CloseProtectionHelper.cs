using System.ComponentModel;
using System.Windows;

namespace QP11.Wpf.Helpers
{
    public static class CloseProtectionHelper
    {
        public static readonly DependencyProperty HasUnsavedChangesProperty =
            DependencyProperty.RegisterAttached("HasUnsavedChanges", typeof(bool), typeof(CloseProtectionHelper),
                new PropertyMetadata(false));

        public static bool GetHasUnsavedChanges(DependencyObject obj) => (bool)obj.GetValue(HasUnsavedChangesProperty);
        public static void SetHasUnsavedChanges(DependencyObject obj, bool value) => obj.SetValue(HasUnsavedChangesProperty, value);
    }
}
