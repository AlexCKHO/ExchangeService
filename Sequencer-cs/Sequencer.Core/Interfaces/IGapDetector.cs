using System.Net;

namespace Sequencer.Core.Interfaces;


// For receiver end, follower sequencer use this interface to detect any orders gap from master  sequencer
public interface IGapDetector
{
    void OnReceived(ulong seqId);
}