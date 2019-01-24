using System;
using System.Runtime.Serialization;

namespace WebApplication1
{
    [Serializable]
    internal class DetermineClientTypeException : Exception
    {
        public DetermineClientTypeException()
        {
        }

        public DetermineClientTypeException(string message) : base(message)
        {
        }

        public DetermineClientTypeException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected DetermineClientTypeException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}