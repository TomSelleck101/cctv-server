using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebApplication1.Enums;
using WebApplication1.Models;

namespace WebApplication1
{
    public sealed class ConnectionService
    {
        private const int MESSAGE_CHUNK_SIZE = 4096;
        private const int MESSAGE_PREFIX_SIZE = 4;
        private TcpListener server = null;
        private Int32 port = 80;
        private IPAddress localAddr = IPAddress.Parse("127.0.0.1");

        private const string ACKNOWLEDGE = "1";

        private static readonly Lazy<ConnectionService> lazy = new Lazy<ConnectionService>(() => new ConnectionService());

        public static ConnectionService Instance { get { return lazy.Value; } }

        private ConnectionService()
        {
        }

        public ConcurrentDictionary<string, CameraHub> cameraHubs = new ConcurrentDictionary<string, CameraHub>();
        public ConcurrentBag<TcpClient> viewingClients = new ConcurrentBag<TcpClient>();

        private bool _listen = false;

        public async Task StartListening()
        {
            TcpListener server = null;
            _listen = true;
            try
            {
                server = new TcpListener(localAddr, port);
                server.Start();

                // Enter the listening loop.
                while (_listen)
                {
                    System.Diagnostics.Trace.WriteLine("Waiting for a connection...");

                    TcpClient client = await server.AcceptTcpClientAsync();

                    Trace.WriteLine("Connected to a client...");

                    // Should this be another service?
                    // e.g ConnectionService - Listen, Send, Receive only
                    // CaptureConnectionHandler
                    // ViewerConnectionHandler
                    Task.Factory.StartNew(() => _startClientCommunication(client));
                }
            }
            catch (SocketException e)
            {
                System.Diagnostics.Trace.WriteLine(e);
            }
            finally
            {
                // Stop listening for new clients.
                server.Stop();
            }
        }

        private async Task _startClientCommunication(TcpClient client)
        {
            NetworkStream clientStream = client.GetStream();
            Trace.WriteLine("Waiting to hear client type...");
            ClientConnection newConnection;
            try
            {
                var receivedMessage = await _getInitialMessage(clientStream); //GET_INIT_MSG
                var clientType = _determineClientType(receivedMessage);

                switch (clientType)
                {
                    case ClientType.CAPTURE:
                    {
                        Trace.WriteLine("Adding capture client...");

                        CameraHub hub = _getHubDetails(receivedMessage);
                        hub.Connection = client;
                        cameraHubs[hub.Name] = hub;

                        Task.Factory.StartNew(() => _startReceiveCommunication(hub));
                        Task.Factory.StartNew(() => _startSendCommunication(hub));
                        break;
                    }
                    case ClientType.VIEW:
                    {
                        Trace.WriteLine("Adding viewing client...");
                        viewingClients.Add(client);
                        var viewClient = new ViewClient() { Connection = client, ClientType = clientType };

                        Task.Factory.StartNew(() => _startReceiveCommunication(viewClient));
                        Task.Factory.StartNew(() => _startSendCommunication(viewClient));
                        break;
                    }
                    default:
                    {
                        Trace.WriteLine("Error switching on enum type...");
                        client.Close();
                        return;
                    }
                }
                Trace.WriteLine("Client comms started - sending ack");
                await _send(clientStream, ACKNOWLEDGE);

            }
            catch (Exception e)
            {
                Trace.WriteLine($"Exception determining client type...\n\n{e.StackTrace}");
                
                client.Close();
            }
        }

        private async Task<string> _getInitialMessage(NetworkStream stream)
        {
            List<byte> messageBuffer = new List<byte>();
            byte[] tempBuffer = new byte[MESSAGE_CHUNK_SIZE];
            string message;
            try
            {
                while (messageBuffer.Count < MESSAGE_PREFIX_SIZE)
                {
                    int bytesRead = await stream.ReadAsync(tempBuffer, 0, StateObject.messageChunkSize);
                    messageBuffer.AddRange(tempBuffer.Take(bytesRead));
                }

                int messageLength = _getMessageLength(messageBuffer);

                messageBuffer = messageBuffer.Skip(MESSAGE_PREFIX_SIZE).ToList();

                while (messageBuffer.Count < messageLength)
                {
                    int bytesRead = await stream.ReadAsync(tempBuffer, 0, StateObject.messageChunkSize);
                    messageBuffer.AddRange(tempBuffer.Take(bytesRead));
                }

                var wholeMessage = messageBuffer.Take(messageLength).ToList();

                messageBuffer = messageBuffer.Skip(messageLength).ToList();
                message = Encoding.Default.GetString(wholeMessage.ToArray());
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Exception receiving message:\n\n{ex.StackTrace}");
                throw new ReceiveMessageException();
            }

            return message;
        }

        private async Task _startSendCommunication(ClientConnection connection)
        {
            connection.SendMessages = true;
            while (connection.SendMessages)
            {
                string message = connection.SendQueue.Take();
                if (message == "END") break;
                try
                {
                    Trace.WriteLine($"Sending: {message}...");
                    await _send(connection.Connection.GetStream(), message);
                }
                catch (Exception e)
                {
                    Trace.WriteLine("Exception in send thread, disconnecting...");
                    connection.Disconnect();
                }
            }
            Trace.WriteLine("Ending send thread...");
        }

