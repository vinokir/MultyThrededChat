using System.Windows.Controls;
using Backend.Helpers;

namespace Client.BaseControls
{
    public class BaseTabControl : TabControl
    {
        #region Properties
        public ChatTabItem GlobalPage { get; set; }

        #endregion

        public BaseTabControl()
        {
            GlobalPage = new ChatTabItem { Header = ChatHelper.GLOBAL };
            GlobalPage.DialogBox.Text += ChatHelper.WelcomeMessage;
            Items.Add(GlobalPage);
        }
    }
}
