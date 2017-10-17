using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LogWriterAPI;

namespace TestClientSimulator
{
    public sealed class Logger
    {
        private static volatile LogWriter instance;
        private static object syncRoot = new Object();

        private Logger() { }

        public static LogWriter Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (syncRoot)
                    {
                        if (instance == null) instance = new LogWriter();
                    }
                }
                return instance;
            }
        }
    }
}
