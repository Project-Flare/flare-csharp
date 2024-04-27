using System.Collections.Concurrent;
using System.Net.Security;
using System.Net.WebSockets;
using System.Text;
using Flare.V1;
using Google.Protobuf;

namespace flare_csharp
{
    // Web Socket listener that receives messages from the server
    public class MessageReceivingService
    {
        public enum State
        {
            Connected, Disconnected, Running
        }

        public int ReceivedMessageCount { get => messageQueue.Count; }
        public ClientCredentials Credentials { get; set; }                          // some user credentials are needed, TODO - maybe you should add this class instance to the client manager class
        
        public State ServiceState { get; private set; }                             // Running - the thread runs 
        private ClientWebSocket ws;                                                 // listening web socket
        private const string serverUrl = "wss://ws.f2.project-flare.net/";          // just the url of the web socket server
        private CancellationTokenSource cts = new CancellationTokenSource();        // default is 30s, after that, web socket's asynchronous operations will be cancelled
		private Thread wsListenerThread;                                            // handles the connection, manages receiving messages from the server
        private ConcurrentQueue<InboundUserMessage> messageQueue;                   // thread-safe message queue, enqueues and dequeues messages that are received from the server



        public MessageReceivingService(ClientCredentials cc)
        {
            Credentials = cc;
            ServiceState = State.Disconnected;
            ws = new ClientWebSocket();
            cts.CancelAfter(TimeSpan.FromSeconds(30));
            wsListenerThread = new Thread(RunService);
            wsListenerThread.Name = "Web Socket Listener";
            messageQueue = new ConcurrentQueue<InboundUserMessage>();
        }

		public async Task StartService()
		{
            try
            {
			    await ws.ConnectAsync(new Uri(serverUrl), cts.Token);
                await MakeSubscribeRequest();
                wsListenerThread.Start();
                ServiceState = State.Running;
            }
            catch (Exception)
            {
                ServiceState = State.Disconnected; // TODO - handle the connection
            }
		}

		private void RunService()
        {
			while (true)
            {
                try
                {
                    var receiveMessageTask = ReceiveMessageAsync(5);
                }
                catch (Exception)
                { 
                    // todo - handle connections etc.
                }
            }
        }

        private async Task ReceiveMessageAsync(int timeoutSeconds)
        {
			const int KILOBYTE = 1024;
			byte[] buffer = new byte[KILOBYTE];
			int offset = 0;
			int free = buffer.Length;
            var ct = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

			while (true)
			{
				WebSocketReceiveResult response = await ws.ReceiveAsync(new ArraySegment<byte>(buffer, offset, free), ct.Token);
                await Ping();

				if (response.EndOfMessage)
				{
					offset += response.Count;
					break;
				}

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

            messageQueue.Enqueue(InboundUserMessage.Parser.ParseFrom(buffer));
		}

        public async Task MakeSubscribeRequest()
        {
            try
            {
                var request = new SubscribeRequest
                {
                    Token = Credentials.AuthToken
                };

                await ws.SendAsync(request.ToByteArray(), WebSocketMessageType.Binary, true, cts.Token);

                cts.TryReset();
            }
            catch
            {
                // todo
            }
        }

        public async Task Ping()
        {
            await ws.SendAsync(Encoding.ASCII.GetBytes("Ping me bro"), WebSocketMessageType.Binary, true, CancellationToken.None);
        }

        public int MessageReceiveCount() => messageQueue.Count;


    }
}
