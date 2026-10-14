using SberProject.Core.Models;
using SberProject.Core.Services;

namespace SberProject.Core;

public static class DefaultData
{
    public static List<DepositCategory> Categories() =>
    [
        new(DepositType.OnDemand, AnnualRate: 0.5m, MinOpeningAmount: 0m),
        new(DepositType.Savings, AnnualRate: 9m, MinOpeningAmount: 100m),
        new(DepositType.Pension, AnnualRate: 10.5m, MinOpeningAmount: 50m)
    ];

    public static void SeedDemoAccounts(IAccountService accounts)
    {
        accounts.Open(new Depositor
        {
            LastName = "Иванов",
            FirstName = "Иван",
            MiddleName = "Иванович",
            BirthDate = new DateOnly(1985, 3, 12),
            PassportNumber = "MP1234567",
            PersonalNumber = "3120385A001PB1",
            IssuedBy = "Фрунзенским РУВД г. Минска",
            IssueDate = new DateOnly(2015, 4, 20),
            Address = "г. Минск, ул. Немига, д. 5, кв. 12"
        }, DepositType.Savings, 1500m);

        accounts.Open(new Depositor
        {
            LastName = "Петрова",
            FirstName = "Анна",
            MiddleName = "Сергеевна",
            BirthDate = new DateOnly(1992, 7, 5),
            PassportNumber = "HB2345678",
            PersonalNumber = "4050792B002PB3",
            IssuedBy = "Октябрьским РОВД г. Гродно",
            IssueDate = new DateOnly(2019, 9, 2),
            Address = "г. Гродно, ул. Советская, д. 10"
        }, DepositType.OnDemand, 320.50m);

        accounts.Open(new Depositor
        {
            LastName = "Сидоров",
            FirstName = "Петр",
            MiddleName = "Николаевич",
            BirthDate = new DateOnly(1955, 11, 30),
            PassportNumber = "MC3456789",
            PersonalNumber = "3301155C003PB7",
            IssuedBy = "Ленинским РОВД г. Могилева",
            IssueDate = new DateOnly(2012, 1, 16),
            Address = "г. Могилев, пр-т Мира, д. 21, кв. 3"
        }, DepositType.Pension, 5000m);

        accounts.Open(new Depositor
        {
            LastName = "Козлова",
            FirstName = "Мария",
            MiddleName = "Андреевна",
            BirthDate = new DateOnly(2000, 1, 15),
            PassportNumber = "KH4567890",
            PersonalNumber = "4150100E004PB2",
            IssuedBy = "Центральным РУВД г. Гомеля",
            IssueDate = new DateOnly(2020, 2, 10),
            Address = "г. Гомель, ул. Кирова, д. 7"
        }, DepositType.Savings, 800m);
    }
}
