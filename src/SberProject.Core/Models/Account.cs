using System.ComponentModel;
using System.Text.Json.Serialization;

namespace SberProject.Core.Models;

public enum AccountStatus
{
    [Description("Открыт")]
    Open,

    [Description("Закрыт")]
    Closed
}

public sealed class Account
{
    [JsonConstructor]
    private Account()
    {
    }

    internal Account(string number, Depositor depositor, DepositType depositType, decimal initialAmount, DateTime openedAt)
    {
        if (initialAmount < 0)
            throw new BankException("Первоначальная сумма не может быть отрицательной.");
        Money.EnsureKopecks(initialAmount);

        Number = number;
        Depositor = depositor;
        DepositType = depositType;
        Balance = initialAmount;
        OpenDate = DateOnly.FromDateTime(openedAt);
        LastOperationAt = openedAt;
        LastAccrualDate = OpenDate;
        Status = AccountStatus.Open;
    }

    [JsonInclude, JsonRequired]
    public string Number { get; private set; } = string.Empty;

    [JsonInclude, JsonRequired]
    public Depositor Depositor { get; private set; } = new();

    [JsonInclude, JsonRequired]
    public DepositType DepositType { get; private set; }

    [JsonInclude, JsonRequired]
    public decimal Balance { get; private set; }

    [JsonInclude]
    public decimal AccruedInterest { get; private set; }

    [JsonInclude, JsonRequired]
    public DateOnly OpenDate { get; private set; }

    [JsonInclude]
    public DateOnly? CloseDate { get; private set; }

    [JsonInclude, JsonRequired]
    public DateTime LastOperationAt { get; private set; }

    [JsonInclude, JsonRequired]
    public DateOnly LastAccrualDate { get; private set; }

    [JsonInclude, JsonRequired]
    public AccountStatus Status { get; private set; }

    [JsonIgnore]
    public bool IsOpen => Status == AccountStatus.Open;

    internal Operation Deposit(decimal amount, DateTime at)
    {
        EnsureOpen();
        Money.EnsurePositive(amount);

        Balance += amount;
        LastOperationAt = at;
        return CreateOperation(OperationType.Deposit, amount, at);
    }

    internal Operation Withdraw(decimal amount, DateTime at)
    {
        EnsureOpen();
        Money.EnsurePositive(amount);
        if (amount > Balance)
            throw new BankException($"Недостаточно средств: на счете {Balance:N2}, запрошено {amount:N2}.");

        Balance -= amount;
        LastOperationAt = at;
        return CreateOperation(OperationType.Withdrawal, amount, at);
    }

    internal void AddAccruedInterest(decimal interest, DateOnly accruedUntil)
    {
        EnsureOpen();
        ArgumentOutOfRangeException.ThrowIfNegative(interest);
        if (accruedUntil < LastAccrualDate)
            throw new ArgumentOutOfRangeException(nameof(accruedUntil), "Нельзя начислять проценты задним числом.");

        AccruedInterest += interest;
        LastAccrualDate = accruedUntil;
    }

    internal Operation? CapitalizeInterest(DateTime at)
    {
        EnsureOpen();
        var amount = Money.TruncateToKopecks(AccruedInterest);
        if (amount <= 0)
            return null;

        AccruedInterest -= amount;
        Balance += amount;
        LastOperationAt = at;
        return CreateOperation(OperationType.Interest, amount, at);
    }

    internal Operation Close(DateTime at)
    {
        EnsureOpen();
        var amount = Balance;

        Balance = 0;
        AccruedInterest = 0;
        Status = AccountStatus.Closed;
        CloseDate = DateOnly.FromDateTime(at);
        LastOperationAt = at;
        return CreateOperation(OperationType.Close, amount, at);
    }

    internal void UpdateDepositor(Depositor depositor)
    {
        EnsureOpen();
        Depositor = depositor;
    }

    internal Operation CreateOpeningOperation() =>
        CreateOperation(OperationType.Open, Balance, LastOperationAt);

    private Operation CreateOperation(OperationType type, decimal amount, DateTime at) => new()
    {
        AccountNumber = Number,
        Type = type,
        Amount = amount,
        BalanceAfter = Balance,
        Timestamp = at
    };

    private void EnsureOpen()
    {
        if (!IsOpen)
            throw new BankException($"Вклад {Number} закрыт, операции по нему невозможны.");
    }

    public override string ToString() => $"{Number} {Depositor.FullName}: {Balance:N2}";
}
