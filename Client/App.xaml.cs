using System.Windows;
using Backend.Client;

namespace Client
{
    /// <summary>
    /// Логика взаимодействия для App.xaml
    /// </summary>
    public partial class App
    {
        public App()
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
        }

        /// <summary>
        /// Launching client window
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void App_OnStartup(object sender, StartupEventArgs e)
        {
            var connectForm = new ConnectWindow();
            if (connectForm.ShowDialog() != true)
                return;
            var chatClient = new ChatClient(connectForm.PortNumber, connectForm.IpAddress, connectForm.UserName);
            if (!chatClient.IsConnected)
            {
                Current.Shutdown();
                return;
            }

            var clientForm = new ClientWindow(chatClient);
            clientForm.Show();

        }
    }
}
