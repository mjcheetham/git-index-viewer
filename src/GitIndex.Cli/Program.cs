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
                new Option<string>(new[] {"-i", "--index-file"})
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
                new Option<bool>(new[]{"-U", "--ctime"}, "Use time of file creation, instead of last modification"),
                new Option<bool>(new[]{"-n", "--numeric-uid-gid"}, "Use numeric user and group IDs")
            };

            rootCommand.Add(infoCommand);
            rootCommand.Add(listCommand);

            infoCommand.Handler = CommandHandler.Create<string>(Info);
            listCommand.Handler = CommandHandler.Create<string, string, bool, bool, bool>(List);

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

        private static int List(string indexFile, string path, bool ignoreCase, bool ctime, bool numericUidGid)
        {
            if (!TryGetIndexFile(indexFile, out string filePath))
            {
                Console.Error.WriteLine("Unable to locate index file.");
                return 1;
            }

            Index index = IndexSerializer.Deserialize(filePath);

            // Apply entry filter
            IList<IndexEntry> entries = index.Entries;
            if (!string.IsNullOrWhiteSpace(path))
            {
                entries = index.Entries.Where(
                        x => x.Path.StartsWith(path, ignoreCase, CultureInfo.InvariantCulture)
                    )
                    .ToList();
            }

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
            if (numericUidGid)
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
                PrintEntry(entry, columnFormat, ctime, numericUidGid, maxWidth);
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
