using SberProject.Core;
using SberProject.Core.Models;

namespace SberProject.Tests;

public class AccountTests
{
    private static readonly DateTime OpenedAt = new(2026, 1, 10, 12, 0, 0);

    private static Account NewAccount(decimal amount = 500m) =>
        new("42301933000000000001", Bank.ValidDepositor(), DepositType.Savings, amount, OpenedAt);

    [Fact]
    public void Deposit_IncreasesBalanceAndUpdatesLastOperation()
    {
        var account = NewAccount();
        var at = OpenedAt.AddDays(3);

        var operation = account.Deposit(150.25m, at);

        Assert.Equal(650.25m, account.Balance);
        Assert.Equal(at, account.LastOperationAt);
        Assert.Equal(OperationType.Deposit, operation.Type);
        Assert.Equal(650.25m, operation.BalanceAfter);
    }

    [Fact]
    public void Withdraw_AnyAmountUpToBalance_IsAllowed()
    {
        var account = NewAccount();

        account.Withdraw(500m, OpenedAt.AddHours(1));

        Assert.Equal(0m, account.Balance);
    }

    [Fact]
    public void Withdraw_MoreThanBalance_Throws()
    {
        var account = NewAccount();

        var ex = Assert.Throws<BankException>(() => account.Withdraw(500.01m, OpenedAt));
        Assert.Contains("Недостаточно средств", ex.Message);
        Assert.Equal(500m, account.Balance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1.005)]
    public void Operations_WithInvalidAmount_Throw(decimal amount)
    {
        var account = NewAccount();

        Assert.Throws<BankException>(() => account.Deposit(amount, OpenedAt));
        Assert.Throws<BankException>(() => account.Withdraw(amount, OpenedAt));
        Assert.Equal(500m, account.Balance);
    }

    [Fact]
    public void Close_PaysOutBalanceAndBlocksFurtherOperations()
    {
        var account = NewAccount();

        var operation = account.Close(OpenedAt.AddDays(1));

        Assert.Equal(500m, operation.Amount);
        Assert.Equal(0m, account.Balance);
        Assert.False(account.IsOpen);
        Assert.Throws<BankException>(() => account.Deposit(10m, OpenedAt.AddDays(2)));
    }

    [Fact]
    public void CapitalizeInterest_CreditsWholeKopecksAndCarriesRemainder()
    {
        var account = NewAccount(1000m);
        account.AddAccruedInterest(10.1918m, new DateOnly(2026, 2, 1));

        var operation = account.CapitalizeInterest(OpenedAt.AddDays(21));

        Assert.NotNull(operation);
        Assert.Equal(10.19m, operation.Amount);
        Assert.Equal(1010.19m, account.Balance);
        Assert.Equal(0.0018m, account.AccruedInterest);
    }
}
