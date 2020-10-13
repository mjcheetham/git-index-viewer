using System;
using System.Collections.Generic;
using System.IO;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;

namespace Mjcheetham.Git.IndexViewer.Cli
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var rootCommand = new RootCommand("Utility for inspecting the Git index file.")
            {
                new Option<string>(new[] {"-f", "--file"})
                {
                    Description = "Path to a .git/index file",
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
                new Option<bool>(new[]{"-U", "--ctime"}, "Use time of file creation, instead of last modification"),
                new Option<bool>(new[]{"-n", "--numeric-uid-gid"}, "Use numeric user and group IDs"),
                new Option<bool>(new[]{"-S", "--skip-worktree"}, "Show only entries with the skip-worktree bit set"),
                new Option<bool>(new[]{"-s", "--no-skip-worktree"}, "Do not show entries with the skip-worktree bit set")
            };

            rootCommand.Add(infoCommand);
            rootCommand.Add(listCommand);

            infoCommand.Handler = CommandHandler.Create<InfoOptions>(Info);
            listCommand.Handler = CommandHandler.Create<ListOptions>(List);

            int exitCode = rootCommand.Invoke(args);
            Environment.Exit(exitCode);
        }

        private abstract class CommandOptions
        {
            private string File { get; set; }

            public string IndexFile
            {
                get
                {
                    if (!string.IsNullOrWhiteSpace(File))
                    {
                        return Path.GetFullPath(File);
                    }

                    string dir = Directory.GetCurrentDirectory();
                    while (!string.IsNullOrWhiteSpace(dir))
                    {
                        string path = Path.Combine(dir, ".git", "index");
                        if (System.IO.File.Exists(path))
                        {
                            return path;
                        }

                        dir = Path.GetDirectoryName(dir);
                    }

                    return null;
                }
            }

            public virtual bool Validate()
            {
                if (IndexFile != null && !System.IO.File.Exists(IndexFile))
                {
                    Console.Error.WriteLine("error: unable to locate index file.");
                    return false;
                }

                return true;
            }
        }

        private class InfoOptions : CommandOptions { }

        private class ListOptions : CommandOptions
        {
            public string Path { get; set; }
            public bool IgnoreCase { get; set; }
            public bool CTime { get; set; }
            public bool NumericUidGid { get; set; }
            public bool SkipWorktree { get; set; }
            public bool NoSkipWorktree { get; set; }

            public override bool Validate()
            {
                if (SkipWorktree && NoSkipWorktree)
                {
                    Console.Error.WriteLine("error: cannot specify --skip-worktree and --no-skip-worktree at the same time.");
                    return false;
                }

                return base.Validate();
            }
        }

        private static int Info(InfoOptions options)
        {
            if (!options.Validate()) return 1;

            Index index = IndexSerializer.Deserialize(options.IndexFile);
            PrintSummary(index, options.IndexFile);
            return 0;
        }

        private static int List(ListOptions options)
        {
            if (!options.Validate()) return 1;

            Index index = IndexSerializer.Deserialize(options.IndexFile);

            // Apply entry filters
            IEnumerable<IndexEntry> entriesQuery = index.Entries;
            if (!string.IsNullOrWhiteSpace(options.Path))
            {
                entriesQuery = entriesQuery.Where(
                    x => x.Path.StartsWith(options.Path, options.IgnoreCase, CultureInfo.InvariantCulture)
                );
            }

            if (options.SkipWorktree)
            {
                entriesQuery = entriesQuery.Where(x => x.SkipWorktree);
            }
            else if (options.NoSkipWorktree)
            {
                entriesQuery = entriesQuery.Where(x => !x.SkipWorktree);
            }

            IList<IndexEntry> entries = entriesQuery.ToList();

            // Compute column widths
            int maxWidth = Math.Max(Console.WindowWidth, 120);
            ISet<uint> uids = new HashSet<uint>();
            ISet<uint> gids = new HashSet<uint>();
            long maxSize = 0;
            foreach (IndexEntry entry in entries)
            {
                uids.Add(entry.Status.UserId);
                gids.Add(entry.Status.GroupId);
                maxSize = Math.Max(entry.Status.Size, maxSize);
            }

            int maxUserLen;
            int maxGroupLen;
            if (options.NumericUidGid)
            {
                maxUserLen = uids.Max().ToString().Length;
                maxGroupLen = gids.Max().ToString().Length;
            }
            else
            {
                maxUserLen = uids.Select(GetUserName).Max(x => x.Length);
                maxGroupLen = gids.Select(GetGroupName).Max(x => x.Length);
            }

            int maxSizeLen = maxSize.ToString().Length;
            string columnFormat = "{0} {1} {2} " +
                                    $"{{3,-{maxUserLen}}} {{4,-{maxGroupLen}}} {{5,{maxSizeLen}}} " +
                                    "{6,2} {7:MMM} {7:HH:mm} {8} ";

            // Print count
            Console.WriteLine("total {0}", entries.Count);

            // Print entries
            foreach (IndexEntry entry in entries)
            {
                PrintEntry(entry, columnFormat, options.CTime, options.NumericUidGid, maxWidth);
            }

            return 0;
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

        private static void PrintEntry(IndexEntry entry, string columnFormat, bool ctime, bool numericId, int maxWidth)
        {
            string mode = entry.Status.Mode.ToString();
            var flagsArr = new[] {'-', '-', '-'};
            if (entry.IntentToAdd)  flagsArr[0] = 'a';
            if (entry.SkipWorktree) flagsArr[1] = 's';
            if (entry.AssumeValid)  flagsArr[2] = 'v';
            var flags = new string(flagsArr);

            string oid = entry.ObjectId.ToString(8);
            string user = numericId ? entry.Status.UserId.ToString() : GetUserName(entry.Status.UserId);
            string group = numericId ? entry.Status.GroupId.ToString() : GetGroupName(entry.Status.GroupId);
            uint size = entry.Status.Size;
            DateTime time = (DateTime) (ctime ? entry.Status.CreationTime : entry.Status.ModifiedTime);
            int stage = (int)entry.Stage;

            string otherCol = string.Format(columnFormat, mode, flags, oid, user, group, size, time.Day, time, stage);
            string path = TruncatePath(entry.Path, maxWidth - otherCol.Length);
            Console.Write(otherCol);
            Console.WriteLine(path);
        }

        private static string TruncatePath(string path, int max)
        {
            if (path.Length <= max) return path;
            return "..." + path.Substring(path.Length - max + 3);
        }

        private static string GetUserName(uint uid)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return UnixNative.GetUserName(uid);
            }

            // TODO: support Windows
            return uid.ToString();
        }

        private static string GetGroupName(uint gid)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return UnixNative.GetGroupName(gid);
            }

            // TODO: support Windows
            return gid.ToString();
        }
    }
}
