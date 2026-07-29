using System.Net;
using Sequencer.Core.Domain;

namespace Sequencer.Core.Interfaces;

public class INakResponder
{
    // Purpose: When UDP multicast miss an order, receiver call this method 
    // to retrieve the missing order

    // Input: missing Sequence ID and requester end point ID
    // Process fetch order from cache or hard drive and send it to End Point 
    void OnNakReceived(ulong seqId, EndPoint requester)
    {
    }
}