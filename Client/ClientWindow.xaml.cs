using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Backend.Client;
using Backend.Helpers;
using Client.BaseControls;

namespace Client
{
    /// <summary>
    /// Логика взаимодействия для ClientWindow.xaml
    /// </summary>
    public partial class ClientWindow
    {
        public ClientWindow()
        {
            InitializeComponent();
        }

        private delegate void SetUserList(object sender, EventArgs e);
        private delegate void RecieveMessage(object sender, EventArgs e);
        private delegate void EndConversation();
        private delegate void FailMessage(object sender, EventArgs e);

        public ChatClient ChatClient { get; set; }
        public string Recipient { get; set; }

        public ClientWindow(ChatClient client)
        {
            InitializeComponent();
            ChatClient = client;
            ChatClient.UserListReceived += chatClient_UserListReceived;
            ChatClient.MessageReceived += chatClient_MessageReceived;
            ChatClient.FailReceived += chatClient_FailReceived;
            tbChat.SelectionChanged += tbChat_SelectionChanged;
            KeyDown += tbChat_KeyDown;
            Title = ChatClient.Init() ? string.Format("{0} connected to {1} ({2})", ChatClient.UserName, ChatClient.ServerName,
                ChatClient.ServerAddress) : "Disconnected";
            SetButtons();
        }


        private void chatClient_MessageReceived(object sender, EventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                RecieveMessage r = chatClient_MessageReceived;
                Dispatcher.Invoke(r, sender, e);
            }
            else
            {
                var args = e as MessageEventArgs;
                if (args == null)
                    return;

                var page = FindTabPage((string)sender) ?? AddTabPage((string)sender);
                var time = DateTime.Now.ToString("HH:mm:ss");

                page.DialogBox.AppendText(string.Format("[{0}] {1}: {2}", time, args.User, args.Message));
                page.DialogBox.AppendText(Environment.NewLine);
            }

        }
        private void chatClient_FailReceived(object sender, EventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                FailMessage r = chatClient_MessageReceived;
                Dispatcher.Invoke(r, sender, e);
            }
            else
            {
                var args = e as MessageEventArgs;
                if (args == null)
                    return;

                MessageBox.Show(args.Message, "Ошибка", MessageBoxButton.OK);
            }
        }

        private ChatTabItem FindTabPage(string name)
        {
            return tbChat.Items.Cast<ChatTabItem>().FirstOrDefault(page => (string)page.Header == name);
        }

        private ChatTabItem AddTabPage(string name)
        {
            var page = new ChatTabItem { Header = name };
            page.DialogBox.Text += ChatHelper.ChatWith(name);
            tbChat.Items.Add(page);
            return page;
        }

        private void chatClient_UserListReceived(object sender, EventArgs e)
        {
            var userList = (string)sender;
            var args = e as MessageEventArgs;
            if (args == null)
                return;

            if (!Dispatcher.CheckAccess())
            {
                SetUserList ul = chatClient_UserListReceived;
                Dispatcher.Invoke(ul, sender, e);
            }
            else
            {
                var connStr = string.Format("{0} {1}", args.User, args.Info);
                tbChat.GlobalPage.DialogBox.AppendText(connStr);
                tbChat.GlobalPage.DialogBox.AppendText(Environment.NewLine);

                var userPage = FindTabPage(args.User);
                if (userPage != null)
                {
                    userPage.DialogBox.AppendText(connStr);
                    userPage.DialogBox.AppendText(Environment.NewLine);
                }
                allPanel.Children.Clear();
                var users = userList.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (!users.Any())
                    allPanel.Children.Add(new TextBlock { Text = ChatHelper.NO_USERS_ONLINE });
                else
                    foreach (var user in users)
                        AddUserButton(user);
            }

        }

        private void tbChat_KeyDown(object sender, KeyEventArgs e)
        {
            if (!Keyboard.IsKeyDown(Key.LeftCtrl) || !Keyboard.IsKeyDown(Key.W) || tbChat.SelectedItem.Equals(tbChat.GlobalPage))
                return;
            CloseTab();
        }

        private void AddUserButton(string userName)
        {
            var userBtn = new Button
            {
                Content = userName,
                BorderBrush = Brushes.Transparent,
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            userBtn.Click += user_DoubleClick;
            allPanel.Children.Add(userBtn);
        }

        private void sendMessage_Click(object sender, RoutedEventArgs e)
        {
            SendMessage();
        }

        private void SendMessage()
        {
            var tabItem = tbChat.SelectedItem as ChatTabItem;
            if (tabItem != null)
            {
                var selectedUser = (string)tabItem.Header;
                var message = tbMessage.Text;
                ChatClient.SendMessage(tbMessage.Text, selectedUser);

                var page = FindTabPage(selectedUser) ?? AddTabPage(selectedUser);
                tbChat.SelectedItem = page;
                var time = DateTime.Now.ToString("HH:mm:ss");

                page.DialogBox.AppendText($"[{time}] {ChatClient.UserName}: {message}");
                page.DialogBox.AppendText(Environment.NewLine);
            }
            tbMessage.Clear();
        }

        private void ClientForm_OnClosing(object sender, CancelEventArgs e)
        {
            ChatClient.IsConnected = false;
            ChatClient.CloseConnection();
        }


        private void ClientForm_Load(object sender, RoutedEventArgs e)
        {
            SetButtons();
        }

        private void SetButtons()
        {
            var isUserSelected = tbChat.SelectedItem != null && !IsServiceTab();
            sendMsg.IsEnabled = !string.IsNullOrWhiteSpace(tbMessage.Text) && isUserSelected;
            tbMessage.Visibility = sendMsg.Visibility = isUserSelected ? Visibility.Visible : Visibility.Collapsed;
        }

        private bool IsServiceTab()
        {
            return Equals(tbChat.SelectedItem, tbChat.GlobalPage);
        }

        private void tbMessage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || !sendMsg.IsEnabled)
                return;
            SendMessage();
        }

        private void user_DoubleClick(object sender, RoutedEventArgs e)
        {
            var item = e.Source as Button;
            if (item == null)
                return;
            var name = (string)item.Content;
            tbChat.SelectedItem = FindTabPage(name) ?? AddTabPage(name);
        }


        private void messageTextbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SetButtons();
        }

        private void tbChat_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var tabItem = (TabItem)tbChat.SelectedItem;
            Recipient = tabItem != null ? (string)tabItem.Header : null;
            SetButtons();
        }

        private void ClientForm_OnClosed(object sender, EventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void CloseTab_OnClick(object sender, RoutedEventArgs e)
        {
            CloseTab();
        }


        private void CloseTab()
        {
            var selected = tbChat.SelectedItem;
            tbChat.Items.Remove(selected);
            tbChat.SelectedItem = tbChat.GlobalPage;
        }

        private void FrameworkElement_OnContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var tab = tbChat.SelectedItem as ChatTabItem;
            if (tab != null && (string)tab.Header != ChatHelper.GLOBAL)
                closeTab.IsEnabled = true;
            else
                closeTab.IsEnabled = false;
        }

    }
}
