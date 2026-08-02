# Context: Orderbook System (Ubiquitous Language)

This document defines the shared language and domain boundaries for the Orderbook system. It acts as the source of truth for terminology used across all microservices (OMS, Sequencer, OrderMatchingEngine, Broadcaster).

## System Architecture

The following ASCII diagram illustrates the high-level data flow and bounded contexts:

```text
  [Client/Simulator] 
         |
         | (1) PlaceOrderCommand / CancelOrderCommand
         v
+------------------+     (2)     +------------------+     (3)     +---------------------+
|       OMS        | ----------->|    Sequencer     | ----------->| OrderMatchingEngine |
| (C# / REST/gRPC) | Unsequenced | (C# or Rust)     | Sequenced   | (Rust)              |
| Idempotency Check| Order       | Assigns Monotonic| Order       | Executes Trades     |
+------------------+             | Sequence ID      |             +---------------------+
                                 +------------------+                       |
                                          |                                 | (4)
                                          | (Gap Detection / NAK)           v
                                          v                       +-----------------------+
                                 +------------------+             | MarketDataBroadcaster |
                                 |  INakResponder   |             | (C# / SignalR Hub)    |
                                 +------------------+             +-----------------------+
                                 
```

Failover Mechanics (The "GARP Magic")
Heartbeat Loss: Master dies; Follower detects missing heartbeats (1-2ms).

GARP Broadcast: Follower broadcasts a Gratuitous ARP (GARP) to the Layer 2 switch, moving the VIP to its own MAC address.

Smart Client Reconnect: TCP connections to the old Master break. OMS instances reconnect to the VIP (now the Follower).

Multicast Audit: OMS audits the multicast stream. If an order was sent prior to the crash but not sequenced (missing from the multicast wire), the OMS re-injects the order.

1. Core Domain Entities
Order: The raw incoming order format transmitted from the OMS to the Sequencer.

ClientOrderId (ulong): The identity of an order. Not a raw counter — it is a composite handle packing the account identifier into the high bits and that account's own order sequence into the low bits, assembled at the OMS before the order reaches the Sequencer. Two accounts submitting their first order therefore produce distinct handles.

Price (ulong): Order Price.

Qty (ulong): Order Quantity.

Side (Side): Trade direction (Bid or Ask).

OrderType (OrderType): Execution type (Limit or Market).

Timestamp (long): Initial order arrival time.

SequencedOrder: A value type representing the order after processing by the sequencer. This is the exact format transmitted downstream to the matching engine/OrderBook via multicast.

Order (Order): The original incoming order.

SeqId (ulong): The uniquely appended, monotonically increasing Sequence ID.

IngestTicks (ulong): The precise, high-resolution tick timestamp captured the exact moment the Sequence ID was appended.

WalRecord (Write-Ahead Log Record): The memory-optimized, low-level data structure tailored for persisting the sequenced order to the WAL.

Magic (uint): Used to validate log file headers/structural boundaries.

Version (ushort): Current WAL format version.

PayLoadLen (ushort): Total byte length of the serialized payload.

IngestTicks (ulong): The sequencer's ingestion timestamp.

seqId (ulong): Globally assigned Sequence ID.

Payload (Span): Memory span containing the serialized byte data.

crc32 (uint): Cyclic redundancy check to verify integrity on replay/recovery.

GapResult: Represents the output from the IGapDetector, detailing missing SeqId packets identified on the receiving end. Triggers the INakResponder to request retransmission for the dropped range.

2. Shared Principles
The Wire is Truth: If an order did not happen on the multicast stream, as far as the Replicated State Machine is concerned, it never happened at all.

Zero-Allocation Pipeline: Data is passed between threads using object reuse and struct value types to avoid C# Garbage Collection (GC) pauses.

Monotonic & Gapless: Every SeqId is strictly greater than the previous and contiguous (no holes).

Replayable: Reading the WAL from the start reconstructs the identical stream in the same order.
