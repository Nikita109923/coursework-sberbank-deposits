using SberProject.Core;
using SberProject.Core.Models;

namespace SberProject.Tests;

public class CategoryServiceTests
{
    private readonly Bank _bank = new(new DateTime(2026, 1, 1, 9, 0, 0));

    [Fact]
    public void EveryDepositType_HasCategoryAfterFirstLoad()
    {
        Assert.Equal(Enum.GetValues<DepositType>(), _bank.CategoryService.Categories.Select(c => c.Type));
        Assert.Equal(Enum.GetValues<DepositType>(), DefaultData.Categories().Select(c => c.Type));
    }

    [Fact]
    public void Category_Name_ComesFromEnumDescription()
    {
        Assert.Equal("Накопительный", _bank.CategoryService.GetCategory(DepositType.Savings).Name);
    }

    [Fact]
    public void Update_ChangesParameters()
    {
        var updated = _bank.CategoryService.GetCategory(DepositType.Pension) with { AnnualRate = 11.25m, MinOpeningAmount = 10m };

        _bank.CategoryService.Update(updated);

        Assert.Equal(updated, _bank.CategoryService.GetCategory(DepositType.Pension));
        Assert.Contains(_bank.Log.Lines, l => l.Contains("Пенсионный", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(150, 0)]
    [InlineData(9.555, 0)]
    [InlineData(9, -1)]
    [InlineData(9, 1.005)]
    public void Update_InvalidParameters_Throws(decimal rate, decimal minAmount)
    {
        var category = _bank.CategoryService.GetCategory(DepositType.Savings);

        Assert.Throws<BankException>(() =>
            _bank.CategoryService.Update(category with { AnnualRate = rate, MinOpeningAmount = minAmount }));
        Assert.Equal(category, _bank.CategoryService.GetCategory(DepositType.Savings));
    }

    [Fact]
    public void Update_Rate_AccruesPreviousPeriodAtOldRate()
    {
        var account = _bank.AccountService.Open(Bank.ValidDepositor(), DepositType.Savings, 1000m);
        _bank.Clock.AdvanceDays(10);

        _bank.CategoryService.Update(_bank.CategoryService.GetCategory(DepositType.Savings) with { AnnualRate = 20m });

        Assert.Equal(1000m * 0.09m * 10 / 365, account.AccruedInterest);
        Assert.Equal(20m, _bank.CategoryService.GetCategory(DepositType.Savings).AnnualRate);
    }

    [Fact]
    public void ClosedForNewDeposits_BlocksOpeningButKeepsExistingAccounts()
    {
        var existing = _bank.AccountService.Open(Bank.ValidDepositor(), DepositType.Pension, 100m);

        _bank.CategoryService.Update(_bank.CategoryService.GetCategory(DepositType.Pension) with { IsOpenForNewDeposits = false });

        Assert.DoesNotContain(_bank.CategoryService.AvailableForNewDeposits, c => c.Type == DepositType.Pension);
        var ex = Assert.Throws<BankException>(() =>
            _bank.AccountService.Open(Bank.ValidDepositor("Петров"), DepositType.Pension, 100m));
        Assert.Contains("прекращен", ex.Message, StringComparison.Ordinal);

        _bank.AccountService.Deposit(existing.Number, 50m);
        Assert.Equal(150m, existing.Balance);
    }
}
