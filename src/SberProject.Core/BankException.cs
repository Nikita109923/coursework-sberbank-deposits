using SberProject.Core.Validation;

namespace SberProject.Core;

public class BankException : Exception
{
    public BankException(string message) : base(message)
    {
    }
}

public sealed class DataValidationException : BankException
{
    public DataValidationException(IReadOnlyList<ValidationError> errors)
        : base("Данные заполнены с ошибками:" + Environment.NewLine +
               string.Join(Environment.NewLine, errors.Select(e => "– " + e.Message)))
    {
        Errors = errors;
    }

    public IReadOnlyList<ValidationError> Errors { get; }
}
