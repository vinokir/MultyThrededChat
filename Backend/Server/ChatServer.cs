using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Backend.Helpers;

namespace Backend.Server
{
    public class ChatServer
    {
        #region Fields
        private TcpListener tcpServer;
        private StreamWriter _writter;
        private Thread mainThread;
        private readonly int portNumber;
        private bool isRunning;
        private readonly NetworkInterface networkInterface;
        private readonly string serverName;
        private readonly ConcurrentDictionary<string, ConnectedClient> clients = new ConcurrentDictionary<string, ConnectedClient>();
        private readonly ConcurrentQueue<Data> logs = new ConcurrentQueue<Data>();
        public event EventHandler ClientConnected;
        public event EventHandler ClientDisconnected;
        public event ErrorEventHandler JournalAdd;
        #endregion

        #region Constructor

        public ChatServer(int portNumber, object networkInterface, string serverName)
        {
            this.serverName = serverName;
            this.portNumber = portNumber;
            this.networkInterface = networkInterface as NetworkInterface;

        }

        #endregion

        #region Server Start/Stop

        public void StartServer()
        {
            mainThread = new Thread(StartListen);
            mainThread.Start();
            var logThread = new Thread(Logging);
            logThread.Start();
            JournalAdd("Сервер запущен", null);
        }

        public void StartListen()
        {
            var ip = (networkInterface != null)
                ? GetInterfaceIpAddress()
                : IPAddress.Any;

            tcpServer = new TcpListener(ip, portNumber);
            tcpServer.Start();

            isRunning = true;

            while (isRunning)
            {
                if (!tcpServer.Pending())
                {
                    Thread.Sleep(500);
                    continue;
                }
                // New client is connected, call event to handle it
                var clientThread = new Thread(NewClient);
                var tcpClient = tcpServer.AcceptTcpClient();

                tcpClient.ReceiveTimeout = 120000;
                clientThread.Start(tcpClient.Client);
            }
        }

        private IPAddress GetInterfaceIpAddress()
        {
            var ipAddresses = networkInterface.GetIPProperties().UnicastAddresses;
            return (from ip in ipAddresses where ip.Address.AddressFamily == AddressFamily.InterNetwork select ip.Address).FirstOrDefault();
        }

        public void StopServer()
        {
            isRunning = false;
            if (tcpServer == null)
                return;
            clients.Clear();
            tcpServer.Stop();
            _writter?.Close();
            JournalAdd("Сервер остановлен", null);
        }

        #endregion

        #region Add/Remove Clients
        public void NewClient(object obj)
        {
            ClientAdded(this, new CustomEventArgs((Socket)obj));
        }
        public void ClientAdded(object sender, EventArgs e)
        {
            var socket = ((CustomEventArgs)e).ClientSocket;
            var bytes = new byte[1024];
            var bytesRead = socket.Receive(bytes);

            var newUserName = Encoding.Unicode.GetString(bytes, 0, bytesRead);

            if (clients.Any(client => client.Key == newUserName))
            {
                SendNameAlreadyExist(socket, newUserName);
                return;
            }

            var newClient = new ConnectedClient(newUserName, socket);
            var state = new ChatHelper.StateObject
            {
                WorkSocket = socket
            };
            if (clients.TryAdd(newClient.UserName, newClient))
            {
                OnClientConnected(socket, newUserName);

                foreach (var client in clients)
                    SendUsersList(client.Value.Connection, client.Key, newUserName, ChatHelper.CONNECTED);
                SendSuccess(state.WorkSocket);
            }

            else
                SendFail(state.WorkSocket, new Exception("add client error"));
            //
            var newBytes = new byte[ChatHelper.StateObject.BUFFER_SIZE];
            try
            {
                int byteRead = socket.Receive(newBytes);
                if (byteRead <= 0)
                    return;
                ParseRequestSync(newBytes, socket);
            }
            catch (Exception ex)
            {
                JournalAdd($"TCP соединение закрыто", new ErrorEventArgs(ex));
                DisconnectClient(socket);
            }

            //socket.BeginReceive(state.Buffer, 0, ChatHelper.StateObject.BUFFER_SIZE, 0,
            //OnReceive, state);
        }

        public void SendNameAlreadyExist(Socket clientSocket, string name)
        {
            var data = new Data { Command = Command.NameExist, To = name };
            clientSocket.Send(data.ToByte());
        }

        public void SendUsersList(Socket clientSocket, string userName, string changedUser, string state)
        {
            var data = new Data
            {
                Command = Command.Broadcast,
                To = userName,
                Message = $"{string.Join(",", clients.Select(u => u.Key).Where(name => name != userName))}|{changedUser}|{state}|{serverName}"
            };
            try
            {
                clientSocket.Send(data.ToByte());
            }
            catch (Exception ex)
            {

                JournalAdd($"Ошибка при отправке списка пользователей", new ErrorEventArgs(ex));
            }

        }

        public void OnReceive(IAsyncResult ar)
        {
            var state = ar.AsyncState as ChatHelper.StateObject;
            if (state == null)
                return;
            var handler = state.WorkSocket;
            if (!handler.Connected)
                return;
            try
            {
                var bytesRead = handler.EndReceive(ar);
                if (bytesRead <= 0)
                    return;

                ParseRequest(state, handler);
            }

            catch (Exception ex)
            {
                JournalAdd($"TCP соединение закрыто", new ErrorEventArgs(ex));
                DisconnectClient(handler);
            }
        }

