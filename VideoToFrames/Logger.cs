using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace VideoToFrames
{
    internal static class Logger
    {
        private static StringBuilder _log = new StringBuilder();

        public static void WriteLine(string log)
        {
            Debug.WriteLine(log);
            Console.WriteLine(log);
            _log.AppendLine(log);
        }

        public static void Write(string log)
        {
            Debug.Write(log);
            Console.Write(log);
            _log.Append(log);
        }

        public static void WriteLine()
        {
            WriteLine(String.Empty);
        }

        public static void CreateLogFile(string logfile)
        {
            using (StreamWriter writer = File.CreateText(logfile))
            {
                writer.Write(_log.ToString());
            }
        }
    }
}