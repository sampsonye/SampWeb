//-----------------------------------------------------------------------
// <copyright file="Request.cs" company="YuGuan Corporation">
//     Copyright (c) YuGuan Corporation. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Lifetime;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Web;
using System.Web.Hosting;
using Microsoft.Win32.SafeHandles;

namespace SampWeb
{
    /// <summary>
    /// Request class.
    /// </summary>
    internal sealed class Request : SimpleWorkerRequest
    {
        /// <summary>
        /// Field MaxChunkLength.
        /// </summary>
        private const int MaxChunkLength = 65536;

        /// <summary>
        /// Field MaxHeaderBytes.
        /// </summary>
        private const int MaxHeaderBytes = 32768;

        /// <summary>
        /// Field BadPathChars.
        /// </summary>
        private static readonly char[] BadPathChars = new char[]
        {
            '%',
            '>',
            '<',
            ':',
            '\\'
        };

        /// <summary>
        /// Field DefaultFileNames.
        /// </summary>
        private static readonly string[] DefaultFileNames = new string[]
        {
            "default.aspx",
            "default.asmx",
            "default.htm",
            "default.html"
        };

        /// <summary>
        /// Field RestrictedDirs.
        /// </summary>
        private static readonly string[] RestrictedDirs = new string[]
        {
            "/bin",
            "/app_browsers",
            "/app_code",
            "/app_data",
            "/app_localresources",
            "/app_globalresources",
            "/app_webreferences"
        };

        /// <summary>
        /// Field IntToHex.
        /// </summary>
        private static readonly char[] IntToHex = new char[]
        {
            '0',
            '1',
            '2',
            '3',
            '4',
            '5',
            '6',
            '7',
            '8',
            '9',
            'a',
            'b',
            'c',
            'd',
            'e',
            'f'
        };

        /// <summary>
        /// Field _host.
        /// </summary>
        private Host _host;

        /// <summary>
        /// Field _connection.
        /// </summary>
        private Connection _connection;

        /// <summary>
        /// Field _connectionSponsor.
        /// </summary>
        private Sponsor _connectionSponsor;

        /// <summary>
        /// Field _connectionPermission.
        /// </summary>
        private IStackWalk _connectionPermission = new PermissionSet(PermissionState.Unrestricted);

        /// <summary>
        /// Field _headerBytes.
        /// </summary>
        private byte[] _headerBytes;

        /// <summary>
        /// Field _startHeadersOffset.
        /// </summary>
        private int _startHeadersOffset;

        /// <summary>
        /// Field _endHeadersOffset.
        /// </summary>
        private int _endHeadersOffset;

        /// <summary>
        /// Field _headerByteStrings.
        /// </summary>
        private ArrayList _headerByteStrings;

        /// <summary>
        /// Field _isClientScriptPath.
        /// </summary>
        private bool _isClientScriptPath;

        /// <summary>
        /// Field _verb.
        /// </summary>
        private string _verb;

        /// <summary>
        /// Field _url.
        /// </summary>
        private string _url;

        /// <summary>
        /// Field _port.
        /// </summary>
        private string _port;

        /// <summary>
        /// Field _path.
        /// </summary>
        private string _path;

        /// <summary>
        /// Field _filePath.
        /// </summary>
        private string _filePath;

        /// <summary>
        /// Field _pathInfo.
        /// </summary>
        private string _pathInfo;

        /// <summary>
        /// Field _pathTranslated.
        /// </summary>
        private string _pathTranslated;

        /// <summary>
        /// Field _queryString.
        /// </summary>
        private string _queryString;

        /// <summary>
        /// Field _queryStringBytes.
        /// </summary>
        private byte[] _queryStringBytes;

        /// <summary>
        /// Field _contentLength.
        /// </summary>
        private int _contentLength;

        /// <summary>
        /// Field _preloadedContentLength.
        /// </summary>
        private int _preloadedContentLength;

        /// <summary>
        /// Field _preloadedContent.
        /// </summary>
        private byte[] _preloadedContent;

        /// <summary>
        /// Field _allRawHeaders.
        /// </summary>
        private string _allRawHeaders;

        /// <summary>
        /// Field _unknownRequestHeaders.
        /// </summary>
        private string[][] _unknownRequestHeaders;

        /// <summary>
        /// Field _knownRequestHeaders.
        /// </summary>
        private string[] _knownRequestHeaders;

        /// <summary>
        /// Field _specialCaseStaticFileHeaders.
        /// </summary>
        private bool _specialCaseStaticFileHeaders;

        /// <summary>
        /// Field _headersSent.
        /// </summary>
        private bool _headersSent;

