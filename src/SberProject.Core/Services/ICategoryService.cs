using SberProject.Core.Models;

namespace SberProject.Core.Services;

public interface ICategoryService
{
    IReadOnlyList<DepositCategory> Categories { get; }

    IReadOnlyList<DepositCategory> AvailableForNewDeposits { get; }

    DepositCategory GetCategory(DepositType type);

    void Update(DepositCategory category);
}
