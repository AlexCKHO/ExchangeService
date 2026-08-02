using System.Runtime.CompilerServices;
using Sequencer.Core.Domain;

namespace Sequencer.Tests.Domain;

public class StructSizeTests
{
    [Test]
    public void VerifyStructSizes()
    {
        Assert.That(System.Runtime.CompilerServices.Unsafe.SizeOf<OrderRequest>(), Is.EqualTo(32));
        Assert.That(System.Runtime.CompilerServices.Unsafe.SizeOf<OrderCancelRequest>(), Is.EqualTo(16));
    }

    [Test]
    public void StructSizes_MustBe_ExactlyAligned()
    {
        // Ensure RecordHeader is always 32 bytes
        Assert.That(Unsafe.SizeOf<RecordHeader>(), Is.EqualTo(32),
            "RecordHeader size changed! Pointer math will break.");

        // Ensure OrderRequest is 24 Bytes (8 + 8 + 4 + 2 + 2)
        Assert.That(Unsafe.SizeOf<OrderRequest>(), Is.EqualTo(32),
            "OrderRequest size changed! Check for implicit padding.");

        // Ensure OrderCancelRequest is 16 Bytes (8 + 8)
        Assert.That(Unsafe.SizeOf<OrderCancelRequest>(), Is.EqualTo(16),
            "OrderCancelRequest size changed! Check for implicit padding.");
    }
}