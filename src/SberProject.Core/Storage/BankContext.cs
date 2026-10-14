using SberProject.Core.Models;

namespace SberProject.Core.Storage;

public sealed class BankContext
{
    private readonly IRepository<DepositCategory> _categoryRepository;
    private readonly IRepository<Account> _accountRepository;
    private readonly IRepository<Operation> _operationRepository;

    public BankContext(
        IRepository<DepositCategory> categoryRepository,
        IRepository<Account> accountRepository,
        IRepository<Operation> operationRepository)
    {
        _categoryRepository = categoryRepository;
        _accountRepository = accountRepository;
        _operationRepository = operationRepository;
    }

    public List<DepositCategory> Categories { get; private set; } = [];
    public List<Account> Accounts { get; private set; } = [];
    public List<Operation> Operations { get; private set; } = [];

    public bool IsNewStorage { get; private set; }

    public void Load()
    {
        var categories = _categoryRepository.Load();
        var accounts = _accountRepository.Load();
        var operations = _operationRepository.Load();

        IsNewStorage = !_accountRepository.Exists;
        Accounts = accounts;
        Operations = operations;
        Categories = CompleteCategories(categories, out var categoriesChanged);

        if (categoriesChanged)
            _categoryRepository.Save(Categories);
    }

    public void SaveChanges()
    {
        _categoryRepository.Save(Categories);
        _accountRepository.Save(Accounts);
        _operationRepository.Save(Operations);
    }

    public void ResetStorage(string location)
    {
        if (_categoryRepository.Location == location) _categoryRepository.Reset();
        if (_accountRepository.Location == location) _accountRepository.Reset();
        if (_operationRepository.Location == location) _operationRepository.Reset();
    }

    private static List<DepositCategory> CompleteCategories(List<DepositCategory> stored, out bool changed)
    {
        var defaults = DefaultData.Categories();
        var result = Enum.GetValues<DepositType>()
            .Select(type => stored.FirstOrDefault(c => c.Type == type) ?? defaults.First(c => c.Type == type))
            .ToList();

        changed = !result.SequenceEqual(stored);
        return result;
    }
}
