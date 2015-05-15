using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SampWeb.Consts
{
    public class RequestHeader
    {
        public HttpVerb Verb { get; set; }
        public string QueryStr { get; set; }/*URL参数*/
        public string Protocal { get; set; }/*HTTP/1.0 HTTP/2.0 HTTPS*/
        public int ContentLength { get; set; }/*该选项仅POST和PUT具有*/
        public string UserAgent { get; set; }/*UA*/
    }

    public enum HttpVerb
    {
        GET,
        POST,
        PUT,
        DELETE
    }
}
