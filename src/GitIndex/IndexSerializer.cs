using System;
using System.IO;
using System.Text;

namespace Mjcheetham.Git.IndexViewer
{
    public static class IndexSerializer
    {
        private const int BaseEntryLength = 62;
        private const ushort AssumeValidFlag = 0x8000;
        private const ushort ExtendedFlag = 0x4000;
        private const ushort SkipWorktreeExFlag = 0x4000;
        private const ushort IntentToAddExFlag = 0x2000;

        public static Index Deserialize(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            return Deserialize(fs);
        }

        public static Index Deserialize(Stream stream)
        {
            var index = new Index();

            using var reader = new BigEndianBinaryReader(stream, Encoding.UTF8, true);
            index.Header = ReadHeader(reader);

            string prevPath = null;
            for (int i = 0; i < index.Header.EntryCount; i++)
            {
                IndexEntry entry = ReadEntry(reader, index.Header.Version, prevPath);
                index.Entries.Add(entry);
                prevPath = entry.Path;
            }

            index.Extensions = ReadExtensions(reader);
            index.Checksum = ReadChecksum(reader);

            return index;
        }

        public static void Serialize(Index index, string filePath, bool overwrite)
        {
            using var fs = new FileStream(filePath, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write);
            Serialize(index, fs);
        }

        public static void Serialize(Index index, Stream stream)
        {
            using var reader = new BigEndianBinaryWriter(stream, Encoding.UTF8, true);
            WriteHeader(reader, index.Header);

            foreach (IndexEntry entry in index.Entries)
            {
                WriteEntry(reader, index.Header.Version, entry);
            }

            WriteExtensions(reader, index.Extensions);
            WriteChecksum(reader, index.Checksum);
        }

        #region Read

        private static IndexHeader ReadHeader(BigEndianBinaryReader reader)
        {
            uint sig = reader.ReadUInt32();
            uint ver = reader.ReadUInt32();
            uint cnt = reader.ReadUInt32();

            return new IndexHeader
            {
                Signature  = sig,
                Version    = ver,
                EntryCount = cnt
            };
        }

        private static IndexEntry ReadEntry(BigEndianBinaryReader reader, uint version, string prevPath)
        {
            int entryLength = BaseEntryLength;

            UnixFileStatus status = ReadFileStatus(reader);

            // TODO: support SHA-256 object IDs
            byte[] sha1Bytes = reader.ReadBytes(Constants.Sha1Length);
            var objectId = new ObjectId(sha1Bytes);

            // Read flags and path length
            ushort flags = reader.ReadUInt16();
            bool assumeValid = (flags & AssumeValidFlag) != 0;
            bool isExtended = (flags & ExtendedFlag) != 0;
            var stage = (IndexEntryStage) (((flags << 18) >> 30) & 3);
            int pathLength = (ushort) ((flags << 20) >> 20) & 4095;
            entryLength += pathLength;

            var entry = new IndexEntry
            {
                Status = status,
                ObjectId = objectId,
                AssumeValid = assumeValid,
                IsExtended = isExtended,
                Stage = stage,
                PathLength = pathLength
            };

            // Read extended flags
            if (version > 2 && isExtended)
            {
                ushort extendedFlags = reader.ReadUInt16();
                entry.SkipWorktree = (extendedFlags & SkipWorktreeExFlag) != 0;
                entry.IntentToAdd = (extendedFlags & IntentToAddExFlag) != 0;
                entryLength += 2;
            }

            // Read path
            entry.Path = ReadPath(reader, version, pathLength, prevPath ?? string.Empty);

            if (version < 4)
            {
                // Consume remaining null bytes
                int nullBytes = 8 - (entryLength % 8);
                reader.ReadBytes(nullBytes);
            }

            return entry;
        }

        private static UnixFileStatus ReadFileStatus(BigEndianBinaryReader reader)
        {
            uint ctimeSecs = reader.ReadUInt32();
            uint ctimeNano = reader.ReadUInt32();
            uint mtimeSecs = reader.ReadUInt32();
            uint mtimeNano = reader.ReadUInt32();
            uint dev  = reader.ReadUInt32();
            uint ino  = reader.ReadUInt32();
            uint mode = reader.ReadUInt32();
            uint uid  = reader.ReadUInt32();
            uint gid  = reader.ReadUInt32();
            uint size = reader.ReadUInt32();

            return new UnixFileStatus
            {
                CreationTime = new UnixFileTime(ctimeSecs, ctimeNano),
                ModifiedTime = new UnixFileTime(mtimeSecs, mtimeNano),
                Device = dev,
                Inode = ino,
                Mode = new UnixFileMode(mode),
                UserId = uid,
                GroupId = gid,
                Size = size
            };
        }