        private async Task _startReceiveCommunication(ClientConnection connection)
        {
            connection.ListenForMessages = true;

            // 1. Start receive thread
            Task receiveMessageThread = Task.Factory.StartNew(() => _receiveMessage(connection));

            // 2. TryTake messages

            while (connection.ListenForMessages)
            {
                string message = connection.ReceiveQueue.Take();

                //Trace.WriteLine("Received: " + message.Length);
                switch (connection.ClientType)
                {
                    case ClientType.VIEW:
                        var command = message.Split(':');
                        CameraHub hub;
                        if (!cameraHubs.TryGetValue(command[1], out hub))
                        {
                            connection.SendQueue.TryAdd("hub_not_found", 0);
                            continue;
                        }
                        if (command[0].Contains("send"))
                        {
                            //TODO Replace with cam id
                            hub.SendQueue.TryAdd("SEND_FOOTAGE", 0);
                            hub.ViewClients.Add(connection);
                            connection.SendQueue.TryAdd("requesting_feed", 0);
                            continue;
                        }
                        else
                        {
                            //TODO Replace with cam id
                            hub.SendQueue.TryAdd("STOP_SEND_FOOTAGE", 0);
                            hub.ViewClients.Remove(connection);
                            connection.SendQueue.TryAdd("requesting_stop", 0);
                        }
                        break;

                    case ClientType.CAPTURE:
                        // Assume any message from capture is an image
                        if (((CameraHub)connection).ViewClients.Count < 1)
                        {
                            ((CameraHub)connection).SendQueue.TryAdd("STOP_SEND_FOOTAGE", 0);
                            break;
                        }

                        foreach(var viewClient in ((CameraHub)connection).ViewClients)
                        {
                            try
                            {
                                await _send(viewClient.Connection.GetStream(), message);
                            } catch (SendMessageException e)
                            {
                                ((CameraHub)connection).ViewClients.Remove(viewClient);
                                viewClient.Disconnect();
                            }
                        }
                        break;
                }
            }
        }

        private CameraHub _getHubDetails(string receivedMessage)
        {
            var sections = receivedMessage.Split('&');
            var hubnameSection = sections.SingleOrDefault(s => s.Contains("hub_name"));
            var hubname = hubnameSection.Split(':')[1];

            var cameraIdSections = sections.Where(s => s.Contains("camera_id"));

            List<string> cameraIds = new List<string>();
            foreach(var cameraIdSection in cameraIdSections)
            {
                cameraIds.Add(cameraIdSection.Split(':')[1]);
            }

            return new CameraHub() { Name = hubname, CameraIds = cameraIds };
        }

        private ClientType _determineClientType(string receivedMessage)
        {
            ClientType clientType;

            var sections = receivedMessage.Split('&');
            var clientTypeSection = sections.SingleOrDefault(s => s.Contains("client_type"));
            var clientTypeValue = clientTypeSection.Split(':')[1];

            if (!Enum.TryParse(clientTypeValue, true, out clientType))
            {
                Trace.WriteLine("Received message didn't represent a client type...");
            }

            return clientType;
        }

        private async Task _receiveMessage(ClientConnection connection)
        {
            List<byte> messageBuffer = new List<byte>();
            byte[] tempBuffer = new byte[MESSAGE_CHUNK_SIZE];
            var stream = connection.Connection.GetStream();
            connection.ListenForMessages = true;
            try
            {
                Trace.WriteLine("Listening for messages...");
                while (connection.ListenForMessages)
                {
                    while (messageBuffer.Count < MESSAGE_PREFIX_SIZE)
                    {
                        int bytesRead = await stream.ReadAsync(tempBuffer, 0, StateObject.messageChunkSize);
                        messageBuffer.AddRange(tempBuffer.Take(bytesRead));
                    }

                    int messageLength = _getMessageLength(messageBuffer);
                    Trace.WriteLine("Received: " + messageLength);
                    messageBuffer = messageBuffer.Skip(MESSAGE_PREFIX_SIZE).ToList();

                    while (messageBuffer.Count < messageLength)
                    {
                        int bytesRead = await stream.ReadAsync(tempBuffer, 0, StateObject.messageChunkSize);
                        messageBuffer.AddRange(tempBuffer.Take(bytesRead));
                    }

                    var wholeMessage = messageBuffer.Take(messageLength).ToList();
                    var messageString = Encoding.Default.GetString(wholeMessage.ToArray());

                    connection.ReceiveQueue.TryAdd(messageString, 0);

                    messageBuffer = messageBuffer.Skip(messageLength).ToList();
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Exception receiving message:\n\n{ex.StackTrace}");
                connection.Disconnect();
            }
            Trace.WriteLine("Ending receive thread...");
        }

        private async Task _send(NetworkStream stream, string data)
        {
            int messageSize = data.Length;
            byte[] contentData = Encoding.ASCII.GetBytes(data);
            byte[] messageSizeArray = BitConverter.GetBytes(messageSize);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(messageSizeArray);
            }

            byte[] message = new byte[StateObject.messagePrefixSize + messageSize];

            Array.Copy(messageSizeArray, message, StateObject.messagePrefixSize);
            Array.Copy(contentData, 0, message, StateObject.messagePrefixSize, messageSize);

            // Begin sending the data to the remote device.  
            try
            {
                await stream.WriteAsync(message, 0, StateObject.messagePrefixSize + messageSize);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception sending message: \n\n{e}");
                throw new SendMessageException();
            }
        }

        private int _getMessageLength(List<byte> message)
        {
            byte[] bytes = { message[3], message[2], message[1], message[0] };

            return BitConverter.ToInt32(bytes, 0);
        }
    }
}
