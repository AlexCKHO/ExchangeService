using Sequencer.Core.Interfaces;

namespace Sequencer.Core.Services;

public class MonotonicSequenceAllocator : ISequencerAllocators
{
    private ulong _sequencerId;

    public MonotonicSequenceAllocator(ulong startSeqId)
    {
        this._sequencerId = startSeqId;
    }

    public ulong NextSequenceNumber()
    {
        return _sequencerId++;
    }
}