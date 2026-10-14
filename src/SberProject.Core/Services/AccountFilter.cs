using System.ComponentModel;
using SberProject.Core.Models;

namespace SberProject.Core.Services;

public enum AccountSortField
{
    [Description("по ФИО")]
    FullName,

    [Description("по номеру счета")]
    Number,

    [Description("по сумме вклада")]
    Balance,

    [Description("по дате последней операции")]
    LastOperation
}

public sealed record AccountFilter
{
    public string? Text { get; init; }

    public DepositType? DepositType { get; init; }

    public bool IncludeClosed { get; init; }
    public AccountSortField SortBy { get; init; } = AccountSortField.FullName;
    public bool Descending { get; init; }
}
