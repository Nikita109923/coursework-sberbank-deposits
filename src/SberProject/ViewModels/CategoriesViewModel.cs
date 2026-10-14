using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SberProject.Core;
using SberProject.Core.Models;
using SberProject.Core.Services;
using SberProject.Core.Validation;

namespace SberProject.ViewModels;

public sealed partial class CategoriesViewModel : ValidatableViewModel
{
    private readonly ICategoryService _categoryService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private DepositCategory? _selectedCategory;

    [ObservableProperty] private string _rateText = string.Empty;
    [ObservableProperty] private string _minAmountText = string.Empty;
    [ObservableProperty] private bool _isOpenForNewDeposits;

    public CategoriesViewModel(ICategoryService categoryService)
    {
        _categoryService = categoryService;
        Reload(selectType: categoryService.Categories.Count > 0 ? categoryService.Categories[0].Type : null);
    }

    public override string Title => "Категории вкладов";

    public ObservableCollection<DepositCategory> Categories { get; } = [];

    public bool HasSelection => SelectedCategory is not null;

    partial void OnSelectedCategoryChanged(DepositCategory? value)
    {
        if (value is null)
            return;

        RateText = value.AnnualRate.ToString("0.##", CultureInfo.CurrentCulture);
        MinAmountText = value.MinOpeningAmount.ToString("0.00", CultureInfo.CurrentCulture);
        IsOpenForNewDeposits = value.IsOpenForNewDeposits;
        ClearAllErrors();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Save()
    {
        var errors = new List<ValidationError>();
        if (!Money.TryParseNumber(RateText, out var rate))
            errors.Add(new(nameof(RateText), "Ставка должна быть числом, например 9,5."));
        if (!Money.TryParseNumber(MinAmountText, out var minAmount))
            errors.Add(new(nameof(MinAmountText), "Минимальная сумма должна быть числом, например 100."));

        if (errors.Count > 0)
        {
            SetErrors(errors);
            return;
        }

        var updated = SelectedCategory! with
        {
            AnnualRate = rate,
            MinOpeningAmount = minAmount,
            IsOpenForNewDeposits = IsOpenForNewDeposits
        };

        if (TryExecute(() => _categoryService.Update(updated)))
            Reload(updated.Type);
    }

    private void Reload(DepositType? selectType)
    {
        Categories.Clear();
        foreach (var category in _categoryService.Categories)
            Categories.Add(category);

        SelectedCategory = Categories.FirstOrDefault(c => c.Type == selectType);
    }
}