        /// <summary>
        /// Field _responseStatus.
        /// </summary>
        private int _responseStatus;

        /// <summary>
        /// Field _responseHeadersBuilder.
        /// </summary>
        private StringBuilder _responseHeadersBuilder;

        /// <summary>
        /// Field _responseBodyBytes.
        /// </summary>
        private ArrayList _responseBodyBytes;

        /// <summary>
        /// Initializes a new instance of the <see cref="Request"/> class.
        /// </summary>
        /// <param name="host">The host.</param>
        /// <param name="connection">The connection.</param>
        [SecurityPermission(SecurityAction.Assert, Flags = SecurityPermissionFlag.RemotingConfiguration)]
        public Request(Host host, Connection connection)
            : base(string.Empty, string.Empty, null)
        {
            _host = host;
            _connection = connection;
            var lease = (ILease)RemotingServices.GetLifetimeService(_connection);
            _connectionSponsor = new Sponsor();
            lease.Register(_connectionSponsor);
        }

        /// <summary>
        /// Processes request.
        /// </summary>
        [AspNetHostingPermission(SecurityAction.Assert, Level = AspNetHostingPermissionLevel.Unrestricted, Unrestricted = true)]
        public void Process()
        {
            if (!TryParseRequest())
            {
                return;
            }

            if (_verb == "POST" && _contentLength > 0 && _preloadedContentLength < _contentLength)
            {
                _connection.Write100Continue();
            }

            if (_host.RequireAuthentication && !TryNtlmAuthenticate())
            {
                return;
            }

            if (_isClientScriptPath)
            {
                _connection.WriteEntireResponseFromFile(_host.PhysicalClientScriptPath + _path.Substring(_host.NormalizedClientScriptPath.Length), false);
                return;
            }

            if (IsRequestForRestrictedDirectory())
            {
                _connection.WriteErrorAndClose(403);
                return;
            }

            if (ProcessDefaultDocumentRequest())
            {
                return;
            }

            PrepareResponse();
           // HttpRuntime.AppDomainAppVirtualPath
            HttpRuntime.ProcessRequest(this);
        }

        /// <summary>
        /// Returns the virtual path to the requested URI.
        /// </summary>
        /// <returns>The path to the requested URI.</returns>
        public override string GetUriPath()
        {
            return _path;
        }

        /// <summary>
        /// Returns the query string specified in the request URL.
        /// </summary>
        /// <returns>The request query string.</returns>
        public override string GetQueryString()
        {
            return _queryString;
        }

        /// <summary>
        /// When overridden in a derived class, returns the response query string as an array of bytes.
        /// </summary>
        /// <returns>An array of bytes containing the response.</returns>
        public override byte[] GetQueryStringRawBytes()
        {
            return _queryStringBytes;
        }

        /// <summary>
        /// Returns the URL path contained in the header with the query string appended.
        /// </summary>
        /// <returns>The raw URL path of the request header.Note:The returned URL is not normalized. Using the URL for access control, or security-sensitive decisions can expose your application to canonicalization security vulnerabilities.</returns>
        public override string GetRawUrl()
        {
            return _url;
        }

        /// <summary>
        /// Returns the HTTP request verb.
        /// </summary>
        /// <returns>The HTTP verb for this request.</returns>
        public override string GetHttpVerbName()
        {
            return _verb;
        }

        /// <summary>
        /// Returns the HTTP version string of the request (for example, "HTTP/1.1").
        /// </summary>
        /// <returns>The HTTP version string returned in the request header.</returns>
        public override string GetHttpVersion()
        {
            return _port;
        }

        /// <summary>
        /// Returns the IP address of the client.
        /// </summary>
        /// <returns>The client's IP address.</returns>
        public override string GetRemoteAddress()
        {
            _connectionPermission.Assert();

            return _connection.RemoteIp;
        }

        /// <summary>
        /// Returns the client's port number.
        /// </summary>
        /// <returns>The client's port number.</returns>
        public override int GetRemotePort()
        {
            return 0;
        }

        /// <summary>
        /// Returns the server IP address of the interface on which the request was received.
        /// </summary>
        /// <returns>The server IP address of the interface on which the request was received.</returns>
        public override string GetLocalAddress()
        {
            _connectionPermission.Assert();

            return _connection.LocalIp;
        }

        /// <summary>
        /// When overridden in a derived class, returns the name of the local server.
        /// </summary>
        /// <returns>The name of the local server.</returns>
        public override string GetServerName()
        {
            var localAddress = GetLocalAddress();

            if (localAddress.Equals("127.0.0.1") || localAddress.Equals("::1") || localAddress.Equals("::ffff:127.0.0.1"))
            {
                return "localhost";
            }

            return localAddress;
        }

