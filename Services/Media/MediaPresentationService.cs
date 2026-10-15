using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SwitchCast.Models;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace SwitchCast.Services.Media;

/// <summary>
/// Authoritative implementation of direct image rendering and video playback for SwitchCast Presentation Output.
/// </summary>
public sealed partial class MediaPresentationService : ObservableObject, IMediaPresentationService
{
    private DispatcherQueue? _dispatcherQueue;
    private IUIThreadDispatcher? _dispatcher;
    private readonly IApplicationSettingsService? _settingsService;
    private MediaPlayer? _player;
    private MediaSource? _currentMediaSource;
    private bool _disposed;

    [ObservableProperty]
    private ImageSource? _directImageSource;

    [ObservableProperty]
    private MediaFileSource? _activeMediaSource;

    [ObservableProperty]
    private bool _isVideoPlaying;

    [ObservableProperty]
    private bool _isVideoPaused;

    [ObservableProperty]
    private bool _isVideoEnded;

    [ObservableProperty]
    private bool _isLooping;

    [ObservableProperty]
    private double _volume = 1.0;

    [ObservableProperty]
    private bool _isMuted = false;

    [ObservableProperty]
    private TimeSpan _position = TimeSpan.Zero;

    [ObservableProperty]
    private TimeSpan _duration = TimeSpan.Zero;

    [ObservableProperty]
    private string? _lastErrorMessage;

    public event EventHandler? MediaStateChanged;

    public MediaPresentationService(IApplicationSettingsService? settingsService = null, IUIThreadDispatcher? uiDispatcher = null)
    {
        _settingsService = settingsService;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _dispatcher = UIThreadDispatcherProvider.Resolve(uiDispatcher);

        if (_settingsService is not null)
        {
            _volume = Math.Clamp(_settingsService.CurrentSettings.MediaVolume, 0.0, 1.0);
            _isMuted = _settingsService.CurrentSettings.IsMediaMuted;
        }

        InitializePlayer();
    }

    /// <summary>
    /// Configures the authoritative UI thread DispatcherQueue for media state and image rendering dispatch.
    /// </summary>
    public void SetDispatcherQueue(DispatcherQueue? dispatcherQueue)
    {
        if (_disposed)
        {
            return;
        }

        _dispatcherQueue = dispatcherQueue;
        _dispatcher = dispatcherQueue is not null ? new DispatcherQueueAdapter(dispatcherQueue) : null;
    }

    /// <summary>
    /// Configures a testable UI thread dispatcher for this service.
    /// </summary>
    public void SetUIThreadDispatcher(IUIThreadDispatcher? dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public MediaPlayer? Player => _player;

    private void InitializePlayer()
    {
        if (_player is not null)
        {
            return;
        }

        try
        {
            _player = new MediaPlayer
            {
                IsMuted = IsMuted,
                Volume = Math.Clamp(Volume, 0.0, 1.0),
                AutoPlay = true,
                IsLoopingEnabled = false
            };

            _player.MediaOpened += OnPlayerMediaOpened;
            _player.MediaEnded += OnPlayerMediaEnded;
            _player.MediaFailed += OnPlayerMediaFailed;
            _player.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;
            _player.PlaybackSession.PositionChanged += OnPositionChanged;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MediaPresentationService] MediaPlayer initialization error: {ex.Message}");
            LastErrorMessage = $"Failed to initialize media player: {ex.Message}";
        }
    }

    public Task LoadImageAsync(ImageMediaSource imageSource)
    {
        ArgumentNullException.ThrowIfNull(imageSource);

        ClearVideoInternal();

        ActiveMediaSource = imageSource;
        LastErrorMessage = null;

        RunOnUIThread(() =>
        {
            try
            {
                if (!File.Exists(imageSource.FilePath))
                {
                    LastErrorMessage = $"Image file not found: {imageSource.FilePath}";
                    DirectImageSource = null;
                    return;
                }

                var bitmapImage = new BitmapImage(new Uri(imageSource.FilePath));
                DirectImageSource = bitmapImage;
                MediaStateChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                LastErrorMessage = $"Failed to load image (0x{ex.HResult:X8}): {ex.Message}";
                DirectImageSource = null;
                Diagnostics.SwitchCastDiagnostics.LogException(nameof(MediaPresentationService), nameof(LoadImageAsync), ex, imageSource.Id);
            }
        });

        return Task.CompletedTask;
    }

