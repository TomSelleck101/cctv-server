using System;
using System.Runtime.Serialization;

namespace WebApplication1
{
    [Serializable]
    internal class ReceiveMessageException : Exception
    {
        public ReceiveMessageException()
        {
        }

        public ReceiveMessageException(string message) : base(message)
        {
        }

        public ReceiveMessageException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected ReceiveMessageException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}