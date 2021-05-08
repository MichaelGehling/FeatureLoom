using FeatureLoom.Helpers;
using FeatureLoom.Synchronization;
using System;
using System.Collections;
using System.Threading.Tasks;

namespace FeatureLoom.DataFlows
{
    // TODO: Refactor-> IAggregationData must do the whole work, no func necessary
    public class Aggregator<T, A> : IDataFlowSink<T>, IDataFlowConnection, IAlternativeDataFlow where A : IAggregationData, new()
    {
        private SourceValueHelper sourceHelper = new SourceValueHelper();
        private LazyValue<SourceHelper> alternativeSender;

        private A aggregationData = new A();
        private FeatureLock dataLock = new FeatureLock();
        private readonly Func<T, A, bool> aggregate;

        /// <summary> The constructor taking the aggregation function. </summary>
        /// <param name="aggregate">
        ///     The aggregation function must take the following parameters: (T) input message and
        ///     (A) aggregation object and returns (bool) if the message was used for aggregation
        ///     (true) or ignored (false).
        /// </param>
        public Aggregator(Func<T, A, bool> aggregate)
        {
            this.aggregate = aggregate;
        }

        public void Post<M>(in M message)
        {
            bool alternative = true;
            (bool ready, object msg, bool enumerate) output = default;
            if (message is T validMessage)
            {
                using (dataLock.Lock())
                {
                    alternative = !aggregate(validMessage, aggregationData);
                    output = aggregationData.TryCreateOutputMessage();
                }
            }

            if (output.ready && output.msg != null)
            {
                if (output.enumerate && output.msg is IEnumerable outputMessages) foreach (var msg in outputMessages) sourceHelper.Forward(msg);
                else sourceHelper.Forward(in output.msg);
            }
            if (alternative) alternativeSender.ObjIfExists?.Forward(in message);
        }

        public void Post<M>(M message)
        {
            bool alternative = true;
            (bool ready, object msg, bool enumerate) output = default;
            if (message is T validMessage)
            {
                using (dataLock.Lock())
                {
                    alternative = !aggregate(validMessage, aggregationData);
                    output = aggregationData.TryCreateOutputMessage();
                }
            }

            if (output.ready && output.msg != null)
            {
                if (output.enumerate && output.msg is IEnumerable outputMessages) foreach (var msg in outputMessages) sourceHelper.Forward(msg);
                else sourceHelper.Forward(output.msg);
            }
            if (alternative) alternativeSender.ObjIfExists?.Forward(message);
        }

        public Task PostAsync<M>(M message)
        {
            bool alternative = true;
            (bool ready, object msg, bool enumerate) output = default;
            if (message is T validMessage)
            {
                using (dataLock.Lock())
                {
                    alternative = !aggregate(validMessage, aggregationData);
                    output = aggregationData.TryCreateOutputMessage();
                }
            }

            if (output.ready && output.msg != null)
            {
                if (output.enumerate && output.msg is IEnumerable outputMessages) foreach (var msg in outputMessages) sourceHelper.Forward(msg);
                else return sourceHelper.ForwardAsync(output.msg);
            }
            if (alternative) return alternativeSender.ObjIfExists?.ForwardAsync(message);
            return Task.CompletedTask;
        }

        public int CountConnectedSinks => sourceHelper.CountConnectedSinks;

        public IDataFlowSource Else => alternativeSender.Obj;

        public void DisconnectAll()
        {
            sourceHelper.DisconnectAll();
        }

        public void DisconnectFrom(IDataFlowSink sink)
        {
            sourceHelper.DisconnectFrom(sink);
        }

        public IDataFlowSink[] GetConnectedSinks()
        {
            return sourceHelper.GetConnectedSinks();
        }

        public void ConnectTo(IDataFlowSink sink, bool weakReference = false)
        {
            sourceHelper.ConnectTo(sink, weakReference);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink, bool weakReference = false)
        {
            return sourceHelper.ConnectTo(sink, weakReference);
        }
    }

    public interface IAggregationData
    {
        /// <summary>
        ///     If the aggregation data is complete, it generates the resulting output message(s)
        ///     and resets the aggregationData object if necessary.
        /// </summary>
        /// <returns>
        ///     Returns a tuple consisting of:
        ///     1. A bool indicating if the aggregation is ready to create the output message(s),
        ///     2. The output message(s) (if there are multiple resulting messages it returns an
        ///        enumerable container)
        ///     3. A bool indicating if there are multiple messages in a container
        /// </returns>
        (bool ready, object msg, bool enumerate) TryCreateOutputMessage();
    }
}