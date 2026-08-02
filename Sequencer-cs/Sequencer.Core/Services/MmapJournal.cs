using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Runtime.CompilerServices;
using Sequencer.Core.Domain;
using Sequencer.Core.Domain.Enums;
using Sequencer.Core.Interfaces;

namespace Sequencer.Core.Services;

public unsafe class MmapJournal : IJournal, IDisposable
{
    // Current File Resources
    private static readonly int PageSize = Environment.SystemPageSize;

    private MemoryMappedFile _mmf;
    private MemoryMappedViewAccessor _accessor;
    private byte* _basePointer;
    private string _currentFilePath;
    private long _currentOffset;

    // Next File Resources (Pre-warmed)
    private MemoryMappedFile _mmfNext;
    private MemoryMappedViewAccessor _accessorNext;
    private byte* _basePointerNext;
    private string _nextFilePath;
    private int _currentFileSeq = 1;

    // Record the last writing position
    private readonly long _fileCapacityBytes;

    private volatile bool _isPreparingNextFile = false;
    private readonly long _rollingThreshold;

    // Dedicated Pre-warmer Thread Controls
    private readonly Thread _prewarmerThread;
    private readonly AutoResetEvent _prewarmSignal = new(false);
    private volatile bool _disposed = false;

    public MmapJournal(string fileName, long fileCapacityBytes)
    {
        // 1. Set basic

        _currentFilePath = fileName;
        _fileCapacityBytes = fileCapacityBytes;
        // Create new journal file when current size reaches 80%
        _rollingThreshold = (long)(fileCapacityBytes * 0.8);

        // 2. Set up MemoryMappedFile

        _mmf = MemoryMappedFile.CreateFromFile(_currentFilePath, FileMode.OpenOrCreate, null, fileCapacityBytes,
            MemoryMappedFileAccess.ReadWrite);

        // 3. Set up MemoryMappedViewAccessor

        _accessor = _mmf.CreateViewAccessor();
        _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _basePointer);
        _currentOffset = 0;

        // 4. Initialize and start the dedicated background thread
        _prewarmerThread = new Thread(PrewarmWorkerLoop)
        {
            IsBackground = true,
            Name = "MmapJournal-PrewarmerWorker"
        };
        _prewarmerThread.Start();
    }

    private void PrewarmWorkerLoop()
    {
        while (!_disposed)
        {
            // Sleep until Append signals work is needed
            _prewarmSignal.WaitOne();

            if (_disposed) break;

            PrepareNextFile();
        }
    }

    public void Append<T>(CommandType orderType, ulong seqId, ushort version, ref T payload) where T : unmanaged
    {
        int payloadSize = sizeof(T);
        int totalLength = sizeof(RecordHeader) + payloadSize;

        if (_currentOffset > _rollingThreshold && !_isPreparingNextFile)
        {
            _isPreparingNextFile = true;
            Task.Run(() => PrepareNextFile());
        }

        if (_currentOffset + totalLength > _fileCapacityBytes)
        {
            SwitchToNextFile();
            _prewarmSignal.Set();
        }

        // Pointer Casting 
        byte* currentPointer = _basePointer + _currentOffset;
        RecordHeader* header = (RecordHeader*)currentPointer;

        header->Command = orderType;
        header->Padding = 0;
        header->SchemaVer = version;
        header->SeqId = seqId;
        header->IngestTicks = (ulong)System.Diagnostics.Stopwatch.GetTimestamp();
        header->Reserved = 0;

        byte* payloadPointer = currentPointer + sizeof(RecordHeader);

        // start at payloadPointer memory address, pointer casting to payload
        *(T*)payloadPointer = payload;

        header->CheckSum = HashHelper.CalculateChecksum(currentPointer, totalLength);
        Volatile.Write(ref header->FrameLength, totalLength);

        // Use bitwise jump to next 32 multiple
        _currentOffset += (totalLength + 31) & ~31;
    }

    private void PrepareNextFile()
    {
        try
        {
            string directoryPath = Path.GetDirectoryName(_currentFilePath) ?? "";
            string extension = Path.GetExtension(_currentFilePath);

            int nextSeq = _currentFileSeq + 1;
            string nextFilePath = Path.Combine(directoryPath, $"journal-{nextSeq}{extension}");

            // Set up temp mmf
            var tempMmf = MemoryMappedFile.CreateFromFile(nextFilePath, FileMode.OpenOrCreate, null, _fileCapacityBytes,
                MemoryMappedFileAccess.ReadWrite);
            var tempAccessor = tempMmf.CreateViewAccessor();
            byte* tempPtr = null;
            tempAccessor.SafeMemoryMappedViewHandle.AcquirePointer(ref tempPtr);

            preWarmPage(tempPtr, _fileCapacityBytes);

            // Set nextMmf to temp mmf
            _nextFilePath = nextFilePath;
            _currentFileSeq = nextSeq;
            _mmfNext = tempMmf;
            _accessorNext = tempAccessor;

            Thread.MemoryBarrier();
            _basePointerNext = tempPtr;
        }
        catch (Exception ex)
        {
            _isPreparingNextFile = false;
        }
    }

    private void preWarmPage(byte* basePtr, long capacity)
    {
        if (basePtr == null || capacity == 0) return;

        // Looping thru the whole 
        for (long offset = 0; offset < capacity; offset += PageSize)
        {
            *(basePtr + offset) = 0;
        }

        // Writing the last byte
        *(basePtr + capacity - 1) = 0;
    }

    private void SwitchToNextFile()
    {
        if (_basePointerNext == null)
            throw new InvalidOperationException("Next file is not ready yet!");

        var oldMmf = _mmf;
        var oldAccessor = _accessor;
        var oldPointer = _basePointer;

        // Set next mmf to current mmf
        _mmf = _mmfNext;
        _accessor = _accessorNext;
        _basePointer = _basePointerNext;
        _currentFilePath = _nextFilePath;
        _currentOffset = 0;

        // Reset the _mmfNext and related fields
        _mmfNext = null;
        _accessorNext = null;
        _basePointerNext = null;

        _isPreparingNextFile = false;

        // Use another thread to dispose current access of the old file on OS / harddisk
        Task.Run(() =>
        {
            if (oldPointer != null) oldAccessor.SafeMemoryMappedViewHandle.ReleasePointer();
            oldAccessor?.Dispose();
            oldMmf?.Dispose();
        });
    }

    public void ReplayFrom(ulong seqId, JournalRecordHandler handler)
    {
    }

    public void Dispose()
    {
        if (_basePointer != null)
        {
            _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            _basePointer = null;
        }

        _accessor?.Dispose();
        _mmf?.Dispose();

        if (_basePointerNext != null)
        {
            _accessorNext.SafeMemoryMappedViewHandle.ReleasePointer();
            _basePointerNext = null;
        }

        _accessorNext?.Dispose();
        _mmfNext?.Dispose();
    }
}