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
        double smoothPointVAL = 0;
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

        private int OutPutVal = 0;

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
                    ResetAllCharts();
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

        private void btZoomReset_Click(object sender, RoutedEventArgs e)
        {
            chart1.ChartAreas[0].AxisY.Maximum = 33000;
            chart1.ChartAreas[0].AxisY.Minimum = 0;
            tbZoomLower.Text = "Lower Val";
            tbZoomUpper.Text = "Higher Val";
            ZoomValsCorrect();
        }

        private void btAutoZoomSet_Click(object sender, RoutedEventArgs e)
        {
            if (OutPutVal != 0)
            {
                chart1.ChartAreas[0].AxisY.Maximum = OutPutVal + 300;
                chart1.ChartAreas[0].AxisY.Minimum = OutPutVal - 300;
            }
        }

        private void btZoomSet_Click(object sender, RoutedEventArgs e)
        {
            int minVal = 0;
            int maxVal = 0;
            try
            {
                minVal = Int32.Parse(tbZoomLower.Text);
                maxVal = Int32.Parse(tbZoomUpper.Text);
            }
            catch (Exception)
            {
                ZoomValsError();
            }
            if (minVal >= maxVal)
            {
                ZoomValsError();
            }
            else
            {
                //if vals are valid
                ZoomValsCorrect();
                chart1.ChartAreas[0].AxisY.Maximum = maxVal;
                chart1.ChartAreas[0].AxisY.Minimum = minVal;
            }


        }

        private void ZoomValsError()
        {
            tbZoomLower.BorderBrush = Brushes.Red;
            tbZoomUpper.BorderBrush = Brushes.Red;
        }

        private void ZoomValsCorrect()
        {
            tbZoomLower.BorderBrush = Brushes.Gray;
            tbZoomUpper.BorderBrush = Brushes.Gray;
        }

        private void btStore_Click(object sender, RoutedEventArgs e)
        {
            tbStorage.Text += OutPutVal.ToString() + "\n";
        }

        private void BtClear_Click(object sender, RoutedEventArgs e)
        {
            ResetAllCharts();
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
                        OutPutVal = LowPassFilter(val);
                        tbPoints.Text = OutPutVal.ToString();
                        tbmv.Text = (((double)OutPutVal) * 0.03125).ToString();
                        IncDataRateCnt();
                        AddToChart(val, OutPutVal);
                    }
                ScrollCharts();
            }
        }

        private int LowPassFilter(int val)
        {
            smoothPointVAL = 0.98 * smoothPointVAL + 0.02 * (double)val;
            return (int)smoothPointVAL;
        }
       
        private void oneSecStep_Tick(object sender, EventArgs e)
        {
            UpdateDataRate();
        }

        #endregion

        #region parsing functions

        private void ParseData()
        {
            int byteCount = 0;
            try
            {
                byteCount = serialPort.BytesToRead;
            }
            catch (Exception)
            {
            }
            int val;
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
                            val = (int)serialPort.ReadByte();
                            val = val * 256 + (int)serialPort.ReadByte();
                            byteCount -= 2;
                        }
                        catch (Exception)
                        {
                            parseStep = ParseStatus.idle;
                            break;
                        }
                        points.Add(val);    //add to value list
                        break;

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

        private void AddToChart(int val, int SmoothVal)
        {
            chart1.Series[0].Points.AddY(val);
            chart1.Series[1].Points.AddY(SmoothVal);
        }


        private void ScrollCharts()
        {
            while (chart1.Series[0].Points.Count > MAX_POINTS)
            {
                chart1.Series[0].Points.RemoveAt(0);
            }
            while (chart1.Series[1].Points.Count > MAX_POINTS)
            {
                chart1.Series[1].Points.RemoveAt(0);
            }
        }

        private void ResetAllCharts()
        {
            chart1.Series[0].Points.Clear();
            chart1.Series[1].Points.Clear();
            InitChartSeries();
        }

        private void InitChartSeries()
        {
            chart1.Series[0].Points.Add(0);
            chart1.Series[1].Points.Add(0);
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

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S||e.Key == Key.Space)
                tbStorage.Text+= tbPoints.Text +'\n';
            if (e.Key == Key.C)
                ResetAllCharts();
            // open form
        }

        private void cbOriginalGraph_Checked(object sender, RoutedEventArgs e)
        {
                chart1.Series[0].Enabled = true;
        }

        private void cbOriginalGraph_Unchecked(object sender, RoutedEventArgs e)
        {
            chart1.Series[0].Enabled = false;
        }
    }
}
