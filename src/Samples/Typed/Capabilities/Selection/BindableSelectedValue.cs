using System.Windows;
using System.Windows.Controls;
using Microsoft.Xaml.Behaviors;

namespace Elmish.WPF.Samples.Capabilities;

internal class BindableSelectedValue : Behavior<TreeView>
{
    public static readonly DependencyProperty SelectedValueProperty =
        DependencyProperty.Register(nameof(SelectedValue), typeof(object), typeof(BindableSelectedValue),
            new UIPropertyMetadata(null, OnSelectedValueChanged));

    public object SelectedValue
    {
        get => GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    private static void OnSelectedValueChanged(DependencyObject _, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is TreeViewItem item) item.SetValue(TreeViewItem.IsSelectedProperty, true);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.SelectedItemChanged += OnTreeViewSelectedItemChanged;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.SelectedItemChanged -= OnTreeViewSelectedItemChanged;
    }

    private void OnTreeViewSelectedItemChanged(object _1, RoutedPropertyChangedEventArgs<object> _2)
    {
        SelectedValue = AssociatedObject.SelectedValue;
    }
}