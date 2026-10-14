using SberProject.Core.Logging;

namespace SberProject.ViewModels;

public sealed class LogViewModel : DialogViewModel
{
    public LogViewModel(IAuditLog log)
    {
        Lines = log.ReadAll().Reverse().ToList();
    }

    public override string Title => "Журнал действий";

    public IReadOnlyList<string> Lines { get; }
}
