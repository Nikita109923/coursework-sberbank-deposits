namespace SberProject.Core.Storage;

public interface IRepository<T>
{
    string Location { get; }

    bool Exists { get; }

    List<T> Load();

    void Save(IEnumerable<T> items);

    void Reset();
}

public sealed class StorageException : Exception
{
    public StorageException(string location, string message, Exception? inner = null)
        : base(message, inner)
    {
        Location = location;
    }

    public string Location { get; }
}
