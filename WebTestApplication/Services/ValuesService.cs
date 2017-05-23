using JsonRpc.Standard.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonRpc.AspNetCore;
using JsonRpc.Standard.Contracts;
using Microsoft.AspNetCore.Http;

namespace WebTestApplication.Services
{
    public class ValuesService : JsonRpcService
    {

        [JsonRpcMethod]
        public object GetValue()
        {
            return new[] {"value1", "value2"};
        }

        [JsonRpcMethod]
        public object GetValue(int id)
        {
            return "value of " + id;
        }

        [JsonRpcMethod(IsNotification = true)]
        public void Notify()
        {
            var session = RequestContext.GetHttpContext().Session;
            var ct = session.GetInt32("counter") ?? 0;
            session.SetInt32("counter", ct + 1);
        }

        [JsonRpcMethod]
        public int GetCounter()
        {
            return RequestContext.GetHttpContext().Session.GetInt32("counter") ?? -1;
        }

    }
}
