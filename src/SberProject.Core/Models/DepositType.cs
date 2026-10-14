using System.ComponentModel;

namespace SberProject.Core.Models;

public enum DepositType
{
    [Description("До востребования")]
    OnDemand,

    [Description("Накопительный")]
    Savings,

    [Description("Пенсионный")]
    Pension
}