        private void ParseRequest(ChatHelper.StateObject state, Socket handlerSocket)
        {
            var data = new Data(state.Buffer);
            if (data.Command == Command.Disconnect)
            {
                DisconnectClient(state.WorkSocket);
                return;
            }
            var clientStr = clients.FirstOrDefault(cl => cl.Key == data.To).Value;
            if (clientStr?.Connection == null)
                return;

            try
            {
                clientStr.Connection.Send(data.ToByte());
                logs.Enqueue(data);
                SendSuccess(state.WorkSocket);
            }
            catch (Exception ex)
            {
                SendFail(state.WorkSocket, ex);
            }
            handlerSocket.BeginReceive(state.Buffer, 0, ChatHelper.StateObject.BUFFER_SIZE, 0,
              OnReceive, state);
        }

        private void ParseRequestSync(byte[] bytes, Socket socket)
        {
            var data = new Data(bytes);
            if (data.Command == Command.Disconnect)
            {
                DisconnectClient(socket);
                return;
            }
            var clientStr = clients.FirstOrDefault(cl => cl.Key == data.To).Value;
            if (clientStr?.Connection == null)
                return;

            try
            {
                clientStr.Connection.Send(data.ToByte());
                logs.Enqueue(data);
                SendSuccess(socket);
            }
            catch (Exception ex)
            {
                SendFail(socket, ex);
            }
            //handlerSocket.BeginReceive(state.Buffer, 0, ChatHelper.StateObject.BUFFER_SIZE, 0,
            //  OnReceive, state);
            var newBytes = new byte[ChatHelper.StateObject.BUFFER_SIZE];
            try
            {
                int bytesRead = socket.Receive(newBytes);
                if (bytesRead <= 0)
                    return;
                ParseRequestSync(newBytes, socket);
            }
            catch (Exception ex)
            {
                JournalAdd($"TCP соединение закрыто", new ErrorEventArgs(ex));
                DisconnectClient(socket);
            }

        }


        private void SendSuccess(Socket workSocket)
        {
            var clientStr = clients.FirstOrDefault(k => k.Value.Connection == workSocket).Value;
            if (clientStr?.Connection == null)
                return;

            var data = new Data
            {
                Command = Command.Success,
                To = clientStr.UserName,
                Message = $"Success"
            };
            clientStr?.Connection.Send(data.ToByte());
        }

        private void SendFail(Socket workSocket, Exception ex)
        {
            var clientStr = clients.FirstOrDefault(k => k.Value.Connection == workSocket).Value;
            if (clientStr?.Connection == null)
                return;

            var data = new Data
            {
                Command = Command.Fail,
                To = clientStr.UserName,
                Message = ex.Message
            };
            clientStr?.Connection.Send(data.ToByte());
        }

        public void DisconnectClient(Socket clientSocket)
        {
            try
            {
                var clientStr = clients.FirstOrDefault(k => k.Value.Connection == clientSocket).Value;
                if (clientStr?.Connection == null)
                    return;
                clientStr.IsConnected = false;
                OnClientDisconnected(clientSocket, clientStr.UserName);

                clientSocket.Close();
                ConnectedClient removeClient;
                if (!clients.TryRemove(clientStr.UserName, out removeClient))
                {
                    Thread.Sleep(50);
                    clients.TryRemove(clientStr.UserName, out removeClient);
                }

                foreach (var client in clients)
                    SendUsersList(client.Value.Connection, client.Key, clientStr.UserName, ChatHelper.DISCONNECTED);

                SendSuccess(clientSocket);
            }
            catch (Exception ex)
            {
                SendFail(clientSocket, ex);
            }
        }

        #endregion

        public void Logging()
        {
            string pathDir = $@"{Directory.GetCurrentDirectory()}\Log";
            string pathFile = $@"{pathDir}\log.txt";
            DirectoryInfo dirInfo = new DirectoryInfo(pathDir);
            if (!dirInfo.Exists)
            {
                dirInfo.Create();
            }
            if (!File.Exists(pathFile))
            {
                var myFile = File.Create(pathFile);
                myFile.Close();
            }
            try
            {
                _writter = new StreamWriter(pathFile, true);

                while (isRunning)
                {
                    if (logs.Count == 0)
                    {
                        Thread.Sleep(500);
                        continue;
                    }
                    Data q;
                    if (logs.TryDequeue(out q))
                    {
                        WriteLog(q, _writter);
                    }
                }
            }
            catch (Exception ex)
            {
                JournalAdd("Ошибка логгирования", new ErrorEventArgs(ex));
                _writter?.Close();
            }
            if (!isRunning)
            {
                _writter.Close();
            }
        }
        public void WriteLog(Data data, StreamWriter writer)
        {
            string mes = $"{DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss")} | {data.From} | {data.To} | {data.Message}";
            byte[] array = System.Text.Encoding.Default.GetBytes(mes);
            writer.WriteLine(mes);
        }

        #region Event Invokers

        protected virtual void OnClientConnected(Socket clientSocket, string name)
        {
            var handler = ClientConnected;
            handler?.Invoke(name, new CustomEventArgs(clientSocket));
        }

        protected virtual void OnClientDisconnected(Socket clientSocket, string name)
        {
            var handler = ClientDisconnected;
            handler?.Invoke(name, new CustomEventArgs(clientSocket));
        }

        #endregion
    }

    public class NetworkInterfaceDescription
    {
        public string Description { get; set; }
        public NetworkInterfaceDescription(string description)
        {
            Description = description;
        }
    }
}
