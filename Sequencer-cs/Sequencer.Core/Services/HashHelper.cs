using System.IO.Hashing;
using System.Runtime.InteropServices;

namespace Sequencer.Core.Services;

public static class HashHelper
{
    public static uint CalculateChecksum<T>(ref T payload) where T : unmanaged
    {
        ushort lengthToHash = 24;

        Span<byte> headerBytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref payload, 1));
        Span<byte> bytesToHash = headerBytes.Slice(0, lengthToHash);

        return XxHash32.HashToUInt32(bytesToHash);
    }
}