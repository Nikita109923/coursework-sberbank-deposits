using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SberProject.Core;
using SberProject.Core.Models;
using SberProject.Core.Services;
using SberProject.Core.Validation;

namespace SberProject.ViewModels;

public sealed partial class AccountEditViewModel : ValidatableViewModel
{
    private readonly IAccountService _accountService;
    private readonly PassportValidator _validator;
    private readonly IClock _clock;
    private readonly Account? _account;

    [ObservableProperty] private string _lastName = string.Empty;
    [ObservableProperty] private string _firstName = string.Empty;
    [ObservableProperty] private string _middleName = string.Empty;
    [ObservableProperty] private DateTime? _birthDate;
    [ObservableProperty] private string _passportNumber = string.Empty;
    [ObservableProperty] private string _personalNumber = string.Empty;
    [ObservableProperty] private string _issuedBy = string.Empty;
    [ObservableProperty] private DateTime? _issueDate;
    [ObservableProperty] private string _address = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MinAmountHint))]
    private DepositCategory? _selectedCategory;

    [ObservableProperty] private string _initialAmountText = "0";

    public AccountEditViewModel(
        IAccountService accountService,
        ICategoryService categoryService,
        PassportValidator validator,
        IClock clock,
        Account? account)
    {
        _accountService = accountService;
        _validator = validator;
        _clock = clock;
        _account = account;
        Categories = categoryService.AvailableForNewDeposits;

        if (account is null)
        {
            _selectedCategory = Categories.Count > 0 ? Categories[0] : null;
            return;
        }

        var d = account.Depositor;
        _lastName = d.LastName;
        _firstName = d.FirstName;
        _middleName = d.MiddleName ?? string.Empty;
        _birthDate = d.BirthDate.ToDateTime(TimeOnly.MinValue);
        _passportNumber = d.PassportNumber;
        _personalNumber = d.PersonalNumber;
        _issuedBy = d.IssuedBy;
        _issueDate = d.IssueDate.ToDateTime(TimeOnly.MinValue);
        _address = d.Address ?? string.Empty;
    }

    public override string Title => IsNew ? "Открытие вклада" : $"Данные вкладчика — счет {_account!.Number}";

    public bool IsNew => _account is null;

    public IReadOnlyList<DepositCategory> Categories { get; }

    public string MinAmountHint => SelectedCategory is null
        ? string.Empty
        : $"Ставка {SelectedCategory.AnnualRate:0.##} % годовых, минимальная сумма {SelectedCategory.MinOpeningAmount:N2} {Money.Currency}";

    public string? ResultAccountNumber { get; private set; }

    [RelayCommand]
    private void Save()
    {
        var depositor = BuildDepositor();
        var errors = _validator.Validate(depositor, _clock.Today).ToList();

        decimal initialAmount = 0;
        if (IsNew)
        {
            if (SelectedCategory is null)
                errors.Add(new(nameof(SelectedCategory), "Выберите категорию вклада."));
            if (!Money.TryParse(InitialAmountText, allowZero: true, out initialAmount, out var amountError))
                errors.Add(new(nameof(InitialAmountText), amountError!));
        }

        if (errors.Count > 0)
        {
            SetErrors(errors);
            return;
        }

        var saved = TryExecute(() =>
        {
            if (IsNew)
            {
                ResultAccountNumber = _accountService.Open(depositor, SelectedCategory!.Type, initialAmount).Number;
            }
            else
            {
                _accountService.UpdateDepositor(_account!.Number, depositor);
                ResultAccountNumber = _account.Number;
            }
        });

        if (saved)
            Close(true);
    }

    private Depositor BuildDepositor() => new()
    {
        LastName = LastName.Trim(),
        FirstName = FirstName.Trim(),
        MiddleName = string.IsNullOrWhiteSpace(MiddleName) ? null : MiddleName.Trim(),
        BirthDate = BirthDate is { } birth ? DateOnly.FromDateTime(birth) : default,
        PassportNumber = PassportNumber.Trim().ToUpperInvariant(),
        PersonalNumber = PersonalNumber.Trim().ToUpperInvariant(),
        IssuedBy = IssuedBy.Trim(),
        IssueDate = IssueDate is { } issue ? DateOnly.FromDateTime(issue) : default,
        Address = string.IsNullOrWhiteSpace(Address) ? null : Address.Trim()
    };
}
