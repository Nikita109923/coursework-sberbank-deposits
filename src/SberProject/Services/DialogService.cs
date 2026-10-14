using System.Windows;
using Microsoft.Win32;
using SberProject.ViewModels;
using SberProject.Views;

namespace SberProject.Services;

public sealed class DialogService : IDialogService
{
    private const string Caption = "Сбербанк — учет вкладчиков";

    private readonly Dictionary<Type, Func<Window>> _views = [];

    public DialogService()
    {
        Register<AccountEditViewModel, AccountEditWindow>();
        Register<OperationViewModel, OperationWindow>();
        Register<HistoryViewModel, HistoryWindow>();
        Register<CategoriesViewModel, CategoriesWindow>();
        Register<LogViewModel, LogWindow>();
    }

    public bool ShowDialog(DialogViewModel viewModel)
    {
        if (!_views.TryGetValue(viewModel.GetType(), out var createWindow))
            throw new InvalidOperationException($"Для {viewModel.GetType().Name} не зарегистрировано окно.");

        var window = createWindow();
        window.DataContext = viewModel;
        window.Owner = ActiveWindow();
        window.WindowStartupLocation = window.Owner is null
            ? WindowStartupLocation.CenterScreen
            : WindowStartupLocation.CenterOwner;

        void OnCloseRequested(object? sender, bool result) => window.DialogResult = result;
        viewModel.CloseRequested += OnCloseRequested;
        try
        {
            return window.ShowDialog() == true;
        }
        finally
        {
            viewModel.CloseRequested -= OnCloseRequested;
        }
    }

    public void ShowInfo(string message) =>
        Show(message, MessageBoxButton.OK, MessageBoxImage.Information);

    public void ShowError(string message) =>
        Show(message, MessageBoxButton.OK, MessageBoxImage.Warning);

    public bool Confirm(string message) =>
        Show(message, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public string? AskSaveFilePath(string defaultFileName, string filter)
    {
        var dialog = new SaveFileDialog
        {
            FileName = defaultFileName,
            Filter = filter,
            AddExtension = true,
            OverwritePrompt = true
        };
        return dialog.ShowDialog(ActiveWindow()) == true ? dialog.FileName : null;
    }

    private void Register<TViewModel, TWindow>()
        where TViewModel : DialogViewModel
        where TWindow : Window, new() =>
        _views[typeof(TViewModel)] = () => new TWindow();

    private static MessageBoxResult Show(string message, MessageBoxButton buttons, MessageBoxImage image)
    {
        var owner = ActiveWindow();
        return owner is null
            ? MessageBox.Show(message, Caption, buttons, image)
            : MessageBox.Show(owner, message, Caption, buttons, image);
    }

    private static Window? ActiveWindow() =>
        Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
        ?? Application.Current.MainWindow;
}
