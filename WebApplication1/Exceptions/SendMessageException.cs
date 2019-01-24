using System;
using System.Runtime.Serialization;

namespace WebApplication1
{
    [Serializable]
    internal class SendMessageException : Exception
    {
        public SendMessageException()
        {
        }

        public SendMessageException(string message) : base(message)
        {
        }

        public SendMessageException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected SendMessageException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}