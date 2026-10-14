using SberProject.Core;
using SberProject.Core.Models;
using SberProject.Core.Services;

namespace SberProject.Tests;

public class AccountServiceTests
{
    private readonly Bank _bank = new(new DateTime(2026, 1, 1, 9, 0, 0));

    [Fact]
    public void Open_CreatesAccountWithUnique20DigitNumbers()
    {
        var first = _bank.AccountService.Open(Bank.ValidDepositor(), DepositType.Savings, 500m);
        var second = _bank.AccountService.Open(Bank.ValidDepositor("Петров"), DepositType.OnDemand, 0m);

        Assert.Equal("42301933000000000001", first.Number);
        Assert.Equal("42301933000000000002", second.Number);
        Assert.All(new[] { first, second }, a => Assert.Matches(@"^\d{20}$", a.Number));
        Assert.Equal(500m, first.Balance);
        Assert.Equal(_bank.Clock.Now, first.LastOperationAt);
        Assert.Equal(OperationType.Open, Assert.Single(_bank.AccountService.GetHistory(first.Number)).Type);
        Assert.True(_bank.Accounts.Exists);
    }

    [Fact]
    public void Open_BelowCategoryMinimum_Throws()
    {
        var ex = Assert.Throws<BankException>(() => _bank.AccountService.Open(Bank.ValidDepositor(), DepositType.Savings, 99.99m));

        Assert.Contains("Минимальная сумма", ex.Message);
        Assert.Empty(_bank.AccountService.Accounts);
    }

    [Fact]
    public void Open_WithInvalidPassport_ThrowsWithFieldErrors()
    {
        var depositor = Bank.ValidDepositor(passport: "12345");

        var ex = Assert.Throws<DataValidationException>(() => _bank.AccountService.Open(depositor, DepositType.Savings, 500m));

        Assert.Contains(ex.Errors, e => e.Field == nameof(Depositor.PassportNumber));
        Assert.Empty(_bank.AccountService.Accounts);
    }

    [Fact]
    public void Deposit_AccruesInterestBeforeChangingBalance()
    {
        var account = _bank.AccountService.Open(Bank.ValidDepositor(), DepositType.Savings, 1000m);
        _bank.Clock.AdvanceDays(10);

        _bank.AccountService.Deposit(account.Number, 500m);

        // Проценты за 10 дней посчитаны на 1000, а не на 1500
        Assert.Equal(1000m * 0.09m * 10 / 365, account.AccruedInterest);
        Assert.Equal(1500m, account.Balance);
        Assert.Equal(_bank.Clock.Now, account.LastOperationAt);
    }

    [Fact]
    public void Withdraw_ExceedingBalance_IsRejected()
    {
        var account = _bank.AccountService.Open(Bank.ValidDepositor(), DepositType.Savings, 650.25m);

        Assert.Throws<BankException>(() => _bank.AccountService.Withdraw(account.Number, 1000m));
        Assert.Equal(650.25m, account.Balance);
    }

    [Fact]
    public void Close_PaysOutBalanceWithInterest()
    {
        var account = _bank.AccountService.Open(Bank.ValidDepositor(), DepositType.Savings, 1000m);
        _bank.Clock.AdvanceDays(45); // 15.02.2026

        var operation = _bank.AccountService.Close(account.Number);

        // Январь: 1000 × 9 % × 31/365 = 7,64383 → 7,64 (остаток 0,00383);
        // февраль, 14 дней: 1007,64 × 9 % × 14/365 + 0,00383 = 3,48226 → 3,48
        Assert.Equal(1011.12m, operation.Amount);
        Assert.False(account.IsOpen);
        var types = _bank.AccountService.GetHistory(account.Number).Select(o => o.Type);
        Assert.Equal([OperationType.Open, OperationType.Interest, OperationType.Interest, OperationType.Close], types);
    }

    [Fact]
    public void AccrueInterestForAll_CatchesUpMissedMonths()
    {
        _bank.AccountService.Open(Bank.ValidDepositor(), DepositType.Savings, 1000m);
        _bank.AccountService.Open(Bank.ValidDepositor("Петров"), DepositType.Pension, 1000m);
        _bank.Clock.Now = new DateTime(2026, 4, 2, 9, 0, 0);

        var count = _bank.AccountService.AccrueInterestForAll();

        Assert.Equal(6, count);
        Assert.All(_bank.AccountService.Accounts, a => Assert.Equal(new DateOnly(2026, 4, 2), a.LastAccrualDate));
    }

    [Fact]
    public void UpdateDepositor_ChangesPassportData()
    {
        var account = _bank.AccountService.Open(Bank.ValidDepositor(), DepositType.Savings, 500m);
        var changed = Bank.ValidDepositor("Иванова", "HB7654321");

        _bank.AccountService.UpdateDepositor(account.Number, changed);

        Assert.Equal("HB7654321", account.Depositor.PassportNumber);
        Assert.StartsWith("Иванова", account.Depositor.FullName);
    }

    [Fact]
    public void Find_FiltersByTextAndCategoryAndSorts()
    {
        _bank.AccountService.Open(Bank.ValidDepositor("Иванов", "MP1111111"), DepositType.Savings, 300m);
        _bank.AccountService.Open(Bank.ValidDepositor("Петров", "MP2222222"), DepositType.Savings, 900m);
        var closed = _bank.AccountService.Open(Bank.ValidDepositor("Сидоров", "MP3333333"), DepositType.OnDemand, 100m);
        _bank.AccountService.Close(closed.Number);

        var service = _bank.AccountService;

        Assert.Equal(["Петров", "Иванов"],
            service.Find(new AccountFilter { SortBy = AccountSortField.Balance, Descending = true })
                .Select(a => a.Depositor.LastName));
        Assert.Single(service.Find(new AccountFilter { Text = "петр" }));
        Assert.Single(service.Find(new AccountFilter { Text = "mp111" }));
        Assert.Single(service.Find(new AccountFilter { Text = "0000000002" }));
        Assert.Empty(service.Find(new AccountFilter { DepositType = DepositType.OnDemand }));
        Assert.Single(service.Find(new AccountFilter { DepositType = DepositType.OnDemand, IncludeClosed = true }));
    }
}
