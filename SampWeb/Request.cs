using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Lifetime;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using Microsoft.Win32.SafeHandles;
using SampWeb.Consts;

namespace SampWeb
{
    public class Request : SimpleWorkerRequest
    {
        private readonly WebConfig _config;
        private readonly RequestHeader _header;
        private  RequestProcessor _requestProcessor;
        private readonly string _rawUri;
        private readonly string _querystr;
        private readonly Dictionary<string,string>_knownHeaders=new Dictionary<string, string>();
        private readonly string[][] _unKnownHeaders;
        private Sponsor _connectionSponsor;
        /*经过托管方面返回的信息*/
        private int _responseStatusCode;
        private StringBuilder _responseHeadersBuilder;
        private bool _specialCaseStaticFileHeaders;
        private bool _headersSent;
        private List<byte>_responseContentBytes=new List<byte>();

         [SecurityPermission(SecurityAction.Assert, Flags = SecurityPermissionFlag.RemotingConfiguration)]
        public Request(RequestHeader header,WebConfig config, RequestProcessor requestProcessor): base(string.Empty, string.Empty, null)
        {
            _header = header;
            _config = config;
            _requestProcessor = requestProcessor;
            var index = _header.RelativeUrl.IndexOf('?');
            _rawUri = index > -1 ? _header.RelativeUrl.Substring(0, index) : _header.RelativeUrl;
            _querystr = index > -1 ? _header.RelativeUrl.Substring(index + 1) : string.Empty;
            var lst = new List<string[]>();
            foreach (var head in _header.HeadCollection)
            {
                var knowIndex = GetKnownResponseHeaderIndex(head.Key);
                if (knowIndex>=0)
                {
                    _knownHeaders.Add(head.Key,head.Value);
                }
                else
                {
                    lst.Add(new []{head.Key,head.Value});
                }
            }
            _unKnownHeaders=lst.ToArray();
           _responseHeadersBuilder=new StringBuilder();

           var lease = (ILease)RemotingServices.GetLifetimeService(_requestProcessor);
           _connectionSponsor = new Sponsor();
           lease.Register(_connectionSponsor);
        }



        public void Process()
        {
            if (IsRequestForRestrictedDirectory())
            {
                _requestProcessor.WriteErrorAndClose(403);
                return;
            }
            if (ProcessDefaultDocumentRequest())
            {
                return;
            }
            HttpRuntime.ProcessRequest(this);
        }

        public override string GetUriPath()
        {
            return _rawUri;
        }

        public override string GetQueryString()
        {
            return _querystr;
        }

        public override byte[] GetQueryStringRawBytes()
        {
            return RuntimeVars.RequestEncoding.GetBytes(_querystr);
        }

        public override string GetRawUrl()
        {
            return _header.RelativeUrl;
        }

        public override string GetHttpVerbName()
        {
            return _header.Verb.ToString();
        }

        public override string GetHttpVersion()
        {
            return _header.Protocal;
        }

        public override string GetRemoteAddress()
        {
            return _header.RemoteIpe.Address.ToString();
        }

        public override int GetRemotePort()
        {
            return _header.RemoteIpe.Port;
        }

        public override string GetLocalAddress()
        {
            return _header.LocalIpe.Address.ToString();
        }

        public override int GetLocalPort()
        {
            return _header.LocalIpe.Port;
        }

        public override string GetServerName()
        {
            var address = _header.LocalIpe.Address.ToString();
            if (address=="127.0.0.1")
            {
                return "localhost";
            }
            return address;
        }

        public override string GetFilePath()
        {
            return _rawUri;
        }

        public override string GetFilePathTranslated()
        {
            return MapPath(_rawUri);
        }

        public override string GetPathInfo()
        {
            return _rawUri.Substring(GetFilePath().Length);
        }

        public override string GetAppPath()
        {
            return _config.VirtualPath;
        }

        public override string GetAppPathTranslated()
        {
            return _config.PhysicalPath;
        }

        public override byte[] GetPreloadedEntityBody()
        {
            return _header.Content;
        }

        public override int GetPreloadedEntityBodyLength()
        {
            return _header.ContentLength;
        }

        public override bool IsEntireEntityBodyIsPreloaded()
        {
            return true;
        }

        public override int ReadEntityBody(byte[] buffer, int offset, int size)
        {
            if (_header.ContentLength>0)
            {
                var left = _header.ContentLength - offset;
                var actSize = Math.Min(left,size);
                Buffer.BlockCopy(_header.Content, offset, buffer, 0, actSize);
                return actSize;
            }
            return 0;
        }

        public override string GetKnownRequestHeader(int index)
        {
            return _knownHeaders.Values.ToArray()[index];
        }

        public override string GetUnknownRequestHeader(string name)
        {
            if (_knownHeaders.ContainsKey(name))
            {
                return _knownHeaders[name];
            }
            return string.Empty;
        }

