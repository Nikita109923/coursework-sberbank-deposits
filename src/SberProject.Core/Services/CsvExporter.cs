using System.Globalization;
using System.Text;
using SberProject.Core.Models;

namespace SberProject.Core.Services;

public sealed class CsvExporter
{
    private const char Separator = ';';
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("ru-RU");

    public void Export(IEnumerable<Account> accounts, string filePath) =>
        File.WriteAllText(filePath, Build(accounts), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

    public string Build(IEnumerable<Account> accounts)
    {
        var builder = new StringBuilder();
        AppendRow(builder, "Номер счета", "ФИО", "Паспорт", "Личный номер", "Категория",
            "Сумма вклада", "Начисленные проценты", "Дата последней операции", "Статус");

        foreach (var a in accounts)
        {
            AppendRow(builder,
                a.Number,
                a.Depositor.FullName,
                a.Depositor.PassportNumber,
                a.Depositor.PersonalNumber,
                a.DepositType.GetDescription(),
                a.Balance.ToString("0.00", Culture),
                Money.TruncateToKopecks(a.AccruedInterest).ToString("0.00", Culture),
                a.LastOperationAt.ToString("dd.MM.yyyy HH:mm", Culture),
                a.Status.GetDescription());
        }

        return builder.ToString();
    }

    private static void AppendRow(StringBuilder builder, params string[] values)
    {
        builder.AppendJoin(Separator, values.Select(Escape));
        builder.Append("\r\n");
    }

    private static string Escape(string value)
    {
        if (value.IndexOfAny([Separator, '"', '\r', '\n']) < 0)
            return value;
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
