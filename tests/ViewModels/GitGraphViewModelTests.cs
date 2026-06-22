using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aero.Models.Git;
using Aero.Services.Git;
using Aero.ViewModels;
using Xunit;

namespace Aero.Tests.ViewModels;

public class GitGraphViewModelTests
{
    static GitGraphCommit C(string sha, string msg,
        IReadOnlyList<string>? parents = null,
        IReadOnlyList<string>? labels = null) => new(
        sha, msg, "Author", DateTimeOffset.Now,
        parents ?? Array.Empty<string>(),
        labels ?? Array.Empty<string>());

    [Fact] public void LoadAsync_NoCommits_ReturnsEmpty()
    {
        var vm = new GitGraphViewModel();
        vm.LoadAsync(new FakeGitService(Array.Empty<GitGraphCommit>())).GetAwaiter().GetResult();
        Assert.Empty(vm.Commits); Assert.Empty(vm.Nodes); Assert.Empty(vm.Lanes);
    }

    [Fact] public void LoadAsync_LinearHistory_OneLane()
    {
        var cs = new[] { C("c3","3",new[]{"c2"},new[]{"main"}), C("c2","2",new[]{"c1"}), C("c1","1",new[]{"c0"}) };
        var vm = new GitGraphViewModel();
        vm.LoadAsync(new FakeGitService(cs)).GetAwaiter().GetResult();
        Assert.Equal(3, vm.Nodes.Count); Assert.Equal(1, vm.Lanes.Count);
        Assert.All(vm.Nodes, n => Assert.Equal(0, n.LaneIndex));
    }

    [Fact] public void LoadAsync_TwoBranches_TwoLanes()
    {
        var cs = new[] {
            C("c3","C3",new[]{"c2"},new[]{"main"}), C("c2","C2",new[]{"c1"}),
            C("f3","F3",new[]{"f2"},new[]{"feature"}), C("f2","F2",new[]{"c1"}), C("c1","C1",new[]{"c0"}),
        };
        var vm = new GitGraphViewModel();
        vm.LoadAsync(new FakeGitService(cs)).GetAwaiter().GetResult();
        Assert.Equal(5, vm.Nodes.Count); Assert.Equal(2, vm.Lanes.Count);
        Assert.Equal(0, vm.Nodes[0].LaneIndex); Assert.Equal(1, vm.Nodes[2].LaneIndex);
    }

    [Fact] public void LoadAsync_Merge_RecyclesLane()
    {
        var cs = new[] {
            C("c4","C4",new[]{"c3"},new[]{"main"}), C("c3","Merge",new[]{"c2","f3"}),
            C("c2","C2",new[]{"c1"}), C("f3","F3",new[]{"f2"},new[]{"feature"}),
            C("f2","F2",new[]{"c1"}), C("c1","C1",new[]{"c0"}),
        };
        var vm = new GitGraphViewModel();
        vm.LoadAsync(new FakeGitService(cs)).GetAwaiter().GetResult();
        Assert.Equal(6, vm.Nodes.Count); Assert.True(vm.Lanes.Count <= 2);
    }

    [Fact] public void LoadAsync_CappedAt_12Lanes()
    {
        var cs = new List<GitGraphCommit>();
        for (int i = 1; i <= 15; i++) cs.Add(C($"c{i}",$"B{i}",new[]{"root"},new[]{$"b{i}"}));
        cs.Add(C("root","Root",Array.Empty<string>()));
        var vm = new GitGraphViewModel();
        vm.LoadAsync(new FakeGitService(cs.AsReadOnly())).GetAwaiter().GetResult();
        Assert.True(vm.Lanes.Count <= 13);
        Assert.Contains(vm.Nodes, n => n.LaneIndex >= 12);
    }

    [Fact] public void SelectCommit_SetsAndClears()
    {
        var cs = new[] { C("c2","2",new[]{"c1"},new[]{"main"}), C("c1","1",new[]{"c0"}) };
        var vm = new GitGraphViewModel();
        vm.LoadAsync(new FakeGitService(cs)).GetAwaiter().GetResult();
        Assert.Null(vm.SelectedCommit);
        vm.SelectCommit(cs[0]); Assert.Equal("c2", vm.SelectedCommit!.Sha);
        vm.SelectCommit(null); Assert.Null(vm.SelectedCommit);
    }

