using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;

namespace SampWeb
{
    [PermissionSet(SecurityAction.InheritanceDemand, Name = "FullTrust")]
    public class WebServer : MarshalByRefObject, IDisposable
    {





        #region [重写]
        public void Dispose()
        {

        }
        #endregion
    }
}
