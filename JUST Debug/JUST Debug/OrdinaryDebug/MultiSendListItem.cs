using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace JUST_Debug
{
    public class MultiSendListItem
    {
        public int ID { set; get; }
        //public bool Enabled { set; get; }
        public string SendText;
        //public string sendText { get; set; }
        //public string sendBtn { get; set; }
        public MultiSendListItem(int id)
        {
            ID = id;
        }
        public void TextBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            SendText = (sender as TextBox).Text; 
        }
        //public void RadioButtonCheck(object sender,RoutedEventArgs args)
        //{
        //    if ((sender as RadioButton).IsChecked == true)
        //        return;
        //    (sender as RadioButton).IsChecked =false;
        //}
    }
}
