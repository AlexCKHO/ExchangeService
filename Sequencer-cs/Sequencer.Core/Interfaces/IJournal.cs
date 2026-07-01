using Sequencer.Core.Domain;

namespace Sequencer.Core.Interfaces;

public interface IJournal
{
    // Input: New SequencedOrder, 
    // Process: Saving sequencedOrder to mmap
    // Return null;
    void Append(SequencedOrder sequencedOrder);

    // Purpose: if follower sequence or Matching Engine is down,
    // send a sequence id to replay the whole list

    // Input: Starting sequence ID
    // Process: Fetch from hard-drive
    // Return: whole list of sequencedOrders
    IEnumerable<SequencedOrder> ReplayFrom(ulong seqId);
}