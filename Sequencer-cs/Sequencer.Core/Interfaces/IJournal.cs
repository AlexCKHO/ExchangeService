using Sequencer.Core.Domain;
using Sequencer.Core.Domain.Enums;

namespace Sequencer.Core.Interfaces;

public delegate void JournalRecordHandler(ref RecordHeader header, ReadOnlySpan<byte> payload);

public interface IJournal
{
    // Input: New OrderRequest or OrderCancelRequest, 
    // Process: Saving Request to mmap with RecordHeader (Zero Allocation)
    void Append<T>(CommandType orderType, ushort version, ref T payload) where T : unmanaged;


    // Purpose: if follower sequence or Matching Engine is down,
    // send a sequence id to replay the whole list
    // Input: Starting sequence ID & The callback function to handle raw data
    // Process: Fetch from hard-drive and push Memory Span to handler (Zero Allocation)
    void ReplayFrom(ulong seqId, JournalRecordHandler handler);
}