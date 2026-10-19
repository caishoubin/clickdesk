ClickDesk records mouse click positions, order, and timing, then replays the sequence at your chosen pace.

New in v1.1.0: an English desktop interface, English messages and quick-start guide, and an English product website with an interactive demo.

## Download and run

Download **ClickDesk-Windows-x64.zip**, extract it, and run **ClickDesk.exe**. Supports Windows 10 / 11 x64. The .NET runtime is included, so no separate installation is needed.

The product website, desktop interface, settings, messages, test pad, and bundled quick-start guide are in English.

## Features

- Left, right, and middle clicks, preserving double-click timing and button hold duration.
- **F6** to start / finish recording, **F7** to start / stop playback, and **F8** to stop anytime.
- Repeat counts, continuous loops, playback speed, an extra pause between rounds, and a start countdown.
- Local recording files, a click timeline, a tray menu, and a built-in click test pad.

## Verification and compatibility

The release workflow runs 12 core regression tests on Windows and verifies that the portable app starts, creates its main window, and exits cleanly. Actual global input, monitor layouts, and different scaling settings still need manual verification.

Playback uses absolute screen coordinates. Keep the target window position, monitor arrangement, resolution, and scaling unchanged. This version does not recognize screen content or game states, navigate, record keyboard input or scrolling, or reproduce continuous movement paths.

Compatibility with games is not verified. Games may restrict automation or ignore simulated mouse input; check their rules before use.

The release includes the source ZIP and SHA-256 checksums.
