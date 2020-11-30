using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backend.Client
{
    public class MessageEventArgs : EventArgs
    {
        public string Message { get; set; }
        public string ServerName { get; set; }
        public string Info { get; set; }
        public string User { get; set; }

        public MessageEventArgs(string changedUser, string info, string serverName)
        {
            User = changedUser;
            ServerName = serverName;
            Info = info;
        }

        public MessageEventArgs(string msg)
        {
            Message = msg;
        }
        public MessageEventArgs(string msg, string fromUser)
        {
            Message = msg;
            User = fromUser;
        }

    }
}
