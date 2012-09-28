using Microsoft.PowerShell.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace RenameBook
{
    [Cmdlet(VerbsCommon.Rename, "Book")]
    public class RenameBookCommand : PSCmdlet
    {
        protected override void ProcessRecord()
        {
            this.InvokeProvider.Item.Rename(this.Path, GetNewName());
        }

        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        [ValidateNotNullOrEmpty]
        public string Path { get; set; }

        private string GetIsbn()
        {
            string isbn = System.IO.Path.GetFileNameWithoutExtension(Path);
            if (!Regex.IsMatch(isbn, "\\d"))
                throw new InvalidOperationException(string.Concat(isbn, " does not look like a valid isbn."));

            return isbn;
        }

        private string GetNewName()
        {
            string isbn = GetIsbn();
            string newName = string.Concat(IsbnToName(isbn), System.IO.Path.GetExtension(Path));

            return newName;
        }

        #region Utils

        private static string TextTag(string text)
        {
            return (text ?? string.Empty).IndexOf(',') != -1 ? string.Format("[{0}]", text) : text;
        }

        private static string ReadLine(string pattern, string prompt)
        {
            Console.Write(prompt);

            string input;
            while (!Regex.IsMatch(input = Console.ReadLine(), pattern)) { Console.Write(prompt); }
            return input;
        }

        private static string IsbnToName(string isbn)
        {
            using (WebClient wc = new WebClient())
            {
                string s = wc.DownloadString(string.Concat("https://www.googleapis.com/books/v1/volumes?q=isbn:", isbn));
                dynamic json = new JavaScriptSerializer().Deserialize<dynamic>(s);

                // If no records are found, abort.
                if ((int)json["items"].Length == 0)
                    throw new InvalidOperationException(string.Concat("No records returned for ", isbn));

                // Build filename candidates.
                List<string> filenames = new List<string>(json["items"].Length);
                for (int i = 0; i < json["items"].Length; i++)
                {
                    dynamic volumeInfo = json["items"][i]["volumeInfo"];

                    if (!volumeInfo.ContainsKey("title") || !volumeInfo.ContainsKey("authors") || !volumeInfo.ContainsKey("publisher"))
                        continue; // Does not seem like a valid volume info.

                    string title = TextTag(volumeInfo["title"]),
                        authors = TextTag(string.Join(", ", ((object[])volumeInfo["authors"]).Select(author => TextTag(Convert.ToString(author))))),
                        publisher = TextTag(volumeInfo["publisher"]);

                    filenames.Add(string.Join(" ", string.Join(", ", authors, title), TextTag(string.Join(", ", publisher, isbn))));
                }

                // Process filename candidates.
                switch (filenames.Count)
                {
                    case 0:
                        throw new InvalidOperationException(string.Concat("No valid records returned for ", isbn));
                    case 1:
                        return filenames[0];
                    default:
                        // Display the list for the user to chose.
                        for (int i = 0; i < filenames.Count; i++)
                            Console.WriteLine(string.Concat(i, " : ", filenames[i]));

                        return filenames[int.Parse(ReadLine("\\d", ""))];
                }
            }
        }

        #endregion

    }
}
