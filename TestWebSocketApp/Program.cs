using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace TestWebSocketApp
{
    public class Laputa : WebSocketBehavior
    {
        protected override void OnMessage(MessageEventArgs e)
        {
            var msg = e.Data.ToUpper();

            Trace.WriteLine("Received Message");

            Send(msg);
        }

        protected override void OnOpen()
        {
            var t = this.Context.Host;
            var x = this.ID;
            foreach(var session in this.Sessions.Sessions)
            {
                var s = session;
            }
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            var wssv = new WebSocketServer("ws://192.168.0.101");
            wssv.AddWebSocketService<Laputa>("/Laputa");
            wssv.Start();
            Console.ReadKey(true);
            wssv.Stop();
        }
    }
}