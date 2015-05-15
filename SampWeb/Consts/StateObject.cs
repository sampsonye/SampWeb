using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SampWeb.Consts
{
    public class StateObject
    {
        public const int BufferSize = 1024;

        public byte[] Buffer { get; set; }/*当前缓冲区*/

        public Socket ClientSocket { get; set; }/*当前客户端连接*/

        public List<byte> TotalBytes { get; set; }/*接收到的所有字节*/
        public int TotalByteLength { get; set; }/*所有字节长度*/
        public int HeaderLength { get; set; }/*HTTP头的长度*/
        public RequestHeader Header { get; set; }

        public ManualResetEvent ReceiveDone { get; set; }/*锁定量，用于超时放弃*/
        public StateObject()
        {
            Buffer=new byte[BufferSize];
            TotalBytes=new List<byte>();
            TotalByteLength = 0;
            ReceiveDone=new ManualResetEvent(false);
        }
    }
}
