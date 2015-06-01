//-----------------------------------------------------------------------
// <copyright file="WebServer.cs" company="YuGuan Corporation">
//     Copyright (c) YuGuan Corporation. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Permissions;
using System.Security.Principal;
using System.Threading;
using System.Web.Hosting;

namespace SampWeb
{
    /// <summary>
    /// Web service or Asp.net hosting.
    /// </summary>
    [PermissionSet(SecurityAction.InheritanceDemand, Name = "FullTrust")]
    public class WebServer : MarshalByRefObject, IDisposable
    {
        /// <summary>
        /// Field TOKEN_ALL_ACCESS.
        /// </summary>
        private const int TokenAllAccess = 983551;

        /// <summary>
        /// Field TOKEN_EXECUTE.
        /// </summary>
        private const int TokenExecute = 131072;

        /// <summary>
        /// Field TOKEN_READ.
        /// </summary>
        private const int TokenRead = 131080;

        /// <summary>
        /// Field TOKEN_IMPERSONATE.
        /// </summary>
        private const int TokenImpersonate = 4;

        /// <summary>
        /// Field SecurityImpersonation.
        /// </summary>
        private const int SecurityImpersonation = 2;

        /// <summary>
        /// Field CurrentAssemblyFullPath.
        /// </summary>
        private static readonly string CurrentAssemblyFullPath = Path.GetFullPath(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);

        /// <summary>
        /// Field CurrentAssemblyFilename.
        /// </summary>
        private static readonly string CurrentAssemblyFilename = Path.GetFileName(CurrentAssemblyFullPath);

        /// <summary>
        /// Field _syncRoot.
        /// </summary>
        private readonly object _syncRoot = new object();

        /// <summary>
        /// Field _requireAuthentication.
        /// </summary>
        private readonly bool _requireAuthentication;

        /// <summary>
        /// Field _disableDirectoryListing.
        /// </summary>
        private readonly bool _disableDirectoryListing;

        /// <summary>
        /// Field _onStart.
        /// </summary>
        private readonly WaitCallback _onStart;

        /// <summary>
        /// Field _onSocketAccept.
        /// </summary>
        private readonly WaitCallback _onSocketAccept;

        /// <summary>
        /// Field _shutdownInProgress.
        /// </summary>
        private bool _shutdownInProgress;

        /// <summary>
        /// Field _appManager.
        /// </summary>
        private readonly ApplicationManager _appManager;

        /// <summary>
        /// Field _socketIPv4.
        /// </summary>
        private Socket _socketIPv4;

        /// <summary>
        /// Field _socketIPv6.
        /// </summary>
        private Socket _socketIPv6;

        /// <summary>
        /// Field _host.
        /// </summary>
        private Host _host;

        /// <summary>
        /// Field _processUser.
        /// </summary>
        private string _processUser;

        /// <summary>
        /// Field _isBinFolderExists.
        /// </summary>
        private bool _isBinFolderExists;

        /// <summary>
        /// Field _binFolder.
        /// </summary>
        private readonly string _binFolder;

        /// <summary>
        /// Field _binFolderReferenceFile.
        /// </summary>
        private readonly string _binFolderReferenceFile;

        /// <summary>
        /// Field _disposed.
        /// </summary>
        private bool _disposed;


