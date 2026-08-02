namespace Sequencer.Core.Domain;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
public struct FileHeader
{
    public ulong Magic; // Used to verify if this is our Journal (e.g., 0x4C4E524A)
    public ushort Version; // File format version (e.g., 1)
    public int FileSeq; // File sequence number (e.g., 1, 2, 3...)
    public ulong FirstSeqId; // SeqId of the first record in this segment
    public long CreatedTicks; // Creation timestamp
}