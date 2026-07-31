using Sequencer.Core.Domain.Enums;

namespace Sequencer.Core.Domain;

// Incoming Order object type from OMS to Sequencer 

public readonly struct Order
{
    public readonly ulong ClientOrderId { get; }
    public readonly ulong Price { get; }
    public readonly ulong Qty { get; }
    public readonly uint InstrumentId { get; }
    public readonly Side Side { get; }
    public readonly OrderType OrderType { get; }

    public Order(ulong clientOrderId, ulong price, ulong qty, uint instrumentId, Side side, OrderType orderType)
    {
        ClientOrderId = clientOrderId;
        Price = price;
        Qty = qty;
        InstrumentId = instrumentId;
        Side = side;
        OrderType = orderType;
    }
}