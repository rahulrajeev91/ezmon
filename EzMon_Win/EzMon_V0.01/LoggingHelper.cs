using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace EzMon_V0._01
{
    class LoggingHelper
    {
        private const string path = "test.csv";
        StreamWriter sw;

        public void init()
        {
            sw = new StreamWriter(path);

            if (!File.Exists(path))
            {
                // Create a file to write to. 
                try
                {

                    sw = File.CreateText(path);
                }
                catch (Exception)
                {
                }
            }
        }

        public void writeToFile(uint ppg, double x, double y, double z)
        { 
            sw.WriteLine(ppg.ToString() + ',' + x.ToString() + ',' + y.ToString() + ',' + z.ToString());
        }

        public void close()
        {
            sw.Close();
        }
    }
}
