using System.IO;
using SberProject.Core;
using SberProject.Core.Models;
using SberProject.Core.Services;
using SberProject.Core.Storage;

namespace SberProject.Tests;

public sealed class StorageAndExportTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "SberProjectTests", Guid.NewGuid().ToString("N"));

    public StorageAndExportTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private string FilePath(string name) => Path.Combine(_directory, name);

    [Fact]
    public void Account_RoundTripsThroughJson()
    {
        var account = new Account("42301933000000000007", Bank.ValidDepositor(), DepositType.Savings, 1000m, new DateTime(2026, 1, 1, 10, 0, 0));
        account.AddAccruedInterest(1.2345m, new DateOnly(2026, 1, 6));
        account.Withdraw(250.50m, new DateTime(2026, 1, 6, 11, 30, 0));
        var repository = new JsonRepository<Account>(FilePath("accounts.json"));

        repository.Save([account]);
        var loaded = Assert.Single(repository.Load());

        Assert.Equal(account.Number, loaded.Number);
        Assert.Equal(749.50m, loaded.Balance);
        Assert.Equal(1.2345m, loaded.AccruedInterest);
        Assert.Equal(new DateOnly(2026, 1, 6), loaded.LastAccrualDate);
        Assert.Equal(account.LastOperationAt, loaded.LastOperationAt);
        Assert.Equal(account.Depositor.PassportNumber, loaded.Depositor.PassportNumber);
        Assert.True(loaded.IsOpen);
        Assert.Contains("Иванов", File.ReadAllText(FilePath("accounts.json")));
    }

    [Fact]
    public void MissingFile_LoadsEmptyList()
    {
        var repository = new JsonRepository<Account>(FilePath("absent.json"));

        Assert.False(repository.Exists);
        Assert.Empty(repository.Load());
    }

    [Fact]
    public void CorruptedFile_ThrowsStorageException_AndResetMovesItAside()
    {
        var path = FilePath("accounts.json");
        File.WriteAllText(path, "{ это не JSON");
        var repository = new JsonRepository<Account>(path);

        var ex = Assert.Throws<StorageException>(() => repository.Load());
        Assert.Equal(path, ex.Location);

        repository.Reset();
        Assert.False(File.Exists(path));
        Assert.Single(Directory.GetFiles(_directory, "accounts.json.corrupt-*"));
        Assert.Empty(repository.Load());
    }

    [Fact]
    public void BankContext_PersistsAllCollections()
    {
        BankContext NewContext() => new(
            new JsonRepository<DepositCategory>(FilePath("categories.json")),
            new JsonRepository<Account>(FilePath("accounts.json")),
            new JsonRepository<Operation>(FilePath("operations.json")));

        var context = NewContext();
        context.Load();
        Assert.True(context.IsNewStorage);
        var clock = new FakeClock(new DateTime(2026, 1, 1));
        var log = new MemoryAuditLog();
        var interest = new InterestService();
        var categories = new CategoryService(context, interest, clock, log);
        var service = new AccountService(context, categories, interest, new AccountNumberGenerator(),
            new Core.Validation.PassportValidator(), clock, log);
        var account = service.Open(Bank.ValidDepositor(), DepositType.Savings, 500m);
        service.Deposit(account.Number, 100m);

        var reloaded = NewContext();
        reloaded.Load();

        Assert.False(reloaded.IsNewStorage);
        Assert.Equal(3, reloaded.Categories.Count);
        Assert.Equal(600m, Assert.Single(reloaded.Accounts).Balance);
        Assert.Equal(2, reloaded.Operations.Count);
    }

    [Fact]
    public void Csv_HasHeaderAndEscapesSeparators()
    {
        var depositor = Bank.ValidDepositor() with { LastName = "Иванов;Петров" };
        var account = new Account("42301933000000000001", depositor, DepositType.Savings, 1234.5m, new DateTime(2026, 1, 1, 10, 0, 0));

        var csv = new CsvExporter().Build([account]);
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, lines.Length);
        Assert.StartsWith("Номер счета;ФИО;", lines[0]);
        Assert.Contains("\"Иванов;Петров Иван Иванович\"", lines[1]);
        Assert.Contains(";Накопительный;1234,50;", lines[1], StringComparison.Ordinal);
        Assert.EndsWith(";01.01.2026 10:00;Открыт", lines[1]);
    }

    [Fact]
    public void Categories_RoundTripThroughJson()
    {
        var repository = new JsonRepository<DepositCategory>(FilePath("categories.json"));
        var categories = DefaultData.Categories();
        categories[1] = categories[1] with { IsOpenForNewDeposits = false };

        repository.Save(categories);

        Assert.Equal(categories, repository.Load());
        Assert.Contains("\"Type\": \"Savings\"", File.ReadAllText(FilePath("categories.json")), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""[{"Type":"Savings","AnnualRate":9,"MinOpeningAmount":100,"Code":"SAVINGS"}]""")]
    [InlineData("""[{"Type":"Unknown","AnnualRate":9,"MinOpeningAmount":100}]""")]
    [InlineData("""[{"Type":1,"AnnualRate":9,"MinOpeningAmount":100}]""")]
    [InlineData("""[{"AnnualRate":9,"MinOpeningAmount":100}]""")]
    public void UnexpectedJson_IsReportedAsCorruptedFile(string json)
    {
        var path = FilePath("categories.json");
        File.WriteAllText(path, json);

        Assert.Throws<StorageException>(() => new JsonRepository<DepositCategory>(path).Load());
    }

    [Fact]
    public void MissingCategories_AreCompletedWithDefaults()
    {
        var categoryRepository = new InMemoryRepository<DepositCategory>();
        categoryRepository.Save([new DepositCategory(DepositType.Savings, 12m, 0m)]);
        var context = new BankContext(categoryRepository, new InMemoryRepository<Account>(), new InMemoryRepository<Operation>());

        context.Load();

        Assert.Equal(Enum.GetValues<DepositType>(), context.Categories.Select(c => c.Type));
        Assert.Equal(12m, context.Categories.Single(c => c.Type == DepositType.Savings).AnnualRate);
        Assert.Equal(3, categoryRepository.Load().Count);
    }

    [Theory]
    [InlineData("150,25", 150.25)]
    [InlineData("150.25", 150.25)]
    [InlineData("1 000", 1000)]
    [InlineData("1 000,5", 1000.5)]
    public void Money_ParsesUserInput(string text, decimal expected)
    {
        Assert.True(Money.TryParse(text, allowZero: false, out var amount, out _));
        Assert.Equal(expected, amount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("0")]
    [InlineData("1,005")]
    public void Money_RejectsInvalidInput(string text)
    {
        Assert.False(Money.TryParse(text, allowZero: false, out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }
}
