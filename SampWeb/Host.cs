using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Hosting;
using SampWeb.Consts;

namespace SampWeb
{
    public class Host : MarshalByRefObject, IRegisteredObject
    {
        public Host()
        {
            HostingEnvironment.RegisterObject(this);
        }
        public void Stop(bool immediate)
        {
            throw new NotImplementedException();
        }

        public void ProcessRequest(RequestProcessor processor)
        {
            //var conn = new RequestProcessor(state);
            //conn.StartProcess(config);

        }
    }
}
