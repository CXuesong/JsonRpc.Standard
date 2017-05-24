using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using JsonRpc.Dataflow;
using JsonRpc.Standard;
using JsonRpc.Standard.Client;
using JsonRpc.Standard.Contracts;
using JsonRpc.Standard.Server;

namespace ConsoleTestApp
{
    static class Program
    {
        private static readonly IJsonRpcContractResolver myContractResolver = new JsonRpcContractResolver
        {
            // Use camelcase for RPC method names.
            NamingStrategy = new CamelCaseJsonRpcNamingStrategy(),
            // Use camelcase for the property names in parameter value objects
            ParameterValueConverter = new CamelCaseJsonValueConverter()
        };

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            // Here we use two buffers to simulate the console I/O
            var serverBuffer = new BufferBlock<Message>();      // server --> client
            var clientBuffer = new BufferBlock<Message>();      // client --> server
            // Let's start the test client first.
            var clientTask = RunClientAsync(serverBuffer, clientBuffer);
            // Configure & build service host
            var session = new LibrarySessionFeature();
            var host = BuildServiceHost(session);
            // Connect the datablocks
            // If we want server to stop, just stop the source
            using (host.Attach(clientBuffer, serverBuffer))
            using (session.CancellationToken.Register(() => clientBuffer.Complete()))
            {
                // Wait for exit
                session.CancellationToken.WaitHandle.WaitOne();
            }
            Console.WriteLine("Server exited.");
            // Wait for the client to exit.
            clientTask.GetAwaiter().GetResult();
        }

        private static DataflowRpcServerHandler BuildServiceHost(LibrarySessionFeature session)
        {
            var builder = new JsonRpcServiceHostBuilder
            {
                ContractResolver = myContractResolver,
            };
            // Register all the services (public classes) found in the assembly
            builder.Register(typeof(Program).GetTypeInfo().Assembly);
            // Add a middleware to log the requests and responses
            builder.Intercept(async (context, next) =>
            {
                Console.WriteLine("> {0}", context.Request);
                await next();
                Console.WriteLine("< {0}", context.Response);
            });
            var host = builder.Build();
            var features = new FeatureCollection();
            // Though it's suggested that all feature types be interface types, for sake of
            // simplicity, here we just use a concrete class.
            features.Set<LibrarySessionFeature>(session);
            return new DataflowRpcServerHandler(host) {DefaultFeatures = features};
        }

        private static void ClientWriteLine(object s)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(s);
            Console.ResetColor();
        }

        public static async Task RunClientAsync(BufferBlock<Message> inBuffer, BufferBlock<Message> outBuffer)
        {
            await Task.Yield(); // We want this task to run on another thread.
            var clientHandler = new DataflowRpcClientHandler();
            var client = new JsonRpcClient(clientHandler);
            var builder = new JsonRpcProxyBuilder
            {
                ContractResolver = myContractResolver
            };
            var proxy = builder.CreateProxy<ILibraryService>(client);
            clientHandler.Attach(inBuffer, outBuffer);
            ClientWriteLine("Add books…");
            await proxy.PutBookAsync(new Book("Somewhere Within the Shadows", "Juan Díaz Canales & Juanjo Guarnido",
                new DateTime(2004, 1, 1),
                "1596878177"));
            await proxy.PutBookAsync(new Book("Arctic Nation", "Juan Díaz Canales & Juanjo Guarnido",
                new DateTime(2004, 1, 1),
                "0743479351"));
            ClientWriteLine("Available books:");
            foreach (var isbn in await proxy.EnumBooksIsbn())
            {
                var book = await proxy.GetBookAsync(isbn);
                ClientWriteLine(book);
            }
            ClientWriteLine("Attempt to query for an inexistent ISBN…");
            try
            {
                await proxy.GetBookAsync("test", true);
            }
            catch (JsonRpcRemoteException ex)
            {
                ClientWriteLine(ex);
            }
            ClientWriteLine("Attempt to pass some invalid argument…");
            try
            {
                await proxy.PutBookAsync(null);
            }
            catch (JsonRpcRemoteException ex)
            {
                ClientWriteLine(ex);
            }
            ClientWriteLine("Will shut down server in 5 seconds…");
            await Task.Delay(5000);
            proxy.Terminate();
        }
    }
}