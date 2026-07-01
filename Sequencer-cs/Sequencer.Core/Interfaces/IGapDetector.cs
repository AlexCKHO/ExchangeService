using System.Net;

namespace Sequencer.Core.Interfaces;


// For receiver end: 
public interface IGapDetector
{
    void OnReceived(ulong seqId);
}