    [Fact] public void LoadAsync_NullService_DoesNotThrow()
    {
        var vm = new GitGraphViewModel();
        Assert.Null(Record.Exception(() => vm.LoadAsync(null!).GetAwaiter().GetResult()));
    }

    [Fact] public void HeadNode_HasIsHeadTrue()
    {
        var cs = new[] { C("c3","3",new[]{"c2"},new[]{"main"}), C("c2","2",new[]{"c1"}), C("c1","1",new[]{"c0"}) };
        var vm = new GitGraphViewModel();
        vm.LoadAsync(new FakeGitService(cs)).GetAwaiter().GetResult();
        Assert.True(vm.Nodes[0].IsHead); Assert.False(vm.Nodes[1].IsHead);
    }

    [Fact] public void MergeCommit_HasIsMergeTrue()
    {
        var cs = new[] { C("c3","M",new[]{"c2","f1"},new[]{"main"}), C("c2","2",new[]{"c1"}), C("f1","F1",new[]{"c1"},new[]{"feature"}), C("c1","1",new[]{"c0"}) };
        var vm = new GitGraphViewModel();
        vm.LoadAsync(new FakeGitService(cs)).GetAwaiter().GetResult();
        Assert.True(vm.Nodes[0].IsMerge); Assert.Equal(2, vm.Nodes[0].ParentConnections.Count);
    }

    [Fact] public void BranchLabel_AttachedToCorrectNode()
    {
        var cs = new[] { C("c2","2",new[]{"c1"},new[]{"main"}), C("c1","1",new[]{"c0"}) };
        var vm = new GitGraphViewModel();
        vm.LoadAsync(new FakeGitService(cs)).GetAwaiter().GetResult();
        Assert.Equal("main", vm.Nodes[0].BranchLabel); Assert.Null(vm.Nodes[1].BranchLabel);
    }

    [Fact] public void LaneColors_AreDeterministic()
    {
        var cs = new[] { C("c2","2",new[]{"c1"},new[]{"main"}), C("c1","1",new[]{"c0"}) };
        var vm1 = new GitGraphViewModel(); vm1.LoadAsync(new FakeGitService(cs)).GetAwaiter().GetResult();
        var vm2 = new GitGraphViewModel(); vm2.LoadAsync(new FakeGitService(cs)).GetAwaiter().GetResult();
        Assert.Equal(vm1.Lanes[0].Color, vm2.Lanes[0].Color);
    }
}


file sealed class FakeGitService : IGitService
{
    readonly IReadOnlyList<GitGraphCommit> _c;
    public FakeGitService(IReadOnlyList<GitGraphCommit> c) => _c = c;
    public string Name => "Fake";
    public void Dispose() { }
    public Task<IReadOnlyList<GitGraphCommit>> GetGraphAsync(int cnt, CancellationToken ct)
        => Task.FromResult(_c.Take(cnt).ToList() as IReadOnlyList<GitGraphCommit> ?? Array.Empty<GitGraphCommit>());
    public Task<GitRepositoryInfo> GetRepositoryInfoAsync(CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyList<GitFileStatus>> GetStatusAsync(CancellationToken ct) => throw new NotImplementedException();
    public Task StageAsync(string fp, CancellationToken ct) => throw new NotImplementedException();
    public Task UnstageAsync(string fp, CancellationToken ct) => throw new NotImplementedException();
    public Task<GitCommitResult> CommitAsync(string m, string an, string ae, CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyList<GitBranchInfo>> GetBranchesAsync(CancellationToken ct) => throw new NotImplementedException();
    public Task CheckoutAsync(string bn, CancellationToken ct) => throw new NotImplementedException();
    public Task<GitDiff> GetFileDiffAsync(string fp, CancellationToken ct) => throw new NotImplementedException();
    public Task<IReadOnlyList<GitCommitInfo>> GetLogAsync(int cnt, CancellationToken ct) => throw new NotImplementedException();
    public Task<string[]> GetConfigAsync(string[] k, CancellationToken ct) => throw new NotImplementedException();
}
