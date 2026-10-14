using SberProject.Core.Logging;
using SberProject.Core.Models;
using SberProject.Core.Storage;

namespace SberProject.Core.Services;

public sealed class CategoryService : ICategoryService
{
    private const decimal MaxRate = 100m;

    private readonly BankContext _context;
    private readonly InterestService _interestService;
    private readonly IClock _clock;
    private readonly IAuditLog _log;

    public CategoryService(BankContext context, InterestService interestService, IClock clock, IAuditLog log)
    {
        _context = context;
        _interestService = interestService;
        _clock = clock;
        _log = log;
    }

    public IReadOnlyList<DepositCategory> Categories => _context.Categories;

    public IReadOnlyList<DepositCategory> AvailableForNewDeposits =>
        _context.Categories.Where(c => c.IsOpenForNewDeposits).ToList();

    public DepositCategory GetCategory(DepositType type) =>
        _context.Categories.FirstOrDefault(c => c.Type == type)
        ?? throw new BankException($"Категория вклада «{type.GetDescription()}» не настроена.");

    public void Update(DepositCategory category)
    {
        Validate(category);
        var existing = GetCategory(category.Type);
        if (existing == category)
            return;

        if (existing.AnnualRate != category.AnnualRate)
        {
            foreach (var account in _context.Accounts.Where(a => a.IsOpen && a.DepositType == category.Type))
                _context.Operations.AddRange(_interestService.AccrueUntil(account, existing.AnnualRate, _clock.Today));
        }

        _context.Categories[_context.Categories.IndexOf(existing)] = category;
        _context.SaveChanges();

        _log.Write($"Изменена категория «{category.Name}»: ставка {category.AnnualRate} %, " +
                   $"минимальная сумма {category.MinOpeningAmount:N2}, " +
                   (category.IsOpenForNewDeposits ? "прием новых вкладов открыт" : "прием новых вкладов прекращен"));
    }

    private static void Validate(DepositCategory category)
    {
        if (!Enum.IsDefined(category.Type))
            throw new BankException("Неизвестная категория вклада.");

        if (category.AnnualRate is < 0 or > MaxRate)
            throw new BankException($"Годовая ставка должна быть от 0 до {MaxRate} %.");
        if (decimal.Round(category.AnnualRate, 2) != category.AnnualRate)
            throw new BankException("Ставка указывается не более чем с двумя знаками после запятой.");

        if (category.MinOpeningAmount < 0)
            throw new BankException("Минимальная сумма открытия не может быть отрицательной.");
        if (category.MinOpeningAmount > Money.MaxAmount)
            throw new BankException($"Минимальная сумма открытия не может превышать {Money.MaxAmount:N0}.");
        Money.EnsureKopecks(category.MinOpeningAmount);
    }
}
