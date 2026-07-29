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