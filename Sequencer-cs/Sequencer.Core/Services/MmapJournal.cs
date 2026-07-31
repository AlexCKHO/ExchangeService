using System.IO.MemoryMappedFiles;
using Sequencer.Core.Domain;
using Sequencer.Core.Interfaces;

namespace Sequencer.Core.Services;

public class MmapJournal : IJournal
{
    private string filePath;

    private MemoryMappedViewAccessor _viewAccessor;
    
    public MmapJournal()
    {
    }

    public void Append(SequencedOrder sequencedOrder)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<SequencedOrder> ReplayFrom(ulong seqId)
    {
        throw new NotImplementedException();
    }
}