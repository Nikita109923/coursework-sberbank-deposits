<#
.SYNOPSIS
    Дымовая проверка интерфейса: собирает приложение во временную папку, запускает его
    с чистыми данными, по очереди открывает все окна через UI Automation и сохраняет скриншоты.

.DESCRIPTION
    Нужна Windows PowerShell 5.1 или PowerShell 7 на Windows. Файл сохранен в UTF-8 с BOM:
    без BOM Windows PowerShell 5.1 читает кириллицу в ANSI и скрипт не разбирается.
    Скрипт не трогает данные в bin\Debug — приложение запускается из отдельной папки.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\ui-smoke.ps1
    powershell -ExecutionPolicy Bypass -File tools\ui-smoke.ps1 -OutDir docs\screenshots
#>
param(
    [string]$OutDir = (Join-Path $env:TEMP "SberProject-ui-smoke\screenshots")
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$appDir = Join-Path $env:TEMP "SberProject-ui-smoke\app"

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class SmokeWin {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
}
"@
[SmokeWin]::SetProcessDPIAware() | Out-Null

$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
$CT = [System.Windows.Automation.ControlType]

function Save-Screenshot($element, [string]$name) {
    $handle = [IntPtr]$element.Current.NativeWindowHandle
    [SmokeWin]::SetForegroundWindow($handle) | Out-Null
    Start-Sleep -Milliseconds 1200
    $r = New-Object SmokeWin+RECT
    [SmokeWin]::GetWindowRect($handle, [ref]$r) | Out-Null
    $bitmap = New-Object System.Drawing.Bitmap ($r.Right - $r.Left), ($r.Bottom - $r.Top)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CopyFromScreen($r.Left, $r.Top, 0, 0, $bitmap.Size)
    $path = Join-Path $OutDir "$name.png"
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose(); $bitmap.Dispose()
    Write-Host "  скриншот: $path"
}

function Find-ByName($root, [string]$name) {
    $condition = New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $name)
    $element = $root.FindFirst($TS::Descendants, $condition)
    if (-not $element) { throw "Не найден элемент «$name»" }
    return $element
}

function Find-ByType($root, $controlType) {
    $condition = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $controlType)
    return $root.FindFirst($TS::Descendants, $condition)
}

function Invoke-Button($root, [string]$name) {
    (Find-ByName $root $name).GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
}

function Wait-Dialog($main) {
    $condition = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $CT::Window)
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Milliseconds 300
        $dialog = $main.FindFirst($TS::Children, $condition)
        if ($dialog) { return $dialog }
    }
    throw "Окно не открылось"
}

function Close-Window($window) {
    $window.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern).Close()
    Start-Sleep -Milliseconds 500
}

function Set-Text($root, [string]$text) {
    (Find-ByType $root $CT::Edit).GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($text)
}

Write-Host "Сборка в $appDir"
Remove-Item -Recurse -Force $appDir -ErrorAction SilentlyContinue
dotnet build (Join-Path $repoRoot "src\SberProject") -o $appDir -v q -nologo | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Сборка не удалась" }
New-Item -ItemType Directory -Force $OutDir | Out-Null

$process = Start-Process (Join-Path $appDir "SberProject.exe") -PassThru
try {
    Start-Sleep -Seconds 4
    $condition = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $process.Id)
    $main = $AE::RootElement.FindFirst($TS::Children, $condition)
    Write-Host "Главное окно: $($main.Current.Name)"
    Save-Screenshot $main "01-main"

    Write-Host "Открытие вклада с пустой формой"
    Invoke-Button $main "Открыть вклад"
    $dialog = Wait-Dialog $main
    Save-Screenshot $dialog "02-open-account"
    Invoke-Button $dialog "Сохранить"
    Save-Screenshot $dialog "03-open-account-errors"
    Close-Window $dialog

    $row = Find-ByType $main $CT::DataItem
    $row.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()

    Write-Host "Прием 150,25"
    Invoke-Button $main "Прием"
    $dialog = Wait-Dialog $main
    Set-Text $dialog "150,25"
    Save-Screenshot $dialog "04-deposit"
    Invoke-Button $dialog "Принять"
    $info = Wait-Dialog $main
    Save-Screenshot $info "05-deposit-done"
    Close-Window $info

    Write-Host "Выдача больше остатка"
    Invoke-Button $main "Выдача"
    $dialog = Wait-Dialog $main
    Set-Text $dialog "1000000"
    Invoke-Button $dialog "Выдать"
    Save-Screenshot $dialog "06-withdraw-insufficient"
    Close-Window $dialog

    foreach ($step in @(
            @{ Button = "История"; Name = "07-history" },
            @{ Button = "Данные вкладчика"; Name = "08-edit-depositor" },
            @{ Button = "Категории вкладов"; Name = "09-categories" },
            @{ Button = "Журнал"; Name = "10-log" })) {
        Write-Host $step.Button
        Invoke-Button $main $step.Button
        $dialog = Wait-Dialog $main
        Save-Screenshot $dialog $step.Name
        Close-Window $dialog
    }

    Save-Screenshot $main "11-main-after"
    $errors = Get-Content -Encoding utf8 (Join-Path $appDir "logs\app.log") | Select-String "Непредвиденная ошибка"
    if ($errors) { throw "В журнале есть непредвиденные ошибки:`n$errors" }
    Write-Host "Готово: все окна открылись без ошибок."
}
finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id }
}
