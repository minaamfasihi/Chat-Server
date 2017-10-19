using System;
using System.IO;
using System.Threading;
using System.Collections;
using System.Collections.Generic;

namespace LogWriterAPI
{
    public class LogWriter
    {
        private static Queue<string> logs = new Queue<string>();
        private static AutoResetEvent worker = new AutoResetEvent(false);
        private static FileStream file = null;

        public LogWriter()
        {
        }

        public void Log(string msg)
        {
            try
            {
                lock (logs)
                {
                    logs.Enqueue(msg);
                    worker.Set();
                }
            }
            catch (Exception e)
            {
                //Console.WriteLine("Cannot write logs, exiting with error: {0}", e.ToString());
            }
        }

        public void WriteToFile(string fileName)
        {
            while (true)
            {
                worker.WaitOne();
                try
                {
                    Queue<string> _queue;
                    lock (logs)
                    {
                        _queue = new Queue<string>(logs);
                        logs.Clear();
                    }
                    if (_queue.Count != 0)
                    {
                        using (file = File.Exists(fileName) ? File.Open(fileName, FileMode.Append) : File.Open(fileName, FileMode.CreateNew))
                        using (var stream = new StreamWriter(file))
                        {
                            while (logs.Count != 0)
                            {
                                string msg = logs.Dequeue();
                                stream.WriteLine(msg);
                            }
                        }
                    }
                }

                catch (Exception e)
                {
                    //Console.WriteLine("Cannot write logs, exiting with error: {0}", e.ToString());
                }
            }
        }
    }
}