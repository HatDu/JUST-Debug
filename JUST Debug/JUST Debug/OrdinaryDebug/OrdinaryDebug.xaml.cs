using System;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace JUST_Debug
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class OrdinaryDebug : Page
    {
        private SerialDevice serialPort = null;
        private string lastPortName = "";
        DataReader dataReaderObject = null;
        private DataWriter dataWriteObject = null;
        private ObservableCollection<DeviceInformation> listOfDevices;
        private CancellationTokenSource ReadCancellationTokenSource;
        
        
        //串口是否处于侦听状态
        private bool isListening = false;
        //是否显示串口接收到的数据
        private bool isShowRecData = true;
        //是否发送回显
        private bool isSendEcho = false;
        private Timer autoSendTimer=null;
        //是否选择十六进制发送
        private bool HexModeSend = false;
        //多项发送
        private int NumOfSendItems=0;
        private ObservableCollection<MultiSendListItem> listOfSendItems;
        private Timer multiSendTimer;
        private int iter = 0;

        public OrdinaryDebug()
        {
            this.InitializeComponent();
            listOfDevices = new ObservableCollection<DeviceInformation>();

            //多项发送
            listOfSendItems = new ObservableCollection<MultiSendListItem>();
            listOfSendItems.Add(new MultiSendListItem(NumOfSendItems++));
            listOfSendItems.Add(new MultiSendListItem(NumOfSendItems++));
            listOfSendItems.Add(new MultiSendListItem(NumOfSendItems++));
            listOfSendItems.Add(new MultiSendListItem(NumOfSendItems++));
            //listOfSendItems.Add(new MultiSendListItem(NumOfSendItems++));
            //listOfSendItems.Add(new MultiSendListItem(NumOfSendItems++));
            //listOfSendItems.Add(new MultiSendListItem(NumOfSendItems++));
        }

        private void Page_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            StopBitsCbx.Items.Add(SerialStopBitCount.One);
            StopBitsCbx.Items.Add(SerialStopBitCount.OnePointFive);
            StopBitsCbx.Items.Add(SerialStopBitCount.Two);
            StopBitsCbx.SelectedIndex = 0;

            ParityCbx.Items.Add(SerialParity.None);
            ParityCbx.Items.Add(SerialParity.Even);
            ParityCbx.Items.Add(SerialParity.Mark);
            ParityCbx.Items.Add(SerialParity.Odd);
            ParityCbx.Items.Add(SerialParity.Space);
            ParityCbx.SelectedIndex = 0;

            DatabitsCbx.Items.Add(8);
            DatabitsCbx.Items.Add(7);
            DatabitsCbx.Items.Add(6);
            DatabitsCbx.Items.Add(5);
            DatabitsCbx.SelectedIndex = 0;

            ListAvailablePorts();
        }

        /// <summary>
        /// 列出可用串口
        /// </summary>
        private async void ListAvailablePorts()
        {
            try
            {
                string aqs = SerialDevice.GetDeviceSelector();
                var dis = await DeviceInformation.FindAllAsync(aqs);

                listOfDevices.Clear();
                DeviceListCbx.Items.Clear();
                DeviceListCbx.SelectedIndex = -1;

                for (int i = 0; i < dis.Count; i++)
                {
                    listOfDevices.Add(dis[i]);
                    DeviceListCbx.Items.Add(dis[i].Name);
                    if (dis[i].Name.Contains(lastPortName))
                        DeviceListCbx.SelectedIndex = i;
                }
                ReadCancellationTokenSource = new CancellationTokenSource();
            }
            catch
            {
                MainPage.Notify("无可用串口\n");
            }
        }
 
        /// <summary>
        /// 创建、关闭串口连接
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void SerialPortSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            //创建串口连接
            if ((sender as ToggleSwitch).IsOn)
            {
                if (isListening)
                    return;
                int k = DeviceListCbx.SelectedIndex;
                if (k == -1)
                {
                    MainPage.Notify("请选择一个串口");
                    return;
                }
                try
                {
                    var selectedPort = listOfDevices[k];
                    serialPort = await SerialDevice.FromIdAsync(selectedPort.Id);
                    serialPort.ReadTimeout = TimeSpan.FromMilliseconds(1000);
                    serialPort.BaudRate = uint.Parse(BaudTbx.Text);
                    serialPort.Parity = (SerialParity)ParityCbx.SelectedItem;
                    serialPort.StopBits = (SerialStopBitCount)StopBitsCbx.SelectedItem;
                    serialPort.DataBits = ushort.Parse(DatabitsCbx.SelectedItem.ToString());

                    isListening = true;
                    ReadCancellationTokenSource = new CancellationTokenSource();
                    recvTbx.Text += "\n";

                    MainPage.ShowStatus("串口调试："+selectedPort.Name);
                }
                catch
                {
                    MainPage.Notify("串口丢失");
                    (sender as ToggleSwitch).IsOn = false;
                }
            }
            //关闭串口连接
            else
            {
                try
                {
                    CancelReadTask();
                    isListening = false;
                    CloseDevice();
                    CyclSendSwitch.IsOn = false;
                }
                catch
                {
                    MainPage.Notify("关闭串口设备");
                }
            }
            
        }

        private void DeviceListCbx_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            ListAvailablePorts();
        }

        /// <summary>
        /// 接收发送计数清零
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            recCountTbk.Text = "0";
            sendCountTbk.Text = "0";
        }

        #region 串口接收

        /// <summary>
        /// 串口读
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task ReadAsync(CancellationToken cancellationToken)
        {
            Task<UInt32> loadAsyncTask;

            uint ReadBufferLength = 128;
            
            cancellationToken.ThrowIfCancellationRequested();
            
            dataReaderObject.InputStreamOptions = InputStreamOptions.Partial;
            
            loadAsyncTask = dataReaderObject.LoadAsync(ReadBufferLength).AsTask(cancellationToken);

            // Launch the task and wait
            UInt32 bytesRead = await loadAsyncTask;

            if (bytesRead > 0)
            {
                byte[] buffer = new byte[bytesRead];
                dataReaderObject.ReadBytes(buffer);
                showRecData(buffer);
            }
        }

        private async void showRecData(byte[] buffer)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                string output = String.Empty;
                if (HexRecCbx.IsChecked == true)
                {
                    string cell = String.Empty;
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        cell = Convert.ToString(buffer[i], 16);
                        if (cell.Length == 1)
                            cell = "0" + cell;
                        output += cell.ToUpper() + " ";
                    }
                }
                else
                {
                    output = Encoding.ASCII.GetString(buffer);
                }
                if (isShowRecData)
                    recvTbx.Text += output;
                else
                {
                    recvTbx.Text += " ";
                    recvTbx.Text.Remove(recvTbx.Text.Length - 1);
                }
                recCountTbk.Text = (int.Parse(recCountTbk.Text) + buffer.Length).ToString();
            });
        }

        private async void recvTbx_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isListening)
                return;
            try
            {
                if (serialPort != null)
                {
                    dataReaderObject = new DataReader(serialPort.InputStream);
                    await ReadAsync(ReadCancellationTokenSource.Token);
                }
            }
            catch (Exception ex)
            {
                if (ex.GetType().Name == "TaskCanceledException")
                {
                    MainPage.Notify("关闭串口侦听");
                }
            }
            finally
            {
                // Cleanup once complete
                if (dataReaderObject != null)
                {
                    dataReaderObject.DetachStream();
                    dataReaderObject = null;
                }
            }
        }

        /// <summary>
        /// 关闭串口侦听
        /// </summary>
        private void CancelReadTask()
        {
            if (ReadCancellationTokenSource != null)
            {
                if (!ReadCancellationTokenSource.IsCancellationRequested)
                {
                    ReadCancellationTokenSource.Cancel();
                }
            }
        }

        /// <summary>
        /// 关闭设备
        /// </summary>
        private void CloseDevice()
        {
            if (serialPort != null)
            {
                serialPort.Dispose();
            }
            lastPortName = serialPort.PortName;
            serialPort = null;
            ListAvailablePorts();
        }

        private void BaudTbx_LostFocus(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (!Regex.IsMatch(BaudTbx.Text, "^[0-9]*$"))
            {
                BaudTbx.Text = "9600";
                MainPage.Notify("只能输入数字");
            }
        }

        /// <summary>
        /// UWP 文本框回滚到末尾
        /// </summary>
        /// <param name="textBox"></param>
        private void TextBoxScrollToEnd(TextBox textBox)
        {
            var grid = (Grid)VisualTreeHelper.GetChild(textBox, 0);
            for (var i = 0; i <= VisualTreeHelper.GetChildrenCount(grid) - 1; i++)
            {
                object obj = VisualTreeHelper.GetChild(grid, i);
                if (!(obj is ScrollViewer)) continue;
                ((ScrollViewer)obj).ChangeView(0.0f, ((ScrollViewer)obj).ExtentHeight, 1.0f);
                break;
            }
        }

        /// <summary>
        /// 保存接收数据
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void RecDataSaveBtn(object sender, RoutedEventArgs e)
        {
            if (isListening == true)
            {
                MainPage.Notify("请关闭串口后尝试");
                return;
            }
            string writingData = recvTbx.Text;
            FileSavePicker fp = new FileSavePicker();
            var filedb = new[] { ".txt", ".dat" };
            fp.FileTypeChoices.Add("DB", filedb);
            fp.SuggestedFileName = "savedata" + DateTime.Now.Day + "-" + DateTime.Now.Month + "-" + DateTime.Now.Year;
            StorageFile sf = await fp.PickSaveFileAsync();
            if (sf != null)
            {
                using (StorageStreamTransaction transaction = await sf.OpenTransactedWriteAsync())
                {
                    using (DataWriter dataWriter = new DataWriter(transaction.Stream))
                    {
                        dataWriter.WriteString(writingData);
                        transaction.Stream.Size = await dataWriter.StoreAsync();
                        await transaction.CommitAsync();
                    }
                }
            }
        }

        /// <summary>
        /// 清空接收区
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RecTextClear(object sender, RoutedEventArgs e)
        {
            if (!isListening)
                recvTbx.Text = "";
            else
                MainPage.Notify("请先关闭串口");
        }

        /// <summary>
        /// 打开文件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OpenFileBtn_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker fop = new FileOpenPicker();
            fop.FileTypeFilter.Add(".txt");
            fop.FileTypeFilter.Add(".dat");

            StorageFile sf = await fop.PickSingleFileAsync();
            if (sf != null)
            {
                using (IRandomAccessStream readStream = await sf.OpenAsync(FileAccessMode.Read))
                {
                    using (DataReader dataReader = new DataReader(readStream))
                    {
                        UInt64 size = readStream.Size;
                        if (size <= UInt32.MaxValue)
                        {
                            UInt32 numBytesLoaded = await dataReader.LoadAsync((UInt32)size);
                            string fileContent = dataReader.ReadString(numBytesLoaded);
                            sendTbx.Text = fileContent;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 是否在接收文本框显示接收数据
        /// </summary>
        private void IsShowPortData(object sender, RoutedEventArgs e)
        {
            if((sender as CheckBox).IsChecked == true)
            {
                isShowRecData = false;
            }else
            {
                isShowRecData = true;
            }
        }

        /// <summary>
        /// 访问UI线程，显示接收数据
        /// </summary>
        /// <param name="str"></param>
        private async void WriteRecTbk(string str)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                recvTbx.Text += str;
            });
        }
        #endregion

        #region 串口发送

        /// <summary>
        /// WriteAsync: Task that asynchronously writes data from the input text box 'sendText' to the OutputStream 
        /// </summary>
        /// <returns></returns>
        private async Task WriteAsync(string sendtext)
        {
            if (!isListening)
                return;
            Task<UInt32> storeAsyncTask;

            if (sendtext.Length != 0)
            {
                // Load the text from the sendText input text box to the dataWriter object
                dataWriteObject.WriteString(sendtext);

                if (isSendEcho)
                {
                    recvTbx.Text += sendtext;
                }
                // Launch an async task to complete the write operation
                storeAsyncTask = dataWriteObject.StoreAsync().AsTask();

                UInt32 bytesWritten = await storeAsyncTask;
                if (bytesWritten > 0)
                {
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        sendCountTbk.Text = (int.Parse(sendCountTbk.Text) + bytesWritten).ToString();
                    });
                }
            }
        }

        private void CancelWriteAsync()
        {
            dataWriteObject = null;
            if (autoSendTimer != null)
            {
                autoSendTimer.Dispose();
                autoSendTimer.Dispose();
                autoSendTimer = null;
            }
        }

        /// <summary>
        /// 点击串口发送按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void sendBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!isListening)
                {
                    MainPage.Notify("请保持串口打开状态");
                    return;
                }
                if (serialPort != null)
                {
                    // Create the DataWriter object and attach to OutputStream
                    dataWriteObject = new DataWriter(serialPort.OutputStream);

                    //Launch the WriteAsync task to perform the write
                    await WriteAsync(sendTbx.Text);
                }
                else
                {
                    MainPage.Notify("请打开串口");
                }
            }
            catch (Exception ex)
            {
                MainPage.Notify("sendTextButton_Click: " + ex.Message);
            }
            finally
            {
                // Cleanup once complete
                if (dataWriteObject != null)
                {
                    dataWriteObject.DetachStream();
                    dataWriteObject = null;
                }
            }
        }

        private void AotoSendSwitch(object sender, RoutedEventArgs e)
        {   
            if ((sender as ToggleSwitch).IsOn)
            {
                if (!isListening)
                {
                    MainPage.Notify("请保持串口打开状态");
                    CyclSendSwitch.IsOn = false;
                    return;
                }
                autoSendTimer = new Timer(autoSendTimerCallback, null, 0, int.Parse(autoSendDuring.Text));
                dataWriteObject = new DataWriter(serialPort.OutputStream);
                
            }
            else
            {
                CancelWriteAsync();
            }
        }

        private async void autoSendTimerCallback(object state)
        {
            if (!isListening)
            {
                closeAutoSendTimer();
                return;
            }
            try
            {
                dataWriteObject = new DataWriter(serialPort.OutputStream);
                //Task<UInt32> storeAsyncTask;
                string sendData = String.Empty;
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    sendData = sendTbx.Text;
                });
                if (sendData == null || sendData.Length == 0)
                    return;
                dataWriteObject = new DataWriter(serialPort.OutputStream);
                await WriteAsync(sendData);
            }
            catch {
                closeAutoSendTimer();
            }
            finally
            {
                if (dataWriteObject != null)
                {
                    dataWriteObject.DetachStream();
                    dataWriteObject = null;
                }
            }
        }

        /// <summary>
        /// 点击循环发送选择框
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CirclSendCbx_Click(object sender, RoutedEventArgs e)
        {
            if (CirclSendCbx.IsChecked == true)
            {
                CyclSendSwitch.Visibility = Visibility.Visible;
                CircleSendArea.Visibility = Visibility.Visible;
                sendBtn.Visibility = Visibility.Collapsed;
                MultiSendCbx.IsEnabled = false;
            }
            else
            {
                CyclSendSwitch.Visibility = Visibility.Collapsed;
                sendBtn.Visibility = Visibility.Visible;
                CircleSendArea.Visibility = Visibility.Collapsed;
                MultiSendCbx.IsEnabled = true;
                closeMultiSendTimer();
            }
        }

        /// <summary>
        /// 发送回显选择
        /// </summary>
        private void IsSendEcho(object sender, RoutedEventArgs e)
        {
            if ((sender as CheckBox).IsChecked == true)
            {
                isSendEcho = true;
            }
            else
            {
                isSendEcho = false;
            }
        }
        private void closeAutoSendTimer()
        {
            if (autoSendTimer!=null)
            {
                autoSendTimer.Dispose();
                autoSendTimer = null;
            }
        }


        #endregion
        /*
         */
        private async void MultiSendItemButtonClick(object sender, RoutedEventArgs e)
        {
            object tag = (sender as Button).Tag;
            int k = int.Parse(tag.ToString());
            MultiSendListItem item = (MultiSendListItem)MultiSendListView.Items[k];
            string sendText = item.SendText;

            try
            {
                if (!isListening)
                {
                    MainPage.Notify("请保持串口打开状态");
                    return;
                }
                if (serialPort != null)
                {
                    dataWriteObject = new DataWriter(serialPort.OutputStream);
                    await WriteAsync(sendText);
                }
                else
                {
                    MainPage.Notify("请打开串口");
                }
            }
            catch (Exception ex)
            {
                MainPage.Notify("sendTextButton_Click: " + ex.Message);
            }
            finally
            {
                // Cleanup once complete
                if (dataWriteObject != null)
                {
                    dataWriteObject.DetachStream();
                    dataWriteObject = null;
                }
            }
        }

        private void MultiSendItemsAdd(object sender, RoutedEventArgs e)
        {
            if (isListening)
            {
                MainPage.Notify("请先暂停发送");
                return;
            }
            Button btn = (sender as Button);
            int k = int.Parse(btn.Tag.ToString());
            for (int i = 0; i < listOfSendItems.Count; i++)
            {
                if (listOfSendItems[i].ID == k)
                {
                    k = i;
                    break;
                }
            }
            MultiSendListItem temp = new MultiSendListItem(NumOfSendItems++);
            listOfSendItems.Insert(k, temp);
            //MultiSendListView.ScrollIntoView(temp);
            //refreshListOfItemsID();
            //MultiSendListView.ScrollIntoView(listOfSendItems[listOfSendItems.Count - 1]);
        }

        private void MultiSendItemsDel(object sender, RoutedEventArgs e)
        {
            if (isListening)
            {
                MainPage.Notify("请先暂停发送");
                return;
            }
            if (listOfSendItems.Count == 1)
                return;

            Button btn= (sender as Button); 
            int k = int.Parse(btn.Tag.ToString());
            for(int i = 0; i < listOfSendItems.Count;i++) {
                if (listOfSendItems[i].ID == k) {
                    k = i;
                    break;
                }
            }
            
            listOfSendItems.RemoveAt(k);
            //refreshListOfItemsID();
        }
      
        private async void MultiSendSendTimerCallback(object state)
        {
            
            if (!isListening)
            {
                closeMultiSendTimer();
                return;
            }
            try
            {
                string sendData = String.Empty;
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    int k = (iter % listOfSendItems.Count);
                    sendData = listOfSendItems[k].SendText;
                    MultiSendListView.SelectedIndex = k;
                    iter++;
                });
                if (sendData == null || sendData.Length == 0)
                    return;
                dataWriteObject = new DataWriter(serialPort.OutputStream);
                await WriteAsync(sendData);
            }
            catch {
                closeMultiSendTimer();
            }
            finally
            {
                if (dataWriteObject != null)
                {
                    dataWriteObject.DetachStream();
                    dataWriteObject = null;
                }
            }
        }

        private void timerDuringCheck(object sender, TextChangedEventArgs e)
        {
            try
            {
                int dur = int.Parse((sender as TextBox).Text);
                if (dur < 20)
                {
                    MainPage.Notify("请填写正确的时间间隔");
                    (sender as TextBox).Text = "20";
                }
            }
            catch
            {
                MainPage.Notify("请填写正确的时间间隔");
                (sender as TextBox).Text = "1000";
            }
        }

        private void MultiSendSwitchToggled(object sender, RoutedEventArgs e)
        {
            if ((sender as ToggleSwitch).IsOn)
            {
                if (!isListening)
                {
                    MainPage.Notify("请保持串口打开状态");
                    (sender as ToggleSwitch).IsOn = false;
                    return;
                }
                dataWriteObject = new DataWriter(serialPort.OutputStream);
                iter = 0;
                multiSendTimer = new Timer(MultiSendSendTimerCallback, null, 0, int.Parse(multisendDurationTextBox.Text));
            }
            else
            {
                closeMultiSendTimer();
            }
        }
        private void closeMultiSendTimer()
        {
            if (multiSendTimer != null)
            {
                multiSendTimer.Dispose();
                multiSendTimer = null;
            }
        }

        private void multiSendChecked(object sender, RoutedEventArgs e)
        {
            if((sender as CheckBox).IsChecked == true)
            {
                MultiSendPannel.Visibility = Visibility.Visible;
                //sendTbx.Visibility = Visibility.Collapsed;
                SingleSendArea.Visibility = Visibility.Collapsed;
                OpenFileBtn.IsEnabled = false;
                CirclSendCbx.IsEnabled = false;
            }
            else
            {
                MultiSendPannel.Visibility = Visibility.Collapsed;
                //sendTbx.Visibility = Visibility.Visible;
                SingleSendArea.Visibility = Visibility.Visible;
                CirclSendCbx.IsEnabled = true;
                OpenFileBtn.IsEnabled = true;
                closeMultiSendTimer();
            }
        }

        private void HyperlinkButton_Click(object sender, RoutedEventArgs e)
        {
            //TextBoxScrollToEnd(recvTbx);
        }

        private void HexModeSendCheck(object sender, RoutedEventArgs e)
        {
            if((sender as CheckBox).IsChecked==true)
            {
                HexModeSend = true;
            }else HexModeSend = false;
        }
        public string getHexString(string str)
        {
            string cell = String.Empty;
            string output = "";
            for (int i = 0; i < str.Length; i++)
            {
                cell = Convert.ToString(str[i], 16);
                if (cell.Length == 1)
                    cell = "0" + cell;
                output += cell.ToUpper() + " ";
            }
            return output;
        }

        private void sendTbx_LostFocus(object sender, RoutedEventArgs e)
        {
            if (HexModeSend == false) return;
            string str = sendTbx.Text;
            str.Trim();
            sendTbx.Text = getHexString(str);
        }

        private void sendTbx_GotFocus(object sender, RoutedEventArgs e)
        {
            if (HexModeSend == false) return;
            string str = sendTbx.Text;
            str.Trim();
            sendTbx.Text = getHexString(str);
        }

        private void sendTbx_PointerMoved(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {

            if (HexModeSend == false) return;
            string str = sendTbx.Text;
            str.Trim();
            sendTbx.Text = getHexString(str);
        }

        private void ClearSendArea_Click(object sender, RoutedEventArgs e)
        {
            sendTbx.Text = "";
        }
    }
}
