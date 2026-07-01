using Sequencer.Core.Domain.Enums;

namespace Sequencer.Core.Domain;

public record Order(ulong ClientOrderId, ulong Price, ulong Qty, Side Side, OrderType OrderType, long Timestamp);