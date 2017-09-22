![Banner](Banner.png)

# JsonRpc.Standard

An asynchronous .NET Standard 1.3 & .NET Framework 4.5 library for JSON RPC client & server implementation. It supports .NET Core and more!

The package is now available on NuGet. To install the package, run [one or more of the following commands](https://github.com/CXuesong/JsonRpc.Standard/wiki/The-NuGet-packages) in the Package Manager Console

```powershell
Install-Package CXuesong.JsonRpc.Standard -Pre
Install-Package CXuesong.JsonRpc.DynamicProxy -Pre
Install-Package CXuesong.JsonRpc.Streams -Pre
Install-Package CXuesong.JsonRpc.AspNetCore -Pre
Install-Package CXuesong.JsonRpc.Http -Pre
```

The package focuses on the implementation of [JSON RPC 2.0](http://www.jsonrpc.org/specification), while 1.0 support might be offered in the future.

For a walkthough of the packages, see the [repository wiki](https://github.com/CXuesong/JsonRpc.Standard/wiki).

For an example based on `FullDuplexStream`, please dive into `ConsoleTestApp`. 

For an example JSON-RPC server over HTTP, see `WebTestApplication`.

For the (WIP) implementation of [Mircosoft Language Server Protocol](https://github.com/Microsoft/language-server-protocol/blob/master/protocol.md) on the top of this library, please take a look at [CXuesong/LanguageServer.NET](https://github.com/CXuesong/LanguageServer.NET). And a step further, for a WIP [Wikitext](https://en.wikipedia.org/wiki/Wiki_markup) language server, please take a look at [CXuesong/MwLanguageServer](https://github.com/CXuesong/MwLanguageServer).

