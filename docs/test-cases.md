# Тест-кейсы

Для каждого функционального требования: шаги ручной проверки в приложении и автоматический тест (`tests/SberProject.Tests`, запуск `dotnet test`). Колонку «Фактический результат» заполнить после ручной проверки и приложить скриншоты в `docs/screenshots/`.

Автотесты: **82 теста, все проходят** (14.10.2026): ядро и модели представления (`ViewModelTests`). Скриншоты окон для колонки «Фактический результат» снимает `tools/ui-smoke.ps1 -OutDir docs/screenshots`.

| № | Требование | Шаги проверки | Ожидаемый результат | Автотест | Фактический результат |
|---|---|---|---|---|---|
| ТК-01 | ФТ-01 Открытие вклада | «Открыть вклад» → заполнить паспортные данные → категория «Накопительный», сумма 500 → «Сохранить» | Вклад появился в списке, сумма 500,00, в истории операция «Открытие вклада» | `AccountServiceTests.Open_CreatesAccountWithUnique20DigitNumbers` | |
| ТК-02 | ФТ-02 Номер счета | Открыть два вклада подряд | Номера из 20 цифр, разные, идут по порядку | `AccountServiceTests.Open_CreatesAccountWithUnique20DigitNumbers` | |
| ТК-03 | ФТ-03 Проверка паспорта | В окне открытия ввести паспорт `12345`, личный номер `123`, дату рождения в будущем → «Сохранить» | Окно не закрывается, поля подсвечены красным, внизу список ошибок | `PassportValidatorTests.*`, `AccountServiceTests.Open_WithInvalidPassport_ThrowsWithFieldErrors` | |
| ТК-04 | ФТ-04 Список вкладов | Запустить программу | Таблица: номер счета, ФИО, паспорт, категория, сумма, проценты, дата последней операции, статус | — (ручная проверка) | |
| ТК-05 | ФТ-05 Поиск | Ввести в поиск «петр», затем часть номера паспорта, затем часть номера счета | Остаются только подходящие вклады | `AccountServiceTests.Find_FiltersByTextAndCategoryAndSorts` | |
| ТК-06 | ФТ-06 Фильтр и сортировка | Категория «Накопительный»; сортировка «по сумме вклада» + «по убыванию» | Только накопительные вклады, по убыванию суммы | `AccountServiceTests.Find_FiltersByTextAndCategoryAndSorts` | |
| ТК-07 | ФТ-07 Изменение данных | Выбрать вклад → «Данные вкладчика» → изменить фамилию и паспорт → «Сохранить» | Новые данные в таблице и после перезапуска | `AccountServiceTests.UpdateDepositor_ChangesPassportData` | |
| ТК-08 | ФТ-08, ФТ-11 Прием | Выбрать вклад с остатком 500 → «Прием» → 150,25 → «Принять» | Остаток 650,25, дата последней операции — сейчас, в истории «Прием +150,25» | `AccountTests.Deposit_IncreasesBalanceAndUpdatesLastOperation` | |
| ТК-09 | ФТ-09 Выдача сверх остатка | «Выдача» → 1000 при остатке 650,25 | Операция отклонена: «Недостаточно средств…», остаток не изменился | `AccountTests.Withdraw_MoreThanBalance_Throws`, `AccountServiceTests.Withdraw_ExceedingBalance_IsRejected` | |
| ТК-10 | ФТ-09 Выдача любой суммы | «Выдача» → «Вся сумма» → «Выдать» | Остаток 0,00 | `AccountTests.Withdraw_AnyAmountUpToBalance_IsAllowed` | |
| ТК-11 | ФТ-10 Некорректная сумма | Ввести `-5`, `0`, `abc`, `1,005` | Сообщение об ошибке у поля, операция не выполнена | `AccountTests.Operations_WithInvalidAmount_Throw`, `StorageAndExportTests.Money_RejectsInvalidInput` | |
| ТК-12 | ФТ-12 Начисление процентов | Автотест: остаток 1000, ставка 12 %, прошел январь (31 день) | Капитализировано 10,19 последним днем месяца, остаток 1010,19 | `InterestServiceTests.FullMonth_IsCapitalizedOnLastDay` и др. | |
| ТК-13 | ФТ-13 Досчет при запуске | Автотест: «перемотать» время на 3 месяца → начисление | Три операции начисления процентов, сложный процент по месяцам | `InterestServiceTests.MissedMonths_AreCaughtUpWithMonthlyCompounding`, `AccountServiceTests.AccrueInterestForAll_CatchesUpMissedMonths` | |
| ТК-14 | ФТ-14 История | Выбрать вклад → «История» (или двойной щелчок) | Все операции по счету по времени, суммы со знаком, остаток после каждой | `AccountServiceTests.Close_PaysOutBalanceWithInterest` | |
| ТК-15 | ФТ-15 Закрытие | «Закрыть вклад» → подтвердить | Сообщение о выданной сумме, вклад скрыт из списка (виден с «показывать закрытые», серым), операции недоступны | `AccountTests.Close_PaysOutBalanceAndBlocksFurtherOperations`, `AccountServiceTests.Close_PaysOutBalanceWithInterest` | |
| ТК-16 | ФТ-16 Категории | «Категории вкладов» → выбрать «Пенсионный» → ставка 11,25, снять «Принимать новые вклады» → «Сохранить» → «Открыть вклад» | Новые параметры сохранены; «Пенсионный» отсутствует в списке категорий при открытии; по открытым пенсионным вкладам прием и выдача работают | `CategoryServiceTests.*`, `ViewModelTests.Categories_*`, `ViewModelTests.AccountEditor_OffersOnlyCategoriesOpenForNewDeposits` | |
| ТК-17 | ФТ-17 Сохранение и загрузка | Открыть вклад → закрыть программу → запустить снова | Вклад на месте | `StorageAndExportTests.BankContext_PersistsAllCollections`, `Account_RoundTripsThroughJson` | |
| ТК-18 | ФТ-18 Поврежденный файл | Закрыть программу → записать мусор в `data/accounts.json` → запустить | Сообщение об ошибке с предложением начать с пустым файлом; при «Да» программа открывается, старый файл сохранен как `.corrupt-…` | `StorageAndExportTests.CorruptedFile_ThrowsStorageException_AndResetMovesItAside` | |
| ТК-19 | ФТ-19 Журнал | Выполнить несколько операций → «Журнал» | Записи с временем, пользователем и действием, новые сверху | — (ручная проверка) | |
| ТК-20 | ФТ-20 Экспорт CSV | «Экспорт в CSV» → сохранить → открыть в Excel | Таблица с заголовками, суммы читаются как числа | `StorageAndExportTests.Csv_HasHeaderAndEscapesSeparators` | |
