using System.IO;
using System.Text;

namespace Mjcheetham.Git.IndexViewer
{
    public class BigEndianBinaryReader : BinaryReader
    {
        public BigEndianBinaryReader(Stream input, Encoding encoding, bool leaveOpen)
            : base(input, encoding, leaveOpen) { }

        public override short ReadInt16()   => base.ReadInt16().Swap();
        public override ushort ReadUInt16() => base.ReadUInt16().Swap();

        public override int ReadInt32()     => base.ReadInt32().Swap();
        public override uint ReadUInt32()   => base.ReadUInt32().Swap();

        public override long ReadInt64()    => base.ReadInt64().Swap();
        public override ulong ReadUInt64()  => base.ReadUInt64().Swap();
    }

    public class BigEndianBinaryWriter : BinaryWriter
    {
        public BigEndianBinaryWriter(Stream input, Encoding encoding, bool leaveOpen)
            : base(input, encoding, leaveOpen) { }

        public override void Write(short value) => base.Write(value.Swap());
        public override void Write(ushort value) => base.Write(value.Swap());

        public override void Write(int value) => base.Write(value.Swap());
        public override void Write(uint value) => base.Write(value.Swap());

        public override void Write(long value) => base.Write(value.Swap());
        public override void Write(ulong value) => base.Write(value.Swap());
    }
}
