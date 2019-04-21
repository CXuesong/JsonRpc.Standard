using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonRpc.AspNetCore;
using JsonRpc.Server;
using JsonRpc.Streams;
using JsonRpc.WebSockets;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace WebTestApplication.Controllers
{
    [Route("api/[controller]")]
    public class JsonRpcController : Controller
    {

        private readonly IJsonRpcServiceHost _JsonRpcServiceHost;

        public JsonRpcController(IJsonRpcServiceHost jsonRpcServiceHost)
        {
            _JsonRpcServiceHost = jsonRpcServiceHost;
        }

        // GET: api/<controller>
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                var serverHandler = new StreamRpcServerHandler(_JsonRpcServiceHost)
                {
                    // This will make IJsonRpcServiceHost built with default JsonRpc.AspNetCore.JsonRpcOptions value works.
                    // Its ServiceFactory needs HttpContext to work.
                    DefaultFeatures = new SingleFeatureCollection<IAspNetCoreFeature>(AspNetCoreFeature.FromHttpContext(HttpContext))
                };
                using (var reader = new WebSocketMessageReader(webSocket))
                using (var writer = new WebSocketMessageWriter(webSocket))
                using (serverHandler.Attach(reader, writer))
                {
                    await reader.WebSocketClose;
                }
                return new EmptyResult();
            }
            return BadRequest("Needs Websocket connection.");
        }

    }
}
