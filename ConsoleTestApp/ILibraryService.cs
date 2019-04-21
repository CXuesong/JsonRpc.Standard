using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using JsonRpc.Contracts;

namespace ConsoleTestApp
{
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

}
