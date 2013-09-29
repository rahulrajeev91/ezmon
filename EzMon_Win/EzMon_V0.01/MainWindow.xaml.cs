using System;
using System.IO.Ports;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading.Tasks;

namespace EzMon_V0._01
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

#region variables

        #region Chart Variables
        //initialization parameters for the chartControl
        private const int CHART_INIT_HEIGHT = 421;
        private const int CHART_INIT_WIDTH = 568;

        private const int MAX_POINTS = 300;

        private ChartControl myChartControl= new ChartControl() ;
        private System.Windows.Forms.DataVisualization.Charting.Chart chart1 = new System.Windows.Forms.DataVisualization.Charting.Chart();
        #endregion

        #region connection variables

        private System.IO.Ports.SerialPort serialPort = new SerialPort();
        private const int BAUD_RATE = 115200;

        enum connectionStatus{
            connected,
            disconnected
        }
        connectionStatus connection = connectionStatus.disconnected ;

        #endregion

        #region timer variables
        private int counter = 0;
        private System.Windows.Threading.DispatcherTimer timer = new System.Windows.Threading.DispatcherTimer();
        private System.Windows.Threading.DispatcherTimer oneSecStep = new System.Windows.Threading.DispatcherTimer();
        #endregion

        #region Plot data variables
        private System.Collections.ArrayList  points = new System.Collections.ArrayList() ;

        #endregion

        #region parsing variables

        enum ParseStatus            //following PacketformatV2
        {
            idle,
            header2,
            data
        }
        ParseStatus parseStep = ParseStatus.idle;
        #endregion

        private int dataRateCnt = 0;


#endregion

        #region initialization functions

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InitChart();
            Reset();        //reset and refresh all parameters to default values at the start of execution

            InitializeTimer();
                        
            //InputMockTestVals();
        }

        private void InitializeTimer()
        {
            timer.Tick += new EventHandler(timer_Tick);
            timer.Interval = new TimeSpan(0,0,0,0,10);
            timer.Start();
            oneSecStep.Tick += new EventHandler(oneSecStep_Tick);
            oneSecStep.Interval = new TimeSpan(0, 0, 1);
            oneSecStep.Start();
        }

        private void InitChart()
        {
            ChartHost.Child = myChartControl;
            chart1 = myChartControl.chartDesign;
            chart1.Height = CHART_INIT_HEIGHT;
            chart1.Width = CHART_INIT_WIDTH;

            ResetAllCharts();

        }

        //reset and refresh all parameters to default values at the start of execution
        private void Reset()    
        {
            UpdateComPortList();
            resetCounter();
        }

        private void resetCounter()
        {
            counter = 0;
        }

        private void UpdateComPortList()
        {
            cbPort.Items.Clear();
            cbPort.Items.Add("None"); 
            foreach (String s in System.IO.Ports.SerialPort.GetPortNames())
            {
                cbPort.Items.Add(s);
            }
            cbPort.SelectedIndex = 0;
        }

#endregion

        #region user event trigers

        private void ConnectDisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (connection == connectionStatus.disconnected)
            {
                if (Connect()){
                    ConnectDisconnectButton.Content = "DISCONNECT";
                }
                else
                    MessageBox.Show("Connection failed. Could not open COM port");
            }
            else if (connection == connectionStatus.connected)
            {
                if (Disconnect())
                    ConnectDisconnectButton.Content = "CONNECT";
                else
                    MessageBox.Show("Disconnect failed.  Could not close COM port");
            }
            else
            {
                //undefined state
                connection = connectionStatus.disconnected;             //reset
            }

        }

        private void cbPort_DropDownOpened(object sender, EventArgs e)
        {
            UpdateComPortList();
        }

