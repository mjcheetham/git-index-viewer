using System;
using System.Collections.Generic;
using System.IO;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Globalization;
using System.Linq;

namespace Mjcheetham.Git.IndexViewer.Cli
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var rootCommand = new RootCommand("Utility for inspecting the Git index file.")
            {
                new Option<string>(new[] {"-i", "--index"})
                {
                    Description = "Path to a Git repository or .git/index file",
                }
            };

            var infoCommand = new Command("info", "Show summary information about the index");
            var listCommand = new Command("list", "List and filter index entries")
            {
                new Argument("path")
                {
                    Description = "Filter based on path prefix",
                    Arity = ArgumentArity.ZeroOrOne
                },
                new Option<bool>("--ignore-case", "Perform case-insensitive filtering on paths"),
                new Option<bool>("--no-table", "Do not show table headings or columns"),
            };

            rootCommand.Add(infoCommand);
            rootCommand.Add(listCommand);

            infoCommand.Handler = CommandHandler.Create<string>(Info);
            listCommand.Handler = CommandHandler.Create<string, string, bool, bool>(List);

            int exitCode = rootCommand.Invoke(args);
            Environment.Exit(exitCode);
        }

        private static int Info(string indexFile)
        {
            if (!TryGetIndexFile(indexFile, out string filePath))
            {
                Console.Error.WriteLine("Unable to locate index file.");
                return 1;
            }

            Index index = IndexSerializer.Deserialize(filePath);
            PrintSummary(index, filePath);
            return 0;
        }

        private static int List(string indexFile, string path, bool ignoreCase, bool noTable)
        {
            if (!TryGetIndexFile(indexFile, out string filePath))
            {
                Console.Error.WriteLine("Unable to locate index file.");
                return 1;
            }

            Index index = IndexSerializer.Deserialize(filePath);

            int consoleWidth = Math.Max(Console.WindowWidth, 120);

            const string PathColHeader = "Path";
            const string OtherColHeaders = " | Mode       | OID          | SkipWT | Add   | Stage          ";

            int pathExtraWidth = consoleWidth - OtherColHeaders.Length - PathColHeader.Length;

            if (!noTable)
            {
                Console.WriteLine("{0}{1}{2}", PathColHeader, new string(' ', pathExtraWidth), OtherColHeaders);
                Console.WriteLine(new string('-', consoleWidth));
            }

            IEnumerable<IndexEntry> entries = index.Entries;
            if (!string.IsNullOrWhiteSpace(path))
            {
                entries = index.Entries.Where(x => x.Path.StartsWith(path, ignoreCase, CultureInfo.InvariantCulture));
            }

            foreach (IndexEntry entry in entries)
            {
                PrintEntry(entry, consoleWidth - OtherColHeaders.Length, noTable);
            }

            return 0;
        }

        private static bool TryGetIndexFile(string arg, out string filePath)
        {
            filePath = string.IsNullOrWhiteSpace(arg)
                ? Directory.GetCurrentDirectory()
                : Path.GetFullPath(arg);

            string fileName = Path.GetFileName(filePath);

            if (!StringComparer.Ordinal.Equals(fileName, "index"))
            {
                string p = Path.Combine(filePath, "index");
                if (File.Exists(p))
                {
                    filePath = p;
                    return true;
                }

                p = Path.Combine(filePath, ".git", "index");
                if (File.Exists(p))
                {
                    filePath = p;
                    return true;
                }
            }
            else if (File.Exists(filePath))
            {
                return true;
            }

            return false;
        }

        private static void PrintSummary(Index index, string filePath)
        {
            var fi = new FileInfo(filePath);
            Console.WriteLine("File       : {0}", filePath);
            Console.WriteLine("Size       : {0} bytes", fi.Length);
            Console.WriteLine("Version    : {0}", index.Header.Version);
            Console.WriteLine("Entries    : {0}", index.Header.EntryCount);
            Console.WriteLine("Checksum   : {0}", index.Checksum);
            Console.WriteLine("Extensions : {0} bytes", index.Extensions.Length);
        }

        private static void PrintEntry(IndexEntry entry, int maxPath, bool noTable)
        {
            char colDividerChar = noTable ? ' ' : '|';
            string formatString = $"{{1,-{maxPath}}} {{0}} {{2,-10}} {{0}} {{3,-12}} {{0}} {{4,-6}} {{0}} {{5,-5}} {{0}} {{6}}";
            string path = TruncatePath(entry.Path, maxPath);

            Console.WriteLine(formatString, colDividerChar,
                path, entry.Status.Mode, entry.ObjectId.ToString(12), entry.SkipWorktree, entry.IntentToAdd, entry.Stage);
        }

        private static string TruncatePath(string path, int max)
        {
            if (path.Length <= max) return path;
            return "..." + path.Substring(path.Length - max + 3);
        }
    }
}
