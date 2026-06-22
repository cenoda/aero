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
    public event Action<GitGraphCommit>? CommitClicked;

    private const double NR = 6.0;
    private const double SNR = 8.0;
    private readonly Dictionary<string, GitGraphCommit> _shaMap = new();

    static GitGraphControl() { AffectsRender<GitGraphControl>(NodesProperty, LanesProperty, SelectedCommitProperty); }

    public void SetCommitLookup(IReadOnlyList<GitGraphCommit> commits)
    {
        _shaMap.Clear();
        if (commits != null)
            foreach (var c in commits) _shaMap[c.Sha] = c;
    }

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
            if (n.BranchLabel == null) continue;
            var txt = new FormattedText(n.BranchLabel, System.Globalization.CultureInfo.CurrentCulture, Avalonia.Media.FlowDirection.LeftToRight, new Typeface("Consolas, Menlo, monospace"), 11, Brushes.White);
            var col = Color.Parse(n.LaneColor);
            var bg = new SolidColorBrush(Color.FromArgb(200, col.R, col.G, col.B));
            var rc = new Rect(n.CenterX + NR + 4, n.CenterY - 8, txt.Width + 8, 16);
            ctx.FillRectangle(bg, rc, 3);
            ctx.DrawText(txt, new Point(n.CenterX + NR + 8, n.CenterY - 7));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pos = e.GetPosition(this);
        var nodes = Nodes;
        if (nodes == null) return;
        GitGraphCommit? closest = null;
        double minDist = double.MaxValue;
        foreach (var n in nodes)
        {
            var dx = pos.X - n.CenterX; var dy = pos.Y - n.CenterY;
            var dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist <= SNR + 2 && dist < minDist) { minDist = dist; _shaMap.TryGetValue(n.Sha, out closest); }
        }
        if (closest != null) { CommitClicked?.Invoke(closest); e.Handled = true; }
    }
}