        /// <summary>
        /// Initializes a new instance of the <see cref="WebServer"/> class.
        /// </summary>
        /// <param name="port">The port.</param>
        /// <param name="physicalPath">The physical path.</param>
        /// <param name="disableDirectoryListing">true if disable directory listing; otherwise, false.</param>
        /// <param name="startNow">true if immediately start service; otherwise, false.</param>
        public WebServer(int port, string physicalPath, bool disableDirectoryListing, bool startNow)
            : this(port, String.Empty, physicalPath, false, disableDirectoryListing, startNow)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WebServer"/> class.
        /// </summary>
        /// <param name="port">The port.</param>
        /// <param name="virtualPath">The virtual path.</param>
        /// <param name="physicalPath">The physical path.</param>
        /// <param name="requireAuthentication">true if require authentication; otherwise, false.</param>
        /// <param name="disableDirectoryListing">true if disable directory listing; otherwise, false.</param>
        /// <param name="startNow">true if immediately start service; otherwise, false.</param>
        public WebServer(int port, string virtualPath, string physicalPath, bool requireAuthentication, bool disableDirectoryListing, bool startNow)
        {
            Port = port;
            VirtualPath = IsNullOrWhiteSpace(virtualPath) ? "/" : "/" + virtualPath.Trim('/');
            PhysicalPath = Path.GetFullPath(IsNullOrWhiteSpace(physicalPath) ? "." : physicalPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            _binFolder = Path.Combine(PhysicalPath, "bin");
            _binFolderReferenceFile = Path.Combine(_binFolder, CurrentAssemblyFilename);
            _requireAuthentication = requireAuthentication;
            _disableDirectoryListing = disableDirectoryListing;
            _onSocketAccept = OnSocketAccept;
            _onStart = OnStart;
            _appManager = ApplicationManager.GetApplicationManager();
           // this.ObtainProcessToken();

            if (startNow)
            {
                Start();
            }
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="WebServer" /> class.
        /// </summary>
        ~WebServer()
        {
            Dispose(false);
        }

        /// <summary>
        /// Gets the virtual path.
        /// </summary>
        public string VirtualPath
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the physical path.
        /// </summary>
        public string PhysicalPath
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the port.
        /// </summary>
        public int Port
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the root URL.
        /// </summary>
        public string RootUrl
        {
            get
            {
                if (Port != 80)
                {
                    return "http://localhost:" + Port + "/" + VirtualPath.TrimStart('/');
                }

                return "http://localhost/" + VirtualPath.TrimStart('/');
            }
        }

        /// <summary>
        /// Releases all resources used by the current instance of the <see cref="WebServer" /> class.
        /// </summary>
        public void Close()
        {
            Dispose();
        }

        /// <summary>
        /// Releases all resources used by the current instance of the <see cref="WebServer" /> class.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Obtains a lifetime service object to control the lifetime policy for this instance.
        /// </summary>
        /// <returns>An object of type <see cref="T:System.Runtime.Remoting.Lifetime.ILease" /> used to control the lifetime policy for this instance. This is the current lifetime service object for this instance if one exists; otherwise, a new lifetime service object initialized to the value of the <see cref="P:System.Runtime.Remoting.Lifetime.LifetimeServices.LeaseManagerPollTime" /> property.</returns>
        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.Infrastructure)]
        public override object InitializeLifetimeService()
        {
            return null;
        }

        /// <summary>
        /// Gets the process token.
        /// </summary>
        /// <returns>IntPtr instance.</returns>
        public IntPtr GetProcessToken()
        {
            CheckDisposed();

            return IntPtr.Zero;
        }

        /// <summary>
        /// Gets the process user.
        /// </summary>
        /// <returns>User string.</returns>
        public string GetProcessUser()
        {
            CheckDisposed();
            if (string.IsNullOrWhiteSpace(_processUser))
            {
                var windowsIdentity = WindowsIdentity.GetCurrent();
                if (windowsIdentity != null) _processUser = windowsIdentity.Name;
            }
            return _processUser;
        }