        /// <summary>
        /// Returns the port number on which the request was received.
        /// </summary>
        /// <returns>The server port number on which the request was received.</returns>
        public override int GetLocalPort()
        {
            return _host.Port;
        }

        /// <summary>
        /// Returns the physical path to the requested URI.
        /// </summary>
        /// <returns>The physical path to the requested URI.</returns>
        public override string GetFilePath()
        {
            return _filePath;
        }

        /// <summary>
        /// Returns the physical file path to the requested URI (and translates it from virtual path to physical path: for example, "/proj1/page.aspx" to "c:\dir\page.aspx")
        /// </summary>
        /// <returns>The translated physical file path to the requested URI.</returns>
        public override string GetFilePathTranslated()
        {
            return _pathTranslated;
        }

        /// <summary>
        /// Returns additional path information for a resource with a URL extension. That is, for the path /virdir/page.html/tail, the return value is /tail.
        /// </summary>
        /// <returns>Additional path information for a resource.</returns>
        public override string GetPathInfo()
        {
            return _pathInfo;
        }

        /// <summary>
        /// Returns the virtual path to the currently executing server application.
        /// </summary>
        /// <returns>The virtual path of the current application.</returns>
        public override string GetAppPath()
        {
            return _host.VirtualPath;
        }

        /// <summary>
        /// Returns the UNC-translated path to the currently executing server application.
        /// </summary>
        /// <returns>The physical path of the current application.</returns>
        public override string GetAppPathTranslated()
        {
            return _host.PhysicalPath;
        }

        /// <summary>
        /// Returns the portion of the HTTP request body that has already been read.
        /// </summary>
        /// <returns>The portion of the HTTP request body that has been read.</returns>
        public override byte[] GetPreloadedEntityBody()
        {
            return _preloadedContent;
        }

        /// <summary>
        /// Returns a value indicating whether all request data is available and no further reads from the client are required.
        /// </summary>
        /// <returns>true if all request data is available; otherwise, false.</returns>
        public override bool IsEntireEntityBodyIsPreloaded()
        {
            return _contentLength == _preloadedContentLength;
        }

        /// <summary>
        /// Reads request data from the client (when not preloaded).
        /// </summary>
        /// <param name="buffer">The byte array to read data into.</param>
        /// <param name="size">The maximum number of bytes to read.</param>
        /// <returns>The number of bytes read.</returns>
        public override int ReadEntityBody(byte[] buffer, int size)
        {
            var num = 0;
            _connectionPermission.Assert();
            var array = _connection.ReadRequestBytes(size);

            if (array != null && array.Length > 0)
            {
                num = array.Length;
                Buffer.BlockCopy(array, 0, buffer, 0, num);
            }

            return num;
        }

        /// <summary>
        /// Returns the standard HTTP request header that corresponds to the specified index.
        /// </summary>
        /// <param name="index">The index of the header. For example, the <see cref="F:System.Web.HttpWorkerRequest.HeaderAllow" /> field.</param>
        /// <returns>The HTTP request header.</returns>
        public override string GetKnownRequestHeader(int index)
        {
            return _knownRequestHeaders[index];
        }

