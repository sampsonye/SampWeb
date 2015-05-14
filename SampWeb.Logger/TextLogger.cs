using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SampWeb.Logger
{
    public class TextLogger:ILogger
    {
        public void Log(LogLevel level, Exception exception, string message, params object[] param)
        {
            
        }
    }

}
