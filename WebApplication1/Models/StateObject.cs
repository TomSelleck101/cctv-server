
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebApplication1.Models
{
    public class StateObject
    {
        // Client  socket.  
        // Size of receive buffer.  
        public const int messageChunkSize = 4096;
        public const int messagePrefixSize = 4;

        // Message length
        public byte[] messageBuffer = new byte[messageChunkSize];

        public string receivedMessage;

        public byte[] content;
    }
}