namespace Sequencer.Core.Interfaces;

public interface ISequencerAllocators
{
    // Purpose return the next sequencer number
    ulong NextSequenceNumber();
}