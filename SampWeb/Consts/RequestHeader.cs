using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SampWeb.Consts
{
    public class RequestHeader
    {
        private Dictionary<string, string> _headCollection;
        public HttpVerb Verb { get; set; }
        public string RelativeUrl { get; set; }/*URL参数*/
        public string Protocal { get; set; }/*HTTP/1.0 HTTP/2.0 HTTPS*/
        public int ContentLength { get; set; }/*该选项仅POST和PUT具有*/
        public string UserAgent { get; set; }/*UA*/
        public IPEndPoint RemoteIpe { get; set; }/*远程IPE*/
        public IPEndPoint LocalIpe { get; set; }/*本地IPE*/
        public byte[] Header { get; set; }
        public byte[] Content { get; set; }


        public Dictionary<string, string> HeadCollection
        {
            get
            {
                if (_headCollection==null)
                {
                    var lines = RuntimeVars.RequestEncoding.GetString(Header).Replace("\r\n","\n").Split('\n');
                    _headCollection=new Dictionary<string, string>();
                    foreach (var line in lines.Skip(1))
                    {
                        var kv = line.Split(':');
                        _headCollection.Add(kv[0].Trim(),kv[1].Trim());
                    }
                }
                return _headCollection;
            }
            
        }
    }

    public enum HttpVerb
    {
        GET,
        POST,
        PUT,
        DELETE
    }
}