        /// <summary>
        /// Starts the server.
        /// </summary>
        public void Start()
        {
            CheckDisposed();
            if (Socket.OSSupportsIPv6)
            {
                try
                {
                    _socketIPv6 = CreateSocketBindAndListen(AddressFamily.InterNetworkV6, IPAddress.IPv6Any, Port);
                }
                catch (SocketException e)
                {
                    if (e.SocketErrorCode == SocketError.AddressAlreadyInUse)
                    {
                        throw;
                    }
                }
            }

            try
            {
                _socketIPv4 = CreateSocketBindAndListen(AddressFamily.InterNetwork, IPAddress.Any, Port);
            }
            catch (SocketException)
            {
                if (_socketIPv6 == null)
                {
                    throw;
                }
            }

            CopyReferenceFile();

            if (_socketIPv6 != null)
            {
                ThreadPool.QueueUserWorkItem(_onStart, _socketIPv6);
            }

            if (_socketIPv4 != null)
            {
                ThreadPool.QueueUserWorkItem(_onStart, _socketIPv4);
            }
        }

        /// <summary>
        /// Stops the server.
        /// </summary>
        public void Stop()
        {
            CheckDisposed();

            _shutdownInProgress = true;

            try
            {
                if (_socketIPv4 != null)
                {
                    _socketIPv4.Close();
                }

                if (_socketIPv6 != null)
                {
                    _socketIPv6.Close();
                }
            }
            catch
            {
            }
            finally
            {
                _socketIPv4 = null;
                _socketIPv6 = null;
            }

            try
            {
                if (_host != null)
                {
                    _host.Shutdown();
                }

                while (_host != null)
                {
                    Thread.Sleep(100);
                }
            }
            catch
            {
            }
            finally
            {
                _host = null;
            }

            RemoveReferenceFile();
        }

        /// <summary>
        /// Stopped host.
        /// </summary>
        internal void HostStopped()
        {
            _host = null;
        }

        /// <summary>
        /// Releases all resources used by the current instance of the <see cref="WebServer" /> class.
        /// protected virtual for non-sealed class; private for sealed class.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (disposing)
            {
                // dispose managed resources
                ////if (managedResource != null)
                ////{
                ////    managedResource.Dispose();
                ////    managedResource = null;
                ////}

                _shutdownInProgress = true;

                try
                {
                    if (_socketIPv4 != null)
                    {
                        _socketIPv4.Close();
                    }

                    if (_socketIPv6 != null)
                    {
                        _socketIPv6.Close();
                    }
                }
                catch
                {
                }
                finally
                {
                    _socketIPv4 = null;
                    _socketIPv6 = null;
                }

                try
                {
                    if (_host != null)
                    {
                        _host.Shutdown();
                    }
                }
                catch
                {
                }
                finally
                {
                    _host = null;
                }

                RemoveReferenceFile();
            }

            // free native resources
            ////if (nativeResource != IntPtr.Zero)
            ////{
            ////    Marshal.FreeHGlobal(nativeResource);
            ////    nativeResource = IntPtr.Zero;
            ////}
        }

