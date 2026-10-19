# SWITCHCAST — AGENT HANDOFF RECORD

---

## Task Details
- **Task**: Mandatory Dual-Player Isolation Guards (Video MediaPlayer vs Background Music MediaPlayer) & Regression Test Matrix
- **Date**: 2026-10-19T21:07:00+08:00 (UTC+8)
- **Status**: Completed, Validated & Ready for Local Commit (411 Tests Passing, 0 Build Warnings/Errors, Strict Dual-Player Coexistence Verified)

---

## 1. Objectives & Implementation Details

1. **Strict Service Isolation**:
   - `MediaPresentationService` exclusively owns its dedicated `MediaPlayer` for presentation output video.
   - `BackgroundMusicService` exclusively owns its dedicated `MediaPlayer` for background music.
   - Neither service shares `MediaPlayer` instances, `MediaSource` instances, mutable state, or disposal logic.
   - `BackgroundMusicService` never calls `MediaPresentationService.StopVideoAsync`, `ClearVideoInternal`, or `PlayVideoAsync`.

2. **Command Ownership**:
   - Video transport commands exclusively control `MediaPresentationService`:
     - `ToggleVideoPlaybackCommand`, `RestartVideoCommand`, `SeekBackward10Command`, `SeekForward10Command`, `ToggleMediaMuteCommand`, `StartScrubbing`, `CompleteScrubbing`.
   - Music transport commands exclusively control `BackgroundMusicService`:
     - `ToggleBackgroundMusicPlayPauseCommand`, `NextBackgroundMusicTrackCommand`, `PreviousBackgroundMusicTrackCommand`, `ToggleBackgroundMusicShuffleCommand`, `CycleBackgroundMusicRepeatModeCommand`, `ToggleBackgroundMusicMuteCommand`, `StartMusicScrubbing`, `CompleteMusicScrubbing`.
   - Source switching commands (`NextSourceCommand`, `PreviousSourceCommand`, `SwitchSourceCommand`) and PowerPoint slide commands (`PowerPointNextSlideCommand`, `PowerPointPreviousSlideCommand`) remain strictly isolated from music track transport.

3. **Independent State and Settings**:
   - Video state: `VideoPositionSeconds`, `VideoDurationSeconds`, `VideoPositionText`, `MediaVolume`, `IsMediaMuted`, `IsVideoPlaying`, `IsVideoPaused`, `IsVideoEnded`.
   - Music state: `BackgroundMusicPositionSeconds`, `BackgroundMusicDurationSeconds`, `BackgroundMusicPositionText`, `BackgroundMusicVolume`, `IsBackgroundMusicMuted`, `IsBackgroundMusicPlaying`, `BackgroundMusicTrackTitle`, `BackgroundMusicPlaylistName`, `BackgroundMusicRepeatMode`, `IsBackgroundMusicShuffleEnabled`.
   - Settings isolation: Added `BackgroundMusicVolume` and `IsBackgroundMusicMuted` to `UserSettings.cs`. Added explicit helper methods `SetMediaVolumeAsync`, `SetMediaMutedAsync`, `SetBackgroundMusicVolumeAsync`, and `SetBackgroundMusicMutedAsync` to `IApplicationSettingsService` and `ApplicationSettingsService`.

4. **Independent Playback Lifecycle**:
   - Source transitions (`Window -> Video -> PowerPoint -> Camera -> Video`) do not stop, recreate, or reset background music.
   - Music track changes do not alter On Air presentation source.
   - Pausing video does not pause music; pausing music does not pause video.
   - Video ended does not advance music playlist; music ended does not advance presentation sources.
   - The only intentional coordination is the explicit `StopPresentationAsync` session-stop policy in `PresentationCoordinator.ExecuteStopPresentationAsync`, which stops both video and background music when a presentation session concludes.

