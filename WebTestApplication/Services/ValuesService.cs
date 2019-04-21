using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonRpc.AspNetCore;
using JsonRpc.Contracts;
using JsonRpc.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace WebTestApplication.Services
{
    public class ValuesService : JsonRpcService
    {

        private readonly ILogger logger;

        public ValuesService(ILoggerFactory loggerFactory)
        {
            // Inject loggerFactory from constructor.
            if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));
            logger = loggerFactory.CreateLogger<ValuesService>();
        }

        [JsonRpcMethod]
        public object GetValue()
        {
            return new[] {"value1", "value2"};
        }

        [JsonRpcMethod]
        public object GetValue(int id)
        {
            if (id < 0) throw new ArgumentOutOfRangeException(nameof(id), "Id should be a non-negative integer.");
            return "value of " + id;
        }

        [JsonRpcMethod(IsNotification = true)]
        public void Notify()
        {
            var session = RequestContext.GetHttpContext().Session;
            var ct = session.GetInt32("counter") ?? 0;
            ct++;
            session.SetInt32("counter", ct);
            logger.LogInformation("Counter increased: {counter}.", ct);
        }

        [JsonRpcMethod]
        public int GetCounter()
        {
            return RequestContext.GetHttpContext().Session.GetInt32("counter") ?? -1;
        }

    }
}
