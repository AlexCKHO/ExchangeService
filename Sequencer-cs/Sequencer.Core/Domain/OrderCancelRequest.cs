namespace Sequencer.Core.Domain;

public readonly record struct OrderCancelRequest(
    ulong origClOrdID,
    ulong engineOrderId
);