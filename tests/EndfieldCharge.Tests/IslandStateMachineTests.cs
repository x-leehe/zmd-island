using EndfieldCharge.Host.Island;
using Xunit;

namespace EndfieldCharge.Tests;

/// <summary>
/// 四态状态机的纯逻辑测试：转换表、自转换拒绝、事件参数、Force 语义。
/// 提交前用 dotnet test tests/EndfieldCharge.Tests 跑一遍。
/// </summary>
public class IslandStateMachineTests
{
    [Fact]
    public void Starts_hidden()
    {
        Assert.Equal(IslandVisualState.Hidden, new IslandStateMachine().Current);
    }

    [Theory]
    [InlineData(IslandVisualState.Hidden, IslandVisualState.Response)]
    [InlineData(IslandVisualState.Hidden, IslandVisualState.Waiting)]
    [InlineData(IslandVisualState.Hidden, IslandVisualState.Contracted)]
    [InlineData(IslandVisualState.Response, IslandVisualState.Hidden)]
    [InlineData(IslandVisualState.Response, IslandVisualState.Waiting)]
    [InlineData(IslandVisualState.Waiting, IslandVisualState.Hidden)]
    [InlineData(IslandVisualState.Waiting, IslandVisualState.Contracted)]
    [InlineData(IslandVisualState.Waiting, IslandVisualState.Response)]
    [InlineData(IslandVisualState.Contracted, IslandVisualState.Waiting)]
    [InlineData(IslandVisualState.Contracted, IslandVisualState.Response)]
    [InlineData(IslandVisualState.Contracted, IslandVisualState.Hidden)]
    public void TryTransition_follows_the_transition_table(IslandVisualState from, IslandVisualState to)
    {
        var sm = new IslandStateMachine();
        if (from != IslandVisualState.Hidden)
            sm.Force(from);

        Assert.True(sm.TryTransition(to));
        Assert.Equal(to, sm.Current);
    }

    [Theory]
    [InlineData(IslandVisualState.Hidden, IslandVisualState.Hidden)]
    [InlineData(IslandVisualState.Response, IslandVisualState.Response)]
    [InlineData(IslandVisualState.Waiting, IslandVisualState.Waiting)]
    [InlineData(IslandVisualState.Contracted, IslandVisualState.Contracted)]
    [InlineData(IslandVisualState.Response, IslandVisualState.Contracted)]
    public void TryTransition_rejects_self_and_illegal_transitions(IslandVisualState from, IslandVisualState to)
    {
        var sm = new IslandStateMachine();
        if (from != IslandVisualState.Hidden)
            sm.Force(from);

        Assert.False(sm.TryTransition(to));
        Assert.Equal(from, sm.Current);
    }

    [Fact]
    public void TryTransition_reports_previous_and_next_state()
    {
        var sm = new IslandStateMachine();
        (IslandVisualState From, IslandVisualState To)? seen = null;
        sm.StateChanged += (from, to) => seen = (from, to);

        sm.TryTransition(IslandVisualState.Waiting);

        Assert.NotNull(seen);
        Assert.Equal((IslandVisualState.Hidden, IslandVisualState.Waiting), seen!.Value);
    }

    [Fact]
    public void Rejected_transition_does_not_raise_the_event()
    {
        var sm = new IslandStateMachine();
        sm.Force(IslandVisualState.Response);

        bool raised = false;
        sm.StateChanged += (_, _) => raised = true;

        Assert.False(sm.TryTransition(IslandVisualState.Contracted));
        Assert.False(raised);
    }

    [Fact]
    public void Force_bypasses_the_table_and_always_raises()
    {
        var sm = new IslandStateMachine();
        sm.Force(IslandVisualState.Response);

        bool raised = false;
        sm.StateChanged += (_, _) => raised = true;

        sm.Force(IslandVisualState.Response); // 自转换也触发
        Assert.True(raised);

        sm.Force(IslandVisualState.Contracted); // 表中 Response → Contracted 非法，Force 仍可
        Assert.Equal(IslandVisualState.Contracted, sm.Current);
    }
}
