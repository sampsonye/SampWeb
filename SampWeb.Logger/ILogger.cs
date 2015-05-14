using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SampWeb.Logger
{
    public interface ILogger
    {
        void Log(LogLevel level, Exception exception,string message,params object[]param);
    }

    public enum LogLevel
    {
        Debug,
        Information,
        Warning,
        Error,
        Fatal
    }
}
