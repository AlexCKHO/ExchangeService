using Sequencer.Core.Domain.Enums;

namespace Sequencer.Core.Domain;

// Incoming Order object type from OMS to Sequencer 
public struct Order(ulong ClientOrderId, ulong Price, ulong Qty, Side Side, OrderType OrderType, long Timestamp);