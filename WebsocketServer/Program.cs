using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace WebsocketServer
{
    class Program
    {
        private const int MESSAGE_CHUNK_SIZE = 4096;
        private const int MESSAGE_PREFIX_SIZE = 4;

        public static void Main()
        {
            TcpListener server = null;
            try
            {
                Int32 port = 80;
                IPAddress localAddr = IPAddress.Parse("127.0.0.1");

                // TcpListener server = new TcpListener(port);
                server = new TcpListener(localAddr, port);
                server.Start();

                // Enter the listening loop.
                while (true)
                {
                    System.Diagnostics.Trace.WriteLine("Waiting for a connection... ");

                    // Perform a blocking call to accept requests.
                    // You could also user server.AcceptSocket() here.
                    TcpClient client = server.AcceptTcpClient();

                    System.Diagnostics.Trace.WriteLine("Connected");

                    // Get a stream object for reading and writing
                    NetworkStream stream = client.GetStream();


                    List<byte> messageBuffer = new List<byte>();
                    byte[] tempBuffer = new byte[MESSAGE_CHUNK_SIZE];

                    try
                    {

                        while (true)
                        {
                            while (messageBuffer.Count < MESSAGE_PREFIX_SIZE)
                            {
                                var bytes = stream.Read(tempBuffer, 0, MESSAGE_CHUNK_SIZE);
                                if (bytes == 0)
                                {
                                    continue;
                                }
                                messageBuffer.AddRange(tempBuffer.Take(bytes));
                            }

                            int messageLength = _getMessageLength(messageBuffer);

                            messageBuffer = messageBuffer.Skip(MESSAGE_PREFIX_SIZE).ToList();

                            while (messageBuffer.Count < messageLength)
                            {
                                var bytes = stream.Read(tempBuffer, 0, MESSAGE_CHUNK_SIZE);
                                if (bytes == 0)
                                {
                                    continue;
                                }
                                messageBuffer.AddRange(tempBuffer.Take(bytes));
                            }

                            var wholeMessage = messageBuffer.Take(messageLength).ToList();
                            var messageString = Encoding.Default.GetString(wholeMessage.ToArray());

                            System.Diagnostics.Trace.WriteLine(messageLength);

                            messageBuffer = messageBuffer.Skip(messageLength).ToList();
                        }
                    }
                    catch (SocketException ex)
                    {
                        System.Diagnostics.Trace.WriteLine(ex.Message);
                    }

                    // Shutdown and end connection
                    client.Close();
                }
            }
            catch (SocketException e)
            {
                System.Diagnostics.Trace.WriteLine($"{e}");
            }
            finally
            {
                // Stop listening for new clients.
                server.Stop();
            }
        }

        private static int _getMessageLength(List<byte> message)
        {
            byte[] bytes = { message[3], message[2], message[1], message[0] };

            return BitConverter.ToInt32(bytes, 0);
        }
    }
}
