using System;
using System.Collections.Generic;
using System.Text;

namespace ConsoleTestApp
{
    // You may apply Newtonsoft.Json Attributes here.
    public class Book
    {

        public Book()
        {
            
        }

        public Book(string title, string author, DateTime publishDate, string isbn)
        {
            Title = title;
            Author = author;
            PublishDate = publishDate;
            Isbn = isbn;
        }

        public string Title { get; set; }

        public string Author { get; set; }

        public DateTime PublishDate { get; set; }

        public string Isbn { get; set; }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{Title} - {Author} ({PublishDate})[{Isbn}]";
        }
    }
}
