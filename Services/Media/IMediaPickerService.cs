namespace SwitchCast.Services.Media;

/// <summary>
/// Service abstraction for launching native desktop file pickers for media assets.
/// </summary>
public interface IMediaPickerService
{
    /// <summary>
    /// Launches a native Windows file picker allowing the user to select one or multiple supported media files.
    /// Returns a list of absolute file paths.
    /// </summary>
    Task<IReadOnlyList<string>> PickMediaFilesAsync();

    /// <summary>
    /// Launches a native Windows file picker allowing the user to select one or multiple PowerPoint files (.pptx).
    /// Returns a list of absolute file paths.
    /// </summary>
    Task<IReadOnlyList<string>> PickPowerPointFilesAsync();

    /// <summary>
    /// Launches a native Windows file picker allowing the user to select one or multiple local audio files (.mp3, .wav, .wma, .m4a, .aac, .flac).
    /// Returns a list of absolute file paths.
    /// </summary>
    Task<IReadOnlyList<string>> PickAudioFilesAsync();
}
