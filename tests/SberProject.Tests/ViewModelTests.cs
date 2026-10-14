using SberProject.Core.Models;
using SberProject.Core.Services;
using SberProject.ViewModels;

namespace SberProject.Tests;

public class ViewModelTests
{
    private readonly Bank _bank = new(new DateTime(2026, 1, 1, 9, 0, 0));
    private readonly FakeDialogService _dialogs = new();

    private MainViewModel CreateMain() => new(
        _bank.AccountService, _bank.CategoryService, new CsvExporter(), _bank.Log, _bank.Clock,
        _dialogs, _bank.CreateViewModelFactory());

    private void OpenDemoAccounts()
    {
        _bank.AccountService.Open(Bank.ValidDepositor("Иванов", "MP1111111"), DepositType.Savings, 300m);
        _bank.AccountService.Open(Bank.ValidDepositor("Петров", "MP2222222"), DepositType.OnDemand, 900m);
    }

    [Fact]
    public void Main_FiltersBySearchTextAndCategory()
    {
        OpenDemoAccounts();
        var main = CreateMain();
        Assert.Equal(2, main.Accounts.Count);

        main.SearchText = "петр";
        Assert.Equal("Петров", Assert.Single(main.Accounts).Account.Depositor.LastName);

        main.SearchText = string.Empty;
        main.SelectedCategoryOption = main.CategoryOptions.Single(o => o.Type == DepositType.Savings);
        Assert.Equal("Иванов", Assert.Single(main.Accounts).Account.Depositor.LastName);
    }

    [Fact]
    public void Main_SortsByEnumField()
    {
        OpenDemoAccounts();
        var main = CreateMain();

        main.SelectedSortField = AccountSortField.Balance;
        main.SortDescending = true;

        Assert.Equal([900m, 300m], main.Accounts.Select(a => a.Balance));
    }

    [Fact]
    public void Main_AccountCommands_RequireOpenSelectedAccount()
    {
        OpenDemoAccounts();
        var main = CreateMain();

        Assert.False(main.DepositCommand.CanExecute(null));
        Assert.True(main.OpenAccountCommand.CanExecute(null));

        main.SelectedAccount = main.Accounts[0];
        Assert.True(main.DepositCommand.CanExecute(null));
        Assert.True(main.CloseAccountCommand.CanExecute(null));
    }

    [Fact]
    public void Main_Deposit_ThroughDialog_UpdatesListAndShowsInfo()
    {
        OpenDemoAccounts();
        var main = CreateMain();
        main.SelectedAccount = main.Accounts.Single(a => a.FullName.StartsWith("Иванов", StringComparison.Ordinal));
        _dialogs.OnShowDialog = vm =>
        {
            var operation = Assert.IsType<OperationViewModel>(vm);
            operation.AmountText = "150,25";
            operation.ConfirmCommand.Execute(null);
            return false;
        };

        main.DepositCommand.Execute(null);

        Assert.Equal(450.25m, main.SelectedAccount!.Balance);
        Assert.Contains("150,25", Assert.Single(_dialogs.Infos), StringComparison.Ordinal);
    }

    [Fact]
    public void Main_CloseAccount_AfterConfirmation_HidesAccount()
    {
        OpenDemoAccounts();
        var main = CreateMain();
        main.SelectedAccount = main.Accounts[0];

        main.CloseAccountCommand.Execute(null);

        Assert.Single(main.Accounts);
        Assert.Single(_dialogs.Infos);
    }

    [Fact]
    public void Main_AccruesInterestOnlyWhenDayChanges()
    {
        _bank.AccountService.Open(Bank.ValidDepositor(), DepositType.Savings, 1000m);
        var main = CreateMain();

        Assert.False(main.AccrueInterestIfNewDay());

        _bank.Clock.AdvanceDays(31);
        Assert.True(main.AccrueInterestIfNewDay());
        Assert.Equal(1007.64m, main.Accounts[0].Balance);
        Assert.False(main.AccrueInterestIfNewDay());
    }

    [Fact]
    public void Operation_InvalidAmount_ShowsFieldErrorAndStaysOpen()
    {
        var account = _bank.AccountService.Open(Bank.ValidDepositor(), DepositType.Savings, 500m);
        var viewModel = new OperationViewModel(_bank.AccountService, account, OperationType.Withdrawal);
        var closed = false;
        viewModel.CloseRequested += (_, _) => closed = true;

        viewModel.AmountText = "1000";
        viewModel.ConfirmCommand.Execute(null);

        Assert.False(closed);
        Assert.True(viewModel.HasErrors);
        Assert.Contains("Недостаточно средств", viewModel.GetErrors(nameof(viewModel.AmountText)).Cast<string>().Single(),
            StringComparison.Ordinal);
        Assert.Equal(500m, account.Balance);
    }

