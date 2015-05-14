using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SampWeb.Consts
{
    internal class WebConfig
    {
        /// <summary>
        /// 监听端口
        /// </summary>
        internal ushort WebPort { get; set; }/*端口*/
        /// <summary>
        /// 网站物理路径
        /// </summary>
        internal string PhysicalPath { get; set; }/*路径*/
        /// <summary>
        /// 虚拟路径
        /// </summary>
        internal string VirtualPath { get; set; }/*虚拟路径*/
        /// <summary>
        /// 是否需要请求认证
        /// </summary>
        internal bool RequireAuthentication { get; set; }/*请求认证*/
        /// <summary>
        /// 是否开启目录浏览
        /// </summary>
        internal bool ShowDirectoryList { get; set; }/*目录浏览*/
    }
}
