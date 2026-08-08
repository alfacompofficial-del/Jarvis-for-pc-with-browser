param($appName)

# Быстрый поиск приложения
$locations = @(
    "$env:LOCALAPPDATA\Programs",
    "$env:APPDATA",
    "$env:LOCALAPPDATA",
    "$env:ProgramFiles",
    "${env:ProgramFiles(x86)}",
    "$env:LOCALAPPDATA\Microsoft\WindowsApps"
)

foreach ($loc in $locations) {
    if (Test-Path $loc) {
        $found = Get-ChildItem -Path $loc -Filter "*$appName*.exe" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($found) {
            Write-Output $found.FullName
            exit 0
        }
    }
}

# Проверяем запущенные процессы
$proc = Get-Process | Where-Object { $_.ProcessName -like "*$appName*" } | Select-Object -First 1
if ($proc) {
    Write-Output $proc.Path
    exit 0
}

exit 1
