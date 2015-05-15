using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Remoting.Lifetime;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using SampWeb.Consts;

namespace SampWeb
{
    public class RequestProcessor : MarshalByRefObject
    {
        private StateObject _currentStateObject;
        public RequestProcessor(StateObject currentStateObject)
        {
            _currentStateObject = currentStateObject;
        }





        public string BuildResponse(int statusCode, Dictionary<string, string> moreHeaders, string content,bool keepalive)
        {
            var builder = new StringBuilder();
            if (moreHeaders!=null)
            {
                foreach (var key in moreHeaders.Keys)
                {
                    builder.AppendFormat("{0}: {1}\r\n",key,moreHeaders[key]);
                }
            }
            var len = RuntimeVars.ResponseEncoding.GetByteCount(content??"");
            var header = BuildHeader(statusCode, builder.ToString(), len, keepalive);
            return header + content;
        }

        public string Build500Response(string content)
        {
            return BuildResponse(500, null, content, false);
        }

        private string BuildHeader(int statusCode, string moreHeaders,int contentLength, bool keepAlive)
        {
           
            var stringBuilder = new StringBuilder();
            stringBuilder.Append("HTTP/1.1 ");
            stringBuilder.Append(statusCode);
            stringBuilder.Append(" ");
            stringBuilder.Append(HttpWorkerRequest.GetStatusDescription(statusCode));
            stringBuilder.Append("\r\n");
            stringBuilder.Append("Server: ");
            stringBuilder.Append(RuntimeVars.CopyingRight);
            stringBuilder.Append("/");
            stringBuilder.Append(RuntimeVars.ServerVersion);
            stringBuilder.Append("\r\n");
            stringBuilder.Append("Date: ");
            stringBuilder.Append(DateTime.Now.ToUniversalTime().ToString("R", DateTimeFormatInfo.InvariantInfo));
            stringBuilder.Append("\r\n");
            if (contentLength >= 0)
            {
                stringBuilder.Append("Content-Length: ");
                stringBuilder.Append(contentLength);
                stringBuilder.Append("\r\n");
            }
            if (!string.IsNullOrWhiteSpace(moreHeaders))
            {
                stringBuilder.Append(moreHeaders);
            }
            if (!keepAlive)
            {
                stringBuilder.Append("Connection: Close\r\n");
            }
            stringBuilder.Append("\r\n");

            return stringBuilder.ToString();
        }

        #region[重载]
        [SecurityPermission(SecurityAction.Assert, Flags = SecurityPermissionFlag.RemotingConfiguration)]
        public override object InitializeLifetimeService()
        {
            var lease = (ILease)base.InitializeLifetimeService();
            if (lease.CurrentState == LeaseState.Initial)
            {
                lease.InitialLeaseTime = TimeSpan.FromMinutes(1.0);
                lease.RenewOnCallTime = TimeSpan.FromSeconds(10.0);
                lease.SponsorshipTimeout = TimeSpan.FromSeconds(30.0);
            }

            return lease;
        }
        #endregion



    }
}
