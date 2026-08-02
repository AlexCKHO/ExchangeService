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
}