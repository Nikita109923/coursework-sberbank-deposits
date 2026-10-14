using SberProject.Core.Logging;
using SberProject.Core.Models;
using SberProject.Core.Services;
using SberProject.Core.Storage;
using SberProject.Core.Validation;
using SberProject.Services;
using SberProject.ViewModels;

namespace SberProject.Tests;

public sealed class FakeClock : IClock
{
    public FakeClock(DateTime now)
    {
        Now = now;
    }

    public DateTime Now { get; set; }

    public void AdvanceDays(int days) => Now = Now.AddDays(days);
}

public sealed class InMemoryRepository<T> : IRepository<T>
{
    private List<T>? _items;

    public string Location => typeof(T).Name;
    public bool Exists => _items is not null;

    public List<T> Load() => _items is null ? [] : [.. _items];

    public void Save(IEnumerable<T> items) => _items = items.ToList();

    public void Reset() => _items = null;
}

public sealed class MemoryAuditLog : IAuditLog
{
    public List<string> Lines { get; } = [];
    public void Write(string message) => Lines.Add(message);
    public IReadOnlyList<string> ReadAll() => Lines;
}

public sealed class FakeDialogService : IDialogService
{
    public List<DialogViewModel> Shown { get; } = [];
    public List<string> Infos { get; } = [];
    public List<string> Errors { get; } = [];

    public Func<DialogViewModel, bool> OnShowDialog { get; set; } = _ => false;

    public bool ConfirmAnswer { get; set; } = true;
    public string? SaveFilePath { get; set; }

    public bool ShowDialog(DialogViewModel viewModel)
    {
        Shown.Add(viewModel);
        var result = false;
        void OnClose(object? sender, bool r) => result = r;
        viewModel.CloseRequested += OnClose;
        try
        {
            return OnShowDialog(viewModel) || result;
        }
        finally
        {
            viewModel.CloseRequested -= OnClose;
        }
    }

    public void ShowInfo(string message) => Infos.Add(message);
    public void ShowError(string message) => Errors.Add(message);
    public bool Confirm(string message) => ConfirmAnswer;
    public string? AskSaveFilePath(string defaultFileName, string filter) => SaveFilePath;
}

public sealed class Bank
{
    public Bank(DateTime now)
    {
        Clock = new FakeClock(now);
        Accounts = new InMemoryRepository<Account>();
        Context = new BankContext(new InMemoryRepository<DepositCategory>(), Accounts, new InMemoryRepository<Operation>());
        Context.Load();

        var interest = new InterestService();
        CategoryService = new CategoryService(Context, interest, Clock, Log);
        AccountService = new AccountService(Context, CategoryService, interest, new AccountNumberGenerator(),
            Validator, Clock, Log);
    }

    public FakeClock Clock { get; }
    public InMemoryRepository<Account> Accounts { get; }
    public BankContext Context { get; }
    public MemoryAuditLog Log { get; } = new();
    public PassportValidator Validator { get; } = new();
    public AccountService AccountService { get; }
    public CategoryService CategoryService { get; }

    public ViewModelFactory CreateViewModelFactory() =>
        new(AccountService, CategoryService, Validator, Clock, Log);

    public static Depositor ValidDepositor(string lastName = "Иванов", string passport = "MP1234567") => new()
    {
        LastName = lastName,
        FirstName = "Иван",
        MiddleName = "Иванович",
        BirthDate = new DateOnly(1985, 3, 12),
        PassportNumber = passport,
        PersonalNumber = "3120385A001PB1",
        IssuedBy = "Фрунзенским РУВД г. Минска",
        IssueDate = new DateOnly(2015, 4, 20),
        Address = "г. Минск"
    };
}
