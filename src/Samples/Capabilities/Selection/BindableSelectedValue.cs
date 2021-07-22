using System.Windows;
using System.Windows.Controls;
using Microsoft.Xaml.Behaviors;

namespace Elmish.WPF.Samples.Capabilities {
  class BindableSelectedValue : Behavior<TreeView> {

    public object SelectedValue {
      get => (object)GetValue(SelectedValueProperty);
      set => SetValue(SelectedValueProperty, value);
    }
    public static readonly DependencyProperty SelectedValueProperty =
      DependencyProperty.Register(nameof(SelectedValue), typeof(object), typeof(BindableSelectedValue), new UIPropertyMetadata(null, OnSelectedValueChanged));
    private static void OnSelectedValueChanged(DependencyObject _, DependencyPropertyChangedEventArgs e) {
      if (e.NewValue is TreeViewItem item) {
        item.SetValue(TreeViewItem.IsSelectedProperty, true);
      }
    }

    protected override void OnAttached() {
      base.OnAttached();
      this.AssociatedObject.SelectedItemChanged += OnTreeViewSelectedItemChanged;
    }
    protected override void OnDetaching() {
      base.OnDetaching();
      this.AssociatedObject.SelectedItemChanged -= OnTreeViewSelectedItemChanged;
    }
    private void OnTreeViewSelectedItemChanged(object _1, RoutedPropertyChangedEventArgs<object> _2) =>
      this.SelectedValue = this.AssociatedObject.SelectedValue;
  }
}