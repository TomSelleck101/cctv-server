using System;
using WebSocketSharp;

namespace TestWebSocketClient
{
    public class Program
    {
        public static void Main(string[] args)
        {
            using (var ws = new WebSocket("ws://127.0.0.1/Laputa"))
            {
                ws.OnMessage += (sender, e) =>
                    Console.WriteLine("Laputa says: " + e.Data);

                ws.Connect();
                ws.Send("BALUSA");
                Console.ReadKey(true);
            }
        }
    }
}