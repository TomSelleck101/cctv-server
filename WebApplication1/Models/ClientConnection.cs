using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Web;
using WebApplication1.Enums;

namespace WebApplication1.Models
{
    public abstract class ClientConnection
    {
        public TcpClient Connection { get; set; }
        public bool SendMessages { get; set; }
        public bool ListenForMessages { get; set; }
        public BlockingCollection<string> ReceiveQueue { get; set; }
        public BlockingCollection<string> SendQueue { get; set; }
        public ClientType ClientType { get; set; }

        internal void Disconnect()
        {
            SendMessages = false;
            SendQueue.Add("END");
            ListenForMessages = false;
            Connection.Close();
        }
    }
}