using Mauw.Erp.Domain;

namespace Mauw.Erp.Application;

public sealed class RecordPaymentCommand
{
    public IReadOnlyList<AgentCommission> Execute(Invoice invoice, Payment payment)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(payment);

        invoice.RegisterPayment(payment);

        if (!invoice.IsFullyPaid)
        {
            return Array.Empty<AgentCommission>();
        }

        var accruedCommissions = new List<AgentCommission>();

        foreach (var commission in invoice.AgentCommissions.Where(commission => commission.Status == AgentCommissionStatus.Pending))
        {
            commission.MarkAccrued();
            accruedCommissions.Add(commission);
        }

        return accruedCommissions;
    }

    public void Handle(Invoice invoice, Payment payment)
    {
        Execute(invoice, payment);
    }

    public static IReadOnlyList<AgentCommission> Record(Invoice invoice, Payment payment)
    {
        var command = new RecordPaymentCommand();
        return command.Execute(invoice, payment);
    }
}
