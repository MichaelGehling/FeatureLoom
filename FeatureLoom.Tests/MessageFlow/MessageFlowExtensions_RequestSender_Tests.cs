using FeatureLoom.MessageFlow;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.MessageFlow;

public class MessageFlowExtensions_RequestSender_Tests
{
    [Fact]
    public async Task Respond_with_RequestSender_returns_computed_response()
    {
        var requester = new RequestSender<int, string>();

        // Register responder on the requester itself
        requester.Respond<int, string>(req => $"R{req}");

        // When sending a request, the responder computes and posts a response back
        var result = await requester.SendRequestAsync(5);

        Assert.Equal("R5", result);
    }

    [Fact]
    public void Respond_with_RequestSender_sync_path_also_works()
    {
        var requester = new RequestSender<int, int>();
        requester.Respond<int, int>(x => x * 10);

        var result = requester.SendRequest(7);

        Assert.Equal(70, result);
    }

    [Fact]
    public async Task RespondWhen_only_replies_when_condition_is_true()
    {
        var requester = new RequestSender<int, string>();
        requester.RespondWhen<int, string>(x => x % 2 == 0, x => $"E{x}");

        // No response for odd
        var oddTask = requester.SendRequestAsync(3);

        // Response for even
        var even = await requester.SendRequestAsync(4);

        Assert.Equal("E4", even);

        // Optional: oddTask should not complete successfully; do not await it to avoid flakiness due to timeouts.
        Assert.False(oddTask.IsCompletedSuccessfully);
    }
}