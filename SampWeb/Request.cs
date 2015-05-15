using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Hosting;
using SampWeb.Consts;

namespace SampWeb
{
    public class Request : SimpleWorkerRequest
    {
        private readonly RequestHeader _header;
        private readonly string _requestBody;
        public Request(byte[]requestBts): base(string.Empty, string.Empty, null)
        {
            var strs = RuntimeVars.ResponseEncoding.GetString(requestBts);
            var header = strs.Substring(0, strs.IndexOf("\r\n\r\n"));
            var lines = header.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

            _header=new RequestHeader();
        }

        public override string GetUriPath()
        {
            return base.GetUriPath();
        }

        public override string GetQueryString()
        {
            return base.GetQueryString();
        }

        public override byte[] GetQueryStringRawBytes()
        {
            return base.GetQueryStringRawBytes();
        }

        public override string GetRawUrl()
        {
            return base.GetRawUrl();
        }

        public override string GetHttpVerbName()
        {
            return base.GetHttpVerbName();
        }

        public override string GetHttpVersion()
        {
            return base.GetHttpVersion();
        }

        public override string GetRemoteAddress()
        {
            return base.GetRemoteAddress();
        }

        public override int GetRemotePort()
        {
            return base.GetRemotePort();
        }

        public override string GetLocalAddress()
        {
            return base.GetLocalAddress();
        }

        public override int GetLocalPort()
        {
            return base.GetLocalPort();
        }

        public override string GetServerName()
        {
            return base.GetServerName();
        }

        public override string GetFilePath()
        {
            return base.GetFilePath();
        }

        public override string GetFilePathTranslated()
        {
            return base.GetFilePathTranslated();
        }

        public override string GetPathInfo()
        {
            return base.GetPathInfo();
        }

        public override string GetAppPath()
        {
            return base.GetAppPath();
        }

        public override string GetAppPathTranslated()
        {
            return base.GetAppPathTranslated();
        }

        public override byte[] GetPreloadedEntityBody()
        {
            return base.GetPreloadedEntityBody();
        }

    }
}