    public void ClearImage()
    {
        RunOnUIThread(() =>
        {
            DirectImageSource = null;
            if (ActiveMediaSource is ImageMediaSource)
            {
                ActiveMediaSource = null;
            }
            MediaStateChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    public Task PlayVideoAsync(VideoMediaSource videoSource)
    {
        ArgumentNullException.ThrowIfNull(videoSource);

        ClearImageInternal();

        ActiveMediaSource = videoSource;
        IsLooping = videoSource.IsLooping;
        LastErrorMessage = null;
        IsVideoEnded = false;

        if (!File.Exists(videoSource.FilePath))
        {
            LastErrorMessage = $"Video file not found: {videoSource.FilePath}";
            return Task.CompletedTask;
        }

        try
        {
            InitializePlayer();
            if (_player is null)
            {
                LastErrorMessage = "Media player is not available.";
                return Task.CompletedTask;
            }

            _currentMediaSource?.Dispose();
            _currentMediaSource = MediaSource.CreateFromUri(new Uri(videoSource.FilePath));

            _player.IsLoopingEnabled = IsLooping;
            _player.Source = _currentMediaSource;
            _player.Play();

            IsVideoPlaying = true;
            IsVideoPaused = false;
        }
        catch (Exception ex)
        {
            LastErrorMessage = $"Failed to play video (0x{ex.HResult:X8}): {ex.Message}";
            Diagnostics.SwitchCastDiagnostics.LogException(nameof(MediaPresentationService), nameof(PlayVideoAsync), ex, videoSource.Id);
            return Task.CompletedTask;
        }

        // Notify subscribers outside the player playback try/catch block so subscriber UI notification failures
        // are never falsely reported as video decoder/playback errors.
        try
        {
            MediaStateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Diagnostics.SwitchCastDiagnostics.LogException(nameof(MediaPresentationService), "MediaStateChanged", ex);
        }

        return Task.CompletedTask;
    }

    public Task StopVideoAsync()
    {
        ClearVideoInternal();
        return Task.CompletedTask;
    }

    private void ClearVideoInternal()
    {
        RunOnUIThread(() =>
        {
            try
            {
                if (_player is not null)
                {
                    _player.Pause();
                    _player.Source = null;
                }

                _currentMediaSource?.Dispose();
                _currentMediaSource = null;

                IsVideoPlaying = false;
                IsVideoPaused = false;
                IsVideoEnded = false;
                Position = TimeSpan.Zero;
                Duration = TimeSpan.Zero;

                if (ActiveMediaSource is VideoMediaSource)
                {
                    ActiveMediaSource = null;
                }

                MediaStateChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Diagnostics.SwitchCastDiagnostics.LogException(nameof(MediaPresentationService), nameof(ClearVideoInternal), ex);
            }
        });
    }

    private void ClearImageInternal()
    {
        RunOnUIThread(() =>
        {
            DirectImageSource = null;
            if (ActiveMediaSource is ImageMediaSource)
            {
                ActiveMediaSource = null;
            }
        });
    }

    public void PauseVideo()
    {
        try
        {
            _player?.Pause();
            IsVideoPaused = true;
            IsVideoPlaying = false;
            MediaStateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MediaPresentationService] PauseVideo error: {ex.Message}");
        }
    }

    public void ResumeVideo()
    {
        try
        {
            if (IsVideoEnded)
            {
                RestartVideo();
                return;
            }

            _player?.Play();
            IsVideoPlaying = true;
            IsVideoPaused = false;
            MediaStateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MediaPresentationService] ResumeVideo error: {ex.Message}");
        }
    }

    public void RestartVideo()
    {
        try
        {
            if (_player is not null)
            {
                _player.PlaybackSession.Position = TimeSpan.Zero;
                _player.Play();
                IsVideoEnded = false;
                IsVideoPlaying = true;
                IsVideoPaused = false;
                MediaStateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MediaPresentationService] RestartVideo error: {ex.Message}");
        }
    }

    public void ToggleLoop()
    {
        SetLooping(!IsLooping);
    }

    public void SetLooping(bool isLooping)
    {
        IsLooping = isLooping;
        if (_player is not null)
        {
            _player.IsLoopingEnabled = isLooping;
        }
        MediaStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Seek(TimeSpan position)
    {
        try
        {
            if (_player is not null && position >= TimeSpan.Zero && position <= Duration)
            {
                _player.PlaybackSession.Position = position;
                Position = position;
                MediaStateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MediaPresentationService] Seek error: {ex.Message}");
        }
    }

    public void SetVolume(double volume)
    {
        var clampedVolume = Math.Clamp(volume, 0.0, 1.0);
        Volume = clampedVolume;

        if (_player is not null)
        {
            _player.Volume = clampedVolume;
        }

        if (_settingsService is not null)
        {
            _settingsService.CurrentSettings.MediaVolume = clampedVolume;
            _ = _settingsService.SaveSettingsAsync();
        }

        MediaStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetMuted(bool isMuted)
    {
        IsMuted = isMuted;

        if (_player is not null)
        {
            _player.IsMuted = isMuted;
        }

        if (_settingsService is not null)
        {
            _settingsService.CurrentSettings.IsMediaMuted = isMuted;
            _ = _settingsService.SaveSettingsAsync();
        }

        MediaStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ToggleMute()
    {
        SetMuted(!IsMuted);
    }

    private void OnPlayerMediaOpened(MediaPlayer sender, object args)
    {
        RunOnUIThread(() =>
        {
            try
            {
                Duration = sender.PlaybackSession.NaturalDuration;
                Position = sender.PlaybackSession.Position;
                IsVideoEnded = false;
                MediaStateChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MediaPresentationService] OnPlayerMediaOpened error: {ex.Message}");
            }
        });
    }

    private void OnPlayerMediaEnded(MediaPlayer sender, object args)
    {
        RunOnUIThread(() =>
        {
            if (!IsLooping)
            {
                IsVideoEnded = true;
                IsVideoPlaying = false;
                IsVideoPaused = false;
                MediaStateChanged?.Invoke(this, EventArgs.Empty);
            }
        });
    }

    private void OnPlayerMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        RunOnUIThread(() =>
        {
            LastErrorMessage = $"Video decoding failure: {args.ErrorMessage} ({args.Error})";
            IsVideoPlaying = false;
            IsVideoPaused = false;
            IsVideoEnded = true;
            Debug.WriteLine($"[MediaPresentationService] OnPlayerMediaFailed: {args.ErrorMessage} - {args.ExtendedErrorCode}");
            MediaStateChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private void OnPlaybackStateChanged(MediaPlaybackSession sender, object args)
    {
        RunOnUIThread(() =>
        {
            switch (sender.PlaybackState)
            {
                case MediaPlaybackState.Playing:
                    IsVideoPlaying = true;
                    IsVideoPaused = false;
                    IsVideoEnded = false;
                    break;
                case MediaPlaybackState.Paused:
                    IsVideoPlaying = false;
                    IsVideoPaused = true;
                    break;
                case MediaPlaybackState.None:
                    IsVideoPlaying = false;
                    IsVideoPaused = false;
                    break;
            }
            MediaStateChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private void OnPositionChanged(MediaPlaybackSession sender, object args)
    {
        RunOnUIThread(() =>
        {
            Position = sender.Position;
        });
    }

    private void RunOnUIThread(Action action)
    {
        if (_disposed || _dispatcher is null)
        {
            return;
        }

        if (_dispatcher.HasThreadAccess)
        {
            action();
        }
        else
        {
            _dispatcher.TryEnqueue(action);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_player is not null)
        {
            _player.MediaOpened -= OnPlayerMediaOpened;
            _player.MediaEnded -= OnPlayerMediaEnded;
            _player.MediaFailed -= OnPlayerMediaFailed;
            _player.PlaybackSession.PlaybackStateChanged -= OnPlaybackStateChanged;
            _player.PlaybackSession.PositionChanged -= OnPositionChanged;

            _player.Pause();
            _player.Source = null;
            _player.Dispose();
            _player = null;
        }

        _currentMediaSource?.Dispose();
        _currentMediaSource = null;
    }
}
