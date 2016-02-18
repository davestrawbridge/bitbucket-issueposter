using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Http;
using CommandLine;
using CsvHelper;

namespace BitbucketIssuePoster {
    class Program {

        static int Main(string[] args) {

            return Parser.Default.ParseArguments<Options>(args).MapResult(OnParseSuccess, OnParseError);
        }

        static int OnParseSuccess(Options options) {

            var url = string.Format("https://api.bitbucket.org/1.0/repositories/{0}/issues", options.Repository);

            var password = options.Password;

            if (string.IsNullOrEmpty(password)) {
                Console.Write("Password: ");
                password = ReadPassword();
            }

            var userAndPassword = options.UserName + ":" + password;

            var defaultWebProxy = WebRequest.DefaultWebProxy;
            defaultWebProxy.Credentials = CredentialCache.DefaultCredentials;

            var handler = new HttpClientHandler() {
                Proxy = defaultWebProxy,
                UseProxy = true,
            };

            var client = new HttpClient(handler);

            var byteArray = Encoding.ASCII.GetBytes(userAndPassword);
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

            IEnumerable<IssueEntry> issueEntries = null;
            if (options.CsvFile != null) {

                issueEntries = ReadCsvFile(options.CsvFile);
                
            } else if (options.IssueTitles != null && options.IssueTitles.Any()) {
                issueEntries = options.IssueTitles.Select(t => new IssueEntry { Title = t }).ToArray();
            }

            if (issueEntries != null) {

                foreach (var issueEntry in issueEntries) {

                    var parameters = new Dictionary<string, string>();
                    parameters["title"] = issueEntry.Title;
                    if (issueEntry.Content != null)
                        parameters["content"] = issueEntry.Content;

                    if (issueEntry.Responsible != null)
                        parameters["responsible"] = issueEntry.Responsible;

                    if (issueEntry.Kind != null)
                        parameters["kind"] = issueEntry.Kind.ToString().ToLower();

                    if (options.Verbose)
                        Console.Write("Creating issue {0}...", issueEntry.Title);

                    var data = new FormUrlEncodedContent(parameters.ToArray());
                    var response = client.PostAsync(url, data).Result;
                    var content = response.Content;

                    if (options.Verbose && response.IsSuccessStatusCode)
                        Console.WriteLine("Issue created");

                    if (!response.IsSuccessStatusCode)
                        Console.WriteLine(response.ReasonPhrase);
                }
            }

            return 0;
        }

        private static IEnumerable<IssueEntry> ReadCsvFile(string csvFile) {

            var reader = File.OpenText(csvFile);
            var csvReader = new CsvReader(reader);
            return csvReader.GetRecords<IssueEntry>();
        }

        public class IssueEntry {
            public string Title { get; set; }
            public string Content { get; set; }
            public string Responsible { get; set; }
            public IssueKind? Kind { get; set; }
            public enum IssueKind {
                Bug,
                Enhancement,
                Proposal,
                Task
            }
        }

        static int OnParseError(IEnumerable < Error > errors) {
            return -1;
        }

        static string ReadPassword() {
            string password = "";
            ConsoleKeyInfo info = Console.ReadKey(true);
            while (info.Key != ConsoleKey.Enter) {
                if (info.Key != ConsoleKey.Backspace) {
                    Console.Write("*");
                    password += info.KeyChar;
                } else if (info.Key == ConsoleKey.Backspace) {
                    if (!string.IsNullOrEmpty(password)) {
                        // remove one character from the list of password characters
                        password = password.Substring(0, password.Length - 1);
                        // get the location of the cursor
                        int pos = Console.CursorLeft;
                        // move the cursor to the left by one character
                        Console.SetCursorPosition(pos - 1, Console.CursorTop);
                        // replace it with space
                        Console.Write(" ");
                        // move the cursor to the left by one character again
                        Console.SetCursorPosition(pos - 1, Console.CursorTop);
                    }
                }
                info = Console.ReadKey(true);
            }
            // add a new line because user pressed enter at the end of their password
            Console.WriteLine();
            return password;
        }


        class Options {

            [Option('u', "user", Required = true, HelpText = "BitBucket username")]
            public string UserName { get; set; }

            [Option('p', "password", HelpText = "BitBucket password (prompt if not supplied)")]
            public string Password { get; set; }

            [Option('r', "repository", Required = true, HelpText = "Repository name (owner/repo-slug)")]
            public string Repository { get; set; }

            [Option('c', "csvfile", HelpText = "CSV file containing list of issues; file must have header row; valid fields are Title, Content, Responsible, Kind", SetName = "issues")]
            public string CsvFile { get; set; }

            [Option('i', "issues", HelpText = "comma-separated list of issue titles", SetName = "issues")]
            public IEnumerable<string> IssueTitles { get; set; }

            [Option('v', "verbose", HelpText = "Prints all messages to console.")]
            public bool Verbose { get; set; }
        }
    }
}
