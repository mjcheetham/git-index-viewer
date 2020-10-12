using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Mjcheetham.Git.IndexViewer
{
    public static class EndianExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short Swap(this short i) => BinaryPrimitives.ReverseEndianness(i);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Swap(this int i) => BinaryPrimitives.ReverseEndianness(i);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long Swap(this long i) => BinaryPrimitives.ReverseEndianness(i);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort Swap(this ushort i) => BinaryPrimitives.ReverseEndianness(i);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint Swap(this uint i) => BinaryPrimitives.ReverseEndianness(i);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Swap(this ulong i) => BinaryPrimitives.ReverseEndianness(i);
    }
}
