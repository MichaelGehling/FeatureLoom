using FeatureLoom.Helpers;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.Helpers;
public class UndoRedoTests
{
    [Fact]
    public void UndoRedo_BasicSync_Works()
    {
        var ur = new UndoRedo();
        int value = 0;

        ur.DoWithUndo(
            () => value = 1,
            () => value = 0,
            "Set to 1"
        );

        Assert.Equal(1, value);
        ur.PerformUndo();
        Assert.Equal(0, value);
        ur.PerformRedo();
        Assert.Equal(1, value);
    }

    [Fact]
    public async Task UndoRedo_BasicAsync_Works()
    {
        var ur = new UndoRedo();
        int value = 0;

        await ur.DoWithUndoAsync(
            async () => { await Task.Delay(1); value = 2; },
            async () => { await Task.Delay(1); value = 0; },
            "Set to 2"
        );

        Assert.Equal(2, value);
        await ur.PerformUndoAsync();
        Assert.Equal(0, value);
        await ur.PerformRedoAsync();
        Assert.Equal(2, value);
    }

    [Fact]
    public void UndoRedo_Transaction_GroupsActions()
    {
        var ur = new UndoRedo();
        int value = 0;

        using (ur.StartTransaction("Transaction"))
        {
            ur.DoWithUndo(() => value += 1, () => value -= 1, "Add 1");
            ur.DoWithUndo(() => value += 2, () => value -= 2, "Add 2");
        }

        Assert.Equal(3, value);
        ur.PerformUndo();
        Assert.Equal(0, value);
        ur.PerformRedo();
        Assert.Equal(3, value);
    }

    [Fact]
    public void UndoRedo_AdditiveActions()
    {
        var ur = new UndoRedo();
        int value = 0;

        ur.DoWithUndo(() => value += 1, () => value -= 1, "Add 1");

        Assert.Equal(1, value);
        ur.PerformUndo();
        Assert.Equal(0, value);
        ur.PerformRedo();
        Assert.Equal(1, value);
    }

    [Fact]
    public void UndoRedo_Clear_RemovesAll()
    {
        var ur = new UndoRedo();

        ur.DoWithUndo(() => { }, () => { }, "Set to 1");
        ur.DoWithUndo(() => { }, () => { }, "Set to 2");

        Assert.Equal(2, ur.NumUndos);
        ur.Clear();
        Assert.Equal(0, ur.NumUndos);
        Assert.Equal(0, ur.NumRedos);
    }

    [Fact]
    public void UndoRedo_Descriptions_AreCorrect()
    {
        var ur = new UndoRedo();
        ur.DoWithUndo(() => { }, () => { }, "First");
        ur.DoWithUndo(() => { }, () => { }, "Second");

        Assert.Contains("First", ur.UndoDescriptions);
        Assert.Contains("Second", ur.UndoDescriptions);
    }

    [Fact]
    public void UndoRedo_HistoryLimit_IsRespected()
    {
        var ur = new UndoRedo(historyLimit: 3);
        int value = 0;

        ur.DoWithUndo(() => value = 1, () => value = 0, "Set to 1");
        ur.DoWithUndo(() => value = 2, () => value = 1, "Set to 2");
        ur.DoWithUndo(() => value = 3, () => value = 2, "Set to 3");
        ur.DoWithUndo(() => value = 4, () => value = 3, "Set to 4");
        ur.DoWithUndo(() => value = 5, () => value = 4, "Set to 5");

        Assert.Equal(3, ur.NumUndos);
        Assert.DoesNotContain("Set to 1", ur.UndoDescriptions);
        Assert.DoesNotContain("Set to 2", ur.UndoDescriptions);
        Assert.Contains("Set to 3", ur.UndoDescriptions);
        Assert.Contains("Set to 4", ur.UndoDescriptions);
        Assert.Contains("Set to 5", ur.UndoDescriptions);

        ur.PerformUndo();
        Assert.Equal(4, value);
        ur.PerformUndo();
        Assert.Equal(3, value);
        ur.PerformUndo();
        Assert.Equal(2, value);

        Assert.Equal(0, ur.NumUndos);
        ur.PerformUndo();
        Assert.Equal(2, value);
    }

    [Fact]
    public void UndoRedo_UnlimitedHistory_Works()
    {
        var ur = new UndoRedo(historyLimit: 0);
        int value = 0;

        for (int i = 1; i <= 100; i++)
        {
            int prev = i - 1;
            int current = i;
            ur.DoWithUndo(() => value = current, () => value = prev, $"Set to {i}");
        }

        Assert.Equal(100, ur.NumUndos);
        Assert.Equal(100, value);

        ur.PerformUndo();
        Assert.Equal(99, value);
        Assert.Equal(99, ur.NumUndos);
        Assert.Equal(1, ur.NumRedos);
    }
}