5. **Player Lifetime Safety**:
   - `MediaPresentationService.Dispose()` and `BackgroundMusicService.Dispose()` are completely independent.
   - No shared `MediaSource`, no stale callbacks, no unobserved playback tasks, and no double disposal.

6. **Audio Safeguards**:
   - Both players play through the default Windows audio output simultaneously without master system volume mutation.
   - Per-instance volume and mute controls; no automatic ducking or mutual muting.

7. **SwitchDeck UI Isolation**:
   - Music controls live in a dedicated contextual popup available across all sources.
   - Source-specific secondary rows (Video playback controls, PowerPoint slide controls) remain completely intact and functional.

8. **Mandatory 16-Test Regression Matrix**:
   - Implemented in `SwitchCast.Tests/Services/DualPlayerIsolationTests.cs`:
     - `Test01`: Play Video & Music simultaneously -> Pause Video -> Music continues.
     - `Test02`: Play Video & Music simultaneously -> Pause Music -> Video continues.
     - `Test03`: Change Music volume -> Video volume remains unchanged.
     - `Test04`: Mute Video -> Music remains audible / unmuted.
     - `Test05`: Mute Music -> Video remains audible / unmuted.
     - `Test06`: Seek Video -> Music position remains unchanged.
     - `Test07`: Seek Music -> Video position remains unchanged.
     - `Test08`: Video reaches end -> Music playlist and track remain unaffected.
     - `Test09`: Music reaches end -> Music advances according to repeat mode -> Video unaffected.
     - `Test10`: Switch Video -> PowerPoint -> Camera -> Video -> Music continues without restarting.
     - `Test11`: Change music playlist while Video is live -> On Air source remains unchanged.
     - `Test12`: Click SwitchDeck Next Source -> Music track does not change.
     - `Test13`: Click SwitchDeck Next Music Track -> Presentation source does not change.
     - `Test14`: Stop Presenting -> Both player sessions stop per explicit session-stop policy -> MainWindow remains alive.
     - `Test15`: Close/hide SwitchDeck non-destructively -> Music and presentation continue uninterrupted.
     - `Test16`: Rapidly switch sources while changing tracks -> No deadlock, player disposal race, or unhandled exceptions.

---

## 2. Verification Performed

- **Automated Tests**: **411 passing tests (0 failures, 0 skipped)** across all test fixtures.
- **Compiler Gates**:
  - `dotnet build SwitchCast.csproj -c Debug -p:Platform=x64 --no-incremental` -> 0 Errors, 0 Warnings.
  - `dotnet build SwitchCast.csproj -c Release -p:Platform=x64 --no-incremental` -> 0 Errors, 0 Warnings.
  - `dotnet test SwitchCast.Tests/SwitchCast.Tests.csproj -c Debug` -> 411 Passed, 0 Failed, 0 Skipped.

---

## 3. Important Files Modified

- `SwitchCast/Models/UserSettings.cs`: Added `BackgroundMusicVolume` and `IsBackgroundMusicMuted`.
- `SwitchCast/Services/IApplicationSettingsService.cs`: Added `SetMediaVolumeAsync`, `SetMediaMutedAsync`, `SetBackgroundMusicVolumeAsync`, and `SetBackgroundMusicMutedAsync`.
- `SwitchCast/Services/ApplicationSettingsService.cs`: Implemented isolated media and background music volume/mute persistence methods.
- `SwitchCast/Services/Media/MediaPresentationService.cs`: Cleanly wired settings updates.
- `SwitchCast.Tests/Services/DualPlayerIsolationTests.cs` *(New)*: Complete 16-test mandatory regression matrix verifying player independence, command separation, settings isolation, and concurrency safety.

---

## 4. Next Steps for Next Agent

1. Maintain dual-player isolation in any future audio ducking or master transport feature requests.
2. Ensure new presentation source types (e.g. browser, PDF) respect the non-interference rule for `BackgroundMusicService`.
3. Keep the 0-error and 0-warning compiler gate intact across Debug and Release x64 builds.