        /// <summary>
        /// Method CheckDisposed.
        /// </summary>
        private void CheckDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("DevLib.Web.Hosting.WebHost40.WebServer");
            }
        }

        

        /// <summary>
        /// Creates the socket bind and listen.
        /// </summary>
        /// <param name="family">The family.</param>
        /// <param name="ipAddress">The ip address.</param>
        /// <param name="port">The port.</param>
        /// <returns>Socket instance.</returns>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Reviewed.")]
        private Socket CreateSocketBindAndListen(AddressFamily family, IPAddress ipAddress, int port)
        {
            Socket socket = null;

            try
            {
                socket = new Socket(family, SocketType.Stream, ProtocolType.Tcp);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                socket.Bind(new IPEndPoint(ipAddress, port));
            }
            catch
            {
                if (socket != null)
                {
                    socket.Close();
                    socket = null;
                }

                throw;
            }

            socket.Listen(int.MaxValue);

            return socket;
        }

        /// <summary>
        /// Called when socket accept.
        /// </summary>
        /// <param name="acceptedSocket">The accepted socket.</param>
        private void OnSocketAccept(object acceptedSocket)
        {
            if (!_shutdownInProgress)
            {
                var connection = new Connection(this, (Socket)acceptedSocket);

                if (connection.WaitForRequestBytes() == 0)
                {
                    connection.WriteErrorAndClose(400);
                    return;
                }

                Host host;

                try
                {
                    host = GetHost();
                }
                catch (Exception e)
                {
                    connection.WriteErrorAndClose(500, e.ToString());
                    return;
                }

                if (host == null)
                {
                    connection.WriteErrorAndClose(500);
                    return;
                }

                try
                {
                    host.ProcessRequest(connection);
                }
                catch (Exception e)
                {
                    var exception = string.Format(Constants.UnhandledException, e.GetType());
                    connection.WriteEntireResponseFromString(500, "Content-type:text/html;charset=utf-8\r\n", Messages.FormatExceptionMessageBody(exception, e.Message, e.StackTrace), true);
                }
            }
        }

        /// <summary>
        /// Called when start.
        /// </summary>
        /// <param name="listeningSocket">The listening socket.</param>
        private void OnStart(object listeningSocket)
        {
            while (!_shutdownInProgress)
            {
                try
                {
                    if (listeningSocket != null)
                    {
                        var state = ((Socket)listeningSocket).Accept();

                        ThreadPool.QueueUserWorkItem(_onSocketAccept, state);
                    }
                }
                catch
                {
                    Thread.Sleep(100);
                }
            }
        }

        /// <summary>
        /// Gets the host.
        /// </summary>
        /// <returns>Host instance.</returns>
        private Host GetHost()
        {
            if (_shutdownInProgress)
            {
                return null;
            }

            var host = _host;

            if (host == null)
            {
                lock (_syncRoot)
                {
                    host = _host;

                    if (host == null)
                    {
                        var text = (VirtualPath + PhysicalPath).ToLowerInvariant();
                        var appId = text.GetHashCode().ToString("x", CultureInfo.InvariantCulture);

                        _host = (Host)_appManager.CreateObject(appId, typeof(Host), VirtualPath, PhysicalPath, false);
                        if (_host != null)
                        {
                            _host.Configure(this, Port, VirtualPath, PhysicalPath, _requireAuthentication, _disableDirectoryListing);

                            host = _host;
                        }
                    }
                }
            }

            return host;
        }

        /// <summary>
        /// Copies the reference file.
        /// </summary>
        private void CopyReferenceFile()
        {
            _isBinFolderExists = Directory.Exists(_binFolder);

            if (!_isBinFolderExists)
            {
                try
                {
                    Directory.CreateDirectory(_binFolder);
                }
                catch
                {
                }
            }

            try
            {
                File.Copy(CurrentAssemblyFullPath, _binFolderReferenceFile, true);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Removes the reference file.
        /// </summary>
        private void RemoveReferenceFile()
        {
            try
            {
                File.Delete(_binFolderReferenceFile);
            }
            catch
            {
            }

            if (!_isBinFolderExists)
            {
                if (IsDirectoryEmpty(_binFolder))
                {
                    try
                    {
                        Directory.Delete(_binFolder, true);
                    }
                    catch
                    {
                    }
                }
            }
        }

        /// <summary>
        /// Determines whether the specified path is empty directory.
        /// </summary>
        /// <param name="sourcePath">The path to check.</param>
        /// <returns>true if the specified path is empty directory; otherwise, false.</returns>
        private bool IsDirectoryEmpty(string sourcePath)
        {
            return !Directory.EnumerateFileSystemEntries(sourcePath, "*", SearchOption.AllDirectories).Any();
        }

        /// <summary>
        /// Indicates whether a specified string is null, empty, or consists only of white-space characters.
        /// </summary>
        /// <param name="value">The string to test.</param>
        /// <returns>true if the value parameter is null or String.Empty, or if value consists exclusively of white-space characters.</returns>
        private bool IsNullOrWhiteSpace(string value)
        {
            if (value == null)
            {
                return true;
            }

            for (var i = 0; i < value.Length; i++)
            {
                if (!char.IsWhiteSpace(value[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
