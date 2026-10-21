using System.Globalization;
using Mauw.Erp.Domain.Accounting;

namespace Mauw.Erp.DataGenerator;

public sealed record GeneratorCommand(string Action, string? Confirmation, bool AllowReset, SimulationOptions Options)
{
    public static AccountingResult<GeneratorCommand> Parse(string[] args)
    {
        if (args.Length == 0 || args[0] is not ("plan" or "seed" or "reset" or "help"))
            return AccountingResult<GeneratorCommand>.Fail("Comando richiesto: plan, seed, reset oppure help.");

        var options = new SimulationOptions();
        string? confirmation = null;
        var allowReset = false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Length; i++)
        {
            var key = args[i];
            if (!seen.Add(key))
                return AccountingResult<GeneratorCommand>.Fail($"Opzione duplicata: {key}.");
            if (key == "--allow-reset")
            {
                allowReset = true;
                continue;
            }
            if (++i >= args.Length)
                return AccountingResult<GeneratorCommand>.Fail($"Valore mancante per {key}.");
            var value = args[i];
            if (key == "--confirm-database")
            {
                confirmation = value;
                continue;
            }
            if (key == "--end-date")
            {
                if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDate))
                    return AccountingResult<GeneratorCommand>.Fail("Data non valida: usare yyyy-MM-dd.");
                options = options with { EndDate = endDate };
                continue;
            }
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                return AccountingResult<GeneratorCommand>.Fail($"Valore intero non valido per {key}.");
            switch (key)
            {
                case "--years": options = options with { Years = number }; break;
                case "--seed": options = options with { Seed = number }; break;
                case "--customers": options = options with { Customers = number }; break;
                case "--active-customers": options = options with { ActiveCustomers = number }; break;
                case "--suppliers": options = options with { Suppliers = number }; break;
                case "--agents": options = options with { Agents = number }; break;
                case "--active-agents": options = options with { ActiveAgents = number }; break;
                case "--products": options = options with { Products = number }; break;
                case "--initial-products": options = options with { InitialProducts = number }; break;
                case "--batch-size": options = options with { BatchSize = number }; break;
                case "--weekly-pieces-factor": options = options with { WeeklyPiecesFactor = number }; break;
                case "--sales-lag-days": options = options with { SalesLagDays = number }; break;
                case "--sell-through-percent": options = options with { SellThroughPercent = number }; break;
                case "--year-end-carryover-percent": options = options with { YearEndCarryoverPercent = number }; break;
                default: return AccountingResult<GeneratorCommand>.Fail($"Opzione sconosciuta: {key}.");
            }
        }

        if (args[0] == "reset" && !allowReset)
            return AccountingResult<GeneratorCommand>.Fail("Reset distruttivo: aggiungere --allow-reset oltre alla conferma del database.");
        if (args[0] != "reset" && allowReset)
            return AccountingResult<GeneratorCommand>.Fail("--allow-reset e ammesso solo per reset.");
        var validated = options.Validate();
        return validated.Success
            ? AccountingResult<GeneratorCommand>.Ok(new GeneratorCommand(args[0], confirmation, allowReset, options))
            : AccountingResult<GeneratorCommand>.Fail(validated.Error);
    }
}
