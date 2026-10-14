# Диаграмма классов

Диаграмма в формате Mermaid отображается на GitHub и в VS Code (расширение Markdown Preview Mermaid). Для пояснительной записки ее можно сохранить в PNG через [mermaid.live](https://mermaid.live).

Обозначения: `+` — public, `~` — internal (меняет состояние, доступно только сервисам ядра).

## Бизнес-логика (`SberProject.Core`)

```mermaid
classDiagram
    direction LR

    class Depositor {
        <<record>>
        +string LastName
        +string FirstName
        +string? MiddleName
        +DateOnly BirthDate
        +string PassportNumber
        +string PersonalNumber
        +string IssuedBy
        +DateOnly IssueDate
        +string? Address
        +string FullName
    }

    class DepositType {
        <<enumeration>>
        OnDemand
        Savings
        Pension
    }

    class DepositCategory {
        <<record>>
        +DepositType Type
        +decimal AnnualRate
        +decimal MinOpeningAmount
        +bool IsOpenForNewDeposits
        +string Name
    }

    class Account {
        +string Number
        +Depositor Depositor
        +DepositType DepositType
        +decimal Balance
        +decimal AccruedInterest
        +DateOnly OpenDate
        +DateTime LastOperationAt
        +DateOnly LastAccrualDate
        +AccountStatus Status
        ~Deposit(amount, at) Operation
        ~Withdraw(amount, at) Operation
        ~AddAccruedInterest(interest, until)
        ~CapitalizeInterest(at) Operation?
        ~Close(at) Operation
        ~UpdateDepositor(depositor)
    }

    class AccountStatus {
        <<enumeration>>
        Open
        Closed
    }

    class Operation {
        <<record>>
        +Guid Id
        +string AccountNumber
        +OperationType Type
        +decimal Amount
        +decimal BalanceAfter
        +DateTime Timestamp
    }

    class OperationType {
        <<enumeration>>
        Open
        Deposit
        Withdrawal
        Interest
        Close
    }

    class IAccountService {
        <<interface>>
        +Open(depositor, type, amount) Account
        +Deposit(number, amount) Operation
        +Withdraw(number, amount) Operation
        +Close(number) Operation
        +UpdateDepositor(number, depositor)
        +AccrueInterestForAll() int
        +GetHistory(number) IReadOnlyList~Operation~
        +Find(filter) IReadOnlyList~Account~
    }

    class AccountService {
        -Execute(number, action) Operation
    }

    class ICategoryService {
        <<interface>>
        +Categories
        +AvailableForNewDeposits
        +GetCategory(type) DepositCategory
        +Update(category)
    }

    class CategoryService

    class InterestService {
        +AccrueUntil(account, rate, until) IReadOnlyList~Operation~
        +CalculateInterest(balance, rate, days, year)$ decimal
    }

    class AccountNumberGenerator {
        +Next(existingNumbers) string
    }

    class PassportValidator {
        +Validate(depositor, today) IReadOnlyList~ValidationError~
    }

    class CsvExporter {
        +Export(accounts, path)
    }

    class BankContext {
        +List~DepositCategory~ Categories
        +List~Account~ Accounts
        +List~Operation~ Operations
        +bool IsNewStorage
        +Load()
        +SaveChanges()
        +ResetStorage(location)
    }

    class IRepository~T~ {
        <<interface>>
        +Load() List~T~
        +Save(items)
        +Reset()
    }

    class JsonRepository~T~

    class IClock {
        <<interface>>
        +DateTime Now
        +DateOnly Today
    }

    class IAuditLog {
        <<interface>>
        +Write(message)
        +ReadAll() IReadOnlyList~string~
    }

    Account *-- Depositor : вкладчик
    Account --> DepositType
    Account --> AccountStatus
    Account ..> Operation : создает
    DepositCategory --> DepositType
    Operation --> OperationType

    AccountService ..|> IAccountService
    CategoryService ..|> ICategoryService
    JsonRepository~T~ ..|> IRepository~T~

    BankContext o-- IRepository~T~
    BankContext o-- Account
    BankContext o-- DepositCategory
    BankContext o-- Operation

    AccountService --> BankContext
    AccountService --> ICategoryService
    AccountService --> InterestService
    AccountService --> AccountNumberGenerator
    AccountService --> PassportValidator
    AccountService --> IClock
    AccountService --> IAuditLog
    CategoryService --> BankContext
    CategoryService --> InterestService
    InterestService ..> Account : начисляет проценты
```

## Интерфейс (`SberProject`, MVVM)

```mermaid
classDiagram
    direction LR

    class DialogViewModel {
        <<abstract>>
        +event CloseRequested
        +string Title
        +CancelCommand
    }
    class ValidatableViewModel {
        <<abstract>>
        +string? ErrorSummary
        +GetErrors(property)
        #TryExecute(action, errorField) bool
    }
    class MainViewModel {
        +ObservableCollection~AccountRow~ Accounts
        +string SearchText
        +CategoryOption SelectedCategoryOption
        +AccountSortField SelectedSortField
        +OpenAccountCommand
        +DepositCommand
        +WithdrawCommand
        +CloseAccountCommand
        +ExportCommand
        +AccrueInterestIfNewDay() bool
    }
    class AccountEditViewModel
    class OperationViewModel
    class CategoriesViewModel
    class HistoryViewModel
    class LogViewModel
    class IViewModelFactory {
        <<interface>>
        +CreateAccountEditor(account)
        +CreateOperation(account, type)
        +CreateHistory(account)
        +CreateCategories()
        +CreateLog()
    }
    class IDialogService {
        <<interface>>
        +ShowDialog(viewModel) bool
        +ShowInfo(message)
        +Confirm(message) bool
    }
    class DialogService {
        -Register~TViewModel, TWindow~()
    }
    class EnumDescriptionConverter

    DialogViewModel <|-- ValidatableViewModel
    ValidatableViewModel <|-- AccountEditViewModel
    ValidatableViewModel <|-- OperationViewModel
    ValidatableViewModel <|-- CategoriesViewModel
    DialogViewModel <|-- HistoryViewModel
    DialogViewModel <|-- LogViewModel
    DialogService ..|> IDialogService

    MainViewModel --> IDialogService
    MainViewModel --> IViewModelFactory
    MainViewModel --> IAccountService
    MainViewModel --> ICategoryService
    IViewModelFactory ..> AccountEditViewModel : создает
    IViewModelFactory ..> OperationViewModel : создает
    IViewModelFactory ..> CategoriesViewModel : создает
    IViewModelFactory ..> HistoryViewModel : создает
    IViewModelFactory ..> LogViewModel : создает
```

Окна (`MainWindow`, `AccountEditWindow`, `OperationWindow`, `CategoriesWindow`, `HistoryWindow`, `LogWindow`) связаны со своими моделями представления только через `DataContext` и привязки. `DialogService` находит окно по типу модели представления в своем реестре. Перечисления в окнах показываются через `EnumDescriptionConverter`. Все зависимости собираются в `App.ConfigureServices` (DI-контейнер).
