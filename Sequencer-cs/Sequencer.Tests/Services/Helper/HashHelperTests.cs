using Sequencer.Core.Domain;
using Sequencer.Core.Services;
using CommandType = Sequencer.Core.Domain.Enums.CommandType;

namespace Sequencer.Tests.Services.Helper;

[TestFixture]
public class HashHelperTests
{
    [Test]
    public unsafe void CalculateChecksum_ShouldIgnore_FrameLength_And_CheckSum_Fields()
    {
        // 1. Allocate a 64-byte temporary memory block on the Stack to simulate an MmapFile
        byte* buffer = stackalloc byte[64];

        int totalLength = sizeof(RecordHeader) + sizeof(OrderRequest); // 32 + 24 = 56
        RecordHeader* header = (RecordHeader*)buffer;

        // 2. Write some actual data
        header->Command = CommandType.ORDERREQUEST;
        header->SeqId = 100;
        buffer[32] = 255; // Write a random byte in the Payload area

        // 3. Calculate the Hash for the first time
        uint initialHash = HashHelper.CalculateChecksum(buffer, totalLength);

        // 4. Intentionally modify FrameLength (Offset 0) and CheckSum (Offset 24)
        header->FrameLength = 9999;
        header->CheckSum = 8888;

        // 5. Calculate the Hash for the second time: The result must be exactly the same as the first time!
        uint hashAfterChangingIgnoredFields = HashHelper.CalculateChecksum(buffer, totalLength);
        Assert.That(hashAfterChangingIgnoredFields, Is.EqualTo(initialHash),
            "Hash changed! HashHelper failed to skip FrameLength or CheckSum.");

        // 6. Intentionally modify the SeqId (Offset 8) which needs to be protected
        header->SeqId = 101;
        uint hashAfterChangingSeqId = HashHelper.CalculateChecksum(buffer, totalLength);

        // 7. Calculate the Hash for the third time: The result must change!
        Assert.That(hashAfterChangingSeqId, Is.Not.EqualTo(initialHash),
            "Hash did not change! SeqId is not being protected by Checksum.");
    }
}