using SberProject.Core.Logging;
using SberProject.Core.Models;
using SberProject.Core.Services;
using SberProject.Core.Validation;

namespace SberProject.ViewModels;

public interface IViewModelFactory
{
    AccountEditViewModel CreateAccountEditor(Account? account);

    OperationViewModel CreateOperation(Account account, OperationType type);

    HistoryViewModel CreateHistory(Account account);

    CategoriesViewModel CreateCategories();

    LogViewModel CreateLog();
}

public sealed class ViewModelFactory : IViewModelFactory
{
    private readonly IAccountService _accounts;
    private readonly ICategoryService _categories;
    private readonly PassportValidator _validator;
    private readonly IClock _clock;
    private readonly IAuditLog _log;

    public ViewModelFactory(
        IAccountService accounts,
        ICategoryService categories,
        PassportValidator validator,
        IClock clock,
        IAuditLog log)
    {
        _accounts = accounts;
        _categories = categories;
        _validator = validator;
        _clock = clock;
        _log = log;
    }

    public AccountEditViewModel CreateAccountEditor(Account? account) =>
        new(_accounts, _categories, _validator, _clock, account);

    public OperationViewModel CreateOperation(Account account, OperationType type) =>
        new(_accounts, account, type);

    public HistoryViewModel CreateHistory(Account account) =>
        new(account, _accounts.GetHistory(account.Number));

    public CategoriesViewModel CreateCategories() => new(_categories);

    public LogViewModel CreateLog() => new(_log);
}
