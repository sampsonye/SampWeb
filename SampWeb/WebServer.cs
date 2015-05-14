using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using System.Web.Hosting;
using SampWeb.Consts;

namespace SampWeb
{
    [PermissionSet(SecurityAction.InheritanceDemand, Name = "FullTrust")]
    public class WebServer : MarshalByRefObject, IDisposable
    {
        private const string CopyingRight = "Server.SampWeb";
        private bool _isDisposed;
        public WebConfig WebConfig { get; private set; }
        private readonly ApplicationManager _applicationManager;
        private Socket _socketServer;
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

        public bool Start(bool async)
        {
            CheckDisposed();
            _socketServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _socketServer.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _socketServer.Bind(new IPEndPoint(IPAddress.Any, WebConfig.WebPort));
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
                throw new ObjectDisposedException(CopyingRight);
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
                    Directory.Exists(binfolder);
                }
                catch (Exception)
                {
                    
                    throw;
                }
            }
        }

        #endregion
    }
}
