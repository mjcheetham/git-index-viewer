using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Mjcheetham.Git.IndexViewer
{
    public class Index
    {
        public IndexHeader Header { get; set; }
        public IList<IndexEntry> Entries { get; } = new List<IndexEntry>();
        public byte[] Extensions { get; set; }
        public ObjectId Checksum { get; set; }
    }

    [DebuggerDisplay("{DebuggerDisplay}")]
    public class IndexHeader
    {
        public uint Signature { get; set; }
        public uint Version { get; set; }
        public uint EntryCount { get; set; }

        public string DebuggerDisplay
        {
            get
            {
                byte[] sigBytes = BitConverter.GetBytes(Signature);
                if (BitConverter.IsLittleEndian) Array.Reverse(sigBytes);
                string sigAscii = Encoding.ASCII.GetString(sigBytes);
                return $"'{sigAscii}' [Version: {Version}, Entries: {EntryCount}]";
            }
        }
    }

    [DebuggerDisplay("{DebuggerDisplay}")]
    public class IndexEntry
    {
        public UnixFileStatus Status { get; set; }
        public ObjectId ObjectId { get; set; }
        public string Path { get; set; }
        public bool AssumeValid { get; set; }
        public bool IsExtended { get; set; }
        public IndexEntryStage Stage { get; set; }
        public int PathLength { get; set; }
        public bool SkipWorktree { get; set; }
        public bool IntentToAdd { get; set; }

        private string DebuggerDisplay => Path;
    }

    public enum IndexEntryStage
    {
        NoConflicts = 0,
        CommonAncestor = 1,
        Yours = 2,
        Theirs = 3
    }
}
