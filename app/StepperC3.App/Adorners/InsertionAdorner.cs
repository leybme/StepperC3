using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace StepperC3.App.Adorners;

/// <summary>
/// Draws a blue horizontal insertion-line indicator on a ListBoxItem,
/// showing exactly where a dragged step will be inserted on drop.
/// </summary>
public sealed class InsertionAdorner : Adorner
{
    private readonly bool _showAtBottom;

    /// <summary>True if the line is drawn at the bottom edge (insert after), false for top (insert before).</summary>
    public bool ShowAtBottom => _showAtBottom;

    private static readonly Pen LinePen;

    static InsertionAdorner()
    {
        LinePen = new Pen(Brushes.DodgerBlue, 2.5);
        LinePen.Freeze();
    }

    public InsertionAdorner(UIElement adornedElement, bool showAtBottom) : base(adornedElement)
    {
        _showAtBottom = showAtBottom;
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext ctx)
    {
        if (AdornedElement is not FrameworkElement el) return;

        double y  = _showAtBottom ? el.ActualHeight : 0;
        double x1 = 6;
        double x2 = el.ActualWidth - 6;

        ctx.DrawLine(LinePen, new Point(x1, y), new Point(x2, y));
        ctx.DrawEllipse(Brushes.DodgerBlue, null, new Point(x1, y), 4, 4);
        ctx.DrawEllipse(Brushes.DodgerBlue, null, new Point(x2, y), 4, 4);
    }
}
