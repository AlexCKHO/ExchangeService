namespace Sequencer.Core.Domain;

public record SequencedOrder(Order Order, ulong SeqId, ulong IngestTicks);