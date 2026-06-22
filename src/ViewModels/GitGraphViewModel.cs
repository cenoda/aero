using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aero.Services.Git;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Aero.ViewModels;

/// <summary>
/// Pre-computed geometry for a single commit node in the branch graph.
/// All values are calculated off-UI-thread — Render() consumes these only.
/// </summary>
public record GraphNodeGeometry(
    string Sha, int RowIndex, int LaneIndex,
    double CenterX, double CenterY, string LaneColor,
    bool IsHead, bool IsMerge, string? BranchLabel,
    IReadOnlyList<ParentEndpoint> ParentConnections);

/// <summary>A connection from a commit to one of its parents.</summary>
public record ParentEndpoint(
    bool IsInGraph, int ParentRowIndex, int ParentLaneIndex,
    double ParentCenterX, double ParentCenterY);

/// <summary>Information about a lane in the branch graph.</summary>
public record GraphLaneInfo(int Index, string Color, string? BranchName);

/// <summary>
/// ViewModel for the branch graph (DAG) tab in the Git panel.
/// Owns commit data, lane-assignment algorithm, and pre-computed node geometry.
/// Lane assignment: greedy left-to-right, recycling after merges, capped at 12.
/// </summary>
public class GitGraphViewModel : ReactiveObject
{
    public const int MaxCommits = 200;

    private static readonly string[] LanePalette =
    {
        "#4CAF50", "#2196F3", "#FF9800", "#9C27B0",
        "#F44336", "#00BCD4", "#795548", "#607D8B",
    };

    private const int MaxLanes = 12;
    private const double RowHeight = 24.0;
    private const double LaneWidth = 28.0;
    private const double LeftPadding = 16.0;

    [Reactive] public bool IsLoading { get; set; }
    [Reactive] public string? ErrorMessage { get; set; }
    [Reactive] public GitGraphCommit? SelectedCommit { get; set; }

    /// <summary>Detail pane ViewModel for the selected commit.</summary>
    public GitGraphCommitDetailViewModel Detail { get; } = new();

    public IReadOnlyList<GitGraphCommit> Commits { get; private set; } = Array.Empty<GitGraphCommit>();
    public IReadOnlyList<GraphNodeGeometry> Nodes { get; private set; } = Array.Empty<GraphNodeGeometry>();
    public IReadOnlyList<GraphLaneInfo> Lanes { get; private set; } = Array.Empty<GraphLaneInfo>();

    public double TotalHeight => Nodes.Count > 0 ? Nodes.Max(n => n.CenterY) + RowHeight : 0;
    public double TotalWidth => Math.Max(300, (Lanes.Count > 0 ? Lanes.Count : 1) * LaneWidth + LeftPadding * 2);

