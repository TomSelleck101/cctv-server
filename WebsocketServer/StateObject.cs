using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebsocketServer
{
    // State object for reading client data asynchronously  
    public class StateObject
    {
        // Size of receive buffer.  
        public const int messageChunkSize = 4096;
        public const int messagePrefixSize = 4;

        // Message length
        public byte[] messageBuffer = new byte[messageChunkSize];

        public string receivedMessage;

        // Received data string.  
        public StringBuilder sb = new StringBuilder();
        public byte[] content;
    }
}
