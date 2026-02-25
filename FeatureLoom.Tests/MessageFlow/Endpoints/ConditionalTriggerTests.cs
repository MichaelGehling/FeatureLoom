using FeatureLoom.Diagnostics;
using FeatureLoom.Helpers;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.MessageFlow;

public class ConditionalTriggerTests
{
    [Fact]
    public void IsOnlySetWhenReceivedValidMessage()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var trigger = new ConditionalTrigger<int, int>(m => m >= 42, null);
        Assert.False(trigger.IsTriggered());
        trigger.Post(41);
        Assert.False(trigger.IsTriggered());
        trigger.Post(42);
        Assert.True(trigger.IsTriggered());
        Assert.True(trigger.IsTriggered(true));
        Assert.False(trigger.IsTriggered());
        trigger.Post(43);
        Assert.True(trigger.IsTriggered());
    }

    [Fact]
    public void IsResetWhenReceivedValidMessage()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var trigger = new ConditionalTrigger<int, int>(m => m > 42, m => m < 42);
        Assert.False(trigger.IsTriggered());
        trigger.Post(43);
        Assert.True(trigger.IsTriggered());
        trigger.Post(42);
        Assert.True(trigger.IsTriggered());
        trigger.Post(41);
        Assert.False(trigger.IsTriggered());
        trigger.Post(42);
        Assert.False(trigger.IsTriggered());
    }

    [Fact]
    public void SameType_BothConditions_Default_AllowsBoth_EndsInReset()
    {
        using var testContext = TestHelper.PrepareTestContext();

        // Default allows trigger and reset from the same message: final state should be reset.
        var trigger = new ConditionalTrigger<int, int>(m => m == 1, m => m == 1);
        Assert.False(trigger.IsTriggered());
        trigger.Post(1);
        Assert.False(trigger.IsTriggered());
    }

    [Fact]
    public void SameType_BothConditions_Disallowed_PrefersTrigger_WhenNotTriggered()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var trigger = new ConditionalTrigger<int, int>(m => m == 1, m => m == 1, allowSameMessageTriggerAndReset: false);
        Assert.False(trigger.IsTriggered());
        trigger.Post(1); // both match, not triggered yet -> prefer trigger
        Assert.True(trigger.IsTriggered());
    }

    [Fact]
    public void SameType_BothConditions_Disallowed_PrefersReset_WhenAlreadyTriggered()
    {
        using var testContext = TestHelper.PrepareTestContext();

        // 2 triggers only; 1 both triggers and resets. With disallow=true, while already triggered, prefer reset.
        var trigger = new ConditionalTrigger<int, int>(m => m == 2 || m == 1, m => m == 1, allowSameMessageTriggerAndReset: false);

        Assert.False(trigger.IsTriggered());
        trigger.Post(2);
        Assert.True(trigger.IsTriggered()); // now triggered

        trigger.Post(1); // both match, already triggered -> prefer reset
        Assert.False(trigger.IsTriggered());
    }

    [Fact]
    public void IgnoresOtherMessageTypes()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var trigger = new ConditionalTrigger<int, int>(_ => true, _ => true);
        Assert.False(trigger.IsTriggered());
        trigger.Post("hello"); // different type, should be ignored
        Assert.False(trigger.IsTriggered());
    }

    [Fact]
    public void WaitAsync_CompletesOnTrigger_And_PostAsyncIsFireAndForget()
    {
        using var testContext = TestHelper.PrepareTestContext();

        var trigger = new ConditionalTrigger<int, int>(m => m == 7, null);
        var waitTask = trigger.WaitAsync();
        Assert.False(waitTask.IsCompleted);

        var postTask = trigger.PostAsync(7);
        Assert.True(postTask.IsCompleted);

        Assert.True(waitTask.Wait(1000));
        Assert.True(waitTask.Result);
    }
}