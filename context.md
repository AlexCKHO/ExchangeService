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