        public override string[][] GetUnknownRequestHeaders()
        {
            return _unKnownHeaders;
        }

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
                                if (this.GetUserToken() != IntPtr.Zero)
                                {
                                    result = "NTLM";
                                }
                            }
                        }
                        else
                        {
                            if (this.GetUserToken() != IntPtr.Zero)
                            {
                                result = string.Empty;
                            }
                        }
                    }
                    else
                    {
                        result = _header.Protocal;
                    }
                }
                else
                {
                    result = RuntimeVars.RequestEncoding.GetString(_header.Header);
                }
            }

            return result;
        }

        public override IntPtr GetUserToken()
        {
            return IntPtr.Zero;
        }

        public override string MapPath(string path)
        {
            if (string.IsNullOrEmpty(path) || path ==Path.PathSeparator.ToString())
            {
                return _config.PhysicalPath;
            }
            var realPath = path;//.Replace("/", Path.PathSeparator.ToString(CultureInfo.InvariantCulture)).TrimStart(Path.PathSeparator);
            if (realPath.StartsWith(_config.VirtualPath))
            {
                return Path.Combine(_config.PhysicalPath, realPath.Substring(_config.VirtualPath.Length));
            }
            return Path.Combine(_config.PhysicalPath, realPath);
        }

        public override void SendStatus(int statusCode, string statusDescription)
        {
            _responseStatusCode = statusCode;
        }

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
                            if (this._specialCaseStaticFileHeaders)
                            {
                                return;
                            }

                            break;

                        case 20:
                            if (value == "bytes")
                            {
                                this._specialCaseStaticFileHeaders = true;
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

        public override void SendCalculatedContentLength(int contentLength)
        {
            if (!this._headersSent)
            {
                this._responseHeadersBuilder.Append("Content-Length: ");
                this._responseHeadersBuilder.Append(contentLength.ToString(CultureInfo.InvariantCulture));
                this._responseHeadersBuilder.Append("\r\n");
            }
        }

        public override bool HeadersSent()
        {
            return _headersSent;
        }

        public override bool IsClientConnected()
        {
            return _requestProcessor.CurrentStateObject.ClientSocket.Connected;
        }

        public override void CloseConnection()
        {
            _requestProcessor.Close();
            var lease = (ILease)RemotingServices.GetLifetimeService(_requestProcessor);
            lease.Unregister(this._connectionSponsor);
            _requestProcessor = null;
            _connectionSponsor = null;
        }


        public override void SendResponseFromMemory(byte[] data, int length)
        {
            if (length > 0)
            {
                var array = new byte[length];
                Buffer.BlockCopy(data, 0, array, 0, length);
                _responseContentBytes.AddRange(array);
            }
        }

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

        public override void FlushResponse(bool finalFlush)
        {
            if (_responseStatusCode == 404 && !_headersSent && finalFlush && _header.Verb==HttpVerb.GET && this.ProcessDirectoryListingRequest())
            {
                return;
            }


            if (!this._headersSent)
            {
                _requestProcessor.WriteHeader(_responseStatusCode,_responseHeadersBuilder.ToString());
                this._headersSent = true;
            }
            _requestProcessor.WriteBody(_responseContentBytes.ToString());
            if (finalFlush)
            {
                _requestProcessor.Close();
            }
        }

        

        #region[辅助方法]
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
        private bool ProcessDirectoryListingRequest()
        {
            if (_header.Verb != HttpVerb.GET)
            {
                return false;
            }
            var _path = GetFilePath();
            var _pathInfo = GetPathInfo();
            var path = GetAppPathTranslated();


            if (_pathInfo.Length > 0)
            {
                path = MapPath(_path);
            }

            if (!Directory.Exists(path))
            {
                return false;
            }

            if (_config.ShowDirectoryList==false)
            {
                return false;
            }

            FileSystemInfo[] elements = null;

            try
            {
                elements = new DirectoryInfo(path).GetFileSystemInfos();
            }
            catch
            {
            }

            string text = null;

            if (_path.Length > 1)
            {
                int num = _path.LastIndexOf('/', _path.Length - 2);
                text = (num > 0) ? _path.Substring(0, num) : "/";

                if (!text.StartsWith(_config.VirtualPath, true, CultureInfo.InvariantCulture))
                {
                    text = null;
                }
            }
            _requestProcessor.WriteResponse(200, new Dictionary<string, string>() { { "Content-Type", " text/html; charset=utf-8" } }, Messages.FormatDirectoryListing(_path, text, elements), false);
            return true;
        }
        private bool IsRequestForRestrictedDirectory()
        {
            string text = CultureInfo.InvariantCulture.TextInfo.ToLower(_rawUri);

            if (_config.VirtualPath != "/")
            {
                text = text.Substring(this._config.VirtualPath.Length);
            }

            string[] array = RuntimeVars.RestrictedDirs;

            for (int i = 0; i < array.Length; i++)
            {
                string text2 = array[i];

                if (text.StartsWith(text2, StringComparison.Ordinal) && (text.Length == text2.Length || text[text2.Length] == '/'))
                {
                    return true;
                }
            }

            return false;
        }
        private bool ProcessDefaultDocumentRequest()
        {
            if (_header.Verb != HttpVerb.GET)
            {
                return false;
            }
            var _path = GetFilePath();
            var _pathInfo = GetPathInfo();
            var text = GetAppPathTranslated();
            

            if (_pathInfo.Length > 0)
            {
                text = this.MapPath(_path);
            }

            if (!Directory.Exists(text))
            {
                return false;
            }

            if (!_path.EndsWith("/", StringComparison.Ordinal))
            {
               
                string text2 = _path + "/";
                string extraHeaders = "Location: " + HttpUtility.UrlEncode(text2) + "\r\n";
                string body = "<html><head><title>Object moved</title></head><body>\r\n<h2>Object moved to <a href='" + text2 + "'>here</a>.</h2>\r\n</body></html>\r\n";
                _requestProcessor.WriteResponseString(302, extraHeaders, body, false);

                return true;
            }

            string[] array = RuntimeVars.DefaultFileNames;

            for (int i = 0; i < array.Length; i++)
            {
                string text3 = array[i];
                string text4 = text + Path.DirectorySeparatorChar + text3;

                if (File.Exists(text4))
                {
                    //_path += text3;
                    //_filePath = this._path;
                    //this._url = (this._queryString != null) ? (this._path + "?" + this._queryString) : this._path;
                    //this._pathTranslated = text4;

                    return false;
                }
            }

            return false;
        }
        #endregion

    }
}
