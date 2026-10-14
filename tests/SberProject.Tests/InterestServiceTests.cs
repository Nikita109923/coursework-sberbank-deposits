using SberProject.Core.Models;
using SberProject.Core.Services;

namespace SberProject.Tests;

public class InterestServiceTests
{
    private readonly InterestService _service = new();

    private static Account NewAccount(decimal amount, DateTime openedAt) =>
        new("42301933000000000001", Bank.ValidDepositor(), DepositType.Savings, amount, openedAt);

    [Fact]
    public void FullMonth_IsCapitalizedOnLastDay()
    {
        var account = NewAccount(1000m, new DateTime(2026, 1, 1, 10, 0, 0));

        var operations = _service.AccrueUntil(account, 12m, new DateOnly(2026, 2, 1));

        // 1000 × 12 % × 31 / 365 = 10,19
        var operation = Assert.Single(operations);
        Assert.Equal(OperationType.Interest, operation.Type);
        Assert.Equal(10.19m, operation.Amount);
        Assert.Equal(new DateTime(2026, 1, 31, 23, 59, 59), operation.Timestamp);
        Assert.Equal(1010.19m, account.Balance);
        Assert.Equal(operation.Timestamp, account.LastOperationAt);
    }

    [Fact]
    public void PartialMonth_AccruesWithoutCapitalization()
    {
        var account = NewAccount(1000m, new DateTime(2026, 1, 1));

        var operations = _service.AccrueUntil(account, 12m, new DateOnly(2026, 1, 11));

        Assert.Empty(operations);
        Assert.Equal(1000m, account.Balance);
        Assert.Equal(1000m * 0.12m * 10 / 365, account.AccruedInterest);
        Assert.Equal(new DateOnly(2026, 1, 11), account.LastAccrualDate);
    }

    [Fact]
    public void MissedMonths_AreCaughtUpWithMonthlyCompounding()
    {
        var account = NewAccount(1000m, new DateTime(2026, 1, 1));

        var operations = _service.AccrueUntil(account, 12m, new DateOnly(2026, 4, 1));

        Assert.Equal(3, operations.Count);
        Assert.All(operations, o => Assert.Equal(OperationType.Interest, o.Type));
        // Январь: 10,19178 → 10,19 (остаток 0,00178 переносится);
        // февраль: 1010,19 × 12 % × 28/365 + 0,00178 = 9,30106 → 9,30;
        // март: 1019,49 × 12 % × 31/365 + 0,00106 = 10,39148 → 10,39
        Assert.Equal([10.19m, 9.30m, 10.39m], operations.Select(o => o.Amount));
        Assert.Equal(1029.88m, account.Balance);
    }

    [Fact]
    public void AccrualInSteps_GivesSameResultAsAtOnce()
    {
        var stepwise = NewAccount(5000m, new DateTime(2026, 3, 5));
        var atOnce = NewAccount(5000m, new DateTime(2026, 3, 5));

        for (var day = new DateOnly(2026, 3, 6); day <= new DateOnly(2026, 6, 20); day = day.AddDays(1))
            _service.AccrueUntil(stepwise, 9m, day);
        _service.AccrueUntil(atOnce, 9m, new DateOnly(2026, 6, 20));

        Assert.Equal(atOnce.Balance, stepwise.Balance);
        Assert.Equal(decimal.Round(atOnce.AccruedInterest, 10), decimal.Round(stepwise.AccruedInterest, 10));
    }

    [Fact]
    public void LeapYear_Uses366Days()
    {
        Assert.Equal(1000m * 0.10m * 29 / 366, InterestService.CalculateInterest(1000m, 10m, 29, 2028));
        Assert.Equal(1000m * 0.10m * 28 / 365, InterestService.CalculateInterest(1000m, 10m, 28, 2026));
    }

    [Fact]
    public void ZeroRate_ProducesNoOperations()
    {
        var account = NewAccount(1000m, new DateTime(2026, 1, 1));

        Assert.Empty(_service.AccrueUntil(account, 0m, new DateOnly(2026, 5, 1)));
        Assert.Equal(1000m, account.Balance);
    }

    [Fact]
    public void ClosedAccount_IsSkipped()
    {
        var account = NewAccount(1000m, new DateTime(2026, 1, 1));
        account.Close(new DateTime(2026, 1, 1, 15, 0, 0));

        Assert.Empty(_service.AccrueUntil(account, 12m, new DateOnly(2026, 5, 1)));
    }
}
