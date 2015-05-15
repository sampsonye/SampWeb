using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using SampWeb.Consts;
using SampWeb.Logger;

namespace SampWeb
{
    [PermissionSet(SecurityAction.InheritanceDemand, Name = "FullTrust")]
    public class WebServer : MarshalByRefObject, IDisposable
    {
       
        private bool _isDisposed;
        public WebConfig WebConfig { get; private set; }
        private readonly ILogger Logger=new TextLogger();
        private readonly ApplicationManager _applicationManager;
        private  Host _host;
        private Socket _socketServer;
        private object _lockObj=new object();
        /// <summary>
        /// Field CurrentAssemblyFullPath.
        /// </summary>
        private static readonly string CurrentAssemblyFullPath = Path.GetFullPath(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);

        /// <summary>
        /// Field CurrentAssemblyFilename.
        /// </summary>
        private static readonly string CurrentAssemblyFilename = Path.GetFileName(CurrentAssemblyFullPath);
        public WebServer(WebConfig config)
        {
            WebConfig = config;
            _applicationManager = ApplicationManager.GetApplicationManager();
           
          

        }

        public bool Start()
        {
            CheckDisposed();
            CopyReferenceFile();
           
            if (_socketServer!=null&&_socketServer.IsBound)
            {
                return false;
            }
            try
            {
                _socketServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _socketServer.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _socketServer.Bind(new IPEndPoint(IPAddress.Any, WebConfig.WebPort));

                GetHost();/**/
            }
            catch (Exception ex)
            {
                Logger.Error(ex,"开启端口监听");
                if (_socketServer!=null)
                {
                    _socketServer.Close();
                    _socketServer = null;
                }
                Environment.Exit(-1);
            }
            _socketServer.Listen(WebConfig.ListenQueue);
            StartAccept();
            return true;
        }

        #region [重写]
        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.Infrastructure)]
        public override object InitializeLifetimeService()
        {
            return null;
        }
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }
            _isDisposed = true;

        }
        #endregion

        #region[辅助方法]
        private void CheckDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(RuntimeVars.CopyingRight);
            }
        }

        private void CopyReferenceFile()
        {
            var binfolder = Path.Combine(WebConfig.PhysicalPath, "bin");
            var mainAssembly = Path.Combine(binfolder, CurrentAssemblyFilename);
            if (!Directory.Exists(binfolder))
            {
                try
                {
                    Directory.CreateDirectory(binfolder);
                }
                catch (Exception)
                {
                    
                    
                }
            }
            try
            {
                File.Copy(CurrentAssemblyFullPath,mainAssembly);
            }
            catch (Exception)
            {
                
               
            }
        }

        private Host GetHost()
        {
            if (_host==null)
            {
                lock (_lockObj)
                {
                    if (_host==null)
                    {
                        string text = (WebConfig.VirtualPath + WebConfig.PhysicalPath).ToLowerInvariant();
                        string appId = text.GetHashCode().ToString("x", CultureInfo.InvariantCulture);
                        var obj = _applicationManager.CreateObject(appId, typeof(Host), "/", WebConfig.PhysicalPath,
                          false);
                        _host = (Host)obj;
                    }
                }
               
            }
            return _host;
        }

        #endregion

        #region[Socket接收]

        private void StartAccept()
        {
            _socketServer.BeginAccept(OnAccept, _socketServer);
        }

        private void OnAccept(IAsyncResult iar)
        {
            var acceptSocket = (Socket)iar.AsyncState;
            var client = acceptSocket.EndAccept(iar);
            acceptSocket.BeginAccept(OnAccept, acceptSocket);/*继续下一个Accept*/
            OnReceive(client);
        }

        private  void OnReceive(Socket client)
        {
            try
            {
                var state = new StateObject {ClientSocket = client};   
                client.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, ReceiveCallback, state);
               
            }
            catch (Exception e)
            {
               Logger.Error(e,"开始接收");
            }
        }
        private  void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                var state = (StateObject)ar.AsyncState;
                var client = state.ClientSocket;   
                var bytesRead = client.EndReceive(ar);
                if (bytesRead > 0)
                {
                    state.TotalBytes.AddRange(state.Buffer.Take(bytesRead));
                    state.ByteLength += bytesRead;
                    /*
                     * 1.先判断本地Header有没有解析出来
                     *  2.1如果未解析出来，继续接收
                     *  2.2如果解析出来
                     *      2.2.1如果是GET/Delete操作，直接进入解析
                     *      2.2.2如果是POST/PUT操作,判断Body的长度是否达到ContentLength,如未继续接收，否则进入解析
                     */
                    var val = AnalyizeRequest(state);
                    if (val.Item1==0)
                    {
                        client.BeginReceive(state.Buffer, 0, StateObject.BufferSize, 0, ReceiveCallback, state);
                    }else if (val.Item1 == -1)
                    {
                        var conn = new RequestProcessor(state);
                        var content = conn.Build500Response(val.Item2);
                        state.ClientSocket.Send(RuntimeVars.ResponseEncoding.GetBytes(content));
                        state.ClientSocket.Close();
                    }else if (val.Item1==1)
                    {
                        SimpleResponse(state);
                    }



                }
                Console.WriteLine(Encoding.UTF8.GetString(state.TotalBytes.ToArray(), 0, state.TotalBytes.Count));
               
            }
            catch (Exception e)
            {
                Logger.Error(e,"异步接收");
            }
        }

        private static readonly byte[] EndChrs = {13,10,13,10};

        private Tuple<int, string> AnalyizeRequest(StateObject state)
        {
            /*
             * 0:数据未接收完全
             * 1:接收完整，解析OK
             * -1:解析出错
             */
           
            if (state.Header==null)
            {
                if (state.TotalBytes.Count>3)
                {
                    try
                    {
                        for (int i = 0; i < state.ByteLength - 3; i++)
                        {
                            if (state.TotalBytes.Skip(i).Take(4).SequenceEqual(EndChrs))/*Header已经解析出来*/
                            {
                                var header = new RequestHeader();
                                var headerArr = RuntimeVars.ResponseEncoding.GetString(state.TotalBytes.ToArray())
                                    .Replace("\r\n", "\n")
                                    .Replace("\r", "\n")
                                    .Split('\n');
                                var first = headerArr[0].Split(' ');
                                HttpVerb ver;
                                if (Enum.TryParse(first[0], out ver))
                                {
                                    header.Verb = ver;
                                }
                                header.QueryStr = first[1];
                                header.Protocal = first[2];
                                state.HeaderLength = i + 4;
                                state.Header = header;
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex,"解析Header头");
                        return new Tuple<int, string>(-1, ex.Message);
                    }
                   
                }
                
            }

            if (state.Header != null)
            {
                if (state.Header.Verb == HttpVerb.GET || state.Header.Verb == HttpVerb.DELETE)
                {
                    return new Tuple<int,  string>(1, null);
                }
                if (state.Header.Verb==HttpVerb.POST)
                {
                    var conn = new RequestProcessor(state);
                    var content = conn.BuildResponse(100, null, DateTime.Now.ToString(), true);
                    state.ClientSocket.Send(RuntimeVars.ResponseEncoding.GetBytes(content));
                }
            }
            return new Tuple<int,  string>(0,  null);
        }

        private void SimpleResponse(StateObject state)
        {

            var conn = new RequestProcessor(state);
            var content = conn.BuildResponse(200, null, DateTime.Now.ToString(), false);
            state.ClientSocket.Send(RuntimeVars.ResponseEncoding.GetBytes(content));
            state.ClientSocket.Close();
        }

        #endregion
    }
}
