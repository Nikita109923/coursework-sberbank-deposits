using SberProject.Core.Logging;
using SberProject.Core.Models;
using SberProject.Core.Storage;
using SberProject.Core.Validation;

namespace SberProject.Core.Services;

public sealed class AccountService : IAccountService
{
    private readonly BankContext _context;
    private readonly ICategoryService _categories;
    private readonly InterestService _interestService;
    private readonly AccountNumberGenerator _numberGenerator;
    private readonly PassportValidator _validator;
    private readonly IClock _clock;
    private readonly IAuditLog _log;

    public AccountService(
        BankContext context,
        ICategoryService categories,
        InterestService interestService,
        AccountNumberGenerator numberGenerator,
        PassportValidator validator,
        IClock clock,
        IAuditLog log)
    {
        _context = context;
        _categories = categories;
        _interestService = interestService;
        _numberGenerator = numberGenerator;
        _validator = validator;
        _clock = clock;
        _log = log;
    }

    public IReadOnlyList<Account> Accounts => _context.Accounts;

    public Account Open(Depositor depositor, DepositType depositType, decimal initialAmount)
    {
        EnsureValid(depositor);
        var category = _categories.GetCategory(depositType);

        if (!category.IsOpenForNewDeposits)
            throw new BankException($"Прием новых вкладов «{category.Name}» прекращен.");
        if (initialAmount < 0)
            throw new BankException("Первоначальная сумма не может быть отрицательной.");
        if (initialAmount > Money.MaxAmount)
            throw new BankException($"Сумма не может превышать {Money.MaxAmount:N0}.");
        if (initialAmount < category.MinOpeningAmount)
            throw new BankException(
                $"Минимальная сумма открытия вклада «{category.Name}» — {category.MinOpeningAmount:N2} {Money.Currency}.");

        var number = _numberGenerator.Next(_context.Accounts.Select(a => a.Number));
        var account = new Account(number, depositor, depositType, initialAmount, _clock.Now);

        _context.Accounts.Add(account);
        _context.Operations.Add(account.CreateOpeningOperation());
        _context.SaveChanges();

        _log.Write($"Открыт вклад {number} «{category.Name}» для {depositor.FullName}, сумма {initialAmount:N2}");
        return account;
    }

    public Operation Deposit(string accountNumber, decimal amount)
    {
        var operation = Execute(accountNumber, account => account.Deposit(amount, _clock.Now));
        _log.Write($"Прием {amount:N2} на счет {accountNumber}, остаток {operation.BalanceAfter:N2}");
        return operation;
    }

    public Operation Withdraw(string accountNumber, decimal amount)
    {
        var operation = Execute(accountNumber, account => account.Withdraw(amount, _clock.Now));
        _log.Write($"Выдача {amount:N2} со счета {accountNumber}, остаток {operation.BalanceAfter:N2}");
        return operation;
    }

    public Operation Close(string accountNumber)
    {
        var operation = Execute(accountNumber, account =>
        {
            var now = _clock.Now;
            if (account.CapitalizeInterest(now) is { } interest)
                _context.Operations.Add(interest);
            return account.Close(now);
        });

        _log.Write($"Закрыт вклад {accountNumber}, выдано {operation.Amount:N2}");
        return operation;
    }

    public void UpdateDepositor(string accountNumber, Depositor depositor)
    {
        EnsureValid(depositor);
        var account = GetAccount(accountNumber);
        if (account.Depositor == depositor)
            return;

        account.UpdateDepositor(depositor);
        _context.SaveChanges();

        _log.Write($"Изменены данные вкладчика по счету {accountNumber}: {depositor.FullName}");
    }

    public int AccrueInterestForAll()
    {
        var count = _context.Accounts.Where(a => a.IsOpen).Sum(AccrueInterest);
        _context.SaveChanges();

        if (count > 0)
            _log.Write($"Начисление процентов: выполнено капитализаций — {count}");
        return count;
    }

    public IReadOnlyList<Operation> GetHistory(string accountNumber) =>
        _context.Operations
            .Where(o => o.AccountNumber == accountNumber)
            .OrderBy(o => o.Timestamp)
            .ToList();

    public IReadOnlyList<Account> Find(AccountFilter filter)
    {
        IEnumerable<Account> query = _context.Accounts;

        if (!filter.IncludeClosed)
            query = query.Where(a => a.IsOpen);

        if (filter.DepositType is { } type)
            query = query.Where(a => a.DepositType == type);

        var text = filter.Text?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            query = query.Where(a =>
                a.Number.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                a.Depositor.FullName.Contains(text, StringComparison.CurrentCultureIgnoreCase) ||
                a.Depositor.PassportNumber.Contains(text, StringComparison.OrdinalIgnoreCase));
        }

        query = filter.SortBy switch
        {
            AccountSortField.Number => Sort(query, a => a.Number, filter.Descending),
            AccountSortField.Balance => Sort(query, a => a.Balance, filter.Descending),
            AccountSortField.LastOperation => Sort(query, a => a.LastOperationAt, filter.Descending),
            _ => Sort(query, a => a.Depositor.FullName, filter.Descending)
        };

        return query.ToList();
    }

    private Operation Execute(string accountNumber, Func<Account, Operation> action)
    {
        var account = GetAccount(accountNumber);
        AccrueInterest(account);

        var operation = action(account);
        _context.Operations.Add(operation);
        _context.SaveChanges();
        return operation;
    }

    private Account GetAccount(string accountNumber) =>
        _context.Accounts.FirstOrDefault(a => a.Number == accountNumber)
        ?? throw new BankException($"Счет {accountNumber} не найден.");

    private int AccrueInterest(Account account)
    {
        var rate = _categories.GetCategory(account.DepositType).AnnualRate;
        var operations = _interestService.AccrueUntil(account, rate, _clock.Today);
        _context.Operations.AddRange(operations);
        return operations.Count;
    }

    private void EnsureValid(Depositor depositor)
    {
        var errors = _validator.Validate(depositor, _clock.Today);
        if (errors.Count > 0)
            throw new DataValidationException(errors);
    }

    private static IOrderedEnumerable<Account> Sort<TKey>(IEnumerable<Account> source, Func<Account, TKey> key, bool descending) =>
        descending ? source.OrderByDescending(key) : source.OrderBy(key);
}
