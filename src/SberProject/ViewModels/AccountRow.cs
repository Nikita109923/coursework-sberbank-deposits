using SberProject.Core;
using SberProject.Core.Models;

namespace SberProject.ViewModels;

public sealed class AccountRow
{
    public AccountRow(Account account)
    {
        Account = account;
    }

    public Account Account { get; }

    public string Number => Account.Number;
    public string FullName => Account.Depositor.FullName;
    public string PassportNumber => Account.Depositor.PassportNumber;
    public DepositType DepositType => Account.DepositType;
    public decimal Balance => Account.Balance;
    public decimal AccruedInterest => Money.TruncateToKopecks(Account.AccruedInterest);
    public DateTime LastOperationAt => Account.LastOperationAt;
    public AccountStatus Status => Account.Status;
    public bool IsOpen => Account.IsOpen;
}

public sealed record CategoryOption(DepositType? Type, string Name);
