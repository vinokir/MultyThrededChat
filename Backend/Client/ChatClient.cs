using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;
using Backend.Helpers;



namespace Backend.Client
{
    public class ChatClient
    {
        #region Fields

        private TcpClient server;

        private Thread tcpRecieveThread;


        #endregion

        #region Events
        public event EventHandler UserListReceived;
        public event EventHandler MessageReceived;
        public event EventHandler FailReceived;

        #endregion

        #region Properties

        #region Profile Info
        public string UserName { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        #endregion

        public string ClientAddress { get; set; }
        public string ServerAddress { get; set; }
        public string ServerName { get; set; }
        public int Port { get; set; }

        public bool IsConnected { get; set; }

        #endregion

        #region Consructor
        public ChatClient(int port, string serverAddress, string userName)
        {
            StartConnection(serverAddress, port, userName);
        }
        #endregion

        #region Methods

        private void StartConnection(string serverAddress, int port, string userName)
        {
            try
            {
                server = new TcpClient(serverAddress, port);
                IsConnected = true;


                ServerAddress = serverAddress;
                UserName = userName;
                Port = port;
            }
            catch (SocketException)
            {
                OnFailReceived("Невозможно подключиться к серверу");
                return;
            }
            var bytes = Encoding.Unicode.GetBytes(userName);
            server.Client.Send(bytes);
        }
        public bool Init()
        {
            if (!ReceiveUsersList())
            {
                return false;
            }

            tcpRecieveThread = new Thread(RecieveFromServer) { Priority = ThreadPriority.Normal };
            tcpRecieveThread.Start();

            return true;
        }

        private void RecieveFromServer()
        {
            var state = new ChatHelper.StateObject
            {
                WorkSocket = server.Client
            };
            while (IsConnected)
            {
                if (IsReceivingData)
                    continue;
                IsReceivingData = true;
                server.Client.BeginReceive(state.Buffer, 0, ChatHelper.StateObject.BUFFER_SIZE, 0,
                    OnReceive, state);
            }
        }

        public bool IsReceivingData { get; set; }

        private bool ReceiveUsersList()
        {
            var bytes = new byte[1024];
            server.Client.Receive(bytes);
            var data = new Data(bytes);

            var serverMessage = data.Message?.Split(new[] { '|' }, StringSplitOptions.None);

            if (data.Command == Command.NameExist)
            {
                OnFailReceived($"Пользователь \"{data.To}\" уже существует");
                return false;
            }

            ServerName = serverMessage!=null ? serverMessage[3]: ServerName;

            OnUserListReceived(serverMessage);

            return true;
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
                ParseMessage(new Data(state.Buffer));

                server.Client.BeginReceive(state.Buffer, 0, ChatHelper.StateObject.BUFFER_SIZE, 0, OnReceive, state);
            }
            catch (SocketException)
            {
                IsConnected = false;
                server.Client.Disconnect(true);
            }
        }

        public void ParseMessage(Data data)
        {
            switch (data.Command)
            {
                case Command.SendMessage:
                    OnMessageReceived(data.Message, data.From);
                    break;
                case Command.Broadcast:
                    OnUserListReceived(data.Message.Split('|'));
                    break;
                case Command.Success:
                    var q = data;
                    break;
                case Command.Fail:
                    OnFailReceived(data.Message);
                    break;
            }
        }
        public void SendMessage(string message, string recipient)
        {
            if (!server.Connected)
            {
                OnFailReceived("Соединение с сервером прервано");
                return;
            }
            var data = new Data { Command = Command.SendMessage, To = recipient, From = UserName, Message = message };
            server.Client.Send(data.ToByte());
        }


        public void CloseConnection()
        {
            IsConnected = false;

            var data = new Data { Command = Command.Disconnect };
            if (server.Client.Connected)
                server.Client.Send(data.ToByte());
        }
        #endregion

        #region Event Invokers

        protected virtual void OnUserListReceived(string[] serverMessage)
        {
            var handler = UserListReceived;
            handler?.Invoke(serverMessage[0], new MessageEventArgs(serverMessage[1], serverMessage[2], serverMessage[3]));
        }

        protected virtual void OnMessageReceived(string message, string sender)
        {
            var handler = MessageReceived;
            handler?.Invoke(sender, new MessageEventArgs(message, sender));
        }
        protected virtual void OnFailReceived(string message)
        {
            var handler = FailReceived;
            handler?.Invoke(null, new MessageEventArgs(message));
        }
        #endregion


    }
}
