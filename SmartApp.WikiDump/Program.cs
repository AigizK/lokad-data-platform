﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Platform.Node;
using ServiceStack.ServiceClient.Web;
using ServiceStack.Text;

namespace SmartApp.WikiDump
{
    class Program
    {
        static IEnumerable<string> ReadLinesSequentially(string path)
        {
            using (var rows = File.OpenText(path))
            {
                while (true)
                {
                    var line = rows.ReadLine();
                    if (null != line)
                    {
                        yield return line;
                    }
                    else
                    {
                        yield break;
                    }
                }
            }
        }

        static void Main(string[] args)
        {
            var path = @"D:\Temp\Stack Overflow Data Dump - Aug 09\Content\posts.xml";

            var JsonClient = new JsonServiceClient(string.Format("http://127.0.0.1:8080"));
            long rowIndex = 0;
            foreach (var line in ReadLinesSequentially(path).Where(l => l.StartsWith("  <row ")))
            {
                rowIndex++;
                var json = ConvertToJson(line);

                var bytes = new List<byte>(Encoding.UTF8.GetBytes(json));
                bytes.Insert(0, 42); //flag for our example

                JsonClient.Post<ClientDto.WriteEvent>("/stream", new ClientDto.WriteEvent()
                {
                    Data = bytes.ToArray(),
                    Stream = "name",
                    ExpectedVersion = -1
                });

                if (rowIndex % 1000 == 0)
                    Console.WriteLine(rowIndex);
            }


        }

        private static string ConvertToJson(string line)
        {
            long defaultLong;
            DateTime defaultDate;
            var json = new Post
                           {
                               Id = long.TryParse(Get(line, "Id"), out defaultLong) ? defaultLong : -1,
                               PostTypeId = long.TryParse(Get(line, "PostTypeId"), out defaultLong) ? defaultLong : -1,
                               CreationDate = DateTime.TryParse(Get(line, "CreationDate"), out defaultDate) ? defaultDate : DateTime.MinValue,
                               ViewCount = long.TryParse(Get(line, "ViewCount"), out defaultLong) ? defaultLong : -1,
                               Body = HttpUtility.HtmlDecode(Get(line, "Body")),
                               OwnerUserId = long.TryParse(Get(line, "OwnerUserId"), out defaultLong) ? defaultLong : -1,
                               LastEditDate = DateTime.TryParse(Get(line, "LastEditDate"), out defaultDate) ? defaultDate : DateTime.MinValue,
                               Title = HttpUtility.HtmlDecode(Get(line, "Title")),
                               AnswerCount = long.TryParse(Get(line, "AnswerCount"), out defaultLong) ? defaultLong : -1,
                               CommentCount = long.TryParse(Get(line, "CommentCount"), out defaultLong) ? defaultLong : -1,
                               FavoriteCount = long.TryParse(Get(line, "FavoriteCount"), out defaultLong) ? defaultLong : -1,
                           };

            return json.ToJson();
        }

        private static string Get(string line, string attributeName)
        {
            var start = line.IndexOf(attributeName + "=\"");
            var end = line.Substring(start + attributeName.Length + 2).IndexOf("\"");

            if (start == -1 || end == -1)
                return "";

            return line.Substring(start + attributeName.Length + 2, end);
        }
    }

    struct Comment
    {
        public long Id { get; set; }
        public long PostId { get; set; }
        public long UserId { get; set; }
        public DateTime CreateDate { get; set; }
        public string Text { get; set; }
    }

    public class Post
    {
        public long Id { get; set; }
        public long PostTypeId { get; set; }
        public DateTime CreationDate { get; set; }
        public long ViewCount { get; set; }
        public string Body { get; set; }
        public long OwnerUserId { get; set; }
        public DateTime LastEditDate { get; set; }
        public string Title { get; set; }
        public long AnswerCount { get; set; }
        public long CommentCount { get; set; }
        public long FavoriteCount { get; set; }
    }
}
