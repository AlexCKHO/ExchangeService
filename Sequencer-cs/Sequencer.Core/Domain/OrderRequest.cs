using Sequencer.Core.Domain.Enums;

namespace Sequencer.Core.Domain;

// Incoming Order object type from OMS to Sequencer 
// TODO: Convert order to byte[] 
public readonly struct OrderRequest(
    ulong clientOrderId,
    ulong price,
    ulong qty,
    uint instrumentId,
    Side side,
    OrderType orderType);