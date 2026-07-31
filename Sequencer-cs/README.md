# Sequencer Architecture Overview

This document outlines the architectural differences between `Sequencer.Core` and `Sequencer.Host`, and details the primary interfaces that form the system's pipeline.

## 1. Project Roles

### Sequencer.Core (Class Library)
* **What it is:** The "heart and soul" of the system. It is a pure class library completely devoid of any hosting, background worker, or I/O implementation details.
* **Responsibilities:** Defines the domain types (e.g., `Order`, `SequencedOrder`), enumerations, and all the core interfaces (contracts) such as `IJournal` and `ISequenceAllocator`. Because of its purity and lack of external dependencies, it can be easily referenced by unit tests and the Host application.

### Sequencer.Host (Worker / Console App)
* **What it is:** The "execution container" or electrical switchboard.
* **Responsibilities:** Acts as the executable background service. It uses Dependency Injection to wire up the concrete implementations of the interfaces defined in the Core. It is responsible for starting background threads (Workers), opening TCP/UDP listeners, and running the actual application process.

---

## 2. System Flow

Below is the abstract sequence flow of an order passing through the system interfaces:

```text
       [Inbound Transport] ──> (Receives external Orders)
                                      │
                                      ▼
                       [Sequence Allocator] (Assigns SeqId)
                                      │
                                      ▼
                             [Journal] (Writes to WAL)
                                      │
                                      ▼
       [Outbound Transport] ──> (Broadcasts/Sends out)
                                      │
                                      ▼
[GapDetector / NakResponder] ──> (Gap detection & retransmission mechanism)
```

---

## 3. Core Interfaces

The following interfaces govern the lifecycle and reliability of the data flowing through the sequencer:

### `ISequenceAllocator`
* **Responsibility:** Sequence ID (SeqId) allocation.
* **Details:** Ensures that every order flowing through the system is assigned a unique, strictly monotonically increasing large integer (e.g., 1, 2, 3...).

### `IJournal` (Write-Ahead Log)
* **Responsibility:** Durability and persistence.
* **Details:** Writes the sequenced orders to a disk or a Memory-Mapped File using an Append-Only structure. This ensures that even if the system crashes, the exact state can be perfectly restored by replaying the journal.

### `IInboundTransport`
* **Responsibility:** Inbound data reception.
* **Details:** Handles incoming external connections (e.g., high-throughput TCP connections or order requests from an OMS). It translates the raw incoming data bytes into `Order` objects and pushes them into the processing pipeline.

### `IOutboundTransport`
* **Responsibility:** Outbound broadcasting.
* **Details:** Takes the sequenced and journaled orders and broadcasts them over the network (e.g., via UDP Multicast) to downstream consumers like a Matching Engine.

### `INakResponder`
* **Responsibility:** Retransmission and error recovery (NAK fulfillment).
* **Details:** When a downstream receiver detects missing data (a gap) and sends a NAK (Negative Acknowledgment), this interface steps in. It retrieves the missing data from a Ring Buffer or disk and resends it to patch the gap.

### `IGapDetector`
* **Responsibility:** Sequence gap detection.
* **Details:** Typically situated on the receiving end. It monitors the incoming SeqIds to ensure there are no skips. If a gap is detected (e.g., receiving SeqId 1045 and then suddenly 1047), it instantly triggers a NAK request to fetch the missing message.

## 4. Core Object

The following is the data type that used in the sequencer application:

### `Order`

- **Responsibility:** The raw incoming order format transmitted from the Order Management System (OMS) to the Sequencer.
    
- **Details:**
    
    - `ulong ClientOrderId`: Client account order ID.
        
    - `ulong Price`: Order Price.
        
    - `ulong Qty`: Order Quantity.
        
    - `Side Side`: Trade direction (Bid or Ask).
        
    - `OrderType OrderType`: Execution type (Limit or Market).
        
    - `long Timestamp`: Initial order arrival time.
        

### `SequencedOrder`

- **Responsibility:** A value type representing the order after it has been successfully processed by the sequencer. This is the exact format transmitted downstream to the matching engine/OrderBook.
    
- **Details:**
    
    - `Order Order`: The original incoming order from the OMS.
        
    - `ulong SeqId`: The uniquely appended, monotonically increasing Sequence ID.
        
    - `ulong IngestTicks`: The precise, high-resolution tick timestamp captured the exact moment the Sequence ID was appended.
        

### `WalRecord`

- **Responsibility:** The memory-optimized, low-level data structure tailored for persisting the sequenced order to the Write-Ahead Log (WAL).
    
- **Details:**
    
    - `uint Magic`: A magic number used to validate log file headers or structural boundaries.
        
    - `ushort Version`: The current WAL format version.
        
    - `ushort PayLoadLen`: The total byte length of the serialized payload.
        
    - `ulong IngestTicks`: The sequencer's ingestion timestamp.
        
    - `ulong seqId`: The gl obally assigned Sequence ID.
        
    - `Span<byte> Payload`: The memory span containing the serialized byte data of the order.
        
    - `uint crc32`: Ag used to verify data integrity upon log replay or crash recovery.
        

### `GapResult`

- **Responsibility:** Represents the output from the `IGapDetector`, detailing any missing `SeqId` packets identified on the receiving end.
    
- **Details:** Used to trigger the `INakResponder` to request retransmission for the specified range of dropped messages.


```
[Fixed-Length Header]               [Variable-Length]   [Checksum]
┌───────┬─────────┬────────────┬─────────────┬───────┬──────────────────┬───────┐
│ Magic │ Version │ PayLoadLen │ IngestTicks │ seqId │     Payload      │ crc32 │
└───────┴─────────┴────────────┴─────────────┴───────┴──────────────────┴───────┘
    │        │           │                         │          │          │
    │        │           │                         │          │          └──> Read immediately after Payload
    │        │           │                         │          │               to verify checksum
    │        │           │                         │          └──────────────── Skip the exact number of bytes
    │        │           │                         │                            specified by PayLoadLen
    │        │           │                         └───────────────────────── Read seqId
    │        │           └─────────────────────────────────────────────────── Read this value (e.g., 256 bytes)
    │        │                                                                to know Payload length
    │        └─────────────────────────────────────────────────────────── Verify format version
    └──────────────────────────────────────────────────────────────────── Verify this is a valid record starts
```