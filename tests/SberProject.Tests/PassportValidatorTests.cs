using SberProject.Core.Models;
using SberProject.Core.Validation;

namespace SberProject.Tests;

public class PassportValidatorTests
{
    private static readonly DateOnly Today = new(2026, 10, 14);
    private readonly PassportValidator _validator = new();

    private IEnumerable<string> FieldsWithErrors(Func<Depositor, Depositor> change) =>
        _validator.Validate(change(Bank.ValidDepositor()), Today).Select(e => e.Field);

    [Fact]
    public void ValidDepositor_HasNoErrors()
    {
        Assert.Empty(_validator.Validate(Bank.ValidDepositor(), Today));
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("mp1234567")]
    [InlineData("MP123456")]
    [InlineData("МР1234567")] // кириллица
    public void InvalidPassportNumber_IsRejected(string passport)
    {
        Assert.Contains(nameof(Depositor.PassportNumber), FieldsWithErrors(d => d with { PassportNumber = passport }));
    }

    [Theory]
    [InlineData("31203850001PB1")]
    [InlineData("3120385A001PB")]
    public void InvalidPersonalNumber_IsRejected(string number)
    {
        Assert.Contains(nameof(Depositor.PersonalNumber), FieldsWithErrors(d => d with { PersonalNumber = number }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Иванов1")]
    [InlineData("Иванов!")]
    public void InvalidLastName_IsRejected(string lastName)
    {
        Assert.Contains(nameof(Depositor.LastName), FieldsWithErrors(d => d with { LastName = lastName }));
    }

    [Fact]
    public void DoubleBarrelledName_IsAccepted()
    {
        Assert.Empty(FieldsWithErrors(d => d with { LastName = "Римский-Корсаков" }));
    }

    [Fact]
    public void BirthDateInFutureOrTooYoung_IsRejected()
    {
        Assert.Contains(nameof(Depositor.BirthDate), FieldsWithErrors(d => d with { BirthDate = Today.AddDays(1) }));
        Assert.Contains(nameof(Depositor.BirthDate), FieldsWithErrors(d => d with { BirthDate = Today.AddYears(-13) }));
    }

    [Fact]
    public void IssueDateBeforeBirthOrInFuture_IsRejected()
    {
        Assert.Contains(nameof(Depositor.IssueDate), FieldsWithErrors(d => d with { IssueDate = new DateOnly(1980, 1, 1) }));
        Assert.Contains(nameof(Depositor.IssueDate), FieldsWithErrors(d => d with { IssueDate = Today.AddDays(1) }));
    }

    [Fact]
    public void MissingRequiredFields_AreAllReported()
    {
        var errors = _validator.Validate(new Depositor(), Today).Select(e => e.Field).ToList();

        Assert.Contains(nameof(Depositor.LastName), errors);
        Assert.Contains(nameof(Depositor.FirstName), errors);
        Assert.Contains(nameof(Depositor.BirthDate), errors);
        Assert.Contains(nameof(Depositor.IssuedBy), errors);
        Assert.Contains(nameof(Depositor.IssueDate), errors);
        Assert.DoesNotContain(nameof(Depositor.MiddleName), errors);
    }
}
