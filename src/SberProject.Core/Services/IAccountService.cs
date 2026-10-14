using SberProject.Core.Models;

namespace SberProject.Core.Services;

public interface IAccountService
{
    IReadOnlyList<Account> Accounts { get; }

    Account Open(Depositor depositor, DepositType depositType, decimal initialAmount);

    Operation Deposit(string accountNumber, decimal amount);

    Operation Withdraw(string accountNumber, decimal amount);

    Operation Close(string accountNumber);

    void UpdateDepositor(string accountNumber, Depositor depositor);

    int AccrueInterestForAll();

    IReadOnlyList<Operation> GetHistory(string accountNumber);

    IReadOnlyList<Account> Find(AccountFilter filter);
}
