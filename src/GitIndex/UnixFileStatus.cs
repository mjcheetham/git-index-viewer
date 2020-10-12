using System;
using System.Text;

namespace Mjcheetham.Git.IndexViewer
{
    public struct UnixFileStatus
    {
        public UnixFileTime CreationTime;
        public UnixFileTime ModifiedTime;
        public uint Device;
        public uint Inode;
        public UnixFileMode Mode;
        public uint UserId;
        public uint GroupId;
        public uint Size;
    }

    public class UnixFileMode
    {
        public uint Value { get; }

        public UnixFileMode(in uint mode)
        {
            Value = mode;
        }

        public UnixFileType Type => (UnixFileType)((Value >> 12) & 0xF);

        public UnixFilePermissions Permissions => (UnixFilePermissions)(Value & 0x1FF);

        public override string ToString()
        {
            var sb = new StringBuilder();
            char typeChar = Type switch
            {
                UnixFileType.RegularFile  => '-',
                UnixFileType.SymbolicLink => 'l',
                UnixFileType.GitLink      => 'g',
                _                         => '?'
            };
            sb.Append(typeChar);

            sb.Append((Permissions & UnixFilePermissions.UserRead)      != 0 ? 'r' : '-');
            sb.Append((Permissions & UnixFilePermissions.UserWrite)     != 0 ? 'w' : '-');
            sb.Append((Permissions & UnixFilePermissions.UserExecute)   != 0 ? 'x' : '-');
            sb.Append((Permissions & UnixFilePermissions.GroupRead)     != 0 ? 'r' : '-');
            sb.Append((Permissions & UnixFilePermissions.GroupWrite)    != 0 ? 'w' : '-');
            sb.Append((Permissions & UnixFilePermissions.GroupExecute)  != 0 ? 'x' : '-');
            sb.Append((Permissions & UnixFilePermissions.OthersRead)    != 0 ? 'r' : '-');
            sb.Append((Permissions & UnixFilePermissions.OthersWrite)   != 0 ? 'w' : '-');
            sb.Append((Permissions & UnixFilePermissions.OthersExecute) != 0 ? 'x' : '-');

            return sb.ToString();
        }
    }

    public enum UnixFileType
    {
        RegularFile  = 0b1000,
        SymbolicLink = 0b1010,
        GitLink      = 0b1110,
    }

    [Flags]
    public enum UnixFilePermissions
    {
        UserRead      = 0b100_000_000,
        UserWrite     = 0b010_000_000,
        UserExecute   = 0b001_000_000,
        GroupRead     = 0b000_100_000,
        GroupWrite    = 0b000_010_000,
        GroupExecute  = 0b000_001_000,
        OthersRead    = 0b000_000_100,
        OthersWrite   = 0b000_000_010,
        OthersExecute = 0b000_000_001,
    }

    public class UnixFileTime
    {
        public uint Seconds { get; }
        public uint Nanoseconds { get; }

        public UnixFileTime(uint seconds, uint nanoseconds)
        {
            Seconds = seconds;
            Nanoseconds = nanoseconds;
        }

        public static explicit operator DateTime(UnixFileTime uft)
        {
            return DateTime.UnixEpoch.AddSeconds(uft.Seconds).AddMilliseconds(uft.Nanoseconds / 1e6);
        }

        public static explicit operator UnixFileTime(DateTime dt)
        {
            TimeSpan span = dt.Subtract(DateTime.UnixEpoch);
            return new UnixFileTime((uint) span.Seconds, (uint) (span.Milliseconds * 1e6));
        }

        public override string ToString()
        {
            return ((DateTime) this).ToString("O");
        }
    }
}
