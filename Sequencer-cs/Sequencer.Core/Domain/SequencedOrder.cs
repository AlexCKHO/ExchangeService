namespace Sequencer.Core.Domain;

// Value type for Order after appended SeqId
public struct SequencedOrder(
    Order Order, // Order from OMS
    ulong SeqId, // Appended Sequence ID
    ulong IngestTicks); // Exact tick right after Sequence ID appended