    [Fact]
    public void Operation_OnlyDepositAndWithdrawalAreAllowed()
    {
        var account = _bank.AccountService.Open(Bank.ValidDepositor(), DepositType.Savings, 500m);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new OperationViewModel(_bank.AccountService, account, OperationType.Interest));
    }

    [Fact]
    public void AccountEditor_EmptyForm_ReportsAllErrors()
    {
        var viewModel = _bank.CreateViewModelFactory().CreateAccountEditor(account: null);

        viewModel.SaveCommand.Execute(null);

        Assert.True(viewModel.HasErrors);
        Assert.True(viewModel.HasErrorSummary);
        Assert.NotEmpty(viewModel.GetErrors(nameof(viewModel.LastName)).Cast<string>());
        Assert.NotEmpty(viewModel.GetErrors(nameof(viewModel.PassportNumber)).Cast<string>());
        Assert.Empty(_bank.AccountService.Accounts);
    }

    [Fact]
    public void AccountEditor_ValidForm_OpensAccount()
    {
        var viewModel = _bank.CreateViewModelFactory().CreateAccountEditor(account: null);
        var d = Bank.ValidDepositor();
        viewModel.LastName = d.LastName;
        viewModel.FirstName = d.FirstName;
        viewModel.BirthDate = d.BirthDate.ToDateTime(TimeOnly.MinValue);
        viewModel.PassportNumber = "mp1234567";
        viewModel.PersonalNumber = d.PersonalNumber;
        viewModel.IssuedBy = d.IssuedBy;
        viewModel.IssueDate = d.IssueDate.ToDateTime(TimeOnly.MinValue);
        viewModel.SelectedCategory = viewModel.Categories.Single(c => c.Type == DepositType.Savings);
        viewModel.InitialAmountText = "250";

        viewModel.SaveCommand.Execute(null);

        var account = Assert.Single(_bank.AccountService.Accounts);
        Assert.Equal(account.Number, viewModel.ResultAccountNumber);
        Assert.Equal("MP1234567", account.Depositor.PassportNumber);
        Assert.Equal(DepositType.Savings, account.DepositType);
    }

    [Fact]
    public void AccountEditor_OffersOnlyCategoriesOpenForNewDeposits()
    {
        _bank.CategoryService.Update(_bank.CategoryService.GetCategory(DepositType.Pension) with { IsOpenForNewDeposits = false });

        var viewModel = _bank.CreateViewModelFactory().CreateAccountEditor(account: null);

        Assert.DoesNotContain(viewModel.Categories, c => c.Type == DepositType.Pension);
    }

    [Fact]
    public void Categories_Save_UpdatesRate()
    {
        var viewModel = new CategoriesViewModel(_bank.CategoryService)
        {
            SelectedCategory = _bank.CategoryService.GetCategory(DepositType.Savings)
        };

        viewModel.RateText = "12,5";
        viewModel.SaveCommand.Execute(null);

        Assert.Equal(12.5m, _bank.CategoryService.GetCategory(DepositType.Savings).AnnualRate);
        Assert.False(viewModel.HasErrors);
    }

    [Fact]
    public void Categories_InvalidRate_ShowsError()
    {
        var viewModel = new CategoriesViewModel(_bank.CategoryService);

        viewModel.RateText = "abc";
        viewModel.SaveCommand.Execute(null);

        Assert.NotEmpty(viewModel.GetErrors(nameof(viewModel.RateText)).Cast<string>());
    }

    [Fact]
    public void EnumDescriptions_AreRussian()
    {
        Assert.Equal("До востребования", DepositType.OnDemand.GetDescription());
        Assert.Equal("Выдача", OperationType.Withdrawal.GetDescription());
        Assert.Equal("Закрыт", AccountStatus.Closed.GetDescription());
        Assert.Equal("по сумме вклада", AccountSortField.Balance.GetDescription());
        Assert.All(Enum.GetValues<DepositType>(), t => Assert.NotEqual(t.ToString(), t.GetDescription()));
    }
}
