using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Windows;
using Backend.Server;

namespace Server
{
    /// <summary>
    /// Логика взаимодействия для ServerWindow.xaml
    /// </summary>
    public partial class ServerWindow
    {
        private ChatServer server;

        public ServerWindow()
        {
            InitializeComponent();
            ObtainNetworkInterfaces();
        }


        private void cbStartStop_Checked(object sender, RoutedEventArgs e)
        {
            if (cbStartStop.IsChecked == true)
            {
                try
                {
                    var port = Int32.Parse(tbPortNumber.Text);
                    server = new ChatServer(port, cbInterfaces.SelectedItem, tbServerName.Text);
                    server.ClientConnected += ServerOnClientConnected;
                    server.ClientDisconnected += ServerOnClientDisconnected;
                    server.JournalAdd += ServerOnJournalAdd;
                    var serverName = tbServerName.Text;
                    if (string.IsNullOrWhiteSpace(serverName))
                    {
                        ServerOnJournalAdd("Введите корректный номер порта или имя сервера", null);
                    }
                    else
                    {
                        server.StartServer();
                        SetControls(false);
                    }
                }
                catch (Exception ex)
                {

                    ServerOnJournalAdd("Введите корректный номер порта или имя сервера", new ErrorEventArgs(ex));
                }
            }

            else
            {
                if (server == null)
                    return;
                server.StopServer();
                SetControls(true);
            }
        }

        private void SetControls(bool enabled)
        {
            tbPortNumber.IsEnabled = enabled;
            tbServerName.IsEnabled = enabled;
            cbInterfaces.IsEnabled = enabled;
        }

        private void ServerOnClientDisconnected(object sender, EventArgs e)
        {
            var userName = (string)sender;
            var item = FormatClient(userName, e);
            UpdateConnectedClients(item, "Delete");
        }

        private void ServerOnClientConnected(object sender, EventArgs e)
        {
            var userName = (string)sender;
            var item = FormatClient(userName, e);
            UpdateConnectedClients(item, "Add");
        }

        private static string FormatClient(string userName, EventArgs e)
        {
            var args = e as CustomEventArgs;
            if (args == null)
                return string.Empty;

            var client = args.ClientSocket;
            var remoteEndPoint = (IPEndPoint)client.RemoteEndPoint;
            var remoteIp = remoteEndPoint.Address.ToString();
            var remotePort = remoteEndPoint.Port.ToString();

            return string.Format("{0} ({1}:{2})", userName, remoteIp, remotePort);

        }


        private void UpdateConnectedClients(string str, string type)
        {
            if (!connectedClients.Dispatcher.CheckAccess())
            {
                Action<string, string> d = UpdateConnectedClients;
                connectedClients.Dispatcher.Invoke(d, str, type);
            }
            else
            {
                if (type.Equals("Add"))
                {
                    connectedClients.Items.Add(str);
                }
                else
                {
                    connectedClients.Items.Remove(str);
                }

            }
        }

        private void ObtainNetworkInterfaces()
        {
            var anyInterface = new NetworkInterfaceDescription("Any");
            var list = new BindingList<object>();
            var allInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var nic in allInterfaces.Where(i => (i.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                || (i.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)))
            {
                list.Add(nic);
            }
            list.Add(anyInterface);
            cbInterfaces.ItemsSource = list;
            cbInterfaces.DisplayMemberPath = "Description";
            cbInterfaces.SelectedItem = anyInterface;
        }


        private void ServerForm_OnClosing(object sender, CancelEventArgs e)
        {
            if (server != null)
                server.StopServer();
        }

        private void ServerOnJournalAdd(object sender, ErrorEventArgs e)
        {
            var message = sender as string;
            if (!string.IsNullOrEmpty(message))
            {
                message = $"{DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss")} {message}";
                UpdateJournal(message);
            }

        }

        private void UpdateJournal(string message)
        {
            if (!journal.Dispatcher.CheckAccess())
            {
                Action<string> d = UpdateJournal;
                journal.Dispatcher.Invoke(d, message);
            }
            else
            {
                journal.Items.Add(message);
            }
        }
    }
}
