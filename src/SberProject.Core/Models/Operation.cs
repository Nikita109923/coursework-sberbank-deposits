using System.ComponentModel;

namespace SberProject.Core.Models;

public enum OperationType
{
    [Description("Открытие вклада")]
    Open,

    [Description("Прием")]
    Deposit,

    [Description("Выдача")]
    Withdrawal,

    [Description("Начисление процентов")]
    Interest,

    [Description("Закрытие вклада")]
    Close
}

public static class OperationTypeExtensions
{
    public static int BalanceSign(this OperationType type) =>
        type is OperationType.Withdrawal or OperationType.Close ? -1 : 1;
}

public sealed record Operation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string AccountNumber { get; init; }
    public required OperationType Type { get; init; }
    public required decimal Amount { get; init; }
    public required decimal BalanceAfter { get; init; }
    public required DateTime Timestamp { get; init; }

    public override string ToString() => $"{Timestamp:g} {Type.GetDescription()} {Amount:N2} → {BalanceAfter:N2}";
}
