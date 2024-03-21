using Flare;
using Google.Protobuf;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace flare_csharp
{
    /// <summary>
    /// The connection to the server process failed.
    /// </summary>
    public class ConnectionFailedException : Exception
    {
        public ConnectionFailedException() : base() { }
        public ConnectionFailedException(string message) : base("Failed to connect the server") { }
        public ConnectionFailedException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Sending the ClientMessage to server process failed.
    /// </summary>
    public class SendClientMessageFailedException : Exception
    {
        public SendClientMessageFailedException() : base() { }
        public SendClientMessageFailedException(string message) : base(message) { }
        public SendClientMessageFailedException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Receiving ServerMessage from the server process failed.
    /// </summary>
    public class ReceiveServerMessageFailedException : Exception
    {
        public ReceiveServerMessageFailedException() : base() { }
        public ReceiveServerMessageFailedException(string message) : base(message) { }
        public ReceiveServerMessageFailedException(string message, Exception innerException) : base(message, innerException) { }
    }

    public static class MessageService
    {
        // Enqueues messages that need to be sent
        private static Queue _messageQueue = new Queue();

        // Enqueues server responses of the sent client messages
        private static Queue _responseQueue = new Queue();

        // Web socket where client "talks" to server directly
        private static ClientWebSocket _webSocket = new ClientWebSocket();

        // Determines how long the asynchronous (sending, receiving messages) process should take
        private static CancellationTokenSource _ctSource = new CancellationTokenSource();

        // Is the client connected to server via web socket
        public static bool Connected { get; private set; } = false;

        // Are all enqueued messages are sent to server
        public static bool AllMessagesSent { get => Connected && _messageQueue.Count == 0; }

        // Hardcoded server url to connect via TCP protocol
        public static string ServerUrl { get; private set; } = "wss://ws.project-flare.net/";
        public static int QueuedMessageCount { get => _messageQueue.Count; }

        /// <summary>
        /// Specify longest time for asynchronous processes of the service should take in seconds
        /// </summary>
        /// <param name="seconds">Longest allowed time for asynchronous processes</param>
        public static void CancelOperationAfter(int seconds)
        {

            // If some dipshit (like me) will somehow specify negative or zero time
            if (seconds <= 0)
            {
                const int ONE_MINUTE = 60;
                seconds = ONE_MINUTE;
            }

            // Set the specified time limit
            _ctSource.CancelAfter(TimeSpan.FromSeconds(seconds));
        }

        /// <summary>
        /// Connects to server via web socket using TCP protocol
        /// </summary>
        /// <exception cref="ConnectionFailedException">Connection to the server failed</exception>
        public static async Task Connect()
        {
            try
            {
                SetRemoteCertificate();
                await _webSocket.ConnectAsync(new Uri(ServerUrl), _ctSource.Token);
            }
            catch (Exception ex)
            {
                throw new ConnectionFailedException("Failed to connect the server: " + ServerUrl, ex);
            }

            if (!ReceiveResponseAsync().Result.ServerMessageTypeCase.Equals(ServerMessage.ServerMessageTypeOneofCase.Hello))
            {
                throw new ConnectionFailedException("The server: " + ServerUrl + " did not greet the client");
            }
            else
            {
                Connected = true;
            }
        }

        /// <summary>
        /// Enqueues the given message to be later sent
        /// </summary>
        /// <param name="clientMessage">Client message later to be sent</param>
        public static void AddMessage(ClientMessage clientMessage)
        {
            _messageQueue.Enqueue(clientMessage);
        }

        public static async Task SendMessageAsync(int messageCount)
        {
            // There are no messages to be sent
            if (_messageQueue.Count == 0)
                return;

            // Not connected to the server
            if (!Connected)
                return;

            while (true)
            {
                // All messages are sent
                if (_messageQueue.Count == 0)
                    return;

                // All requested messages are sent
                if (messageCount == 0)
                    return;

                // Get client message to send
                ClientMessage? message = _messageQueue.Dequeue() as ClientMessage;

                if (message is null)
                    return;

                try
                {
                    // Send message
                    await SendMessageAsync(message);
                }
                catch (Exception ex)
                {
                    throw new SendClientMessageFailedException("Failed to send " + message.ClientMessageTypeCase + " client message",
                        ex);
                }

                try
                {
                    // Get server response
                    ServerMessage response = await ReceiveResponseAsync();

                    // Add to received message queue
                    _responseQueue.Enqueue(response);
                }
                catch (Exception ex)
                {
                    throw new ReceiveServerMessageFailedException(
                        "Failed to receive response from " + message.ClientMessageTypeCase + " client message server response",
                        ex);
                }

                messageCount--;
            }
        }

        public static ServerMessage? GetServerResponse()
        {
            return _responseQueue.Dequeue() as ServerMessage;
        }

        private static void SetRemoteCertificate()
        {
            _webSocket.Options.RemoteCertificateValidationCallback =
            (
                object sender,
                X509Certificate? certificate,
                X509Chain? chain,
                SslPolicyErrors sslPolicyErrors
            ) =>
            {
                if ((sslPolicyErrors & ~SslPolicyErrors.RemoteCertificateChainErrors) != 0)
                    return false;

                if (certificate is null)
                    return false;

                const string pub_key_pin =
                    "04447327fe093b0450bbae0346cf85" +
                    "fb60491ea04adc1c7d10a49c3397bf" +
                    "1a2539e7eea6a6b4109a5c62b2df55" +
                    "003c998b4afb1f103b883f1f649b3b" +
                    "6530ce8dd7";

                return certificate.GetPublicKeyString().ToLower().Equals(pub_key_pin);
            };
        }

        private static async Task SendMessageAsync(ClientMessage message)
        {
            await _webSocket.SendAsync(message.ToByteArray(), WebSocketMessageType.Binary, true, _ctSource.Token);
            _ctSource.TryReset();
        }

        private static async Task<ServerMessage> ReceiveResponseAsync()
        {
            const int KILOBYTE = 1024;
            byte[] buffer = new byte[KILOBYTE];
            int offset = 0;
            int free = buffer.Length;

            while (true)
            {
                WebSocketReceiveResult response = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer, offset, free), _ctSource.Token);

                if (response.EndOfMessage)
                {
                    offset += response.Count;
                    break;
                }

                _ctSource.TryReset();

                // Enlarge if the received message is bigger than the buffer
                if (free.Equals(response.Count))
                {
                    int newSize = buffer.Length * 2;

                    if (newSize > 2_000_000)
                        break;

                    byte[] newBuffer = new byte[newSize];
                    Array.Copy(buffer, 0, newBuffer, 0, buffer.Length);

                    free = newBuffer.Length - buffer.Length;
                    offset = buffer.Length;
                    buffer = newBuffer;
                }

            }

            return ServerMessage.Parser.ParseFrom(buffer, 0, offset);
        }
    }
}
