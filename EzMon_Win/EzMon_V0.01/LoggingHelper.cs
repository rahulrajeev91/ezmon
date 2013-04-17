using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace EzMon_V0._01
{
    class LoggingHelper
    {
        private const string path = "val.csv";
        public void writeToFile(uint ppg, double x, double y, double z)
        {
            StreamWriter sw = new StreamWriter(path);

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
            sw.WriteLine(ppg.ToString() + ',' + x.ToString() + ',' + y.ToString() + ',' + z.ToString() + '\n');

        }
    }
}
