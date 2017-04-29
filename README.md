# JsonRpc.Standard

An asynchronous [TPL Dataflow](https://msdn.microsoft.com/en-us/library/hh228603.aspx)-based .NET Standard library for JSON RPC client & server implementation. Still work in progress and API is subject to change.

The package is now available on NuGet. To install the package, run the following command in the Package Manager Console

```powershell
Install-Package CXuesong.JsonRpc.Standard -Pre
```

This package focuses on the implementation of [JSON RPC 2.0](http://www.jsonrpc.org/specification), while 1.0 support might be offered in the future.

This package is based on .NET Standard 1.3. You may need .NET Framework 4.6 or .NET Core to consume it.

For an example based on two `BufferBlock`s, please dive into `ConsoleTestApp`. For the (WIP) implementation of [Mircosoft Language Server Protocol](https://github.com/Microsoft/language-server-protocol/blob/master/protocol.md) on the top of this library, please take a look at [CXuesong/LanguageServer.NET](https://github.com/CXuesong/LanguageServer.NET).

## Server

Let's take a look at the service implementation first.

```c#
public class LibraryService : JsonRpcService
{

    // The service instance is transcient. You cannot persist state in such a class.
    // So we need session.
    private LibrarySession Session => (LibrarySession) RequestContext.Session;

    [JsonRpcMethod]
    public Book GetBook(string isbn, bool required = false)
    {
        var book = Session.Books.FirstOrDefault(b => AreIsxnEqual(b.Isbn, isbn));
        if (required && book == null)
            throw new JsonRpcException(new ResponseError(1000, $"Cannot find book with ISBN:{isbn}."));
        return book;
    }

    [JsonRpcMethod]
    public ResponseError PutBook(Book book)
    {
        // Yes, you can just throw an ordinary Exception… Though it's not recommended.
        if (book == null) throw new ArgumentNullException(nameof(book));
        if (string.IsNullOrEmpty(book.Isbn))
            return new ResponseError(1001, $"Missing Isbn field of the book: {book}.");
        var index = Session.Books.FindIndex(b => AreIsxnEqual(b.Isbn, book.Isbn));
        if (index > 0)
            Session.Books[index] = book;
        else
            Session.Books.Add(book);
        return null;
    }

    [JsonRpcMethod]
    public IEnumerable<string> EnumBooksIsbn()
    {
        return Session.Books.Select(b => b.Isbn);
    }

    [JsonRpcMethod]
    public void Terminate()
    {
        Session.StopServer();
    }

    private bool AreIsxnEqual(string x, string y)
    {
      	// ...
    }
}
```

It's somewhat like the implementation of a `Controller` in ASP.NET. Derive a class from `JsonRpcService`, write your methods, and mark them either as a request or a notification with `JsonRpcMethodAttribute`.

*   The default JSON RPC method name is the CLR method name. You can specify `CamelCaseJsonRpcNamingStrategy` in the attribute or in `JsonRpcContractResolver.NamingStrategy`
*   You can either implement your method synchronously or asynchronously.
*   `CancellationToken` in the parameters is a synonym of `RequestContext.CancellationToken`.
    *   Still, there is no built-in way to cancel a method from the client. This might be implemented in the future.
*   Optional parameters are supported.
*   You may specify `AllowExtensionData = true` in the attribute to allow extra parameters passed to the method. You can later extract the parameters from `RequestContext.Request` property.
*   Method-overloading is supported by distingushing the method parameter count and type (Number/String/Array/Object).
*   You can report an error by returning a `ResponseError` object or throwing a `JsonRpcException` in your service implementation.
*   You can set the response manually by setting  `RequestContext.Response` property.
*   You can control the granularity of the settings by using, e.g., `JsonRpcScopeAttribute` & `JsonRpcParameterAttribute`.
*   Service classes must have a public parameterless constructor. Or you may need to implement your own `IServiceFactory` and pass it to `ServiceHostBuilder`.

You may host your service in a console application like this: (Based on [CXuesong/LanguageServer.NET](https://github.com/CXuesong/LanguageServer.NET/blob/master/DemoLanguageServer/Program.cs). Note it's different from the code in `ConsoleTestApp`.)

```c#
static void Main(string[] args)
{
    using (var logWriter = File.CreateText("messages-" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".log"))
    using (var cin = Console.OpenStandardInput())
    using (var bcin = new BufferedStream(cin))
    using (var cout = Console.OpenStandardOutput())
    {
        logWriter.AutoFlush = true;
        // Configure & build service host
        var session = new LibrarySession();
        var host = BuildServiceHost(session, logWriter);
        var target = new PartwiseStreamMessageTargetBlock(cout);
        var source = new PartwiseStreamMessageSourceBlock(bcin);
        // Connect the datablocks
        // If we want server to stop, just stop the source
        using (host.Attach(source, target))
        using (session.CancellationToken.Register(() => source.Complete()))
        {
            // Wait for exit
            session.CancellationToken.WaitHandle.WaitOne();
        }
        logWriter.WriteLine("Exited");
    }
}

private static IJsonRpcServiceHost BuildServiceHost(ISession session, TextWriter logWriter)
{
    var builder = new ServiceHostBuilder
    {
        ContractResolver = new JsonRpcContractResolver
        {
            NamingStrategy = new CamelCaseJsonRpcNamingStrategy(),
            ParameterValueConverter = new CamelCaseJsonValueConverter(),
        },
        Session = session,
        Options = JsonRpcServiceHostOptions.ConsistentResponseSequence,
    };
    // Register all the services (public classes) found in the assembly
    builder.Register(typeof(Program).GetTypeInfo().Assembly);
    // Add a middleware to log the requests and responses
    builder.Intercept(async (context, next) =>
    {
        logWriter.WriteLine("> {0}", context.Request);
        await next();
        logWriter.WriteLine("< {0}", context.Response);
    });
    return builder.Build();
}
```

Note that we need `BufferedStream` to encapsulate raw console input, or `cin.ReadAsync` will just block the calling thread rather than make the current task await the completion.

There are other `SourceBlock`/`TargetBlock` available.

*   `ByLineTextSourceBlock`/`ByLineTextTargetBlock`: Use new-line character to indicate the end of a message. 

*   `PartwiseStreamSourceBlock`/`PartwiseStreamTargetBlock`: Use some HTTP-like request text to indicate the boundary of a mesage. Used by [Microsoft Language Protocol](https://github.com/Microsoft/language-server-protocol/blob/master/protocol.md#base-protocol) in VsCode.

*   You may well derive your own SourceBlock/TargetBlock, or even just use some predefined TPL Dataflow blocks such as `BufferBlock`. The example project and the unit test project use `BufferBlock` to connect the service host with the client.

## Client

First of all, you may well use `JsonRpcClient` to send requests & notifications to the server via attached `SourceBlock`/`TargetBlock`.

However, there is another way, and this is where the magic happens :D

As is expatiated in `ConsoleTestApp`, you can create your stub interface first, to describe the RPC contract.

```c#
/// <summary>
/// Library RPC client stub.
/// </summary>
public interface ILibraryService
{
    [JsonRpcMethod("getBook")]
    Task<Book> GetBookAsync(string isbn, bool required);

    [JsonRpcMethod("getBook")]
    Task<Book> GetBookAsync(string isbn);

    [JsonRpcMethod("putBook")]
    Task PutBookAsync(Book book);

    [JsonRpcMethod]
    Task<ICollection<string>> EnumBooksIsbn();

    // A synchronous client call will just block the caller.
    [JsonRpcMethod(IsNotification = true)]
    void Terminate();
}
```

You can either declare the methods as synchronous (returns `void` or something) or asynchronous (returns `Task` or `Task<T>`) depending on your own, then mark all the methods with `JsonRpcMethodAttribute`.

After that, you can build a proxy class that implements this stub interface with JSON RPC requests.

```c#
var client = new JsonRpcClient();
var builder = new JsonRpcProxyBuilder
{
    NamingStrategy = new CamelCaseJsonRpcNamingStrategy(),
    ParameterValueConverter = new CamelCaseJsonValueConverter(),
};
var proxy = builder.CreateProxy<ILibraryService>(client);
client.Attach(clientSource, clientTarget);		// Attach the client just like what we do with service host.
// Add some books
await proxy.PutBookAsync(new Book("Somewhere Within the Shadows", "Juan Díaz Canales & Juanjo Guarnido",
    new DateTime(2004, 1, 1),
    "1596878177"));
await proxy.PutBookAsync(new Book("Arctic Nation", "Juan Díaz Canales & Juanjo Guarnido",
    new DateTime(2004, 1, 1),
    "0743479351"));
// Print all the books
foreach (var isbn in await proxy.EnumBooksIsbn())
{
    var book = await proxy.GetBookAsync(isbn);
    ClientWriteLine(book);
}
// Attempt to query for an inexistent ISBN
try
{
    await proxy.GetBookAsync("test", true);
}
catch (JsonRpcRemoteException ex)
{
    ClientWriteLine(ex);
}
// A JSON RPC notification, that we hope to shut down the server
proxy.Terminate();
```

