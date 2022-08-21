using FeatureLoom.Collections;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace FeatureLoom.MessageFlow
{
    public class MessageLog<T> : IMessageSink
    {
        CircularLogBuffer<T> buffer;
        int storageSize;

        public void Post<M>(in M message)
        {
            throw new NotImplementedException();
        }

        public void Post<M>(M message)
        {
            throw new NotImplementedException();
        }

        public Task PostAsync<M>(M message)
        {
            throw new NotImplementedException();
        }
    }
}
