using Mauw.Erp.Application;
using Mauw.Erp.Domain;

namespace Mauw.Erp.UnitTests;

public class CommissionPaymentTests
{
    [Fact]
    public void PartialPayment_LeavesCommissionPending()
    {
        var invoice = new Invoice(grossTotal: 1200m, netTaxableBase: 1000m);
        var agentId = Guid.NewGuid();
        var commission = new AgentCommission(agentId, invoice.Id, 1000m, 10m);
        invoice.AddCommission(commission);

        var command = new RecordPaymentCommand();
        command.Execute(invoice, new Payment(invoice.Id, 800m));

        Assert.Equal(AgentCommissionStatus.Pending, commission.Status);
    }

    [Fact]
    public void FullPayment_AccruesCommission()
    {
        var invoice = new Invoice(grossTotal: 1200m, netTaxableBase: 1000m);
        var agentId = Guid.NewGuid();
        var commission = new AgentCommission(agentId, invoice.Id, 1000m, 10m);
        invoice.AddCommission(commission);

        var command = new RecordPaymentCommand();
        command.Execute(invoice, new Payment(invoice.Id, 1200m));

        Assert.Equal(AgentCommissionStatus.Accrued, commission.Status);
        Assert.Equal(100m, commission.CommissionAmount);
    }
}
