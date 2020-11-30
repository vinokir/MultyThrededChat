using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace Backend.Helpers
{
    public static class ChatHelper
    {
        #region Titles

        public const string GLOBAL = "Главная";
        public const string CONNECTED = "connected";
        public const string DISCONNECTED = "disconnected";
        public const string LOCAL = "127.0.0.1";
        public const string NO_USERS_ONLINE = "нет активных пользователей";
        #endregion

        #region Errors
        public const string PORT_ERROR = "Номер порта должен быть между 0 и 65535";
        #endregion

        #region Messages 
        public static string WelcomeMessage = string.Format("{0}: ** Добро пожаловть, Нажмите на пользователя для начала переписки**\n", DateTime.Now.ToString("HH:mm:ss"));
        #endregion

        public class StateObject
        {

            public Socket WorkSocket = null;
            public const int BUFFER_SIZE = 5242880;
            public byte[] Buffer = new byte[BUFFER_SIZE];
            public StringBuilder Sb = new StringBuilder();
        }

        public static string ChatWith(string name)
        {
            return string.Format("** Чат с {0} **\n", name);
        }
    }

    public class Data
    {
        public string From { get; set; }
        public string To { get; set; }
        public string Message { get; set; }
        public Command Command { get; set; }
        public Data()
        {
            Command = Command.Null;
            From = null;
            To = null;
            Message = null;
        }

        public Data(byte[] data)
        {
            // First four bytes are for the Command
            Command = (Command)BitConverter.ToInt32(data, 0);
            // Next 4 bytes store length of the recipient name
            var next = sizeof(int);
            var nameLength = BitConverter.ToInt32(data, next) * 2;
            next += sizeof(int);
            if (nameLength > 0)
            {
                To = Encoding.Unicode.GetString(data, next, nameLength);
                next += nameLength;
            }
            // Next 4 bytes store length of the sender name
            var senderNameLength = BitConverter.ToInt32(data, next) * 2;
            next += sizeof(int);
            if (senderNameLength > 0)
            {
                From = Encoding.Unicode.GetString(data, next, senderNameLength);
                next += senderNameLength;
            }
            // Next 4 bytes store length of the message
            var messageLength = BitConverter.ToInt32(data, next) * 2;
            next += sizeof(int);
            if (messageLength > 0)
            {
                Message = Encoding.Unicode.GetString(data, next, messageLength);
                next += messageLength;
            }
        }

        public byte[] ToByte()
        {
            var zeroBytes = BitConverter.GetBytes(0);
            var result = new List<byte>();
            result.AddRange(BitConverter.GetBytes((int)Command));
            if (To != null)
            {
                Encode(To, result);
            }
            else
                result.AddRange(zeroBytes);

            if (From != null)
            {
                Encode(From, result);
            }
            else
                result.AddRange(zeroBytes);

            if (Message != null)
            {
                Encode(Message, result);
            }
            else
                result.AddRange(zeroBytes);

            return result.ToArray();
        }

        private static void Encode(string str, List<byte> result)
        {
            var encoded = Encoding.Unicode.GetBytes(str);
            result.AddRange(BitConverter.GetBytes(str.Length));
            result.AddRange(encoded);
        }

        private static void Encode(IReadOnlyCollection<byte> file, List<byte> result)
        {
            result.AddRange(BitConverter.GetBytes(file.Count));
            result.AddRange(file);
        }
    }


    public enum Command
    {
        Broadcast,
        Disconnect,
        SendMessage,
        NameExist,
        Success,
        Fail,
        Null
    }

    public class ConnectedClient
    {
        private readonly string userName;
        private readonly Socket connection;
        public bool IsConnected { get; set; }
        public Socket Connection
        {
            get { return connection; }
        }
        public string UserName
        {
            get { return userName; }
        }

        public ConnectedClient(string userName, Socket connection)
        {
            this.userName = userName;
            this.connection = connection;
        }
    }

}
