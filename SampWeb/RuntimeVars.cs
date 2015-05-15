using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SampWeb
{
    public static class RuntimeVars
    {
        public const string CopyingRight = "Server.SampWeb";
        public const string ServerVersion = "0.0.1.0 Alpha";
        public static readonly Encoding RequestEncoding = Encoding.UTF8;
        public static readonly Encoding ResponseEncoding = Encoding.UTF8;
        public static readonly string[] RestrictedDirs =
        {
            "/bin",
            "/app_browsers",
            "/app_code",
            "/app_data",
            "/app_localresources",
            "/app_globalresources",
            "/app_webreferences"
        };
        public static readonly string[] DefaultFileNames = new string[]
        {
            "default.aspx",
            "default.asmx",
            "default.htm",
            "default.html"
        };
    }
}
