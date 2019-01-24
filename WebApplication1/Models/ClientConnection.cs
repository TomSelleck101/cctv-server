using System;
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
        public ClientType ClientType { get; set; }
    }
}