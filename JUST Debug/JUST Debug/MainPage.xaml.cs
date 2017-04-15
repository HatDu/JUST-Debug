using Windows.UI.Xaml.Controls;
using Windows.Devices.SerialCommunication;
using Windows.Devices.Enumeration;
using System;
using Windows.Storage.Streams;
using System.Linq;

namespace JUST_Debug
{

    public sealed partial class MainPage : Page
    {
        private static TextBlock statusTbk=null;
        public MainPage()
        {
            this.InitializeComponent();
            statusTbk = StatusTbk;
            this.StatusTbk.Text = "串口调试";
        }

        private void HunburgerButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            MySplitView.IsPaneOpen = !MySplitView.IsPaneOpen;
        }

        private void IconListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MySplitView.IsPaneOpen = false;
            if (OrdinaryListItem.IsSelected) {
                MyFrame.Navigate(typeof(OrdinaryDebug));
                this.StatusTbk.Text = "串口调试";
            }
            else if (VirtualScopeListItem.IsSelected)
            {
                MyFrame.Navigate(typeof(VirtualScope));
                this.StatusTbk.Text = "虚拟示波器";
            }
            //else if (NETListItem.IsSelected)
            //{
            //    MyFrame.Navigate(typeof(Net));
            //    this.StatusTbk.Text = "网络调试";
            //}
            else if (SettingsListItem.IsSelected)
            {
                MyFrame.Navigate(typeof(About));
                this.StatusTbk.Text = "关于";
            }
        }

        private void Page_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            MyFrame.Navigate(typeof(OrdinaryDebug));
        }
        public static void Notify(string message)
        {
            NotifyPopup notifyPopup = new NotifyPopup(message);
            notifyPopup.Show();
        }
        public static void ShowStatus(string status)
        {
            if(statusTbk!=null)
                statusTbk.Text = status;
        }
    }
}
