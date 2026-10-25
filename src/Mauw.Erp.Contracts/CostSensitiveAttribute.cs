namespace Mauw.Erp.Contracts;

/// <summary>
/// Marks a response member that carries purchase costs, margins or stock valuation. It is always nullable and
/// is set to null by <c>Mauw.Erp.Application.Costs.CostRedaction</c> for roles without cost visibility.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CostSensitiveAttribute : Attribute;
