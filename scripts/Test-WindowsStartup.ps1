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
    if ($appProcess.MainWindowTitle -notlike '*ClickDesk*Mouse Recorder*') { throw 'Unexpected main window title.' }
    Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class ClickDeskWindowCaptions {
    private delegate bool Visitor(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, Visitor visitor, IntPtr parameter);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int length);
    public static string Read(IntPtr parent) {
        var captions = new List<string>();
        EnumChildWindows(parent, (window, unused) => {
            var text = new StringBuilder(4096);
            if (GetWindowText(window, text, text.Capacity) > 0) captions.Add(text.ToString());
            return true;
        }, IntPtr.Zero);
        return string.Join("\n", captions);
    }
}
'@
    $captionText = [ClickDeskWindowCaptions]::Read($appProcess.MainWindowHandle)
    foreach ($expected in @('Record', 'Replay', 'Playback settings', 'Click timeline')) {
        if ($captionText -notmatch [regex]::Escape($expected)) { throw "Missing English caption: $expected" }
    }
    if ($captionText -match '[\u3400-\u9fff]') { throw 'Non-English interface text was found.' }
    Write-Host 'PASS: Main window and controls use English captions.'
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
