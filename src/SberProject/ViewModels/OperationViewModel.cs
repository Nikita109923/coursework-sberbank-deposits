using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SberProject.Core;
using SberProject.Core.Models;
using SberProject.Core.Services;

namespace SberProject.ViewModels;

public sealed partial class OperationViewModel : ValidatableViewModel
{
    private readonly IAccountService _accountService;
    private readonly Account _account;

    [ObservableProperty] private string _amountText = string.Empty;

    public OperationViewModel(IAccountService accountService, Account account, OperationType type)
    {
        if (type is not (OperationType.Deposit or OperationType.Withdrawal))
            throw new ArgumentOutOfRangeException(nameof(type), type, "Допустимы только прием и выдача.");

        _accountService = accountService;
        _account = account;
        Type = type;
    }

    public OperationType Type { get; }
    public bool IsWithdrawal => Type == OperationType.Withdrawal;

    public override string Title => IsWithdrawal ? "Выдача со вклада" : "Прием на вклад";
    public string ConfirmText => IsWithdrawal ? "Выдать" : "Принять";

    public string AccountNumber => _account.Number;
    public string FullName => _account.Depositor.FullName;
    public DepositType DepositType => _account.DepositType;
    public decimal Balance => _account.Balance;
    public decimal AccruedInterest => Money.TruncateToKopecks(_account.AccruedInterest);

    public Operation? Result { get; private set; }

    [RelayCommand]
    private void FillWholeBalance() =>
        AmountText = _account.Balance.ToString("0.00", CultureInfo.CurrentCulture);

    [RelayCommand]
    private void Confirm()
    {
        if (!Money.TryParse(AmountText, allowZero: false, out var amount, out var error))
        {
            SetErrors([new(nameof(AmountText), error!)]);
            return;
        }

        var done = TryExecute(
            () => Result = IsWithdrawal
                ? _accountService.Withdraw(_account.Number, amount)
                : _accountService.Deposit(_account.Number, amount),
            errorField: nameof(AmountText));

        // Перед операцией сервис досчитывает проценты, поэтому сумма вклада могла измениться.
        OnPropertyChanged(nameof(Balance));
        OnPropertyChanged(nameof(AccruedInterest));

        if (done)
            Close(true);
    }
}
