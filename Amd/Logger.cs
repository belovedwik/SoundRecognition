using System;
using System.Diagnostics;
using System.IO;

namespace DeltaM.DeltaTell.Amd
{
    public interface Logger
    {
        void Append(string text);
        void Print();
    }

    public enum LogTarget
    {
        Console,
        Database,
        Files,
        EventLog
    }

    public abstract class LogBase{
        protected readonly object lockObj = new object();
        public abstract void Log(string message, bool line = true);
    }

    public class FileLogger : LogBase {

        public string filePath = @"Log.txt";

        public override void Log(string message, bool line = true)
        {
            lock (lockObj)
            {
                
                using (StreamWriter streamWriter = new StreamWriter(filePath, true))
                {
                    if (line)
                        streamWriter.WriteLine(message);
                    else
                        streamWriter.Write(message);
                    streamWriter.Close();
                }
            }
               
        }
    }

    public class DBLogger : LogBase {

        string connectionString = string.Empty;
        public override void Log(string message, bool line = true)
        {
            //Code to log data to the database
            //  lock (lockObj)
            throw new NotImplementedException();
        }
    }

    public class ConsoleLogger : LogBase
    {
        public override void Log(string message, bool line = true)
        {
            if (line)
                Console.WriteLine(message);
            else
                Console.Write(message);
        }
    }

    public class EventLogger : LogBase{
        public override void Log(string message, bool line = true)
        {
            lock (lockObj) {
                EventLog eventLog = new EventLog("");
                eventLog.Source = "IDGEventLog";
                eventLog.WriteEntry(message);
            }
        }
    }

    public static class LogHelper{
        private static LogBase logger = null;
        
        public static void Init(LogTarget target)
        {
            switch (target)
            {
                case LogTarget.Files:
                    logger = new FileLogger();
                break;

                case LogTarget.Database:
                    logger = new DBLogger();
                    break;

                case LogTarget.EventLog:
                    logger = new EventLogger();
                    break;

                case LogTarget.Console:
                    logger = new ConsoleLogger();
                    break;

                default:
                    throw new NotSupportedException();

            }
        }

        public static void Append(string mess)
        {
            logger.Log(mess, false);
        }

        public static void AppendLine(string mess)
        {
            logger.Log(mess);
        }

    }
}
