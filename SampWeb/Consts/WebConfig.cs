using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SampWeb.Consts
{
    public class WebConfig
    {
        /// <summary>
        /// 监听端口
        /// </summary>
        public ushort WebPort { get;private set; }/*端口*/
        /// <summary>
        /// 网站物理路径
        /// </summary>
        public string PhysicalPath { get; private set; }/*路径*/
        /// <summary>
        /// 虚拟路径
        /// </summary>
        public string VirtualPath { get; private set; }/*虚拟路径*/
        /// <summary>
        /// 是否需要请求认证
        /// </summary>
        public bool RequireAuthentication { get; private set; }/*请求认证*/
        /// <summary>
        /// 是否开启目录浏览
        /// </summary>
        public bool ShowDirectoryList { get; private set; }/*目录浏览*/

        public WebConfig(ushort port,string physicalPath,string virtualPath,bool requireAuth,bool showDirectoryList)
        {
            WebPort = port;
            PhysicalPath =  Path.GetFullPath(string.IsNullOrWhiteSpace(physicalPath) ? "." : physicalPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            VirtualPath = string.IsNullOrWhiteSpace(virtualPath)?"":virtualPath;
            RequireAuthentication = requireAuth;
            ShowDirectoryList = showDirectoryList;
        }
    }
}
