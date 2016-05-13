using System;
using System.Diagnostics;

namespace VideoToFrames
{
    internal class Logger
    {
        public static void WriteLine(string log)
        {
            Debug.WriteLine(log);
            Console.WriteLine(log);
        }

        public static void Write(string log)
        {
            Debug.Write(log);
            Console.Write(log);
        }

        public static void WriteLine()
        {
            WriteLine(String.Empty);
        }
    }
}