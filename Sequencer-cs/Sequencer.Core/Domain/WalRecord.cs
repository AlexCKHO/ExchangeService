namespace Sequencer.Core.Domain;

public struct WalRecord(
    uint Magic, // Log file headers or structural boundaries
    ushort Version, // Format version
    ushort PayLoadLen, // Byte length of the serialized payload
    ulong IngestTicks, // Ingestion timestamp.
    ulong seqId, // The Sequence ID.
    Span<byte> Payload, // The memory span containing the serialized byte data of the order.
    uint crc32); //  Cyclic redundancy check value