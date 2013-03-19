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
        private int datacnt = 0;
        private int fallCounter = 0;
        private System.Windows.Threading.DispatcherTimer timer = new System.Windows.Threading.DispatcherTimer();
        private System.Windows.Threading.DispatcherTimer oneSecStep = new System.Windows.Threading.DispatcherTimer();
        #endregion

        #region Plot data variables
        private System.Collections.ArrayList  points = new System.Collections.ArrayList() ;

        private int temperature = 0;
        #endregion

        #region parsing variables

        enum ParseStatus            //following PacketformatV2
        {
            idle,
            header2,
            length,
            type,
            alert,
            contData_subtype,
            contDataPayload,
            singleData_subtype,
            singleDataPayload
        }
        ParseStatus parseStep = ParseStatus.idle;
        #endregion

        #region heart rate vars
        HeartRateHelper HRHelper = HeartRateHelper.Instance;
        #endregion

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
            chart1.Series[0].Points.Add(0);
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
                    HRHelper.ResetHeartRate();
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
                    foreach(uint val in points){

                        /*//debug function
                        datacnt++;
                        if (datacnt > 10000)
                            datacnt = 0;
                        datacount.Content = datacnt.ToString();
                        */

                        chart1.Series[0].Points.AddY(val);
                        addToHeartRateCalculation(val);
                        BeginHRComputation();

                        //debug
                        chart1.Series[1].Points.AddY(HRHelper.getGraph());
                        chart1.Series[2].Points.AddY(HRHelper.getMaxima());
                        chart1.Series[3].Points.AddY(HRHelper.getThreshold());
                    }
                while (chart1.Series[0].Points.Count > MAX_POINTS)
                {
                    chart1.Series[0].Points.RemoveAt(0);
                    //debug
                    chart1.Series[1].Points.RemoveAt(0);
                    chart1.Series[2].Points.RemoveAt(0);
                    chart1.Series[3].Points.RemoveAt(0);
                }
                
            }
        }

        private void oneSecStep_Tick(object sender, EventArgs e)
        {
            UpdateTemp();
            UpdateHeartRate();
            fallGridCheck();
        }

        private void fallGridCheck()
        {
            if (fallGrid.Visibility == Visibility.Visible)
            {
                fallCounter++;
                if (fallCounter > 3)
                    hideFall();
            }
        }

        private void UpdateHeartRate()
        {
            tbHeartRate.Text = HRHelper.getHeartRate().ToString();
        }

        private void UpdateTemp()
        {
            tbTemperature.Text = (temperature+3).ToString();
            //tbTemperature.Text = "34";
        }

        private void showFall()
        {
            fallGrid.Visibility = Visibility.Visible;
            fallCounter = 0;
        }
        
        private void hideFall()
        {
            //debug
            fallGrid.Visibility = Visibility.Collapsed;
        }
        #endregion

        #region parsing functions

        private void ParseData()
        {
            int byteCount = serialPort.BytesToRead;
            uint val,payloadLength;
            Byte tempByte;
            
            points.Clear();
            parseStep = ParseStatus.idle;       //reset
            while (byteCount > 0)
            {
                switch (parseStep)
                {
                    case ParseStatus.idle:
                        tempByte = (Byte)serialPort.ReadByte();
                        byteCount--;
                        if (tempByte == 0xFF)
                        {
                            parseStep = ParseStatus.header2;
                            //debugText.Text += "Hi";
                        }
                        break;

                    case ParseStatus.header2:
                        tempByte = (Byte)serialPort.ReadByte();
                        byteCount--;
                        if (tempByte== 0xFE)
                            parseStep = ParseStatus.length; 
                        else
                            parseStep = ParseStatus.idle;   //reset
                        break;

                    case ParseStatus.length:
                        payloadLength = (Byte)serialPort.ReadByte();
                        byteCount--;
                        parseStep = ParseStatus.type;
                        break;

                    case ParseStatus.type:
                        tempByte = (Byte)serialPort.ReadByte();
                        byteCount--;
                        switch (tempByte)
                        {
                            case 0x02:
                                //Alerts
                                parseStep = ParseStatus.alert;
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
                        showFall();
                        parseStep = ParseStatus.idle;       //reset
                        break;

                    case ParseStatus.contData_subtype:
                        parseStep = ParseStatus.idle;       //reset
                        break;

                    case ParseStatus.contDataPayload:
                        val = (uint)serialPort.ReadByte();
                        val = val * 256 + (uint)serialPort.ReadByte();
                        points.Add(val);
                        byteCount--;
                        //debugText.Text +=val +"\n";

                        parseStep = ParseStatus.idle;       //reset
                        break;

                    case ParseStatus.singleData_subtype:
                        tempByte = (Byte)serialPort.ReadByte();
                        byteCount--;
                        switch (tempByte)
                        {
                            case 0x00:
                                tempByte = (Byte)serialPort.ReadByte();
                                byteCount--;
                                temperature = (int)tempByte;
                                break;
                            default:
                                parseStep = ParseStatus.idle;
                                break;
                        }
                        parseStep = ParseStatus.idle;
                        break;

                    default:
                        parseStep = ParseStatus.idle;
                        break;

                }
            }
        }

#endregion

        #region Heart rate functions

        private void addToHeartRateCalculation(uint val)
        {
            HRHelper.newVal = val;
        }

        private void BeginHRComputation(){

            Action<object> hrAction = (object obj) =>
            {
                HRHelper.setHeartRate();
            };

            Task hrTask = Task.Factory.StartNew(hrAction, "alpha");
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





    }
}
