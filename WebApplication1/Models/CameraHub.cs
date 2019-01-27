using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Web;

namespace WebApplication1.Models
{
    public class CameraHub : ClientConnection
    {
        public string Name { get; set; }
        public IEnumerable<string> CameraIds { get; set; }
        public List<ClientConnection> ViewClients { get; set; }

        public CameraHub()
        {
            this.ViewClients = new List<ClientConnection>();
            this.ReceiveQueue = new BlockingCollection<string>();
            this.SendQueue = new BlockingCollection<string>();
        }
    }
}