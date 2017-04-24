# JsonRpc.Standard

An asynchronous .NET Standard library for JSON RPC client & server implementation. Still work in progress and API is subject to change.

The package is now available on NuGet. To install the package, run the following command in the Package Manager Console

```powershell
Install-Package CXuesong.JsonRpc.Standard -Pre
```

This package focuses on the implementation of [JSON RPC 2.0](http://www.jsonrpc.org/specification), while 1.0 support might be offered in the future.

This package is based on .NET Standard 1.3. You may need .NET Framework 4.6 or .NET Core to consume it.

## Server

It's somewhat like the implementation of a Controller in ASP.NET. Derive from `JsonRpcService`, write your methods, and mark them either as a request or a notification with `JsonRpcMethodAttribute`.

*   The default JSON RPC method name is the camel-case form of the CLR method name.

*   You can either implement your method synchronously or asynchronously.

*   `CancellationToken` in the parameters is a synonym of `RequestContext.CancellationToken`.

*   Optional parameters are supported.

*   Method-overloading is supported by distingushing the method parameter count and type (Number/String/Array/Object).

*   You can report an error by returning a `ResponseError` object or throwing a `JsonRpcException` in your service implementation.

The example code given below is based on the current unit test cases.

```c#
public class TestJsonRpcService : JsonRpcService
{
    [JsonRpcMethod]
    public async Task<int> Add(int x, int y, CancellationToken ct)
    {
        await Task.Delay(500, ct);
        return x + y;
    }

    [JsonRpcMethod]
    public string Add(string a, string b)
    {
        return a + b;
    }

    [JsonRpcMethod]
    public Complex MakeComplex(double real, double imaginary)
    {
        return new Complex(real, imaginary);
    }
}
```

You may host your service in a console application like this: (Based on [another project](https://github.com/CXuesong/LanguageServer.NET/blob/master/DemoLanguageServer/Program.cs) I'm currently working on.)

```c#
static void Main(string[] args)
{
    var rpcResolver = new RpcMethodResolver();
    rpcResolver.Register(typeof(Program).GetTypeInfo().Assembly);
    using (var cin = Console.OpenStandardInput())
    using (var cout = Console.OpenStandardOutput())
    using (var bcin = new BufferedStream(cin))
    {
        var writer = new PartwiseStreamMessageWriter(cout);
        var reader = new PartwiseStreamMessageReader(bcin);
        var host = new JsonRpcServiceHost(reader, writer, rpcResolver,
            JsonRpcServiceHostOptions.ConsistentResponseSequence);
        host.RunAsync().Wait();
    }
}
```

Note that we need `BufferedStream` to encapsulate raw console input, or `cin.ReadAsync` will just block the calling thread rather than setting the current task await the completion.

There are other `MessageReader`/`MessageWriter` available.

*   `ByLineTextMessageReader`/`ByLineTextMessageWriter`: Use new-line character to indicate the end of a message. 

*   `PartwiseStreamMessageReader`/`PartwiseStreamMessageWriter`: Use some HTTP-like request text to indicate the boundary of a mesage. Used by [Microsoft Language Protocol](https://github.com/Microsoft/language-server-protocol/blob/master/protocol.md#base-protocol) in VsCode.

*   You may well derive your own reader/writer. There is an example in `UnitTestProject1/QueueMessageRW.cs` which used `ConcurrentQueue` as a channel for communication.

## Client

First of all, you may well use `JsonRpcClient` to send requests & notifications to the server via `MessageReader`/`MessageWriter`.

However, there is another way, and this is where the magic happens :D

As is expatiated in `UnitTestProject1/ClientTest.cs`, you can create your stub interface first, to describe the RPC contract.

```c#
public interface ITestRpcContract
{
    [JsonRpcMethod]
    int Add(int x, int y);

    [JsonRpcMethod]
    string Add(string a, string b);

    [JsonRpcMethod]
    Task<Complex> MakeComplex(double real, double imaginary);
}
```

You can either declare the methods as synchronous (returns `void` or something) or asynchronous (returns `Task` or `Task<T>`) depending on your own, then mark all the methods with `JsonRpcMethodAttribute`.

After that, you can build a proxy class that implements this stub interface with JSON RPC requests.

```c#
public void ProxyTest()
{
    (var host, var reader, var writer) = Utility.CreateJsonRpcServiceHost();
    host.RunAsync();
    var client = new JsonRpcClient(reader, writer);
    var builder = new JsonRpcProxyBuilder();
    var proxy = builder.CreateProxy<ITestRpcContract>(client);
    Assert.AreEqual(100, proxy.Sum(73, 27));
    Assert.AreEqual("abcdef", proxy.Concat("ab", "cdef"));
    Assert.AreEqual(new Complex(100, 200), proxy.MakeComplex(100, 200).Result);
    host.Stop();
}
```