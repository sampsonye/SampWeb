using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SampWeb.Logger
{
    public class TextLogger:ILogger
    {
        private readonly string logDir;
        private readonly string logPath;

        public TextLogger()
        {
            logDir = Path.Combine(Environment.CurrentDirectory, "Log");
            logPath = Path.Combine(logDir, "SampWeb.log");
        }

        public void Log(LogLevel level, Exception exception, string message, params object[] param)
        {
            var stackFrame = new StackFrame(1);
            var methodName = stackFrame.GetMethod().Name;
           // var fileName = stackFrame.GetFileName();
            var lineNumber = stackFrame.GetFileLineNumber();
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }
            var content = string.Format("{5}:{0} - {1} -{2}行\r\n{3}\r\n{4}",
                DateTime.Now.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"),
                methodName,
                lineNumber,
                message == null ? "" : string.Format(message, param),
                exception == null ? "" : exception.Message + exception.StackTrace,
                level.ToString()
                );
            File.AppendAllText(logPath,content,Encoding.UTF8);
        }

       
    }

    public static class LoggerExtension
    {
        public static void Debug(this ILogger logger, Exception exception, string message, params object[] param)
        {
#if DEBUG
            logger.Log(LogLevel.Debug, exception,message,param);
#endif
        }

        public static void Debug(this ILogger logger, string message, params object[] param)
        {
            Debug(logger,null,message,param);
        }

        public static void Information(this ILogger logger, Exception exception, string message, params object[] param)
        {
            logger.Log(LogLevel.Information, exception, message, param);
        }

        public static void Information(this ILogger logger, string message, params object[] param)
        {
            Information(logger, null, message, param);
        }

        public static void Warning(this ILogger logger, Exception exception, string message, params object[] param)
        {
            logger.Log(LogLevel.Warning, exception, message, param);
        }

        public static void Warning(this ILogger logger, string message, params object[] param)
        {
            Warning(logger, null, message, param);
        }

        public static void Error(this ILogger logger, Exception exception, string message, params object[] param)
        {
            logger.Log(LogLevel.Error, exception, message, param);
        }

        public static void Error(this ILogger logger, string message, params object[] param)
        {
            Error(logger, null, message, param);
        }
        public static void Error(this ILogger logger, Exception exception)
        {
            Error(logger, exception,null);
        }
    }
}
