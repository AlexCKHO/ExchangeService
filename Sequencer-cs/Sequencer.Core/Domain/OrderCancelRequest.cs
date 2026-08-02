namespace Sequencer.Core.Domain;

public struct OrderCancelRequest(
    ulong clientOrderId,
    ulong engineOrderId
);