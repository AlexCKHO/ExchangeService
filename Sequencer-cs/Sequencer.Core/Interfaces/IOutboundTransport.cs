using Sequencer.Core.Domain;

namespace Sequencer.Core.Interfaces;

// For Sending out SequencedOrder

public interface IOutboundTransport
{
    
    
    void Send(in SequencedOrder sequencedOrder);
    

}