using System;
using Avalonia;
using Avalonia.Controls;

namespace Aero.Views;

/// <summary>
/// Horizontal panel that proportionally shrinks children when total desired width
/// exceeds available space — like Chrome tabs. Each child is clamped to its
/// <see cref="Layoutable.MinWidth"/> / <see cref="Layoutable.MaxWidth"/>.
/// </summary>
public class TabShrinkPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        // Measure all children with infinite width so we get their natural size,
        // but constrained height to the available height.
        var childConstraint = new Size(double.PositiveInfinity, availableSize.Height);
        var totalDesired = 0.0;
        var maxHeight = 0.0;

        foreach (var child in Children)
        {
            child.Measure(childConstraint);
            totalDesired += child.DesiredSize.Width;
            maxHeight = Math.Max(maxHeight, child.DesiredSize.Height);
        }

        // If everything fits, use the total desired width
        var panelWidth = availableSize.Width;
        if (!double.IsInfinity(panelWidth) && totalDesired > panelWidth)
        {
            // Need to shrink: re-measure children with constrained width
            DistributeAndMeasure(availableSize);
        }

        return new Size(
            double.IsInfinity(availableSize.Width) ? totalDesired : Math.Min(totalDesired, availableSize.Width),
            Math.Min(maxHeight, availableSize.Height));
    }

    private void DistributeAndMeasure(Size availableSize)
    {
        if (Children.Count == 0)
            return;

        var available = availableSize.Width;
        var desiredWidths = new double[Children.Count];
        var minWidths = new double[Children.Count];
        var maxWidths = new double[Children.Count];
        var totalDesired = 0.0;
        var totalMin = 0.0;

        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            desiredWidths[i] = child.DesiredSize.Width;
            minWidths[i] = child.MinWidth;
            maxWidths[i] = child.MaxWidth > 0 ? child.MaxWidth : double.PositiveInfinity;
            totalDesired += desiredWidths[i];
            totalMin += minWidths[i];
        }

        // If even minimum widths exceed available, just use min widths
        if (totalMin >= available)
        {
            for (var i = 0; i < Children.Count; i++)
            {
                var w = minWidths[i];
                Children[i].Measure(new Size(w, availableSize.Height));
            }
            return;
        }

        // Proportional shrink: each child gets min + their share of the excess
        var excess = available - totalMin;
        var desiredExcess = 0.0;
        for (var i = 0; i < Children.Count; i++)
            desiredExcess += Math.Max(0, desiredWidths[i] - minWidths[i]);

        for (var i = 0; i < Children.Count; i++)
        {
            var desiredExtra = Math.Max(0, desiredWidths[i] - minWidths[i]);
            var share = desiredExcess > 0
                ? excess * (desiredExtra / desiredExcess)
                : excess / Children.Count;

            var allocated = Math.Min(minWidths[i] + share, maxWidths[i]);
            Children[i].Measure(new Size(allocated, availableSize.Height));
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;
        foreach (var child in Children)
        {
            var width = child.DesiredSize.Width;
            child.Arrange(new Rect(x, 0, width, finalSize.Height));
            x += width;
        }

        return finalSize;
    }
}