    public async Task LoadAsync(IGitService gitService, CancellationToken ct = default)
    {
        if (gitService == null) return;
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var commits = await gitService.GetGraphAsync(MaxCommits, ct);
            Commits = commits;
            var (nodes, lanes) = ComputeLayout(commits);
            Nodes = nodes;
            Lanes = lanes;
            SelectedCommit = null;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            Nodes = Array.Empty<GraphNodeGeometry>();
            Lanes = Array.Empty<GraphLaneInfo>();
        }
        finally { IsLoading = false; }
    }

    public void SelectCommit(GitGraphCommit? commit)
    {
        SelectedCommit = commit;
        if (commit != null)
            Detail.Show(commit);
        else
            Detail.Hide();
    }

    private static (IReadOnlyList<GraphNodeGeometry>, IReadOnlyList<GraphLaneInfo>)
        ComputeLayout(IReadOnlyList<GitGraphCommit> commits)
    {
        if (commits == null || commits.Count == 0)
            return (Array.Empty<GraphNodeGeometry>(), Array.Empty<GraphLaneInfo>());

        var shaToRow = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < commits.Count; i++)
            shaToRow[commits[i].Sha] = i;

        // Build child map: parent SHA → list of child SHAs (for lane inheritance)
        var childrenOf = new Dictionary<string, List<string>>();
        foreach (var c in commits)
            foreach (var ps in c.ParentShas)
            {
                if (!childrenOf.ContainsKey(ps))
                    childrenOf[ps] = new List<string>();
                childrenOf[ps].Add(c.Sha);
            }

        var shaToLane = new Dictionary<string, int>(StringComparer.Ordinal);
        var laneAssignments = new List<LaneState>();
        int maxLaneCount = 0;

        // Walk newest → oldest. Branch heads get assigned lanes.
        // Non-branch commits inherit from their first child (already assigned
        // since children are newer). If no child exists, inherit from first parent.
        for (int i = 0; i < commits.Count; i++)
        {
            var c = commits[i];
            int li;

            if (c.BranchLabels.Count > 0)
            {
                li = Math.Min(FindOrAssignLane(laneAssignments, c.BranchLabels[0]), MaxLanes);
            }
            else
            {
                // Inherit from first child (which is newer and already assigned)
                if (childrenOf.TryGetValue(c.Sha, out var ch) && ch.Count > 0
                    && shaToLane.TryGetValue(ch[0], out var childLane))
                {
                    li = childLane;
                }
                // Fall back to first parent (for linear chains where child mapping works)
                else if (c.ParentShas.Count > 0 && shaToLane.TryGetValue(c.ParentShas[0], out var pl))
                {
                    li = pl;
                }
                else
                {
                    li = FindFreeLane(laneAssignments);
                }
            }

            shaToLane[c.Sha] = li;

            // Recycle merged branch lanes
            if (c.ParentShas.Count >= 2 && c.BranchLabels.Count > 0)
                for (int p = 1; p < c.ParentShas.Count; p++)
                    if (shaToLane.TryGetValue(c.ParentShas[p], out var ml) && ml != li && ml < laneAssignments.Count)
                        laneAssignments[ml] = new LaneState(null, false);

            if (li + 1 > maxLaneCount) maxLaneCount = li + 1;
        }

        var laneInfos = new List<GraphLaneInfo>();
        for (int i = 0; i < Math.Min(maxLaneCount, MaxLanes + 1); i++)
            laneInfos.Add(new GraphLaneInfo(i,
                i < MaxLanes ? LanePalette[i % LanePalette.Length] : "#999999",
                i < laneAssignments.Count ? laneAssignments[i].BranchName : null));

        var nodes = new List<GraphNodeGeometry>();
        for (int i = 0; i < commits.Count; i++)
        {
            var c = commits[i];
            var li = Math.Min(shaToLane.GetValueOrDefault(c.Sha, 0), MaxLanes);
            var cx = LeftPadding + li * LaneWidth + LaneWidth / 2;
            var cy = i * RowHeight + RowHeight / 2;

            var conns = new List<ParentEndpoint>();
            foreach (var ps in c.ParentShas)
                if (shaToRow.TryGetValue(ps, out var pr) && shaToLane.TryGetValue(ps, out var pl))
                    conns.Add(new ParentEndpoint(true, pr, pl,
                        LeftPadding + Math.Min(pl, MaxLanes) * LaneWidth + LaneWidth / 2,
                        pr * RowHeight + RowHeight / 2));
                else
                    conns.Add(new ParentEndpoint(false, -1, -1, cx, commits.Count * RowHeight));

            nodes.Add(new GraphNodeGeometry(c.Sha, i, li, cx, cy,
                laneInfos[Math.Min(li, laneInfos.Count - 1)].Color,
                IsHead: i == 0, IsMerge: c.ParentShas.Count >= 2,
                c.BranchLabels.Count > 0 ? c.BranchLabels[0] : null, conns));
        }
        return (nodes, laneInfos);
    }

    private static int FindOrAssignLane(List<LaneState> lanes, string branchName)
    {
        for (int i = 0; i < lanes.Count; i++)
            if (string.Equals(lanes[i].BranchName, branchName, StringComparison.Ordinal))
            {
                lanes[i] = new LaneState(branchName, true); return i;
            }
        for (int i = 0; i < lanes.Count; i++)
            if (!lanes[i].IsActive)
            {
                lanes[i] = new LaneState(branchName, true); return i;
            }
        lanes.Add(new LaneState(branchName, true));
        return lanes.Count - 1;
    }

    private static int FindFreeLane(List<LaneState> lanes)
    {
        for (int i = 0; i < lanes.Count; i++)
            if (!lanes[i].IsActive) return i;
        lanes.Add(new LaneState(null, false));
        return lanes.Count - 1;
    }

    private readonly struct LaneState
    {
        public string? BranchName { get; }
        public bool IsActive { get; }
        public LaneState(string? branchName, bool isActive)
        {
            BranchName = branchName;
            IsActive = isActive;
        }
    }
}
