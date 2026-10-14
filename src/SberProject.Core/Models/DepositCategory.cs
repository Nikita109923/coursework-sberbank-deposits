using System.Text.Json.Serialization;

namespace SberProject.Core.Models;

public sealed record DepositCategory(
    DepositType Type,
    decimal AnnualRate,
    decimal MinOpeningAmount,
    bool IsOpenForNewDeposits = true)
{
    [JsonIgnore]
    public string Name => Type.GetDescription();

    public override string ToString() => $"{Name} ({AnnualRate:0.##} %)";
}
