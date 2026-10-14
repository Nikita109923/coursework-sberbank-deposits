using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SberProject.Core;
using SberProject.Core.Logging;
using SberProject.Core.Models;
using SberProject.Core.Services;
using SberProject.Core.Storage;
using SberProject.Services;

namespace SberProject.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IAccountService _accountService;
    private readonly ICategoryService _categoryService;
    private readonly CsvExporter _csvExporter;
    private readonly IAuditLog _log;
    private readonly IClock _clock;
    private readonly IDialogService _dialogs;
    private readonly IViewModelFactory _viewModels;

    private bool _refreshSuspended;
    private DateOnly _lastAccrualDay;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private CategoryOption? _selectedCategoryOption;
    [ObservableProperty] private AccountSortField _selectedSortField = AccountSortField.FullName;
    [ObservableProperty] private bool _sortDescending;
    [ObservableProperty] private bool _includeClosed;
    [ObservableProperty] private string _summary = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditDepositorCommand), nameof(DepositCommand), nameof(WithdrawCommand),
        nameof(ShowHistoryCommand), nameof(CloseAccountCommand))]
    private AccountRow? _selectedAccount;

    public MainViewModel(
        IAccountService accountService,
        ICategoryService categoryService,
        CsvExporter csvExporter,
        IAuditLog log,
        IClock clock,
        IDialogService dialogs,
        IViewModelFactory viewModels,
        AppPaths paths)
    {
        _accountService = accountService;
        _categoryService = categoryService;
        _csvExporter = csvExporter;
        _log = log;
        _clock = clock;
        _dialogs = dialogs;
        _viewModels = viewModels;
        DataDirectory = paths.DataDirectory;
        _lastAccrualDay = clock.Today;

        RefreshCategoryOptions();
        Refresh();
    }

    public ObservableCollection<AccountRow> Accounts { get; } = [];
    public ObservableCollection<CategoryOption> CategoryOptions { get; } = [];
    public IReadOnlyList<AccountSortField> SortFields { get; } = Enum.GetValues<AccountSortField>();
    public string DataDirectory { get; }

    partial void OnSearchTextChanged(string value) => Refresh();
    partial void OnSelectedCategoryOptionChanged(CategoryOption? value) => Refresh();
    partial void OnSelectedSortFieldChanged(AccountSortField value) => Refresh();
    partial void OnSortDescendingChanged(bool value) => Refresh();
    partial void OnIncludeClosedChanged(bool value) => Refresh();

    public void Refresh(string? selectNumber = null)
    {
        if (_refreshSuspended)
            return;

        selectNumber ??= SelectedAccount?.Number;

        var accounts = _accountService.Find(new AccountFilter
        {
            Text = SearchText,
            DepositType = SelectedCategoryOption?.Type,
            IncludeClosed = IncludeClosed,
            SortBy = SelectedSortField,
            Descending = SortDescending
        });

        Accounts.Clear();
        foreach (var account in accounts)
            Accounts.Add(new AccountRow(account));

        SelectedAccount = Accounts.FirstOrDefault(a => a.Number == selectNumber);

        var openCount = accounts.Count(a => a.IsOpen);
        Summary = $"Показано вкладов: {accounts.Count} (открытых: {openCount}) · " +
                  $"Общая сумма: {accounts.Sum(a => a.Balance):N2} {Money.Currency}";
    }

    public bool AccrueInterestIfNewDay()
    {
        if (_clock.Today == _lastAccrualDay)
            return false;

        _lastAccrualDay = _clock.Today;
        Run(() =>
        {
            _accountService.AccrueInterestForAll();
            Refresh();
        });
        return true;
    }

    private bool IsAccountSelected() => SelectedAccount is not null;
    private bool IsOpenAccountSelected() => SelectedAccount is { IsOpen: true };

    [RelayCommand]
    private void OpenAccount()
    {
        if (_categoryService.AvailableForNewDeposits.Count == 0)
        {
            _dialogs.ShowError("Прием новых вкладов прекращен по всем категориям. Измените настройки в справочнике категорий.");
            return;
        }

        var viewModel = _viewModels.CreateAccountEditor(account: null);
        if (_dialogs.ShowDialog(viewModel))
            Refresh(viewModel.ResultAccountNumber);
    }

    [RelayCommand(CanExecute = nameof(IsOpenAccountSelected))]
    private void EditDepositor()
    {
        if (_dialogs.ShowDialog(_viewModels.CreateAccountEditor(SelectedAccount!.Account)))
            Refresh();
    }

    [RelayCommand(CanExecute = nameof(IsOpenAccountSelected))]
    private void Deposit() => RunOperation(OperationType.Deposit);

    [RelayCommand(CanExecute = nameof(IsOpenAccountSelected))]
    private void Withdraw() => RunOperation(OperationType.Withdrawal);

    private void RunOperation(OperationType type)
    {
        var viewModel = _viewModels.CreateOperation(SelectedAccount!.Account, type);
        var confirmed = _dialogs.ShowDialog(viewModel);
        Refresh();

        if (confirmed && viewModel.Result is { } operation)
        {
            _dialogs.ShowInfo($"{operation.Type.GetDescription()}: {operation.Amount:N2} {Money.Currency}.\n" +
                              $"Остаток на счете: {operation.BalanceAfter:N2} {Money.Currency}.");
        }
    }

    [RelayCommand(CanExecute = nameof(IsAccountSelected))]
    private void ShowHistory() => _dialogs.ShowDialog(_viewModels.CreateHistory(SelectedAccount!.Account));

    [RelayCommand(CanExecute = nameof(IsOpenAccountSelected))]
    private void CloseAccount()
    {
        var row = SelectedAccount!;
        if (!_dialogs.Confirm($"Закрыть вклад {row.Number} ({row.FullName})?\n\n" +
                              "Будут начислены проценты по сегодняшний день, вкладчику выдается весь остаток."))
            return;

        Run(() =>
        {
            var operation = _accountService.Close(row.Number);
            Refresh();
            _dialogs.ShowInfo($"Вклад закрыт. Выдано вкладчику: {operation.Amount:N2} {Money.Currency}.");
        });
    }

    [RelayCommand]
    private void AccrueInterest()
    {
        Run(() =>
        {
            var count = _accountService.AccrueInterestForAll();
            _lastAccrualDay = _clock.Today;
            Refresh();
            _dialogs.ShowInfo(count == 0
                ? "Проценты начислены по сегодняшний день. Завершенных месяцев для капитализации нет."
                : $"Проценты начислены. Капитализировано за завершенные месяцы: {count} операций.");
        });
    }

    [RelayCommand]
    private void ManageCategories()
    {
        _dialogs.ShowDialog(_viewModels.CreateCategories());
        RefreshCategoryOptions();
        Refresh();
    }

    [RelayCommand]
    private void ShowLog() => _dialogs.ShowDialog(_viewModels.CreateLog());

    [RelayCommand]
    private void Export()
    {
        var path = _dialogs.AskSaveFilePath($"vklady_{_clock.Now:yyyy-MM-dd}.csv", "Файлы CSV (*.csv)|*.csv");
        if (path is null)
            return;

        try
        {
            _csvExporter.Export(Accounts.Select(r => r.Account), path);
            _log.Write($"Экспорт списка вкладов в CSV ({Accounts.Count} записей): {path}");
            _dialogs.ShowInfo($"Список сохранен ({Accounts.Count} записей):\n{path}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError("Не удалось сохранить файл: " + ex.Message);
        }
    }

    private void Run(Action action)
    {
        try
        {
            action();
        }
        catch (BankException ex)
        {
            Refresh();
            _dialogs.ShowError(ex.Message);
        }
        catch (StorageException ex)
        {
            _dialogs.ShowError($"Не удалось сохранить данные в файл {ex.Location}:\n{ex.Message}");
        }
    }

    private void RefreshCategoryOptions()
    {
        var selectedType = SelectedCategoryOption?.Type;

        _refreshSuspended = true;
        try
        {
            CategoryOptions.Clear();
            CategoryOptions.Add(new CategoryOption(null, "Все категории"));
            foreach (var category in _categoryService.Categories)
                CategoryOptions.Add(new CategoryOption(category.Type, category.Name));

            SelectedCategoryOption = CategoryOptions.FirstOrDefault(o => o.Type == selectedType) ?? CategoryOptions[0];
        }
        finally
        {
            _refreshSuspended = false;
        }
    }
}
