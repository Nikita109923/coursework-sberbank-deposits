using System.Collections;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SberProject.Core;
using SberProject.Core.Storage;
using SberProject.Core.Validation;

namespace SberProject.ViewModels;

public abstract partial class DialogViewModel : ObservableObject
{
    public event EventHandler<bool>? CloseRequested;

    public abstract string Title { get; }

    protected void Close(bool result) => CloseRequested?.Invoke(this, result);

    [RelayCommand]
    private void Cancel() => Close(false);
}

public abstract partial class ValidatableViewModel : DialogViewModel, INotifyDataErrorInfo
{
    private readonly Dictionary<string, List<string>> _errors = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorSummary))]
    private string? _errorSummary;

    public bool HasErrorSummary => !string.IsNullOrEmpty(ErrorSummary);

    public bool HasErrors => _errors.Count > 0;

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public IEnumerable GetErrors(string? propertyName) =>
        propertyName is not null && _errors.TryGetValue(propertyName, out var messages)
            ? messages
            : Array.Empty<string>();

    protected bool TryExecute(Action action, string? errorField = null)
    {
        try
        {
            action();
            return true;
        }
        catch (DataValidationException ex)
        {
            SetErrors(ex.Errors);
        }
        catch (BankException ex) when (errorField is not null)
        {
            SetErrors([new ValidationError(errorField, ex.Message)]);
        }
        catch (BankException ex)
        {
            ErrorSummary = ex.Message;
        }
        catch (StorageException ex)
        {
            ErrorSummary = "Не удалось сохранить данные: " + ex.Message;
        }

        return false;
    }

    protected void SetErrors(IEnumerable<ValidationError> errors)
    {
        ClearAllErrors();
        foreach (var group in errors.GroupBy(e => e.Field))
        {
            _errors[group.Key] = group.Select(e => e.Message).ToList();
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(group.Key));
        }

        UpdateSummary();
    }

    protected void ClearAllErrors()
    {
        var fields = _errors.Keys.ToList();
        _errors.Clear();
        foreach (var field in fields)
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(field));
        ErrorSummary = null;
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is not null && _errors.Remove(e.PropertyName))
        {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(e.PropertyName));
            UpdateSummary();
        }
    }

    private void UpdateSummary() =>
        ErrorSummary = _errors.Count == 0
            ? null
            : string.Join(Environment.NewLine, _errors.Values.SelectMany(m => m));
}
