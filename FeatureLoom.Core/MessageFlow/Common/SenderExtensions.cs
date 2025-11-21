using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow;

public static class SenderExtensions
{
    /// <summary>
    /// Sends a response message constructed from payload and request id.
    /// </summary>
    /// <typeparam name="T">Type of the response payload.</typeparam>
    /// <param name="sender">The sender to emit the response.</param>
    /// <param name="message">Response payload.</param>
    /// <param name="requestId">The correlating request id.</param>
    public static void SendResponse<T>(this ISender sender, T message, long requestId)
    {
        var response = new ResponseMessage<T>(message, requestId);
        sender.Send(response);
    }

    /// <summary>
    /// Sends an already wrapped response without modification.
    /// </summary>
    /// <typeparam name="T">Type of the response payload.</typeparam>
    /// <param name="sender">The sender to emit the response.</param>
    /// <param name="response">The wrapped response.</param>
    public static void SendResponse<T>(this ISender sender, IResponseMessage<T> response)
    {
        sender.Send(response);
    }

    /// <summary>
    /// Sends a response asynchronously, constructed from payload and request id.
    /// </summary>
    /// <typeparam name="T">Type of the response payload.</typeparam>
    /// <param name="sender">The sender to emit the response.</param>
    /// <param name="message">Response payload.</param>
    /// <param name="requestId">The correlating request id.</param>
    /// <returns>A task that completes when the message was sent asynchronously.</returns>
    public static Task SendResponseAsync<T>(this ISender sender, T message, long requestId)
    {
        var response = new ResponseMessage<T>(message, requestId);
        return sender.SendAsync(response);
    }

    /// <summary>
    /// Sends an already wrapped response asynchronously without modification.
    /// </summary>
    /// <typeparam name="T">Type of the response payload.</typeparam>
    /// <param name="sender">The sender to emit the response.</param>
    /// <param name="response">The wrapped response.</param>
    /// <returns>A task that completes when the message was sent asynchronously.</returns>
    public static Task SendResponseAsync<T>(this ISender sender, IResponseMessage<T> response)
    {
        return sender.SendAsync(response);
    }

    // Response helpers (typed sender)

    /// <summary>
    /// Sends a response message constructed from payload and request id to a typed response sender.
    /// </summary>
    /// <typeparam name="T">Type of the response payload.</typeparam>
    /// <param name="sender">The typed sender to emit the response.</param>
    /// <param name="message">Response payload.</param>
    /// <param name="requestId">The correlating request id.</param>
    public static void SendResponse<T>(this ISender<IResponseMessage<T>> sender, T message, long requestId)
    {
        var response = new ResponseMessage<T>(message, requestId);
        sender.Send(response);
    }

    /// <summary>
    /// Sends an already wrapped response without modification to a typed response sender.
    /// </summary>
    /// <typeparam name="T">Type of the response payload.</typeparam>
    /// <param name="sender">The typed sender to emit the response.</param>
    /// <param name="response">The wrapped response.</param>
    public static void SendResponse<T>(this ISender<IResponseMessage<T>> sender, IResponseMessage<T> response)
    {
        sender.Send(response);
    }

    /// <summary>
    /// Sends a response message asynchronously, constructed from payload and request id, to a typed response sender.
    /// </summary>
    /// <typeparam name="T">Type of the response payload.</typeparam>
    /// <param name="sender">The typed sender to emit the response.</param>
    /// <param name="message">Response payload.</param>
    /// <param name="requestId">The correlating request id.</param>
    /// <returns>A task that completes when the message was sent asynchronously.</returns>
    public static Task SendResponseAsync<T>(this ISender<IResponseMessage<T>> sender, T message, long requestId)
    {
        var response = new ResponseMessage<T>(message, requestId);
        return sender.SendAsync(response);
    }

    /// <summary>
    /// Sends an already wrapped response asynchronously without modification to a typed response sender.
    /// </summary>
    /// <typeparam name="T">Type of the response payload.</typeparam>
    /// <param name="sender">The typed sender to emit the response.</param>
    /// <param name="response">The wrapped response.</param>
    /// <returns>A task that completes when the message was sent asynchronously.</returns>
    public static Task SendResponseAsync<T>(this ISender<IResponseMessage<T>> sender, IResponseMessage<T> response)
    {
        return sender.SendAsync(response);
    }
}
