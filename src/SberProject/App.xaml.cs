using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using SberProject.Core;
using SberProject.Core.Logging;
using SberProject.Core.Models;
using SberProject.Core.Services;
using SberProject.Core.Storage;
using SberProject.Core.Validation;
using SberProject.Services;
using SberProject.ViewModels;
using SberProject.Views;

namespace SberProject;

public partial class App : Application
{
    private static readonly TimeSpan AccrualCheckInterval = TimeSpan.FromMinutes(1);

    private ServiceProvider? _services;
    private DispatcherTimer? _accrualTimer;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ConfigureCulture();
        DispatcherUnhandledException += OnUnhandledException;

        _services = ConfigureServices(AppPaths.NextTo(AppContext.BaseDirectory));
        var log = _services.GetRequiredService<IAuditLog>();

        if (!TryLoad(_services.GetRequiredService<BankContext>(), log))
        {
            Shutdown();
            return;
        }

        log.Write("Программа запущена");
        RunStartupTasks();

        var mainViewModel = _services.GetRequiredService<MainViewModel>();
        MainWindow = new MainWindow { DataContext = mainViewModel };
        MainWindow.Show();

        _accrualTimer = new DispatcherTimer { Interval = AccrualCheckInterval };
        _accrualTimer.Tick += (_, _) => mainViewModel.AccrueInterestIfNewDay();
        _accrualTimer.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _accrualTimer?.Stop();
        _services?.GetService<IAuditLog>()?.Write("Программа закрыта");
        _services?.Dispose();
        base.OnExit(e);
    }

    private static ServiceProvider ConfigureServices(AppPaths paths)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IAuditLog>(sp =>
            new FileAuditLog(paths.LogFile, sp.GetRequiredService<IClock>()));

        services.AddSingleton(_ => new BankContext(
            new JsonRepository<DepositCategory>(paths.CategoriesFile),
            new JsonRepository<Account>(paths.AccountsFile),
            new JsonRepository<Operation>(paths.OperationsFile)));

        services.AddSingleton<InterestService>();
        services.AddSingleton<AccountNumberGenerator>();
        services.AddSingleton<PassportValidator>();
        services.AddSingleton<CsvExporter>();
        services.AddSingleton<ICategoryService, CategoryService>();
        services.AddSingleton<IAccountService, AccountService>();

        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IViewModelFactory, ViewModelFactory>();
        services.AddSingleton<MainViewModel>();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
    }

    private void RunStartupTasks()
    {
        var context = _services!.GetRequiredService<BankContext>();
        var accounts = _services!.GetRequiredService<IAccountService>();
        try
        {
            if (context.IsNewStorage)
                DefaultData.SeedDemoAccounts(accounts);
            accounts.AccrueInterestForAll();
        }
        catch (StorageException ex)
        {
            MessageBox.Show($"Не удалось сохранить данные в файл {ex.Location}:\n{ex.Message}",
                "Ошибка данных", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static bool TryLoad(BankContext context, IAuditLog log)
    {
        while (true)
        {
            try
            {
                context.Load();
                return true;
            }
            catch (StorageException ex)
            {
                log.Write($"Ошибка загрузки {ex.Location}: {ex.Message}");
                var answer = MessageBox.Show(
                    $"Не удалось загрузить данные из файла:\n{ex.Location}\n\n{ex.Message}\n\n" +
                    "Начать с пустым файлом? Поврежденный файл будет сохранен рядом с расширением .corrupt-<дата>.\n" +
                    "«Нет» — закрыть программу.",
                    "Ошибка данных", MessageBoxButton.YesNo, MessageBoxImage.Error);

                if (answer != MessageBoxResult.Yes)
                    return false;

                context.ResetStorage(ex.Location);
                log.Write($"Поврежденный файл отложен: {ex.Location}");
            }
        }
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _services?.GetService<IAuditLog>()?.Write($"Непредвиденная ошибка: {e.Exception}");
        MessageBox.Show($"Непредвиденная ошибка: {e.Exception.Message}\nПодробности записаны в журнал.",
            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void ConfigureCulture()
    {
        var culture = CultureInfo.GetCultureInfo("ru-RU");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        // Формат чисел и дат в привязках берется из свойства Language, а не из CurrentCulture.
        // TextElement (Run и др.) нужно настроить отдельно от FrameworkElement.
        var language = XmlLanguage.GetLanguage(culture.IetfLanguageTag);
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement), new FrameworkPropertyMetadata(language));
        FrameworkContentElement.LanguageProperty.OverrideMetadata(
            typeof(TextElement), new FrameworkPropertyMetadata(language));
    }
}
