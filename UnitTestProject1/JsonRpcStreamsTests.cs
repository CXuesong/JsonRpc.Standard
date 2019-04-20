using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JsonRpc.DynamicProxy.Client;
using JsonRpc.Standard;
using JsonRpc.Standard.Client;
using JsonRpc.Standard.Server;
using JsonRpc.Streams;
using Nerdbank.Streams;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnitTestProject1.Helpers;
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

        private readonly RequestMessage TestMessage = new RequestMessage(1, "test");

        private readonly byte[] TestMessagePartwiseStreamContent =
            Encoding.UTF8.GetBytes(
                "Content-Length: 40\r\nContent-Type: application/json-rpc;charset=utf-8\r\n\r\n{\"id\":1,\"method\":\"test\",\"jsonrpc\":\"2.0\"}");

        [Fact]
        public async Task PartwiseStreamWriterTest()
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new PartwiseStreamMessageWriter(stream))
                {
                    writer.LeaveStreamOpen = true;
                    await writer.WriteAsync(TestMessage, CancellationToken.None);
                }
                stream.Seek(0, SeekOrigin.Begin);
                using (var reader = new StreamReader(stream))
                {
                    var text = reader.ReadToEnd();
                    Output.WriteLine(text);
                }
                Assert.Equal(TestMessagePartwiseStreamContent, stream.ToArray());
            }
        }

        [Fact]
        public async Task PartwiseStreamReaderTest()
        {
            var buffer = TestMessagePartwiseStreamContent.Concat(TestMessagePartwiseStreamContent).ToArray();
            using (var stream = new MemoryStream(buffer, false))
            {
                using (var reader = new PartwiseStreamMessageReader(stream))
                {
                    var message1 = await reader.ReadAsync(m => true, CancellationToken.None);
                    Output.WriteLine(message1.ToString());
                    var message2 = await reader.ReadAsync(m => true, CancellationToken.None);
                    Output.WriteLine(message2.ToString());
                    Assert.Null(await reader.ReadAsync(m => true, CancellationToken.None));
                    Assert.Equal(JsonConvert.SerializeObject(TestMessage), JsonConvert.SerializeObject(message1));
                    Assert.Equal(JsonConvert.SerializeObject(TestMessage), JsonConvert.SerializeObject(message2));
                }
            }
        }

        [Theory]
        [InlineData("Content-Length: -40\r\n\r\n")]
        [InlineData("Content-Length: ABC\r\n\r\n")]
        [InlineData("Content-Length: 3\r\nContent-Type: application/json-rpc;charset=invalid-charset\r\n\r\n{ }")]
        [InlineData("Content-Length: 40\r\nContent-Type: application/json-rpc;charset=utf-8\r\n\r\nABC")]
        [InlineData("Content-Length: 3\r\nContent-Type: application/json-rpc;charset=utf-8\r\n\r\nABC")]
        public async Task PartwiseStreamReaderExceptionTest(string jsonContent)
        {
            var buffer = Encoding.UTF8.GetBytes(jsonContent);
            using (var stream = new MemoryStream(buffer, false))
            {
                using (var reader = new PartwiseStreamMessageReader(stream))
                {
                    var ex = await Assert.ThrowsAsync<MessageReaderException>(() =>
                        reader.ReadAsync(m => true, CancellationToken.None));
                    Output.WriteLine(ex.Message);
                }
            }
        }

        [Fact]
        public async Task ServerHandlerTest()
        {
            var request = new RequestMessage(123, "add", JToken.FromObject(new {x = 20, y = 35}));
            (var ss, var cs) = FullDuplexStream.CreatePair();
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
                    Assert.Equal(55, (int) response.Result);
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
            (var ss, var cs) = FullDuplexStream.CreatePair();
            using (var clientReader = new ByLineTextMessageReader(cs))
            using (var clientWriter = new ByLineTextMessageWriter(cs))
            using (var serverReader = new ByLineTextMessageReader(ss))
            using (var serverWriter = new ByLineTextMessageWriter(ss))
            using (var server = new ServerTestHelper(this, serverReader, serverWriter,
                StreamRpcServerHandlerOptions.None))
            using (var client = new ClientTestHelper(clientReader, clientWriter))
            {
                var e = Assert.Raises<MessageEventArgs>(h => client.ClientHandler.MessageSending += h,
                    h => client.ClientHandler.MessageSending -= h,
                    () => client.ClientStub.One());
                Assert.Equal("one", ((RequestMessage) e.Arguments.Message).Method);
                e = Assert.Raises<MessageEventArgs>(h => client.ClientHandler.MessageReceiving += h,
                    h => client.ClientHandler.MessageReceiving -= h,
                    () => client.ClientStub.One());
                Assert.Equal(new JValue(1), ((ResponseMessage) e.Arguments.Message).Result);
                await TestRoutines.TestStubAsync(client.ClientStub);
                await TestRoutines.TestStubAsync(client.ClientExceptionStub);
            }
        }

        [Fact]
        public async Task PartwiseStreamCancellationTest()
        {
            (var ss, var cs) = FullDuplexStream.CreatePair();
            using (var clientReader = new ByLineTextMessageReader(cs))
            using (var clientWriter = new ByLineTextMessageWriter(cs))
            using (var serverReader = new ByLineTextMessageReader(ss))
            using (var serverWriter = new ByLineTextMessageWriter(ss))
            using (var server = new ServerTestHelper(this, serverReader, serverWriter,
                StreamRpcServerHandlerOptions.SupportsRequestCancellation))
            using (var client = new ClientTestHelper(clientReader, clientWriter))
            {
                await TestRoutines.TestCancellationAsync(client.ClientCancellationStub);
            }
        }

        [Fact]
        public async Task ConsistentResponseSequenceTest()
        {
            (var ss, var cs) = FullDuplexStream.CreatePair();
            using (var clientReader = new ByLineTextMessageReader(cs))
            using (var clientWriter = new ByLineTextMessageWriter(cs))
            using (var serverReader = new ByLineTextMessageReader(ss))
            using (var serverWriter = new ByLineTextMessageWriter(ss))
            using (var client = new ClientTestHelper(clientReader, clientWriter))
            {
                using (var server = new ServerTestHelper(this, serverReader, serverWriter,
                    StreamRpcServerHandlerOptions.None))
                {
                    // The responses are ordered by the time of completion.
                    var delayTask =
                        client.ClientStub.DelayAsync(TimeSpan.FromMilliseconds(200), CancellationToken.None);
                    var addTask = client.ClientStub.AddAsync(3, -4);
                    var result = await addTask;
                    Assert.Equal(-1, result);
                    // addTask completes first.
                    Assert.False(delayTask.IsCompleted);
                    await delayTask;
                }
                using (var server = new ServerTestHelper(this, serverReader, serverWriter,
                    StreamRpcServerHandlerOptions.ConsistentResponseSequence))
                {
                    // The responses are in the same order as the requests.
                    var delayTask =
                        client.ClientStub.DelayAsync(TimeSpan.FromMilliseconds(200), CancellationToken.None);
                    var addTask = client.ClientStub.AddAsync(10, 20);
                    await Task.Delay(100);
                    // addTask is held up.
                    Assert.False(addTask.IsCompleted);
                    await delayTask;
                    var result = await addTask;
                    Assert.Equal(30, result);
                }
            }
        }

        // #5 StreamRpcServerHandler.TryCancelRequest may cause server unable to respond to the subsequent requests
        [Fact]
        public async Task PartwiseStreamConsistentSequenceCancellationTest()
        {
            (var ss, var cs) = FullDuplexStream.CreatePair();
            using (var clientReader = new ByLineTextMessageReader(cs))
            using (var clientWriter = new ByLineTextMessageWriter(cs))
            using (var serverReader = new ByLineTextMessageReader(ss))
            using (var serverWriter = new ByLineTextMessageWriter(ss))
            using (var server = new ServerTestHelper(this, serverReader, serverWriter,
                StreamRpcServerHandlerOptions.ConsistentResponseSequence
                | StreamRpcServerHandlerOptions.SupportsRequestCancellation))
            using (var client = new ClientTestHelper(clientReader, clientWriter))
            {
                await TestRoutines.TestCancellationAsync(client.ClientCancellationStub);
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
                // We use positional parameters when issuing cancelRequest request. Just for more test coverage.
                Client.RequestCancelling += (_, e) => { Client.SendNotificationAsync("cancelRequest", new JArray(e.RequestId.Value), CancellationToken.None); };
                disposables.Add(ClientHandler.Attach(ClientMessageReader, ClientMessageWriter));

                var proxyBuilder = new JsonRpcProxyBuilder {ContractResolver = Utility.DefaultContractResolver};
                ClientStub = proxyBuilder.CreateProxy<ITestRpcContract>(Client);
                ClientExceptionStub = proxyBuilder.CreateProxy<ITestRpcExceptionContract>(Client);
                ClientCancellationStub = proxyBuilder.CreateProxy<ITestRpcCancellationContract>(Client);
            }

            public JsonRpcClient Client { get; }

            public StreamRpcClientHandler ClientHandler { get; }

            public MessageReader ClientMessageReader { get; }

            public MessageWriter ClientMessageWriter { get; }

            public ITestRpcContract ClientStub { get; }

            public ITestRpcExceptionContract ClientExceptionStub { get; }

            public ITestRpcCancellationContract ClientCancellationStub { get; }

            /// <inheritdoc />
            public void Dispose()
            {
                foreach (var d in disposables) d.Dispose();
                disposables.Clear();
            }
        }
    }
}
