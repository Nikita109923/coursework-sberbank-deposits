using System.Globalization;

namespace SberProject.Core;

public static class Money
{
    public const string Currency = "BYN";
    public const decimal MaxAmount = 1_000_000_000m;

    public static decimal TruncateToKopecks(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.ToZero);

    public static bool HasKopecksOnly(decimal value) => decimal.Round(value, 2) == value;

    public static void EnsureKopecks(decimal amount)
    {
        if (!HasKopecksOnly(amount))
            throw new BankException("Сумма указывается с точностью до копейки (не более двух знаков после запятой).");
    }

    public static void EnsurePositive(decimal amount)
    {
        if (amount <= 0)
            throw new BankException("Сумма должна быть больше нуля.");
        if (amount > MaxAmount)
            throw new BankException($"Сумма не может превышать {MaxAmount:N0}.");
        EnsureKopecks(amount);
    }

    public static bool TryParse(string? text, bool allowZero, out decimal amount, out string? error)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            amount = 0;
            error = "Введите сумму.";
            return false;
        }

        if (!TryParseNumber(text, out amount))
        {
            error = "Сумма должна быть числом, например 150,25.";
            return false;
        }

        if (amount == 0 && !allowZero)
        {
            error = "Сумма должна быть больше нуля.";
            return false;
        }

        if (amount > MaxAmount)
        {
            error = $"Сумма не может превышать {MaxAmount:N0}.";
            return false;
        }

        if (!HasKopecksOnly(amount))
        {
            error = "Не более двух знаков после запятой.";
            return false;
        }

        error = null;
        return true;
    }

    public static bool TryParseNumber(string? text, out decimal value)
    {
        var normalized = (text ?? string.Empty)
            .Replace(" ", string.Empty)
            .Replace(" ", string.Empty)
            .Replace(',', '.');

        return decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }
}
