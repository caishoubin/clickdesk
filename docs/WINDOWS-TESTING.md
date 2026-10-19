# Windows manual verification

Core tests cover sequence order, click timing, holds, cancellation, button release,
loop waits, speed conversion, recording validation, and multi-monitor coordinate
normalization. GitHub Actions also checks Windows startup, English control
captions, and clean shutdown. The desktop interactions below still need manual checks.

## Basic workflow

1. On a Windows x64 computer without .NET installed, run the published ClickDesk.exe.
2. Open the click test pad and verify its A / B / C buttons, counts, and event log.
3. Press F6. After the countdown, the main window should minimize while the pad stays visible.
4. Click A, B, and C with roughly 0.5 and 1 second gaps, then press F6.
5. Confirm 3 clicks, correct coordinates, increasing timestamps, and non-negative hold times.
6. Set one round at 1.00 speed. Press F7. Confirm the counts increase by one in the recorded order and rhythm.
7. Repeat for right / middle clicks, double-clicks, and holds.

## Stop and lifecycle

- Press F8 during either countdown; no recording or playback should continue.
- Stop a replay during a hold; the simulated mouse button must not stay pressed.
- Stop continuous playback with F7 / F8, then confirm a new session works.
- Stop through the tray menu while minimized; the main window should return.
- Close during playback; the app should stop before exiting, with a save prompt if needed.
- Stop recording with the control window button; this control click must not enter the timeline.
- Start a second instance; it should report the F8 conflict and block starting.

## Timing and files

- Run three rounds with an extra 1 second pause; only inter-round waits should gain the extra pause.
- Try 0.50 and 2.00 speed; intervals should be roughly doubled and halved.
- Save, close, reopen, and load a recording; contents should match and loading must not start playback.
- Reject malformed JSON, unmatched events, invalid positions, and files over 20 MB while keeping the current recording.
- Verify Save / Discard / Cancel behavior when clearing, replacing, or closing unsaved work.
- Open a v1.0.0 recording in v1.1.0 and confirm it still replays in the same order.

## Display and permissions

- Check 100%, 150%, and 200% scaling; settings remain scrollable and controls accessible.
- Test secondary displays to the left / above the main display; negative coordinates must replay correctly.
- Remove a display containing recorded positions; out-of-bounds playback should be blocked.
- Change the virtual desktop bounds; the layout warning should appear before replay.
- Test ordinary and elevated target apps with matching permissions; rejected input should show an English error.
