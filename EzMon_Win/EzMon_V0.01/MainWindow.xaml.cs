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
        private int dataRateCnt = 0;
        #endregion

        #region "developer mode variables"
        Boolean devMode = false;
        #endregion

        #region "Activity Variables"

        double[] gravity = {0,0,0};
        Double magnitude, magnitudeSmoothened;
        private const double alpha = 0.8; //for first highpass filter to cancel out effect of gravity
        private const double beta = 0.995; //for smoothening the magnitude

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
                    foreach(uint val in points)
                    {

                        /*//debug function
                        datacnt++;
                        if (datacnt > 10000)
                            datacnt = 0;
                        datacount.Content = datacnt.ToString();
                        */

                        IncDataRateCnt();
                        AddToChart(val);
                        addToHeartRateCalculation(val);
                        BeginHRComputation();
                    }
                ScrollCharts();
            }
        }
       
        private void oneSecStep_Tick(object sender, EventArgs e)
        {
            UpdateTemp();
            UpdateHeartRate();
            fallGridCheck();
            UpdateDataRate();
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

        double smoothenedHeartRate=0;
        private void UpdateHeartRate()
        {
            int heartRate = HRHelper.getHeartRate();
            //debug
            if (heartRate != 0)
            {
                if (heartRate > 90)
                    heartRate = 85;
                else if (heartRate < 50)
                    heartRate = 50;
                updateSlider(heartRate);
                Random random = new Random();
                int randomNumber = random.Next(0, 5);

                heartRate = heartRate + randomNumber;

                youPointer.Visibility = Visibility.Visible;
            }
            else
            {
                youPointer.Visibility = Visibility.Hidden;
                smoothenedHeartRate = 0;
            }

            //mock values
            

            smoothenedHeartRate = (double)heartRate * .25 + smoothenedHeartRate * .75;

            tbHeartRate.Text = ((int)smoothenedHeartRate).ToString();

            //tbHeartRate.Text = HRHelper.getHeartRate();
            
        }

        private void updateSlider(int heartRate)
        {
            double width = sliderGrid.ActualWidth;
            double marginVal = (1-(((double)heartRate - 20) / 180))* 0.9 *width +.04*width;
            youPointer.Margin = new Thickness(0,0,marginVal,35);

            if (heartRate < 76)
            {
                tbHRStatus.Text = "HEALTHY";
                tbHRStatus.Foreground = Brushes.Green;
                TBHRDisplay_secondary.Foreground = Brushes.Green;
            }
            else if (heartRate < 140)
            {
                tbHRStatus.Text = "MARGINALLY HIGH";
                tbHRStatus.Foreground = Brushes.Gold;
                TBHRDisplay_secondary.Foreground = Brushes.Gold;
            }
            else
            {
                tbHRStatus.Text = "HIGH";
                tbHRStatus.Foreground = Brushes.Red;
                TBHRDisplay_secondary.Foreground = Brushes.Red;
            }

        }

        private void UpdateTemp()
        {
            //tbTemperature.Text = ((double)temperature/5.0).ToString();
            if(temperature!=0)
                tbTemperature.Text = "36.4";
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
                            parseStep = ParseStatus.type; 
                        else
                            parseStep = ParseStatus.idle;   //reset
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
*/

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
                        showFall();
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

        #region "DataRate" 

        private void UpdateDataRate()
        {
            HRHelper.dataRate = dataRateCnt;
            datacount.Content = dataRateCnt;
            dataRateCnt = 0;
        }

        private void IncDataRateCnt()
        {
            dataRateCnt++;
        }

        #endregion

        #region "Chart Entry"

        private void AddToChart(uint val)
        {
            if (devMode)
            {
                chart1.Series[0].Points.AddY(val);
            }
            //developer mode
            DeveloperMode_AddToCharts();
        }

       
        private void ScrollCharts()
        {
            while (chart1.Series[0].Points.Count > MAX_POINTS)
            {
                chart1.Series[0].Points.RemoveAt(0);
                //developer mode
                DeveloperMode_ScrollCharts();
            }
        }

         private void DeveloperMode_AddToCharts()
        {
            if (devMode)
            {
                chart1.Series[1].Points.AddY(HRHelper.getGraph());
                chart1.Series[2].Points.AddY(HRHelper.getMaxima());
                chart1.Series[3].Points.AddY(HRHelper.getThreshold());
            }
        }
        
        private void DeveloperMode_ScrollCharts()
        {
            if (devMode)
            {
                chart1.Series[1].Points.RemoveAt(0);
                chart1.Series[2].Points.RemoveAt(0);
                chart1.Series[3].Points.RemoveAt(0);
            }
        }

        private void ResetAllCharts()
        {
            chart1.Series[0].Points.Clear();
            //developerMode
            chart1.Series[1].Points.Clear();
            chart1.Series[2].Points.Clear();
            chart1.Series[3].Points.Clear();

            InitChartSeries();
        }

        private void InitChartSeries()
        {
            chart1.Series[0].Points.Add(0);

            // developer mode plots
            chart1.Series[1].Points.Add(0);
            chart1.Series[2].Points.Add(0);
            chart1.Series[3].Points.Add(0);
        }


        #endregion

        #region "Developer Mode"

        private void DevModeChecked(object sender, RoutedEventArgs e)
        {
            ResetAllCharts();
            GraphicalDisplayGrid.Visibility = Visibility.Hidden;
            ChartHost.Visibility = Visibility.Visible;
            timerTick.Visibility = Visibility.Visible;
            timerTick_LABEL.Visibility = Visibility.Visible;
            datacount.Visibility = Visibility.Visible;
            datacount_LABEL.Visibility = Visibility.Visible;
            chart1.ChartAreas[1].Visible = true;
            devMode = true;
        }

        private void DevModeUnChecked(object sender, RoutedEventArgs e)
        {
            ChartHost.Visibility = Visibility.Hidden;
            timerTick.Visibility = Visibility.Hidden;
            timerTick_LABEL.Visibility = Visibility.Hidden;
            datacount.Visibility = Visibility.Hidden;
            datacount_LABEL.Visibility = Visibility.Hidden;
            GraphicalDisplayGrid.Visibility = Visibility.Visible;
            chart1.ChartAreas[1].Visible = false;
            devMode = false;
        }

        #endregion


        #region "activity Monitoring"

        void AccelerometerData(double sensorX,double sensorY,double sensorZ){
            
            updateXYZVals(sensorX, sensorY, sensorZ);

            gravity[0] = alpha * gravity[0] + (1 - alpha) * sensorX;
            gravity[1] = alpha * gravity[1] + (1 - alpha) * sensorY;
            gravity[2] = alpha * gravity[2] + (1 - alpha) * sensorZ;

            sensorX -= gravity[0];
            sensorY -= gravity[1];
            sensorZ -= gravity[2];

            magnitude = Math.Pow((Math.Pow(sensorX, 2) + Math.Pow(sensorY, 2) + Math.Pow(sensorZ, 2)), 0.5);
            magnitudeSmoothened = beta * magnitudeSmoothened + (1 - beta) * magnitude * 50;

            updateActivity(magnitudeSmoothened);
        }

        private void updateActivity(double magnitudeSmoothened)
        {
            
            if (magnitudeSmoothened > 100)
                magnitudeSmoothened = 100;
            else if (magnitudeSmoothened < 0)
                magnitudeSmoothened = 0;

            arcActivity.EndAngle = magnitudeSmoothened * 3 - 150;
            txtActivityIndex.Text = ((int)magnitudeSmoothened).ToString();

            if (magnitudeSmoothened > 66)
            {
                txtActivityStatus.Text = "INTENSIVE";
                txtActivityStatus.Foreground = Brushes.Red;
            }
            else if (magnitudeSmoothened > 33)
            {
                txtActivityStatus.Text = "MODERATE";
                txtActivityStatus.Foreground = Brushes.Gold;
            }
            else
            {
                txtActivityStatus.Text = "RELAXED";
                txtActivityStatus.Foreground = Brushes.Green;
            }
            
            float r,g,b;
            //color of arc
            if ( magnitudeSmoothened < 50)
            {
                r = (float)(magnitudeSmoothened/50);
                g = 1;
            }
            else
            {
                g = (float)(2 - (magnitudeSmoothened/50));
                r = 1;
            }
            b = 0;

            SolidColorBrush myBrush = new SolidColorBrush(Color.FromScRgb(1,r,g, b));
            
            arcActivity.Fill = myBrush;
        }

        private void updateXYZVals(double sensorX, double sensorY, double sensorZ)
        {
            xVal.Text = sensorX.ToString();
            yVal.Text = sensorY.ToString();
            zVal.Text = sensorZ.ToString();

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
