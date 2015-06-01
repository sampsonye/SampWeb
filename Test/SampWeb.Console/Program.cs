using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SampWeb.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            //var server = new WebServer(new WebConfig(83, @"D:\自我开发\MonoMy", "", false, true, Int32.MaxValue));
            //server.Start();
            //Thread.Sleep(-1);

            var server = new WebServer(80, args[0], true, true);
            Thread.Sleep(-1);
        }
    }
}
