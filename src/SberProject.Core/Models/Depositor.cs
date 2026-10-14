using System.Text.Json.Serialization;

namespace SberProject.Core.Models;

public sealed record Depositor
{
    public string LastName { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string? MiddleName { get; init; }
    public DateOnly BirthDate { get; init; }

    public string PassportNumber { get; init; } = string.Empty;

    public string PersonalNumber { get; init; } = string.Empty;

    public string IssuedBy { get; init; } = string.Empty;
    public DateOnly IssueDate { get; init; }
    public string? Address { get; init; }

    [JsonIgnore]
    public string FullName =>
        string.Join(' ', new[] { LastName, FirstName, MiddleName }.Where(p => !string.IsNullOrWhiteSpace(p)));

    public override string ToString() => $"{FullName} ({PassportNumber})";
}
