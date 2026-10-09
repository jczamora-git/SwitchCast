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
    private readonly DispatcherQueue? _dispatcherQueue;
    private MediaPlayer? _player;
    private MediaSource? _currentMediaSource;

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
    private TimeSpan _position = TimeSpan.Zero;

    [ObservableProperty]
    private TimeSpan _duration = TimeSpan.Zero;

    [ObservableProperty]
    private string? _lastErrorMessage;

    public event EventHandler? MediaStateChanged;

    public MediaPresentationService()
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        InitializePlayer();
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
                IsMuted = true, // Audio Policy: Muted by default
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
                LastErrorMessage = $"Failed to load image: {ex.Message}";
                DirectImageSource = null;
                Debug.WriteLine($"[MediaPresentationService] LoadImage error: {ex.Message}");
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
            MediaStateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            LastErrorMessage = $"Failed to play video: {ex.Message}";
            Debug.WriteLine($"[MediaPresentationService] PlayVideo error: {ex.Message}");
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
            Debug.WriteLine($"[MediaPresentationService] ClearVideoInternal error: {ex.Message}");
        }
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
        var dispatcher = _dispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
        if (dispatcher is not null && !dispatcher.HasThreadAccess)
        {
            dispatcher.TryEnqueue(() => action());
        }
        else
        {
            action();
        }
    }

    public void Dispose()
    {
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