#endregion

        #region connection functions

        private Boolean Disconnect()
        {
            if (comPortClose())
            {
                connection = connectionStatus.disconnected ;
                return true;
            }
            else
                return false;
        }

        private bool comPortClose()
        {
            if (serialPort.IsOpen)
            {
                try
                {
                    serialPort.Close();
                }
                catch (Exception)
                {
                    return false;
                }
                return true;
            }
            else
                return true;
        }

        private Boolean Connect()
        {
            if (ComPortOpen())
            {
                if (serialPort.IsOpen)
                {
                    connection = connectionStatus.connected;
                    return true;
                }
            }
            return false;
        }

        private bool ComPortOpen()
        {
            string portName = cbPort.Text;
            if (portName.CompareTo("None") == 0)
                return false;
            serialPort.PortName = portName;
            serialPort.BaudRate = BAUD_RATE;

            try
            {
                serialPort.Open();
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

#endregion

        #region timer functions

        private void timer_Tick(object sender, EventArgs e)
        {
            counter++;
            if (counter >= 10000)
                //prevent overflow
                resetCounter();
            timerTick.Content = counter;
            if (connection == connectionStatus.connected)
            {
                ParseData();
                if (points.Count>0)
                    foreach(int val in points)
                    {
                        tbPoints.Text = val.ToString();
                        tbmv.Text = (((double)val) * 0.03125).ToString();
                        IncDataRateCnt();
                        AddToChart(val);
                    }
                ScrollCharts();
            }
        }
       
        private void oneSecStep_Tick(object sender, EventArgs e)
        {
            UpdateDataRate();
        }

        #endregion

        #region parsing functions

        private void ParseData()
        {
            int byteCount = serialPort.BytesToRead;
            uint val;
            Byte tempByte;
            
            points.Clear();
            parseStep = ParseStatus.idle;       //reset
            while (byteCount > 0)
            {
                switch (parseStep)
                {
                    case ParseStatus.idle:
                        try
                        {
                            tempByte = (Byte)serialPort.ReadByte();
                        }
                        catch (Exception)
                        {
                            parseStep = ParseStatus.idle;
                            break;
                        }
                        byteCount--;
                        if (tempByte == 0xFF)
                        {
                            parseStep = ParseStatus.header2;
                            //debugText.Text += "Hi";
                        }
                        break;

                    case ParseStatus.header2:
                        try
                        {
                            tempByte = (Byte)serialPort.ReadByte();
                        }
                        catch (Exception)
                        {
                            parseStep = ParseStatus.idle;
                            break;
                        }
                        byteCount--;
                        if (tempByte== 0xFE)
                            parseStep = ParseStatus.data; 
                        else
                            parseStep = ParseStatus.idle;   //reset
                        break;
                    case ParseStatus.data:
                        try
                        {
                            val = (uint)serialPort.ReadByte();
                            val = val * 256 + (uint)serialPort.ReadByte();
                            byteCount -= 2;
                        }
                        catch (Exception)
                        {
                            parseStep = ParseStatus.idle;
                            break;
                        }
                        points.Add(val);    //add to value list
                        break;

                    /*                    case ParseStatus.length:
                                            try
                                            {
                                                payloadLength = (Byte)serialPort.ReadByte();
                                            }
                                            catch (Exception)
                                            {
                                                parseStep = ParseStatus.idle;
                                                break;
                                            }
                                            byteCount--;
                                            parseStep = ParseStatus.type;
                                            break;


                                        case ParseStatus.type:
                                            try
                                            {
                                                tempByte = (Byte)serialPort.ReadByte();
                                            }
                                            catch (Exception)
                                            {
                                                parseStep = ParseStatus.idle;
                                                break;
                                            }
                                            byteCount--;
                                            switch (tempByte)
                                            {
                                                case 0x02:
                                                    //Alerts
                                                    //parseStep = ParseStatus.alert;
                                                    parseStep = ParseStatus.idle;   //debug - no alerts
                                                    break;
                                                case 0x03:
                                                    //Continious Data
                                                    //parseStep = ParseStatus.contData_subtype;
                                                    parseStep = ParseStatus.contDataPayload;
                                                    break;
                                                case 0x04:
                                                    //one-shot data
                                                    parseStep = ParseStatus.singleData_subtype;
                                                    break;
                                                default:
                                                    parseStep = ParseStatus.idle;   //reset
                                                    break;
                                            }
                                            break;

                                        case ParseStatus.alert:
                                            //debugText.Text += "Alert!\n";
                                            parseStep = ParseStatus.idle;       //reset
                                            break;

                                        case ParseStatus.contData_subtype:
                                            parseStep = ParseStatus.idle;       //reset
                                            break;

                                        case ParseStatus.contDataPayload:
                                            try
                                            {
                                                val = (uint)serialPort.ReadByte();
                                                val = val * 256 + (uint)serialPort.ReadByte();
                                            }
                                            catch (Exception)
                                            {
                                                parseStep = ParseStatus.idle;
                                                break;
                                            }
                                            points.Add(val);    //add to PPG value list
                                            try
                                            {
                                                AccelerometerData((double)((int)serialPort.ReadByte() - 128) / 64.0, (double)((int)serialPort.ReadByte() - 128) / 64.0, (double)((int)serialPort.ReadByte() - 128) / 64.0);
                                            }
                                            catch (Exception)
                                            {
                                                parseStep = ParseStatus.idle;
                                                break;
                                            }
                                            byteCount-=5;
                                            //debugText.Text +=val +"\n";

                                            parseStep = ParseStatus.idle;       //reset
                                            break;

                                        case ParseStatus.singleData_subtype:
                                            try
                                            {
                                                tempByte = (Byte)serialPort.ReadByte();
                                            }
                                            catch (Exception)
                                            {
                                                parseStep = ParseStatus.idle;
                                                break;
                                            }
                                            byteCount--;
                                            switch (tempByte)
                                            {
                                                case 0x00:
                                                    try
                                                    {
                                                        tempByte = (Byte)serialPort.ReadByte();
                                                    }
                                                    catch (Exception)
                                                    {
                                                        parseStep = ParseStatus.idle;
                                                        break;
                                                    }
                                                    byteCount--;
                                                    temperature = (int)tempByte;    //set the temperature
                                                    break;
                                                default:
                                                    parseStep = ParseStatus.idle;
                                                    break;
                                            }
                                            parseStep = ParseStatus.idle;
                                            break;
                    */
                    default:
                        parseStep = ParseStatus.idle;
                        break;

                }
            }
        }

#endregion

        #region test function/ method stubs

        private void InputMockTestVals()
        {
            for (double i = 0; i < 100; i+=0.1)
            {
                chart1.Series[0].Points.Add(Math.Sin(i));
            }
        }

        #endregion

        #region "DataRate" 

        private void UpdateDataRate()
        {
            datacount.Content = dataRateCnt;
            dataRateCnt = 0;
        }

        private void IncDataRateCnt()
        {
            dataRateCnt++;
        }

        #endregion

        #region "Chart Entry"

        private void AddToChart(int val)
        {
            chart1.Series[0].Points.AddY(val);
        }


        private void ScrollCharts()
        {
            while (chart1.Series[0].Points.Count > MAX_POINTS)
            {
                chart1.Series[0].Points.RemoveAt(0);
            }
        }

        private void ResetAllCharts()
        {
            chart1.Series[0].Points.Clear();
            InitChartSeries();
        }

        private void InitChartSeries()
        {
            chart1.Series[0].Points.Add(0);
        }


        #endregion

       
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Disconnect();
        }


        static string GetIntBinaryString(int n)
        {
            char[] b = new char[32];
            int pos = 31;
            int i = 0;

            while (i < 32)
            {
                if ((n & (1 << i)) != 0)
                {
                    b[pos] = '1';
                }
                else
                {
                    b[pos] = '0';
                }
                pos--;
                i++;
            }
            return new string(b);
        }

        private void btZoomReset_Click(object sender, RoutedEventArgs e)
        {
            chart1.ChartAreas[0].AxisY.Maximum = 33000;
            chart1.ChartAreas[0].AxisY.Minimum = 0;
        }

        private void btZoomSet_Click(object sender, RoutedEventArgs e)
        {
        }

        private void btStore_Click(object sender, RoutedEventArgs e)
        {

        }

    }
}
