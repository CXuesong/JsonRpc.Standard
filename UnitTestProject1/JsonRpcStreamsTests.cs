using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.Standard;
using JsonRpc.Standard.Client;
using JsonRpc.Standard.Server;
using JsonRpc.Streams;
using Nerdbank;
using Newtonsoft.Json.Linq;
using UnitTestProject1;
using Xunit;
using Xunit.Abstractions;

namespace UnitTestProject1
{
    public class JsonRpcStreamsTests : UnitTestBase
    {

        /// <inheritdoc />
        public JsonRpcStreamsTests(ITestOutputHelper output) : base(output)
        {

        }

        [Fact]
        public async Task ServerHandlerTest()
        {
            var request = new RequestMessage(123, "add", JToken.FromObject(new {x = 20, y = 35}));
            (var ss, var cs) = FullDuplexStream.CreateStreams();
            using (var clientReader = new StreamReader(cs))
            using (var clientWriter = new StreamWriter(cs))
            using (var serverReader = new ByLineTextMessageReader(ss))
            using (var serverWriter = new ByLineTextMessageWriter(ss))
            {
                async Task<ResponseMessage> WaitForResponse()
                {
                    var sw = Stopwatch.StartNew();
                    var content = await clientReader.ReadLineAsync();
                    Output.WriteLine($"Received response in {sw.Elapsed}.");
                    return (ResponseMessage) Message.LoadJson(content);
                }
                async Task<ResponseMessage> SendRequest(MessageId messageId)
                {
                    request.Id = messageId;
                    request.WriteJson(clientWriter);
                    clientWriter.WriteLine();
                    await clientWriter.FlushAsync();
                    var response = await WaitForResponse();
                    Assert.Equal(messageId, response.Id);
                    Assert.Null(response.Error);
                    Assert.Equal((int) response.Result, 55);
                    return response;
                }
                using (var server = new ServerTestHelper(this, serverReader, serverWriter,
                    StreamRpcServerHandlerOptions.None))
                {
                    await SendRequest(123);
                    await SendRequest("abc");
                }
            }
        }

        [Fact]
        public async Task PartwiseStreamInteropTest()
        {
            (var ss, var cs) = FullDuplexStream.CreateStreams();
            using (var clientReader = new ByLineTextMessageReader(cs))
            using (var clientWriter = new ByLineTextMessageWriter(cs))
            using (var serverReader = new ByLineTextMessageReader(ss))
            using (var serverWriter = new ByLineTextMessageWriter(ss))
            using (var server = new ServerTestHelper(this, serverReader, serverWriter,
                StreamRpcServerHandlerOptions.None))
            using (var client = new ClientTestHelper(clientReader, clientWriter))
            {
                await TestRoutines.TestStubAsync(client.ClientStub);
                await TestRoutines.TestStubAsync(client.ClientExceptionStub);
            }
        }

        public class ServerTestHelper : IDisposable
    {

            private readonly List<IDisposable> disposables = new List<IDisposable>();

        public ServerTestHelper(UnitTestBase owner, MessageReader reader, MessageWriter writer,
            StreamRpcServerHandlerOptions options)
        {
            ServiceHost = Utility.CreateJsonRpcServiceHost(owner);
            ServerHandler = new StreamRpcServerHandler(ServiceHost, options);
            ServerMessageReader = reader;
            ServerMessageWriter = writer;
            disposables.Add(ServerHandler.Attach(ServerMessageReader, ServerMessageWriter));

            disposables.Add(ServerMessageReader);
            disposables.Add(ServerMessageWriter);
        }

        public IJsonRpcServiceHost ServiceHost { get; }

            public MessageReader ServerMessageReader { get; }

            public MessageWriter ServerMessageWriter { get; }

            public StreamRpcServerHandler ServerHandler { get; }

            /// <inheritdoc />
            public void Dispose()
            {
                foreach (var d in disposables) d.Dispose();
                disposables.Clear();
            }
        }

        public class ClientTestHelper : IDisposable
        {

            private readonly List<IDisposable> disposables = new List<IDisposable>();

            public ClientTestHelper(MessageReader reader, MessageWriter writer)
            {
                ClientHandler = new StreamRpcClientHandler();
                Client = new JsonRpcClient(ClientHandler);
                ClientMessageReader = reader;
                ClientMessageWriter = writer;
                disposables.Add(ClientHandler.Attach(ClientMessageReader, ClientMessageWriter));

                var proxyBuilder = new JsonRpcProxyBuilder {ContractResolver = Utility.DefaultContractResolver};
                ClientStub = proxyBuilder.CreateProxy<ITestRpcContract>(Client);
                ClientExceptionStub = proxyBuilder.CreateProxy<ITestRpcExceptionContract>(Client);

                disposables.Add(ClientMessageReader);
                disposables.Add(ClientMessageWriter);
            }

            public JsonRpcClient Client { get; }

            public StreamRpcClientHandler ClientHandler { get; }

            public MessageReader ClientMessageReader { get; }

            public MessageWriter ClientMessageWriter { get; }

            public ITestRpcContract ClientStub { get; }

            public ITestRpcExceptionContract ClientExceptionStub { get; }
            
            /// <inheritdoc />
            public void Dispose()
            {
                foreach (var d in disposables) d.Dispose();
                disposables.Clear();
            }
        }
    }
}
