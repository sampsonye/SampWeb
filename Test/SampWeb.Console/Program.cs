using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SampWeb.Consts;

namespace SampWeb.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            var server = new WebServer(new WebConfig(83,"D:\\","",false,true,Int32.MaxValue));
            server.Start();
            Thread.Sleep(-1);
        }
    }
}
