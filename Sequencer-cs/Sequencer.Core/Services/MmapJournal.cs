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
    private static readonly int FILE_HEADER_SIZE = 4096; // First page 4kb for header 
    private const ulong JOURNAL_MAGIC = 0x4C4E524A; // "JRNL" in Hex

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
    private readonly ManualResetEventSlim _prewarmSignal = new(false);
    private readonly Thread _prewarmerThread;
    private volatile bool _threadDisposed = false;

    public MmapJournal(string fileName, long fileCapacityBytes)
    {
        // 1. Set basic

        _currentFilePath = fileName;
        _fileCapacityBytes = fileCapacityBytes;
        // Create new journal file when current size reaches 80%
        _rollingThreshold = (long)(fileCapacityBytes * 0.8);
        _currentFileSeq = GetHighestJournalSeq(Path.GetDirectoryName(fileName), Path.GetFileName(fileName));

        // 2. Set up MemoryMappedFile

        bool isNewFile = !File.Exists(_currentFilePath);


        // 3. Set up MemoryMappedFile
        _mmf = MemoryMappedFile.CreateFromFile(_currentFilePath, FileMode.OpenOrCreate, null, fileCapacityBytes,
            MemoryMappedFileAccess.ReadWrite);
        _accessor = _mmf.CreateViewAccessor();
        _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _basePointer);
        if (isNewFile)
        {
            // 新檔案：Format Header 並將 Offset 指向資料區開始 (4096)
            FormatFileHeader(_basePointer, _currentFileSeq);
            _currentOffset = FILE_HEADER_SIZE;
        }
        else
        {
            // 舊檔案復原：從 4096 開始 Scan，搵出下一個可以寫入嘅空位
            _currentOffset = RecoverTailOffset(_basePointer, fileCapacityBytes);
        }

        // 4. Initialize and start the dedicated background thread
        _prewarmerThread = new Thread(PrewarmWorkerLoop)
        {
            IsBackground = true,
            Name = "MmapJournal-PrewarmerWorker"
        };
        _prewarmerThread.Start();
    }

    private int GetHighestJournalSeq(string directoryPath, string baseFileName)
    {
        if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);

        var files = Directory.GetFiles(directoryPath, $"{baseFileName}-*.dat");
        int maxSeq = 1;

        foreach (var file in files)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            string seqString = name.Replace($"{baseFileName}-", "");
            if (int.TryParse(seqString, out int seq) && seq > maxSeq)
            {
                maxSeq = seq;
            }
        }

        return maxSeq;
    }

    private long RecoverTailOffset(byte* basePtr, long capacity)
    {
        long offset = FILE_HEADER_SIZE;
        while (offset < capacity)
        {
            RecordHeader* header = (RecordHeader*)(basePtr + offset);


            if (header->FrameLength == 0) break;

            offset += (header->FrameLength + 31) & ~31;
        }

        return offset;
    }

    private void FormatFileHeader(byte* basePtr, int seq)
    {
        FileHeader* header = (FileHeader*)basePtr;
        header->Magic = JOURNAL_MAGIC;
        header->Version = 1;
        header->FileSeq = seq;
        header->FirstSeqId = 0;
        header->CreatedTicks = DateTime.UtcNow.Ticks;
    }

    private void PrewarmWorkerLoop()
    {
        while (!_threadDisposed)
        {
            // 1. Wait for signal
            _prewarmSignal.Wait();

            if (_threadDisposed) break;

            // 2. Reset signal back to unsignaled state manually
            _prewarmSignal.Reset();

            // 3. Perform Segment Rolling I/O
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
            _prewarmSignal.Set(); // Wake up thread if waiting so it can exit
        }

        if (_currentOffset + totalLength > _fileCapacityBytes)
        {
            SwitchToNextFile();
            _prewarmSignal.Set();
        }

        if (_currentOffset == FILE_HEADER_SIZE)
        {
            FileHeader* fileHeader = (FileHeader*)_basePointer;
            fileHeader->FirstSeqId = seqId;
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

            FormatFileHeader(tempPtr, nextSeq);

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
        _currentOffset = FILE_HEADER_SIZE;

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

        _threadDisposed = true;
        _prewarmSignal.Set();
        if (_prewarmerThread.IsAlive)
        {
            _prewarmerThread.Join();
        }

        _prewarmSignal.Dispose();
    }
}