using System;
using System.Collections;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows
{
    public class Aggregator<T, A> : IDataFlowConnection, IAlternativeDataFlow where A : IAggregationData, new()
    {
        private DataFlowSourceHelper sender = new DataFlowSourceHelper();
        private DataFlowSourceHelper alternativeSender = new DataFlowSourceHelper();

        private A aggregationData = new A();
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
            if(message is T validMessage)
            {
                lock(aggregationData)
                {
                    alternative = !aggregate(validMessage, aggregationData);
                    output = aggregationData.TryCreateOutputMessage();
                }
            }

            if(output.ready && output.msg != null)
            {
                if(output.enumerate && output.msg is IEnumerable outputMessages) foreach(var msg in outputMessages) sender.Forward(msg);
                else sender.Forward(output.msg);
            }
            if(alternative) alternativeSender.Forward(message);
        }

        public Task PostAsync<M>(M message)
        {
            bool alternative = true;
            (bool ready, object msg, bool enumerate) output = default;
            if(message is T validMessage)
            {
                lock(aggregationData)
                {
                    alternative = !aggregate(validMessage, aggregationData);
                    output = aggregationData.TryCreateOutputMessage();
                }
            }

            if(output.ready && output.msg != null)
            {
                if(output.enumerate && output.msg is IEnumerable outputMessages) foreach(var msg in outputMessages) sender.Forward(msg);
                else return sender.ForwardAsync(output.msg);
            }
            if(alternative) return alternativeSender.ForwardAsync(message);
            return Task.CompletedTask;
        }

        public int CountConnectedSinks => ((IDataFlowSource)sender).CountConnectedSinks;

        public IDataFlowSource Else => alternativeSender;

        public void ConnectTo(IDataFlowSink sink)
        {
            ((IDataFlowSource)sender).ConnectTo(sink);
        }

        public IDataFlowSource ConnectTo(IDataFlowConnection sink)
        {
            return ((IDataFlowSource)sender).ConnectTo(sink);
        }

        public void DisconnectAll()
        {
            ((IDataFlowSource)sender).DisconnectAll();
        }

        public void DisconnectFrom(IDataFlowSink sink)
        {
            ((IDataFlowSource)sender).DisconnectFrom(sink);
        }

        public IDataFlowSink[] GetConnectedSinks()
        {
            return ((IDataFlowSource)sender).GetConnectedSinks();
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