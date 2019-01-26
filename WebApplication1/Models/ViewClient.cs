using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebApplication1.Models
{
    public class ViewClient : ClientConnection
    {
        public ViewClient()
        {
            this.ReceiveQueue = new BlockingCollection<string>();
            this.SendQueue = new BlockingCollection<string>();
        }
    }
}