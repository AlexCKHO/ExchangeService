using System.IO;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Sequencer.Core.Domain;
using Sequencer.Core.Domain.Enums;
using Sequencer.Core.Services;

namespace Sequencer.Tests.Services;

[TestFixture]
public class MmapJournalTests
{
    private string _tempFilePath;

    [SetUp]
    public void Setup()
    {
        _tempFilePath = Path.GetTempFileName();
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_tempFilePath))
        {
            File.Delete(_tempFilePath);
        }
    }

    [Test]
    public void Append_ShouldWriteData_ToExactMemoryOffsets()
    {
        // Arrange
        long capacity = 1024 * 1024; // 1MB
        var order = new OrderRequest
        {
            clientOrderId = 12345,
            price = 65000,
            qty = 200,
            instrumentId = 101,
            side = Side.ASK,
            orderType = OrderType.LIMIT
        };

        // Dynamically calculate the expected length, so the test won't break if the struct changes in the future!
        int expectedHeaderSize = Unsafe.SizeOf<RecordHeader>(); // 32
        int expectedPayloadSize = Unsafe.SizeOf<OrderRequest>(); // 32
        int expectedFrameLength = expectedHeaderSize + expectedPayloadSize; // 64

        using (var journal = new MmapJournal(_tempFilePath, capacity))
        {
            // Act: Write the first order (SeqId = 1)
            journal.Append(CommandType.ORDERREQUEST, 1, 1, ref order);
        }

        // Assert: Open the file to check the raw bytes inside
        using (var fs = new FileStream(_tempFilePath, FileMode.Open, FileAccess.Read))
        using (var reader = new BinaryReader(fs))
        {
            // Check Offset 0: FrameLength
            int frameLength = reader.ReadInt32();
            Assert.That(frameLength, Is.EqualTo(expectedFrameLength));

            // Check Offset 4: CommandType
            byte command = reader.ReadByte();
            Assert.That(command, Is.EqualTo((byte)CommandType.ORDERREQUEST));

            // Jump to Offset 8 to check SeqId
            fs.Position = 8;
            ulong seqId = reader.ReadUInt64();
            Assert.That(seqId, Is.EqualTo(1ul));

            // Jump to the start of the Payload (Offset 32) to check ClientOrderId
            fs.Position = expectedHeaderSize;
            ulong clientOrderId = reader.ReadUInt64();
            Assert.That(clientOrderId, Is.EqualTo(12345ul));

            // Check Price (Offset 32 + 8 = 40)
            ulong price = reader.ReadUInt64();
            Assert.That(price, Is.EqualTo(65000ul));
        }
    }
}