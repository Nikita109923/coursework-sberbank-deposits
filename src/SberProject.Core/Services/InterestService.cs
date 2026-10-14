using SberProject.Core.Models;

namespace SberProject.Core.Services;

public sealed class InterestService
{
    private static readonly TimeOnly CapitalizationTime = new(23, 59, 59);

    public IReadOnlyList<Operation> AccrueUntil(Account account, decimal annualRate, DateOnly until)
    {
        var operations = new List<Operation>();
        if (!account.IsOpen)
            return operations;

        var cursor = account.LastAccrualDate;
        while (cursor < until)
        {
            var nextMonth = new DateOnly(cursor.Year, cursor.Month, 1).AddMonths(1);
            var segmentEnd = nextMonth < until ? nextMonth : until;
            var days = segmentEnd.DayNumber - cursor.DayNumber;

            account.AddAccruedInterest(CalculateInterest(account.Balance, annualRate, days, cursor.Year), segmentEnd);

            if (segmentEnd == nextMonth)
            {
                var lastDayOfMonth = nextMonth.AddDays(-1).ToDateTime(CapitalizationTime);
                var operation = account.CapitalizeInterest(lastDayOfMonth);
                if (operation is not null)
                    operations.Add(operation);
            }

            cursor = segmentEnd;
        }

        return operations;
    }

    public static decimal CalculateInterest(decimal balance, decimal annualRate, int days, int year)
    {
        var daysInYear = DateTime.IsLeapYear(year) ? 366 : 365;
        return balance * annualRate / 100m * days / daysInYear;
    }
}
