using System.IO;

namespace SberProject;

public sealed record AppPaths(string DataDirectory, string LogFile)
{
    public string CategoriesFile => Path.Combine(DataDirectory, "categories.json");
    public string AccountsFile => Path.Combine(DataDirectory, "accounts.json");
    public string OperationsFile => Path.Combine(DataDirectory, "operations.json");

    public static AppPaths NextTo(string baseDirectory) => new(
        Path.Combine(baseDirectory, "data"),
        Path.Combine(baseDirectory, "logs", "app.log"));
}
