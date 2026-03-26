using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using StepperC3.App.ViewModels;

namespace StepperC3.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private Point _dragStartPoint;

    public MainWindow()
    {
        InitializeComponent();

        // Auto-scroll log to bottom
        if (DataContext is MainViewModel vm)
        {
            vm.LogMessages.CollectionChanged += (_, _) =>
            {
                if (LogListBox.Items.Count > 0)
                    LogListBox.ScrollIntoView(LogListBox.Items[^1]);
            };
        }

        // Keyboard shortcuts
        InputBindings.Add(new KeyBinding(
            ((MainViewModel)DataContext).NewTaskListCommand, Key.N, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(
            ((MainViewModel)DataContext).OpenTaskListCommand, Key.O, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(
            ((MainViewModel)DataContext).SaveTaskListCommand, Key.S, ModifierKeys.Control));
    }

    // ─── Drag-Drop Reordering ────────────────────────────────────────────

    private void StepListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void StepListBox_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(StepViewModel)))
        {
            e.Effects = DragDropEffects.None;
            return;
        }
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void StepListBox_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(StepViewModel)) is not StepViewModel droppedData) return;
        if (DataContext is not MainViewModel vm) return;

        // Find the target item
        var target = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
        if (target is null) return;

        var targetData = target.DataContext as StepViewModel;
        if (targetData is null || ReferenceEquals(droppedData, targetData)) return;

        var fromIndex = vm.Steps.IndexOf(droppedData);
        var toIndex = vm.Steps.IndexOf(targetData);

        if (fromIndex >= 0 && toIndex >= 0)
            vm.MoveStepInList(fromIndex, toIndex);
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);

        if (e.LeftButton != MouseButtonState.Pressed) return;

        var mousePos = e.GetPosition(null);
        var diff = _dragStartPoint - mousePos;

        if (Math.Abs(diff.X) <= SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) <= SystemParameters.MinimumVerticalDragDistance) return;

        // Find the ListBoxItem under the mouse
        var listBoxItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
        if (listBoxItem?.DataContext is not StepViewModel stepVm) return;

        var data = new DataObject(typeof(StepViewModel), stepVm);
        DragDrop.DoDragDrop(listBoxItem, data, DragDropEffects.Move);
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T target) return target;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}