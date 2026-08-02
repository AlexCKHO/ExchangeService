using System.Runtime.InteropServices;
using Sequencer.Core.Domain.Enums;

namespace Sequencer.Core.Domain;

[StructLayout(LayoutKind.Explicit, Size = 32)]
public struct RecordHeader
{
    [FieldOffset(0)] public int FrameLength;
    [FieldOffset(4)] public CommandType Command;
    [FieldOffset(5)] public byte Padding;
    [FieldOffset(6)] public ushort SchemaVer;
    [FieldOffset(8)] public ulong SeqId;
    [FieldOffset(16)] public ulong IngestTicks;
    [FieldOffset(24)] public uint CheckSum;
    [FieldOffset(28)] public uint Reserved;
}