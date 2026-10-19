param(
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$resolvedPath = (Resolve-Path $ExecutablePath).Path
$appProcess = Start-Process -FilePath $resolvedPath -PassThru
try {
    if (-not $appProcess.WaitForInputIdle(20000)) { throw 'Application did not reach its message loop.' }
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        $appProcess.Refresh()
        if ($appProcess.HasExited) { throw "Application exited early: $($appProcess.ExitCode)." }
        if ($appProcess.MainWindowHandle -ne [IntPtr]::Zero) { break }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($appProcess.MainWindowHandle -eq [IntPtr]::Zero) { throw 'Main window was not created.' }
    if ($appProcess.MainWindowTitle -notlike '*ClickDesk*') { throw 'Unexpected main window title.' }
    if (-not $appProcess.CloseMainWindow()) { throw 'Main window did not accept close request.' }
    if (-not $appProcess.WaitForExit(10000)) { throw 'Application did not shut down cleanly.' }
    if ($appProcess.ExitCode -ne 0) { throw "Application exit code: $($appProcess.ExitCode)." }
    Write-Host 'PASS: Windows self-contained application starts, creates its window, and exits cleanly.'
}
finally {
    $appProcess.Refresh()
    if (-not $appProcess.HasExited) { Stop-Process -Id $appProcess.Id -Force }
    $appProcess.Dispose()
}
