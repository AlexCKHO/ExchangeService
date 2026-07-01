namespace Sequencer.Core.Domain;

public struct SequencedOrder(Order Order, ulong SeqId, ulong IngestTicks);