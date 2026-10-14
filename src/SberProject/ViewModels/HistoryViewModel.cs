using SberProject.Core.Models;

namespace SberProject.ViewModels;

public sealed class HistoryViewModel : DialogViewModel
{
    public HistoryViewModel(Account account, IReadOnlyList<Operation> operations)
    {
        Account = account;
        Rows = operations.Select(o => new OperationRow(o)).ToList();
    }

    public override string Title => $"История операций — счет {Account.Number}";

    public Account Account { get; }
    public IReadOnlyList<OperationRow> Rows { get; }

    public decimal TotalDeposited => Sum(OperationType.Open, OperationType.Deposit);
    public decimal TotalWithdrawn => Sum(OperationType.Withdrawal, OperationType.Close);
    public decimal TotalInterest => Sum(OperationType.Interest);

    private decimal Sum(params OperationType[] types) =>
        Rows.Where(r => types.Contains(r.Type)).Sum(r => r.Operation.Amount);
}

public sealed class OperationRow
{
    public OperationRow(Operation operation)
    {
        Operation = operation;
    }

    public Operation Operation { get; }

    public DateTime Timestamp => Operation.Timestamp;
    public OperationType Type => Operation.Type;
    public decimal SignedAmount => Operation.Type.BalanceSign() * Operation.Amount;
    public decimal BalanceAfter => Operation.BalanceAfter;
    public bool IsIncome => Operation.Type.BalanceSign() > 0;
}
