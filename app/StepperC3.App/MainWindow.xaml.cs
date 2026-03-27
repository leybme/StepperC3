using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using StepperC3.App.Adorners;
using StepperC3.App.ViewModels;
using StepperC3.Core.Models;
using StepperC3.Core.Services;

namespace StepperC3.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private Point    _dragStartPoint;
    private ListBox? _dragSourceList;    // which list the drag started from

    // ─── Drag-drop insertion adorner state ───────────────────────────────
    private InsertionAdorner? _insertionAdorner;
    private ListBoxItem?      _adornerHost;
    private int               _insertionIndex = -1;

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

    // ─── Palette ListBox: drag initiation ────────────────────────────────

    private void PaletteListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Don't begin drag tracking when clicking on a TextBox field
        if (FindAncestor<TextBox>((DependencyObject)e.OriginalSource) is not null) return;
        _dragStartPoint = e.GetPosition(null);
        _dragSourceList = PaletteListBox;
    }

    /// <summary>Stops a click on a palette TextBox from initiating a drag.</summary>
    private void PaletteField_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = false;   // let the TextBox focus; clear drag source so OnPreviewMouseMove won't drag
        _dragSourceList = null;
    }

    private void PaletteListBox_DragOver(object sender, DragEventArgs e)
    {
        // Accept a task step being dragged back for removal
        if (e.Data.GetDataPresent(typeof(StepViewModel)))
        {
            e.Effects  = DragDropEffects.Move;
            e.Handled  = true;
            PaletteDrop.Visibility = Visibility.Visible;
            return;
        }
        e.Effects = DragDropEffects.None;
    }

    private void PaletteListBox_DragLeave(object sender, DragEventArgs e)
    {
        PaletteDrop.Visibility = Visibility.Collapsed;
    }

    private void PaletteListBox_Drop(object sender, DragEventArgs e)
    {
        PaletteDrop.Visibility = Visibility.Collapsed;

        if (e.Data.GetData(typeof(StepViewModel)) is not StepViewModel stepVm) return;
        if (DataContext is not MainViewModel vm) return;

        // Remove the step from the task list
        var idx = vm.Steps.IndexOf(stepVm);
        if (idx >= 0)
        {
            vm.RemoveStepAt(idx);
        }
    }

    // ─── Task-list ListBox: drag initiation ──────────────────────────────

    private void StepListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Don't begin drag tracking if clicking a Button (e.g. ✏ / ✓ / ✗)
        if (FindAncestor<Button>((DependencyObject)e.OriginalSource) is not null) return;
        _dragStartPoint = e.GetPosition(null);
        _dragSourceList = StepListBox;
    }

    // ─── Common mouse-move: initiate drag from either list ───────────────

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);

        if (e.LeftButton != MouseButtonState.Pressed || _dragSourceList is null) return;
        if (FindAncestor<Button>((DependencyObject)e.OriginalSource) is not null) return;

        var mousePos = e.GetPosition(null);
        var diff     = _dragStartPoint - mousePos;

        if (Math.Abs(diff.X) <= SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) <= SystemParameters.MinimumVerticalDragDistance) return;

        var listBoxItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
        if (listBoxItem is null) return;

        if (ReferenceEquals(_dragSourceList, PaletteListBox) &&
            listBoxItem.DataContext is PaletteItemViewModel paletteItem)
        {
            // Drag from palette → carry PaletteItemViewModel
            var data = new DataObject(typeof(PaletteItemViewModel), paletteItem);
            _dragSourceList = null;
            DragDrop.DoDragDrop(listBoxItem, data, DragDropEffects.Copy);
        }
        else if (ReferenceEquals(_dragSourceList, StepListBox) &&
                 listBoxItem.DataContext is StepViewModel stepVm)
        {
            // Drag from task list → carry StepViewModel (for reorder or remove)
            var data = new DataObject(typeof(StepViewModel), stepVm);
            _dragSourceList = null;
            DragDrop.DoDragDrop(listBoxItem, data, DragDropEffects.Move);
        }
    }

    // ─── Task-list DragOver ───────────────────────────────────────────────

    private void StepListBox_DragOver(object sender, DragEventArgs e)
    {
        bool hasStep     = e.Data.GetDataPresent(typeof(StepViewModel));
        bool hasType      = e.Data.GetDataPresent(typeof(PaletteItemViewModel));

        if (!hasStep && !hasType)
        {
            e.Effects = DragDropEffects.None;
            return;
        }
        e.Effects = hasType ? DragDropEffects.Copy : DragDropEffects.Move;
        e.Handled = true;

        UpdateInsertionAdorner(e);
    }

    private void StepListBox_DragLeave(object sender, DragEventArgs e)
    {
        RemoveInsertionAdorner();
    }

    // ─── Task-list Drop ───────────────────────────────────────────────────

    private void StepListBox_Drop(object sender, DragEventArgs e)
    {
        RemoveInsertionAdorner();
        if (DataContext is not MainViewModel vm) return;

        // ── Drop from palette: insert new step ──────────────────────────
        if (e.Data.GetData(typeof(PaletteItemViewModel)) is PaletteItemViewModel paletteItem)
        {
            var step = paletteItem.CreateStep();
            var idx  = _insertionIndex >= 0 ? Math.Min(_insertionIndex, vm.Steps.Count) : vm.Steps.Count;
            vm.InsertStepAt(idx, step);
            _insertionIndex = -1;
            return;
        }

        // ── Drop from task list: reorder ────────────────────────────────
        if (e.Data.GetData(typeof(StepViewModel)) is StepViewModel dragged)
        {
            var fromIndex = vm.Steps.IndexOf(dragged);
            var toIndex   = _insertionIndex;

            if (fromIndex < 0 || toIndex < 0 || fromIndex == toIndex)
            {
                _insertionIndex = -1;
                return;
            }

            // Adjust target because removing the source shifts indices
            if (toIndex > fromIndex) toIndex--;
            vm.MoveStepInList(fromIndex, toIndex);
            _insertionIndex = -1;
        }
    }

    // ─── Insertion adorner helpers ────────────────────────────────────────

    private void UpdateInsertionAdorner(DragEventArgs e)
    {
        var item = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
        if (item is null)
        {
            RemoveInsertionAdorner();
            return;
        }

        var pos          = e.GetPosition(item);
        var showAtBottom = pos.Y > item.ActualHeight / 2.0;

        if (ReferenceEquals(item, _adornerHost) &&
            _insertionAdorner is not null &&
            _insertionAdorner.ShowAtBottom == showAtBottom)
            return;

        RemoveInsertionAdorner();

        var layer = AdornerLayer.GetAdornerLayer(item);
        if (layer is null) return;

        _adornerHost      = item;
        _insertionAdorner = new InsertionAdorner(item, showAtBottom);
        layer.Add(_insertionAdorner);

        if (DataContext is MainViewModel vm && item.DataContext is StepViewModel sv)
        {
            var idx = vm.Steps.IndexOf(sv);
            _insertionIndex = showAtBottom ? idx + 1 : idx;
        }
    }

    private void RemoveInsertionAdorner()
    {
        if (_insertionAdorner is null || _adornerHost is null) return;

        var layer = AdornerLayer.GetAdornerLayer(_adornerHost);
        layer?.Remove(_insertionAdorner);
        _insertionAdorner = null;
        _adornerHost      = null;
    }

    // ─── Inline edit keyboard shortcuts ──────────────────────────────────

    private void EditTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not StepViewModel vm) return;

        if (e.Key == Key.Return)
        {
            vm.CommitEditCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.CancelEditCommand.Execute(null);
            e.Handled = true;
        }
    }

    // ─── Utility ─────────────────────────────────────────────────────────

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