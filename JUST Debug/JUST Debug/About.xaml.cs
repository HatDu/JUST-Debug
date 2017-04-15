using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace JUST_Debug
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class About : Page
    {
        public About()
        {
            this.InitializeComponent();
        }

 

        public static async Task FeedbackAsync(string address, string subject, string body)
        {
            if (address == null)
                return;
            var mailto = new Uri($"mailto:{address}?subject={subject}&body={body}");
            await Launcher.LaunchUriAsync(mailto);
        }

        private async void EmailAdderssHyperLinkButton_Click(object sender, RoutedEventArgs e)
        {
            await FeedbackAsync((string)EmailAdderssHyperLinkButton.Content, "Just Debug 使用反馈", "请在此处填写反馈内容...");
        }

        private void SourceCodeLinkButton_Click(object sender, RoutedEventArgs e)
        {
            
        }
    }
}
