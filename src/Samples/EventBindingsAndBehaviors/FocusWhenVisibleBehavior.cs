using System.Windows;
using Microsoft.Xaml.Behaviors;

namespace Elmish.WPF.Samples.EventBindingsAndBehaviors
{
  public class FocusWhenVisibleBehavior : Behavior<UIElement>
  {
    protected override void OnAttached()
    {
      base.OnAttached();
      AssociatedObject.IsVisibleChanged += UIElement_IsVisibleChanged;

    }

    private void UIElement_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
      if (e.NewValue is bool b && b == true)
      {
        AssociatedObject.Focus();
      }
    }

    protected override void OnDetaching()
    {
      base.OnDetaching();
      AssociatedObject.IsVisibleChanged -= UIElement_IsVisibleChanged;
    }
  }
}