        private static string ReadPath(BigEndianBinaryReader reader, in uint version, in int pathLength, in string prevPath)
        {
            if (version > 3)
            {
                int replaceLength = ReadPathReplaceLength(reader);

                var path = new StringBuilder(pathLength);

                // Copy the common prefix from the previous path
                int prefixLength = prevPath.Length - replaceLength;
                string prefix = prevPath.Substring(0, prefixLength);
                path.Append(prefix);

                // Append the rest of the current path
                char c;
                while ((c = reader.ReadChar()) != '\0') path.Append(c);

                return path.ToString();
            }

            byte[] pathBytes = reader.ReadBytes(pathLength);
            return Encoding.UTF8.GetString(pathBytes);
        }

        private static int ReadPathReplaceLength(BigEndianBinaryReader reader)
        {
            int headerByte = reader.ReadByte();
            int offset = headerByte & 0x7F;

            for (int i = 0; (headerByte & 0x80) != 0; i++)
            {
                headerByte = reader.ReadByte();
                offset += 1;
                offset = (offset << 7) + (headerByte & 0x7F);
            }

            return offset;
        }

        private static byte[] ReadExtensions(BigEndianBinaryReader reader)
        {
            Stream stream = reader.BaseStream;

            // TODO: support SHA-256 object IDs
            int extensionsLength = (int)(stream.Length - stream.Position - Constants.Sha1Length);

            return reader.ReadBytes(extensionsLength);
        }

        private static ObjectId ReadChecksum(BigEndianBinaryReader reader)
        {
            // TODO: support SHA-256 object IDs
            byte[] bytes = reader.ReadBytes(Constants.Sha1Length);
            return new ObjectId(bytes);
        }

        #endregion

        #region Write

        private static void WriteHeader(BigEndianBinaryWriter writer, IndexHeader header)
        {
            writer.Write(header.Signature);
            writer.Write(header.Version);
            writer.Write(header.EntryCount);
        }

        private static void WriteEntry(BigEndianBinaryWriter writer, uint version, IndexEntry entry)
        {
            int entryLength = BaseEntryLength;

            WriteFileStatus(writer, entry.Status);

            // TODO: support SHA-256 object IDs
            if (entry.ObjectId.Type != ObjectIdType.Sha1) throw new NotImplementedException();
            writer.Write(entry.ObjectId.Bytes);

            // Write flags and path length
            ushort flags = 0;
            if (entry.AssumeValid) flags |= AssumeValidFlag;
            if (entry.IsExtended) flags |= ExtendedFlag;
            flags |= (ushort)((byte)entry.Stage << 12);
            flags |= (ushort) entry.Path.Length;
            writer.Write(flags);

            entryLength += entry.Path.Length;

            // Write extended flags
            if (entry.IsExtended)
            {
                ushort extendedFlags = 0;
                if (entry.SkipWorktree) extendedFlags |= SkipWorktreeExFlag;
                if (entry.IntentToAdd) extendedFlags |= IntentToAddExFlag;
                writer.Write(extendedFlags);
                entryLength += 2;
            }

            // Write path
            WritePath(writer, version, entry.Path);

            // Add remaining null bytes
            var nullBytes = new byte[8 - (entryLength % 8)];
            writer.Write(nullBytes);
        }

        private static void WriteFileStatus(BigEndianBinaryWriter writer, UnixFileStatus status)
        {
            writer.Write(status.CreationTime.Seconds);
            writer.Write(status.CreationTime.Nanoseconds);
            writer.Write(status.ModifiedTime.Seconds);
            writer.Write(status.ModifiedTime.Nanoseconds);
            writer.Write(status.Device);
            writer.Write(status.Inode);
            writer.Write(status.Mode.Value);
            writer.Write(status.UserId);
            writer.Write(status.GroupId);
            writer.Write(status.Size);
        }

        private static void WritePath(BigEndianBinaryWriter writer, in uint version, string path)
        {
            // TODO: support version 4
            if (version < 1 || version > 3) throw new NotImplementedException();
            byte[] pathBytes = Encoding.UTF8.GetBytes(path);
            writer.Write(pathBytes);
        }

        private static void WriteExtensions(BigEndianBinaryWriter writer, byte[] extensions)
        {
            writer.Write(extensions);
        }

        private static void WriteChecksum(BigEndianBinaryWriter writer, ObjectId checksum)
        {
            // TODO: support SHA-256 object IDs
            if (checksum.Type != ObjectIdType.Sha1) throw new NotImplementedException();
            writer.Write(checksum.Bytes);
        }

        #endregion
    }
}
