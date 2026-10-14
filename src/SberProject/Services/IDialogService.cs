using SberProject.ViewModels;

namespace SberProject.Services;

public interface IDialogService
{
    bool ShowDialog(DialogViewModel viewModel);

    void ShowInfo(string message);
    void ShowError(string message);
    bool Confirm(string message);

    string? AskSaveFilePath(string defaultFileName, string filter);
}
