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

            try
            {
                //await _send(clientStream, TELL_CLIENT_TYPE, TELL_CLIENT_TYPE.Length);
                var receivedMessage = await _receiveMessage(clientStream);
                var clientType = _determineClientType(receivedMessage);

                switch (clientType)
                {
                    case ClientType.CAPTURE:
                    {
                        Trace.WriteLine("Adding capture client...");

                        CameraHub hub = _getHubDetails(receivedMessage);
                        hub.Connection = client;

                        cameraHubs[hub.Name] = hub;
                        break;
                    }
                    case ClientType.VIEW:
                    {
                        Trace.WriteLine("Adding viewing client...");
                        viewingClients.Add(client);
                        break;
                    }
                    default:
                    {
                        Trace.WriteLine("Error switching on enum type...");
                        client.Close();
                        return;
                    }
                }
                await _send(clientStream, ACKNOWLEDGE, ACKNOWLEDGE.Length);

            }
            catch (Exception e)
            {
                Trace.WriteLine($"Exception determining client type...\n\n{e}");

                client.Close();
            }
            Task.Factory.StartNew(() => _startReceiveCommunication(client));
            Task.Factory.StartNew(() => _startSendCommunication(client));
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

        /*NEED REFACTOR*/

        /*BY READING CHUNKS OF 4096, YOU CAN END UP THROWING AWAY SOME OF THE MESSAGE*/
        /*READ SIZE (4), THEN MESSAGE (SIZE) AND RETURN*/

        private async Task<string> _receiveMessage(NetworkStream stream)
        {
            List<byte> messageBuffer = new List<byte>();
            byte[] tempBuffer = new byte[MESSAGE_CHUNK_SIZE];
            int messageLength = 0;
            try
            {
                while (messageBuffer.Count < MESSAGE_PREFIX_SIZE)
                {
                    var bytes = await stream.ReadAsync(tempBuffer, 0, MESSAGE_CHUNK_SIZE);
                    if (bytes == 0)
                    {
                        continue;
                    }
                    messageBuffer.AddRange(tempBuffer.Take(bytes));
                }

                messageLength = _getMessageLength(messageBuffer);

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
                //System.Diagnostics.Trace.WriteLine(messageLength);
                messageBuffer = messageBuffer.Take(messageLength).ToList();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Exception receiving message:\n\n{ex.Message}");
                throw new ReceiveMessageException();
            }
            var wholeMessage = messageBuffer.Take(messageLength).ToList();
            return Encoding.Default.GetString(wholeMessage.ToArray());
        }

        private async Task _send(NetworkStream stream, string data, int messageSize)
        {
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
