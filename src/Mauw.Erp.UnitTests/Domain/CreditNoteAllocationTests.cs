using Mauw.Erp.Domain.Entities;

namespace Mauw.Erp.UnitTests.Domain;

public sealed class CreditNoteAllocationTests
{
    [Fact]
    public void AllocateCreditTracksPartialAndFullConsumption()
    {
        var creditNote = new CreditNote(Guid.NewGuid(), "NC-TEST")
        {
            GrossTotal = 60.00m
        };

        var partial = creditNote.AllocateCredit(20.00m);
        Assert.True(partial.Success, partial.Error);
        Assert.Equal(20.00m, creditNote.AllocatedAmount);
        Assert.Equal(40.00m, creditNote.RemainingCredit);

        var complete = creditNote.AllocateCredit(40.00m);
        Assert.True(complete.Success, complete.Error);
        Assert.Equal(60.00m, creditNote.AllocatedAmount);
        Assert.Equal(0.00m, creditNote.RemainingCredit);
    }

    [Fact]
    public void AllocateCreditRejectsOverAllocationWithoutMutation()
    {
        var creditNote = new CreditNote(Guid.NewGuid(), "NC-TEST")
        {
            GrossTotal = 60.00m
        };

        var result = creditNote.AllocateCredit(60.01m);

        Assert.False(result.Success);
        Assert.Equal(0.00m, creditNote.AllocatedAmount);
        Assert.Equal(60.00m, creditNote.RemainingCredit);
    }

    public static TheoryData<decimal> InvalidAllocationAmounts => new()
    {
        0.00m,
        -1.00m,
        1.001m
    };

    [Theory]
    [MemberData(nameof(InvalidAllocationAmounts))]
    public void AllocateCreditRejectsInvalidAmounts(decimal amount)
    {
        var creditNote = new CreditNote(Guid.NewGuid(), "NC-TEST")
        {
            GrossTotal = 60.00m
        };

        var result = creditNote.AllocateCredit(amount);

        Assert.False(result.Success);
        Assert.Equal(0.00m, creditNote.AllocatedAmount);
    }
}
