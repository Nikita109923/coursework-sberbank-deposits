using System.Text.RegularExpressions;
using SberProject.Core.Models;

namespace SberProject.Core.Validation;

public sealed record ValidationError(string Field, string Message);

/// <summary>
/// Проверка паспортных данных вкладчика.
/// </summary>
public sealed partial class PassportValidator
{
    private const int MinAge = 14;
    private const int MaxTextLength = 300;

    [GeneratedRegex(@"^\p{L}+([ -]\p{L}+)*$")]
    private static partial Regex NamePattern();

    [GeneratedRegex(@"^[A-Z]{2}\d{7}$")]
    private static partial Regex PassportPattern();

    [GeneratedRegex(@"^\d{7}[A-Z]\d{3}[A-Z]{2}\d$")]
    private static partial Regex PersonalNumberPattern();

    public IReadOnlyList<ValidationError> Validate(Depositor depositor, DateOnly today)
    {
        var errors = new List<ValidationError>();

        ValidateName(errors, nameof(Depositor.LastName), "Фамилия", depositor.LastName, required: true);
        ValidateName(errors, nameof(Depositor.FirstName), "Имя", depositor.FirstName, required: true);
        ValidateName(errors, nameof(Depositor.MiddleName), "Отчество", depositor.MiddleName, required: false);

        if (depositor.BirthDate == default)
            errors.Add(new(nameof(Depositor.BirthDate), "Укажите дату рождения."));
        else if (depositor.BirthDate > today)
            errors.Add(new(nameof(Depositor.BirthDate), "Дата рождения не может быть в будущем."));
        else if (depositor.BirthDate.AddYears(MinAge) > today)
            errors.Add(new(nameof(Depositor.BirthDate), $"Вкладчику должно быть не меньше {MinAge} лет."));
        else if (depositor.BirthDate.Year < 1900)
            errors.Add(new(nameof(Depositor.BirthDate), "Некорректная дата рождения."));

        if (!PassportPattern().IsMatch(depositor.PassportNumber ?? string.Empty))
            errors.Add(new(nameof(Depositor.PassportNumber),
                "Серия и номер паспорта: 2 заглавные латинские буквы и 7 цифр, например MP1234567."));

        if (!PersonalNumberPattern().IsMatch(depositor.PersonalNumber ?? string.Empty))
            errors.Add(new(nameof(Depositor.PersonalNumber),
                "Личный номер: 14 символов в формате 1234567A123PB1."));

        if (string.IsNullOrWhiteSpace(depositor.IssuedBy))
            errors.Add(new(nameof(Depositor.IssuedBy), "Укажите, кем выдан паспорт."));
        else if (depositor.IssuedBy.Length > MaxTextLength)
            errors.Add(new(nameof(Depositor.IssuedBy), $"Поле «Кем выдан» длиннее {MaxTextLength} символов."));

        if (depositor.IssueDate == default)
            errors.Add(new(nameof(Depositor.IssueDate), "Укажите дату выдачи паспорта."));
        else if (depositor.IssueDate > today)
            errors.Add(new(nameof(Depositor.IssueDate), "Дата выдачи не может быть в будущем."));
        else if (depositor.BirthDate != default && depositor.IssueDate < depositor.BirthDate)
            errors.Add(new(nameof(Depositor.IssueDate), "Дата выдачи не может быть раньше даты рождения."));

        if (depositor.Address?.Length > MaxTextLength)
            errors.Add(new(nameof(Depositor.Address), $"Адрес длиннее {MaxTextLength} символов."));

        return errors;
    }

    private static void ValidateName(List<ValidationError> errors, string field, string title, string? value, bool required)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
                errors.Add(new(field, $"Укажите поле «{title}»."));
            return;
        }

        if (value.Length > 50)
            errors.Add(new(field, $"Поле «{title}» длиннее 50 символов."));
        else if (!NamePattern().IsMatch(value))
            errors.Add(new(field, $"Поле «{title}» может содержать только буквы, пробел и дефис."));
    }
}
