using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Aero.Services.Git;
using Aero.ViewModels;

namespace Aero.Views;

public class GitGraphControl : Control
{
    public static readonly StyledProperty<IReadOnlyList<GraphNodeGeometry>?> NodesProperty =
        AvaloniaProperty.Register<GitGraphControl, IReadOnlyList<GraphNodeGeometry>?>(nameof(Nodes));
    public static readonly StyledProperty<IReadOnlyList<GraphLaneInfo>?> LanesProperty =
        AvaloniaProperty.Register<GitGraphControl, IReadOnlyList<GraphLaneInfo>?>(nameof(Lanes));
    public static readonly StyledProperty<GitGraphCommit?> SelectedCommitProperty =
        AvaloniaProperty.Register<GitGraphControl, GitGraphCommit?>(nameof(SelectedCommit));

    public IReadOnlyList<GraphNodeGeometry>? Nodes { get => GetValue(NodesProperty); set => SetValue(NodesProperty, value); }
    public IReadOnlyList<GraphLaneInfo>? Lanes { get => GetValue(LanesProperty); set => SetValue(LanesProperty, value); }
    public GitGraphCommit? SelectedCommit { get => GetValue(SelectedCommitProperty); set => SetValue(SelectedCommitProperty, value); }
    /// <summary>Raised with the SHA of the clicked commit.</summary>
    public event Action<string>? CommitClicked;

    private const double NR = 6.0;
    private const double SNR = 8.0;

    static GitGraphControl() { AffectsRender<GitGraphControl>(NodesProperty, LanesProperty, SelectedCommitProperty); }

    public override void Render(DrawingContext ctx)
    {
        var nodes = Nodes; var lanes = Lanes;
        if (nodes == null || lanes == null || nodes.Count == 0) return;
        var selSha = SelectedCommit?.Sha;
        DrawLanes(ctx, lanes, nodes);
        DrawConns(ctx, nodes);
        DrawCircles(ctx, nodes, selSha);
        DrawLabels(ctx, nodes);
    }

    private static void DrawLanes(DrawingContext ctx, IReadOnlyList<GraphLaneInfo> lanes, IReadOnlyList<GraphNodeGeometry> nodes)
    {
        double maxY = nodes.Max(n => n.CenterY) + 12;
        foreach (var l in lanes)
        {
            var x = l.Index * 28.0 + 16.0 + 14.0;
            var col = Color.Parse(l.Color);
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(80, col.R, col.G, col.B)), 2);
            ctx.DrawLine(pen, new Point(x, 0), new Point(x, maxY));
        }
    }

    private static void DrawConns(DrawingContext ctx, IReadOnlyList<GraphNodeGeometry> nodes)
    {
        var pen = new Pen(new SolidColorBrush(Colors.Gray, 0.4), 1.5);
        var dash = new Pen(new SolidColorBrush(Colors.Gray, 0.3), 1, new DashStyle(new[] { 4.0, 4.0 }, 0));
        foreach (var n in nodes)
            foreach (var c in n.ParentConnections)
                if (c.IsInGraph)
                {
                    if (n.LaneIndex == c.ParentLaneIndex)
                        ctx.DrawLine(pen, new Point(n.CenterX, n.CenterY - NR), new Point(c.ParentCenterX, c.ParentCenterY + NR));
                    else
                    {
                        var my = (n.CenterY + c.ParentCenterY) / 2;
                        ctx.DrawLine(pen, new Point(n.CenterX, n.CenterY - NR), new Point(n.CenterX, my));
                        ctx.DrawLine(pen, new Point(n.CenterX, my), new Point(c.ParentCenterX, my));
                        ctx.DrawLine(pen, new Point(c.ParentCenterX, my), new Point(c.ParentCenterX, c.ParentCenterY + NR));
                    }
                }
                else
                    ctx.DrawLine(dash, new Point(n.CenterX, n.CenterY), new Point(c.ParentCenterX, c.ParentCenterY));
    }

    private static void DrawCircles(DrawingContext ctx, IReadOnlyList<GraphNodeGeometry> nodes, string? selSha)
    {
        foreach (var n in nodes)
        {
            var fill = new SolidColorBrush(Color.Parse(n.LaneColor));
            var sel = selSha != null && n.Sha == selSha;
            var r = sel ? SNR : NR;
            var pt = new Point(n.CenterX, n.CenterY);
            ctx.DrawEllipse(fill, null, pt, r, r);
            if (n.IsHead) ctx.DrawEllipse(Brushes.White, null, pt, r * 0.4, r * 0.4);
            if (sel) ctx.DrawEllipse(null, new Pen(Brushes.White, 2), pt, r + 2, r + 2);
        }
    }

    private static void DrawLabels(DrawingContext ctx, IReadOnlyList<GraphNodeGeometry> nodes)
    {
        foreach (var n in nodes)
        {
            var textX = n.CenterX + NR + 8;

            if (n.BranchLabel != null)
            {
                var branchText = new FormattedText(
                    n.BranchLabel,
                    System.Globalization.CultureInfo.CurrentCulture,
                    Avalonia.Media.FlowDirection.LeftToRight,
                    new Typeface("Consolas, Menlo, monospace"),
                    11,
                    Brushes.White);

                var col = Color.Parse(n.LaneColor);
                var bg = new SolidColorBrush(Color.FromArgb(200, col.R, col.G, col.B));
                var rc = new Rect(textX - 4, n.CenterY - 8, branchText.Width + 8, 16);
                ctx.FillRectangle(bg, rc, 3);
                ctx.DrawText(branchText, new Point(textX, n.CenterY - 7));

                textX += branchText.Width + 12;
            }

            if (!string.IsNullOrWhiteSpace(n.Message))
            {
                var messageText = new FormattedText(
                    n.Message,
                    System.Globalization.CultureInfo.CurrentCulture,
                    Avalonia.Media.FlowDirection.LeftToRight,
                    new Typeface("Consolas, Menlo, monospace"),
                    11,
                    Brushes.Gainsboro);

                ctx.DrawText(messageText, new Point(textX, n.CenterY - 7));
            }
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);
        var nodes = Nodes;
        if (nodes == null) return;
        string? closestSha = null;
        double minDist = double.MaxValue;
        foreach (var n in nodes)
        {
            var dx = pos.X - n.CenterX; var dy = pos.Y - n.CenterY;
            var dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist <= SNR + 2 && dist < minDist) { minDist = dist; closestSha = n.Sha; }
        }
        if (closestSha != null) { CommitClicked?.Invoke(closestSha); e.Handled = true; }
    }
}