        /// <summary>
        /// Returns a nonstandard HTTP request header value.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <returns>The header value.</returns>
        public override string GetUnknownRequestHeader(string name)
        {
            var num = _unknownRequestHeaders.Length;

            for (var i = 0; i < num; i++)
            {
                if (string.Compare(name, _unknownRequestHeaders[i][0], StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return _unknownRequestHeaders[i][1];
                }
            }

            return null;
        }

        /// <summary>
        /// Get all nonstandard HTTP header name-value pairs.
        /// </summary>
        /// <returns>An array of header name-value pairs.</returns>
        public override string[][] GetUnknownRequestHeaders()
        {
            return _unknownRequestHeaders;
        }

        /// <summary>
        /// Returns a single server variable from a dictionary of server variables associated with the request.
        /// </summary>
        /// <param name="name">The name of the requested server variable.</param>
        /// <returns>The requested server variable.</returns>
        public override string GetServerVariable(string name)
        {
            var result = string.Empty;

            if (name != null)
            {
                if (!(name == "ALL_RAW"))
                {
                    if (!(name == "SERVER_PROTOCOL"))
                    {
                        if (!(name == "LOGON_USER"))
                        {
                            if (name == "AUTH_TYPE")
                            {
                                if (GetUserToken() != IntPtr.Zero)
                                {
                                    result = "NTLM";
                                }
                            }
                        }
                        else
                        {
                            if (GetUserToken() != IntPtr.Zero)
                            {
                                result = _host.GetProcessUser();
                            }
                        }
                    }
                    else
                    {
                        result = _port;
                    }
                }
                else
                {
                    result = _allRawHeaders;
                }
            }

            return result;
        }

        /// <summary>
        /// Returns the client's impersonation token.
        /// </summary>
        /// <returns>A value representing the client's impersonation token. The default is <see cref="F:System.IntPtr.Zero" />.</returns>
        public override IntPtr GetUserToken()
        {
            return _host.GetProcessToken();
        }

        /// <summary>
        /// Returns the physical path corresponding to the specified virtual path.
        /// </summary>
        /// <param name="path">The virtual path.</param>
        /// <returns>The physical path that corresponds to the virtual path specified in the <paramref name="path" /> parameter.</returns>
        public override string MapPath(string path)
        {
            string text;

            if (path == null || path.Length == 0 || path.Equals("/"))
            {
                if (_host.VirtualPath == "/")
                {
                    text = _host.PhysicalPath;
                }
                else
                {
                    text = string.Empty;
                }
            }
            else
            {
                if (_host.IsVirtualPathAppPath(path))
                {
                    text = _host.PhysicalPath;
                }
                else
                {
                    bool flag;
                    if (_host.IsVirtualPathInApp(path, out flag))
                    {
                        if (flag)
                        {
                            text = _host.PhysicalClientScriptPath + path.Substring(_host.NormalizedClientScriptPath.Length);
                        }
                        else
                        {
                            text = _host.PhysicalPath + path.Substring(_host.NormalizedVirtualPath.Length);
                        }
                    }
                    else
                    {
                        if (path.StartsWith("/", StringComparison.Ordinal))
                        {
                            text = _host.PhysicalPath + path.Substring(1);
                        }
                        else
                        {
                            text = _host.PhysicalPath + path;
                        }
                    }
                }
            }

            text = text.Replace('/', Path.DirectorySeparatorChar);

            if (text.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) && !text.EndsWith(":"+Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                text = text.Substring(0, text.Length - 1);
            }

            return text;
        }

        /// <summary>
        /// Specifies the HTTP status code and status description of the response; for example, SendStatus(200, "Ok").
        /// </summary>
        /// <param name="statusCode">The status code to send</param>
        /// <param name="statusDescription">The status description to send.</param>
        public override void SendStatus(int statusCode, string statusDescription)
        {
            _responseStatus = statusCode;
        }

        /// <summary>
        /// Adds a standard HTTP header to the response.
        /// </summary>
        /// <param name="index">The header index. For example, <see cref="F:System.Web.HttpWorkerRequest.HeaderContentLength" />.</param>
        /// <param name="value">The header value.</param>
        public override void SendKnownResponseHeader(int index, string value)
        {
            if (_headersSent)
            {
                return;
            }

            switch (index)
            {
                case 1:
                case 2:
                    break;

                default:
                    switch (index)
                    {
                        case 18:
                        case 19:
                            if (_specialCaseStaticFileHeaders)
                            {
                                return;
                            }

                            break;

                        case 20:
                            if (value == "bytes")
                            {
                                _specialCaseStaticFileHeaders = true;
                                return;
                            }

                            break;

                        default:
                            if (index == 26)
                            {
                                return;
                            }

                            break;
                    }

                    _responseHeadersBuilder.Append(GetKnownResponseHeaderName(index));
                    _responseHeadersBuilder.Append(": ");
                    _responseHeadersBuilder.Append(value);
                    _responseHeadersBuilder.Append("\r\n");

                    return;
            }
        }

        /// <summary>
        /// Adds a nonstandard HTTP header to the response.
        /// </summary>
        /// <param name="name">The name of the header to send.</param>
        /// <param name="value">The value of the header.</param>
        public override void SendUnknownResponseHeader(string name, string value)
        {
            if (_headersSent)
            {
                return;
            }

            _responseHeadersBuilder.Append(name);
            _responseHeadersBuilder.Append(": ");
            _responseHeadersBuilder.Append(value);
            _responseHeadersBuilder.Append("\r\n");
        }

        /// <summary>
        /// Adds a Content-Length HTTP header to the response for message bodies that are less than or equal to 2 GB.
        /// </summary>
        /// <param name="contentLength">The length of the response, in bytes.</param>
        public override void SendCalculatedContentLength(int contentLength)
        {
            if (!_headersSent)
            {
                _responseHeadersBuilder.Append("Content-Length: ");
                _responseHeadersBuilder.Append(contentLength.ToString(CultureInfo.InvariantCulture));
                _responseHeadersBuilder.Append("\r\n");
            }
        }

        /// <summary>
        /// Returns a value indicating whether HTTP response headers have been sent to the client for the current request.
        /// </summary>
        /// <returns>true if HTTP response headers have been sent to the client; otherwise, false.</returns>
        public override bool HeadersSent()
        {
            return _headersSent;
        }

        /// <summary>
        /// Returns a value indicating whether the client connection is still active.
        /// </summary>
        /// <returns>true if the client connection is still active; otherwise, false.</returns>
        public override bool IsClientConnected()
        {
            _connectionPermission.Assert();

            return _connection.Connected;
        }

        /// <summary>
        /// Terminates the connection with the client.
        /// </summary>
        public override void CloseConnection()
        {
            _connectionPermission.Assert();

            CloseConnectionInternal();
        }

        /// <summary>
        /// Adds the contents of a byte array to the response and specifies the number of bytes to send.
        /// </summary>
        /// <param name="data">The byte array to send.</param>
        /// <param name="length">The number of bytes to send.</param>
        public override void SendResponseFromMemory(byte[] data, int length)
        {
            if (length > 0)
            {
                var array = new byte[length];
                Buffer.BlockCopy(data, 0, array, 0, length);
                _responseBodyBytes.Add(array);
            }
        }

        /// <summary>
        /// Adds the contents of the file with the specified name to the response and specifies the starting position in the file and the number of bytes to send.
        /// </summary>
        /// <param name="filename">The name of the file to send.</param>
        /// <param name="offset">The starting position in the file.</param>
        /// <param name="length">The number of bytes to send.</param>
        public override void SendResponseFromFile(string filename, long offset, long length)
        {
            if (length == 0L)
            {
                return;
            }

            FileStream fileStream = null;

            try
            {
                fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                SendResponseFromFileStream(fileStream, offset, length);
            }
            finally
            {
                if (fileStream != null)
                {
                    fileStream.Close();
                }
            }
        }

        /// <summary>
        /// Adds the contents of the file with the specified handle to the response and specifies the starting position in the file and the number of bytes to send.
        /// </summary>
        /// <param name="handle">The handle of the file to send.</param>
        /// <param name="offset">The starting position in the file.</param>
        /// <param name="length">The number of bytes to send.</param>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Reviewed.")]
        public override void SendResponseFromFile(IntPtr handle, long offset, long length)
        {
            if (length == 0L)
            {
                return;
            }

            FileStream fileStream = null;

            try
            {
                var handle2 = new SafeFileHandle(handle, false);
                fileStream = new FileStream(handle2, FileAccess.Read);
                SendResponseFromFileStream(fileStream, offset, length);
            }
            finally
            {
                if (fileStream != null)
                {
                    fileStream.Dispose();
                    fileStream = null;
                }
            }
        }

        /// <summary>
        /// Sends all pending response data to the client.
        /// </summary>
        /// <param name="finalFlush">true if this is the last time response data will be flushed; otherwise, false.</param>
        public override void FlushResponse(bool finalFlush)
        {
            if (_responseStatus == 404 && !_headersSent && finalFlush && _verb == "GET" && ProcessDirectoryListingRequest())
            {
                return;
            }

            _connectionPermission.Assert();

            if (!_headersSent)
            {
                _connection.WriteHeaders(_responseStatus, _responseHeadersBuilder.ToString());
                _headersSent = true;
            }

            for (var i = 0; i < _responseBodyBytes.Count; i++)
            {
                var array = (byte[])_responseBodyBytes[i];
                _connection.WriteBody(array, 0, array.Length);
            }

            _responseBodyBytes = new ArrayList();

            if (finalFlush)
            {
                CloseConnectionInternal();
            }
        }

        /// <summary>
        /// Notifies the <see cref="T:System.Web.HttpWorkerRequest" /> that request processing for the current request is complete.
        /// </summary>
        public override void EndOfRequest()
        {
        }

        /// <summary>
        /// Gets the URL encode redirect.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>The URL encode redirect string.</returns>
        private static string GetUrlEncodeRedirect(string path)
        {
            var bytes = Encoding.UTF8.GetBytes(path);

            var num = bytes.Length;

            var num2 = 0;

            for (var i = 0; i < num; i++)
            {
                if ((bytes[i] & 128) != 0)
                {
                    num2++;
                }
            }

            if (num2 > 0)
            {
                var array = new byte[num + (num2 * 2)];

                var num3 = 0;

                for (var j = 0; j < num; j++)
                {
                    var b = bytes[j];

                    if ((b & 128) == 0)
                    {
                        array[num3++] = b;
                    }
                    else
                    {
                        array[num3++] = 37;
                        array[num3++] = (byte)IntToHex[b >> 4 & 15];
                        array[num3++] = (byte)IntToHex[b & 15];
                    }
                }

                path = Encoding.ASCII.GetString(array);
            }

            if (path.IndexOf(' ') >= 0)
            {
                path = path.Replace(" ", "%20");
            }

            return path;
        }

        /// <summary>
        /// Resets this instance.
        /// </summary>
        private void Reset()
        {
            _headerBytes = null;
            _startHeadersOffset = 0;
            _endHeadersOffset = 0;
            _headerByteStrings = null;
            _isClientScriptPath = false;
            _verb = null;
            _url = null;
            _port = null;
            _path = null;
            _filePath = null;
            _pathInfo = null;
            _pathTranslated = null;
            _queryString = null;
            _queryStringBytes = null;
            _contentLength = 0;
            _preloadedContentLength = 0;
            _preloadedContent = null;
            _allRawHeaders = null;
            _unknownRequestHeaders = null;
            _knownRequestHeaders = null;
            _specialCaseStaticFileHeaders = false;
        }

        /// <summary>
        /// Tries to parse request.
        /// </summary>
        /// <returns>true if succeeded; otherwise, false.</returns>
        private bool TryParseRequest()
        {
            Reset();

            ReadAllHeaders();

            if (_headerBytes == null || _endHeadersOffset < 0 || _headerByteStrings == null || _headerByteStrings.Count == 0)
            {
                _connection.WriteErrorAndClose(400);
                return false;
            }

            ParseRequestLine();

            if (IsBadPath())
            {
                _connection.WriteErrorAndClose(400);
                return false;
            }

            if (!_host.IsVirtualPathInApp(_path, out _isClientScriptPath))
            {
                _connection.WriteErrorAndClose(404);
                return false;
            }

            ParseHeaders();

            ParsePostedContent();

            return true;
        }

        /// <summary>
        /// Tries to check NTLM authenticate.
        /// </summary>
        /// <returns>true if succeeded; otherwise, false.</returns>
        private bool TryNtlmAuthenticate()
        {
            return true;
        }

        /// <summary>
        /// Tries to read all headers.
        /// </summary>
        /// <returns>true if succeeded; otherwise, false.</returns>
        private bool TryReadAllHeaders()
        {
            var array = _connection.ReadRequestBytes(32768);

            if (array == null || array.Length == 0)
            {
                return false;
            }

            if (_headerBytes != null)
            {
                var num = array.Length + _headerBytes.Length;

                if (num > 32768)
                {
                    return false;
                }

                var array2 = new byte[num];

                Buffer.BlockCopy(_headerBytes, 0, array2, 0, _headerBytes.Length);

                Buffer.BlockCopy(array, 0, array2, _headerBytes.Length, array.Length);

                _headerBytes = array2;
            }
            else
            {
                _headerBytes = array;
            }

            _startHeadersOffset = -1;
            _endHeadersOffset = -1;
            _headerByteStrings = new ArrayList();
            var byteParser = new ByteParser(_headerBytes);

            while (true)
            {
                var byteString = byteParser.ReadLine();

                if (byteString == null)
                {
                    return true;
                }

                if (_startHeadersOffset < 0)
                {
                    _startHeadersOffset = byteParser.CurrentOffset;
                }

                if (byteString.IsEmpty)
                {
                    break;
                }

                _headerByteStrings.Add(byteString);
            }

            _endHeadersOffset = byteParser.CurrentOffset;

            return true;
        }

        /// <summary>
        /// Reads all headers.
        /// </summary>
        private void ReadAllHeaders()
        {
            _headerBytes = null;

            while (TryReadAllHeaders())
            {
                if (_endHeadersOffset >= 0)
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Parses the request line.
        /// </summary>
        private void ParseRequestLine()
        {
            var byteString = (ByteString)_headerByteStrings[0];

            var array = byteString.Split(' ');

            if (array == null || array.Length < 2 || array.Length > 3)
            {
                _connection.WriteErrorAndClose(400);
                return;
            }

            _verb = array[0].GetString();

            var byteString2 = array[1];

            _url = byteString2.GetString();

            if (_url.IndexOf('�') >= 0)
            {
                _url = byteString2.GetString(Encoding.Default);
            }

            if (array.Length == 3)
            {
                _port = array[2].GetString();
            }
            else
            {
                _port = "HTTP/1.0";
            }

            var num = byteString2.IndexOf('?');

            if (num > 0)
            {
                _queryStringBytes = byteString2.Substring(num + 1).GetBytes();
            }
            else
            {
                _queryStringBytes = new byte[0];
            }

            num = _url.IndexOf('?');

            if (num > 0)
            {
                _path = _url.Substring(0, num);
                _queryString = _url.Substring(num + 1);
            }
            else
            {
                _path = _url;
                _queryString = string.Empty;
            }

            if (_path.IndexOf('%') >= 0)
            {
                _path = HttpUtility.UrlDecode(_path, Encoding.UTF8);

                num = _url.IndexOf('?');

                if (num >= 0)
                {
                    _url = _path + _url.Substring(num);
                }
                else
                {
                    _url = _path;
                }
            }

            if (_path != null)
            {
                var num2 = _path.LastIndexOf('.');

                var num3 = _path.LastIndexOf('/');

                if (num2 >= 0 && num3 >= 0 && num2 < num3)
                {
                    var num4 = _path.IndexOf('/', num2);

                    _filePath = _path.Substring(0, num4);

                    _pathInfo = _path.Substring(num4);
                }
                else
                {
                    _filePath = _path;

                    _pathInfo = string.Empty;
                }
            }

            _pathTranslated = MapPath(_filePath);
        }

        /// <summary>
        /// Determines whether the path is bad path.
        /// </summary>
        /// <returns>true if the path is bad path; otherwise, false.</returns>
        private bool IsBadPath()
        {
            return _path.IndexOfAny(BadPathChars) >= 0 || CultureInfo.InvariantCulture.CompareInfo.IndexOf(_path, "..", CompareOptions.Ordinal) >= 0 || CultureInfo.InvariantCulture.CompareInfo.IndexOf(_path, "//", CompareOptions.Ordinal) >= 0;
        }

        /// <summary>
        /// Parses the headers.
        /// </summary>
        private void ParseHeaders()
        {
            _knownRequestHeaders = new string[40];

            var arrayList = new ArrayList();

            for (var i = 1; i < _headerByteStrings.Count; i++)
            {
                var string1 = ((ByteString)_headerByteStrings[i]).GetString();

                var num = string1.IndexOf(':');

                if (num >= 0)
                {
                    var text = string1.Substring(0, num).Trim();

                    var text2 = string1.Substring(num + 1).Trim();

                    var knownRequestHeaderIndex = GetKnownRequestHeaderIndex(text);

                    if (knownRequestHeaderIndex >= 0)
                    {
                        _knownRequestHeaders[knownRequestHeaderIndex] = text2;
                    }
                    else
                    {
                        arrayList.Add(text);
                        arrayList.Add(text2);
                    }
                }
            }

            var num2 = arrayList.Count / 2;

            _unknownRequestHeaders = new string[num2][];

            var num3 = 0;

            for (var j = 0; j < num2; j++)
            {
                _unknownRequestHeaders[j] = new string[2];
                _unknownRequestHeaders[j][0] = (string)arrayList[num3++];
                _unknownRequestHeaders[j][1] = (string)arrayList[num3++];
            }

            if (_headerByteStrings.Count > 1)
            {
                _allRawHeaders = Encoding.UTF8.GetString(_headerBytes, _startHeadersOffset, _endHeadersOffset - _startHeadersOffset);
                return;
            }

            _allRawHeaders = string.Empty;
        }

        /// <summary>
        /// Parses the posted content.
        /// </summary>
        private void ParsePostedContent()
        {
            _contentLength = 0;

            _preloadedContentLength = 0;

            var text = _knownRequestHeaders[11];

            if (text != null)
            {
                try
                {
                    _contentLength = int.Parse(text, CultureInfo.InvariantCulture);
                }
                catch
                {
                }
            }

            if (_headerBytes.Length > _endHeadersOffset)
            {
                _preloadedContentLength = _headerBytes.Length - _endHeadersOffset;

                if (_preloadedContentLength > _contentLength)
                {
                    _preloadedContentLength = _contentLength;
                }

                if (_preloadedContentLength > 0)
                {
                    _preloadedContent = new byte[_preloadedContentLength];
                    Buffer.BlockCopy(_headerBytes, _endHeadersOffset, _preloadedContent, 0, _preloadedContentLength);
                }
            }
        }

        /// <summary>
        /// Skips the all posted content.
        /// </summary>
        private void SkipAllPostedContent()
        {
            if (_contentLength > 0 && _preloadedContentLength < _contentLength)
            {
                byte[] array;

                for (var i = _contentLength - _preloadedContentLength; i > 0; i -= array.Length)
                {
                    array = _connection.ReadRequestBytes(i);

                    if (array == null || array.Length == 0)
                    {
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Determines whether the request is for restricted directory.
        /// </summary>
        /// <returns>true if the request is for restricted directory; otherwise, false.</returns>
        private bool IsRequestForRestrictedDirectory()
        {
            var text = CultureInfo.InvariantCulture.TextInfo.ToLower(_path);

            if (_host.VirtualPath != "/")
            {
                text = text.Substring(_host.VirtualPath.Length);
            }

            var array = RestrictedDirs;

            for (var i = 0; i < array.Length; i++)
            {
                var text2 = array[i];

                if (text.StartsWith(text2, StringComparison.Ordinal) && (text.Length == text2.Length || text[text2.Length] == '/'))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Processes the default document request.
        /// </summary>
        /// <returns>true if succeeded; otherwise, false.</returns>
        private bool ProcessDefaultDocumentRequest()
        {
            if (_verb != "GET")
            {
                return false;
            }

            var text = _pathTranslated;

            if (_pathInfo.Length > 0)
            {
                text = MapPath(_path);
            }

            if (text != null && !Directory.Exists(text))
            {
                return false;
            }

            if (!_path.EndsWith("/", StringComparison.Ordinal))
            {
                var text2 = _path + "/";
                var extraHeaders = "Location: " + GetUrlEncodeRedirect(text2) + "\r\n";
                var body = "<html><head><title>Object moved</title></head><body>\r\n<h2>Object moved to <a href='" + text2 + "'>here</a>.</h2>\r\n</body></html>\r\n";
                _connection.WriteEntireResponseFromString(302, extraHeaders, body, false);

                return true;
            }

            var array = DefaultFileNames;

            for (var i = 0; i < array.Length; i++)
            {
                var text3 = array[i];
                var text4 = text +Path.DirectorySeparatorChar + text3;

                if (File.Exists(text4))
                {
                    _path += text3;
                    _filePath = _path;
                    _url = (_queryString != null) ? (_path + "?" + _queryString) : _path;
                    _pathTranslated = text4;

                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Processes the directory listing request.
        /// </summary>
        /// <returns>true if succeeded; otherwise, false.</returns>
        private bool ProcessDirectoryListingRequest()
        {
            if (_verb != "GET")
            {
                return false;
            }

            var path = _pathTranslated;

            if (_pathInfo.Length > 0)
            {
                path = MapPath(_path);
            }

            if (path != null && !Directory.Exists(path))
            {
                return false;
            }

            if (_host.DisableDirectoryListing)
            {
                return false;
            }

            FileSystemInfo[] elements = null;

            try
            {
                if (path != null) elements = new DirectoryInfo(path).GetFileSystemInfos();
            }
            catch
            {
            }

            string text = null;

            if (_path.Length > 1)
            {
                var num = _path.LastIndexOf('/', _path.Length - 2);
                text = (num > 0) ? _path.Substring(0, num) : "/";

                if (!_host.IsVirtualPathInApp(text))
                {
                    text = null;
                }
            }

            _connection.WriteEntireResponseFromString(200, "Content-type: text/html; charset=utf-8\r\n", Messages.FormatDirectoryListing(_path, text, elements), false);

            return true;
        }

        /// <summary>
        /// Prepares the response.
        /// </summary>
        private void PrepareResponse()
        {
            _headersSent = false;
            _responseStatus = 200;
            _responseHeadersBuilder = new StringBuilder();
            _responseBodyBytes = new ArrayList();
        }

        /// <summary>
        /// Closes the connection.
        /// </summary>
        private void CloseConnectionInternal()
        {
            if (_connection != null)
            {
                _connection.Close();
                var lease = (ILease)RemotingServices.GetLifetimeService(_connection);
                lease.Unregister(_connectionSponsor);
                _connection = null;
                _connectionSponsor = null;
            }
        }

        /// <summary>
        /// Sends the response from file stream.
        /// </summary>
        /// <param name="fileStream">The file stream.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="length">The length.</param>
        private void SendResponseFromFileStream(FileStream fileStream, long offset, long length)
        {
            var length2 = fileStream.Length;

            if (length == -1L)
            {
                length = length2 - offset;
            }

            if (length == 0L || offset < 0L || length > length2 - offset)
            {
                return;
            }

            if (offset > 0L)
            {
                fileStream.Seek(offset, SeekOrigin.Begin);
            }

            if (length <= 65536L)
            {
                var array = new byte[(int)length];
                var length3 = fileStream.Read(array, 0, (int)length);
                SendResponseFromMemory(array, length3);

                return;
            }

            var array2 = new byte[65536];
            var i = (int)length;

            while (i > 0)
            {
                var count = (i < 65536) ? i : 65536;
                var num = fileStream.Read(array2, 0, count);
                SendResponseFromMemory(array2, num);
                i -= num;

                if (i > 0 && num > 0)
                {
                    FlushResponse(false);
                }
            }
        }
    }
}
