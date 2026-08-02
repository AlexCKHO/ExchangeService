using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Runtime.CompilerServices;
using Sequencer.Core.Domain;
using Sequencer.Core.Domain.Enums;
using Sequencer.Core.Interfaces;

namespace Sequencer.Core.Services;

public unsafe class MmapJournal : IJournal, IDisposable
{
    private readonly MemoryMappedFile _mmf;
    private readonly MemoryMappedViewAccessor _accessor;
    private byte* _basePointer;

    // Record the last writing position
    private int _currentOffset;
    private ISequencerAllocators _allocators;

    public MmapJournal(string filePath, long capacityBytes, ISequencerAllocators allocators)
    {
        // 1. Open or create a file 
        // 2. Create MemoryMappedFile

        _mmf = MemoryMappedFile.CreateFromFile(filePath, FileMode.OpenOrCreate, null, capacityBytes,
            MemoryMappedFileAccess.ReadWrite);

        // 3. Set up MemoryMappedViewAccessor
        _accessor = _mmf.CreateViewAccessor();
        _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _basePointer);
        _allocators = allocators;

        _currentOffset = 0;
    }

    public void Append<T>(CommandType orderType, ushort version, ref T payload) where T : unmanaged
    {
        int payloadSize = sizeof(T);
        int totalLength = sizeof(RecordHeader) + payloadSize;

        byte* currentPointer = _currentOffset + _basePointer;

        // Pointer Casting 
        RecordHeader* header = (RecordHeader*)currentPointer;
        header->Command = orderType;
        header->Padding = 0;
        header->SchemaVer = version;
        header->SeqId = _allocators.NextSequenceNumber();
        header->IngestTicks = (ulong)System.Diagnostics.Stopwatch.GetTimestamp();
        header->Reserved = 0;
        
        byte* payloadPointer = currentPointer + sizeof(RecordHeader);
        
          *(T*)payloadPointer = payload;
        
        // Unsafe.CopyBlock(
        //     destination: payloadPointer,
        //     source: Unsafe.AsPointer(ref payload),
        //     byteCount: (uint)payloadSize
        //     );

        header->CheckSum = HashHelper.CalculateChecksum(ref payload);
        Volatile.Write(ref header->FrameLength, totalLength);
        
        // Use bitwise jump to next 32 multiple
        _currentOffset += (totalLength + 31) & ~31;

    }

    public void ReplayFrom(ulong seqId, JournalRecordHandler handler)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
    }
}