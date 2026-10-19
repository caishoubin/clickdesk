# ClickDesk · Windows Mouse Recorder

Record click positions, order, and timing, then replay the same sequence. A portable desktop tool for Windows 10 / 11 x64.

[Product website](https://clickdesk.still-moose-7651.chatgpt.site/) · [Download the latest release](https://github.com/caishoubin/clickdesk/releases/latest) · [中文说明](README.zh-CN.md)

The website, desktop interface, and bundled quick-start guide are in English.

## Quick start

1. Download `ClickDesk-Windows-x64.zip`, extract it, and run `ClickDesk.exe`. The runtime is included; no Python or .NET installation is required.
2. Put your target window in position. Press **F6** and wait for the start countdown.
3. Perform the clicks once. Press **F6** or **F8** to finish recording.
4. Set a repeat count and keep the speed at **1.00** for the original rhythm. Press **F7** to replay.
5. Press **F8** anytime to cancel the countdown, stop recording, or stop playback.

The app includes a click test pad with A, B, and C buttons. Use it to try a short recording before automating another application. Global shortcuts continue working while ClickDesk is minimized.

## Features

- Left, right, and middle clicks, including holds and double-click timing.
- Fixed repeat counts or continuous loops, with playback speeds from 0.1× to 10×.
- A start countdown and an optional extra pause between rounds.
- A timeline of click positions, order, intervals, and hold duration.
- Local `.clickdesk.json` recording files, a tray menu, and a built-in test pad.

The time before the first click and after the final click is preserved in each round. Playback uses a monotonic clock and absolute event scheduling; actual timing depends on Windows scheduling and target application responsiveness.

## Compatibility

Playback uses absolute screen coordinates. Keep the target window position, monitor arrangement, resolution, and display scaling unchanged. The tool records clicks, not keyboard input, scrolling, or continuous mouse movement paths. It does not recognize screen content or follow windows automatically.

Compatibility with games is not verified. Some games restrict automation or ignore simulated input. Check their rules before use. If the target application runs as administrator, ClickDesk needs the same permissions.

Recordings stay local. The desktop tool has no recording upload feature or account requirement.

## Build and verification

Requires the .NET 10 SDK:

```powershell
dotnet run --project tests/ClickDesk.Core.Tests -c Release
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

GitHub Actions runs 12 core regression tests and a Windows startup / clean shutdown check. Actual desktop input still needs manual verification. Pushing a version tag matching the application version publishes the portable ZIP, source ZIP, and SHA-256 checksums.
