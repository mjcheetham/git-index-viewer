using System;
using System.IO;

namespace Mjcheetham.Git.IndexViewer.Cli
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length > 0 && StringComparer.OrdinalIgnoreCase.Equals("--help", args[0]))
            {
                Console.WriteLine("usage: git-index [<path>]");
                return;
            }

            string filePath = args.Length > 0
                ? Path.GetFullPath(args[0])
                : Directory.GetCurrentDirectory();

            string fileName = Path.GetFileName(filePath);

            if (!StringComparer.Ordinal.Equals(fileName, "index"))
            {
                string p = Path.Combine(filePath, "index");
                if (File.Exists(p))
                {
                    filePath = p;
                }
                else
                {
                    p = Path.Combine(filePath, ".git", "index");
                    if (File.Exists(p))
                    {
                        filePath = p;
                    }
                    else
                    {
                        Console.Error.WriteLine("Unable to locate index file.");
                        Environment.Exit(1);
                    }
                }
            }

            Index index = IndexSerializer.Deserialize(filePath);

            PrintSummary(index, filePath);
            Console.WriteLine();

            int consoleWidth = Math.Max(Console.WindowWidth, 120);

            const string PathColHeader = "Path";
            const string OtherColHeaders = " | Mode       | OID          | SkipWT | Add   | Stage          ";

            int pathExtraWidth = consoleWidth - OtherColHeaders.Length - PathColHeader.Length;

            Console.WriteLine("{0}{1}{2}", PathColHeader, new string(' ', pathExtraWidth), OtherColHeaders);
            Console.WriteLine(new string('-', consoleWidth));
            foreach (IndexEntry entry in index.Entries)
            {
                PrintEntry(entry, consoleWidth - OtherColHeaders.Length);
            }
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

        private static void PrintEntry(IndexEntry entry, int maxPath)
        {
            string formatString = $"{{0,-{maxPath}}} | {{1,-10}} | {{2,-12}} | {{3,-6}} | {{4,-5}} | {{5}}";
            string path = TruncatePath(entry.Path, maxPath);

            Console.WriteLine(formatString,
                path, entry.Status.Mode, entry.ObjectId.ToString(12), entry.SkipWorktree, entry.IntentToAdd, entry.Stage);
        }

        private static string TruncatePath(string path, int max)
        {
            if (path.Length <= max) return path;
            return "..." + path.Substring(path.Length - max + 3);
        }
    }
}
