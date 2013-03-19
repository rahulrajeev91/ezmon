using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EzMon_V0._01
{
    public class HeartRateHelper
    {
#region variables

        private static HeartRateHelper heartRateHelperInstance = new HeartRateHelper();

        public uint newVal = 0;
        int heartRate = 0;
        private int thresholdResetCounter = 0;
        //===============================================
        // Baseline correction variables
        //===============================================
        private double baselineAvg = 0;
        private const double BLFACTOR = 0.8;
        private double valAfterBLCorrection = 0;
        //===============================================
        // HighPass filter variables
        //===============================================
        private double hpSmoothed = 0;
        private const double HPSMOOTHINGFACTOR = 0.1;
        private double valAfterHPF = 0;
        //===============================================
        // Lowpass filter variables
        //===============================================
        private double lpSmoothed = 0;
        private const double LPSMOOTHINGFACTOR = 100;
        private double valAfterLPF = 0;
        //===============================================
        //GeneralFilter and Threshold variables
        //===============================================
        private double threshold = 0;
        private double valAfterFilter = 0;
        private double localMaxima  = 1;
        private int THRESHOLD_SAMPLING_WIDTH = 1000;
        private const double FILTER_SCALING_FACTOR = 1;       //TO BE DIVIDED
        private const double MAXIMA_RESET_FACTOR  = 3;        //TO BE DIVIDED
        private const double THRESHOLD_BALANCING_FACTOR  = 0.5;
        private const double THRESHOLD_CONVERSION_FACTOR = 0.3;
        //===============================================
        // HR calculation variables
        //===============================================
        private double valAfterThreshold = 0;
        private Boolean peakLock= false;
        private int peakCounter = 0;
        private int interPeakCounter = 0;
        private System.Collections.ArrayList hrArray = new System.Collections.ArrayList();
        private const double DATARATE = 100;            //readings per second

#endregion

        private HeartRateHelper() {}

        public static HeartRateHelper Instance
        {
            get{
                return heartRateHelperInstance;
            }
        }

        public void setHeartRate()
        {
            valAfterFilter = FilterInputToIsolatePeaks(Convert.ToDouble(newVal));
            valAfterThreshold = CutOffUsingThreshold(Convert.ToDouble(valAfterFilter));
            DetectRPeak(valAfterThreshold);
            if (getAvgHR() > 160)
                heartRate = 160;
            else
                heartRate = Convert.ToInt32(getAvgHR());
        }

        public void ResetHeartRate(){
            hrArray.Clear();
        }

        public int getHeartRate()
        {
            return heartRate;
        }

        public double getGraph()
        {
            return valAfterThreshold;
        }

        public double getThreshold()
        {
            return threshold;
        }

        public double getMaxima()
        {
            return localMaxima;
        }

#region Signal Processing Functions

        private double FilterInputToIsolatePeaks(double myVal){
            valAfterBLCorrection = BaselineCorrection_2ndLevel(newVal);
            //valAfterLPF = LowPassFilter(valAfterBLCorrection);
            //valAfterHPF = HighPassFilter(valAfterLPF);
            //return valAfterHPF / FILTER_SCALING_FACTOR;
            return valAfterBLCorrection;
        }

        private double BaselineCorrection_2ndLevel(double myVal){
            baselineAvg = BLFACTOR * baselineAvg + (1 - BLFACTOR) * myVal;
            return myVal - baselineAvg;
        }

        private double HighPassFilter(double myVal){
            hpSmoothed = HPSMOOTHINGFACTOR * hpSmoothed + (1 - HPSMOOTHINGFACTOR) * myVal;
            return myVal - hpSmoothed;
        }

        private double LowPassFilter(double myVal){
            lpSmoothed += ((myVal * myVal * myVal) - lpSmoothed) / LPSMOOTHINGFACTOR;
            return lpSmoothed;
        }

#endregion

#region Cleaning Signal Using Threshold
    private double CutOffUsingThreshold(double myVal){

        UpdateLocalMaxima(myVal);
        UpdateTimerAndResetValues();

        if(myVal > threshold)
            return myVal;
        else
            return 0;
    }

    private void UpdateLocalMaxima(double myVal){
        if (myVal > localMaxima && myVal < 200 * localMaxima)
            localMaxima = myVal;
    }

    private void UpdateTimerAndResetValues(){
        thresholdResetCounter += 1;
        if (thresholdResetCounter > THRESHOLD_SAMPLING_WIDTH)
            ResetAndUpdateAllValues();
    }

    private void ResetAndUpdateAllValues(){
        thresholdResetCounter = 0;
        UpdateThreshold();
        ResetLocalMaxima();
    }

    private void UpdateThreshold(){
        if(threshold > localMaxima ) //after unavoidable huge spikes
            threshold = 0.4 * localMaxima;
        else
            threshold = THRESHOLD_BALANCING_FACTOR * threshold + (1 - THRESHOLD_BALANCING_FACTOR) * THRESHOLD_CONVERSION_FACTOR * localMaxima;
    }

    private void ResetLocalMaxima(){
        localMaxima /= MAXIMA_RESET_FACTOR;
        if (localMaxima < 0.1)
            localMaxima = 1;     //to prevent value from dying to 0
    }

#endregion

#region HeartRate Computation Functions

    private void DetectRPeak(double myVal){

        interPeakCounter += 1;
        if (valAfterThreshold > threshold){  //during peak
            if(!peakLock){    //if start of peak
                if (interPeakCounter < (0.4 * ConvertHeartRateToCount(getAvgHR())) && hrArray.Count > 10)
                    //false peak
                    ;
                else
                    //real r peak
                    peakLock = true;
            }
            peakCounter += 1;
        }else{
            if (peakLock){   //end of peak
                peakLock = false;
                if (/*peakCounter < 10*/ true) //if real peak
                    //HeartRate = Convert.ToInt64(ConvertCountToHeartRate(interPeakCounter))
                    AddHRToArray(ConvertCountToHeartRate(interPeakCounter+peakCounter));
                else
                    //ignore the peak
                    ;
                interPeakCounter = 0;
                peakCounter = 0;
            }
        }
    }

    private void AddHRToArray(double myVal){
        double presentHeartRate = getAvgHR();
        if (myVal >= 10 && myVal <= 240){           //only possible value range

            if ((myVal > 0.4 * presentHeartRate && myVal < 2.5 * presentHeartRate) || (hrArray.Count < 10))//to ignore false positives
            {
                hrArray.Add(myVal);
                if (hrArray.Count > 20)
                    hrArray.RemoveAt(0);
            }
        }
    }

    private double ConvertCountToHeartRate(int myVal){
        if (myVal > 0)
            return (60 * DATARATE) / myVal;
        else
            return 0;
    }

    private double ConvertHeartRateToCount(double myVal){
        if (myVal > 0)
            return (60 * DATARATE) / myVal;
        else
            return 0;
    }

    private double getAvgHR(){

        if (hrArray.Count > 5){
            double total = 0;
            int i = 0;
            for (i = 0; i < hrArray.Count; i++)
                try
                {
                    total += (double)hrArray[i];
                }
                catch (Exception)
                {
                }

            double avg =  total / hrArray.Count;
            if (avg > 200)
                return 200;
            else return avg;
        }
        return 0;
    }
#endregion


    }
}
