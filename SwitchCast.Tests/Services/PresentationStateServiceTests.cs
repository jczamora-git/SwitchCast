using SwitchCast.Models;
using SwitchCast.Services;
using Xunit;

namespace SwitchCast.Tests.Services;

public class PresentationStateServiceTests
{
    [Fact]
    public void InitialState_IsIdle_AndSelectedSourcesEmpty()
    {
        var service = new PresentationStateService();

        Assert.Equal(PresentationStatus.Idle, service.Status);
        Assert.Equal(PresenterSwitchMode.LiveOnly, service.SwitchMode);
        Assert.Null(service.ActiveSource);
        Assert.Null(service.SelectedSource);
        Assert.Null(service.ForegroundSource);
        Assert.Empty(service.SelectedSources);
        Assert.Equal(0, service.SelectedSourceCount);
    }

    [Fact]
    public void SetStatus_UpdatesStatus_AndRaisesPropertyChanged()
    {
        var service = new PresentationStateService();
        var raised = false;
        service.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(IPresentationStateService.Status))
            {
                raised = true;
            }
        };

        service.SetStatus(PresentationStatus.Active);

        Assert.Equal(PresentationStatus.Active, service.Status);
        Assert.True(raised);
    }

    [Fact]
    public void SetSwitchMode_UpdatesSwitchMode_AndRaisesPropertyChanged()
    {
        var service = new PresentationStateService();
        var raised = false;
        service.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(IPresentationStateService.SwitchMode))
            {
                raised = true;
            }
        };

        service.SetSwitchMode(PresenterSwitchMode.ActiveAndLive);

        Assert.Equal(PresenterSwitchMode.ActiveAndLive, service.SwitchMode);
        Assert.True(raised);
    }

    [Fact]
    public void SetSelectedSource_And_SetForegroundSource_UpdateCorrectly()
    {
        var service = new PresentationStateService();
        var source = new WindowSource { Id = "win-1", Title = "Window 1" };

        service.SetSelectedSource(source);
        service.SetForegroundSource(source);

        Assert.Equal(source, service.SelectedSource);
        Assert.Equal(source, service.ForegroundSource);
    }

    [Fact]
    public void AddSelectedSource_AddsUniqueSource_IncrementsCount()
    {
        var service = new PresentationStateService();
        var source = new WindowSource
        {
            Id = "win-1",
            Title = "Visual Studio Code",
            WindowHandle = 12345,
            ProcessId = 999,
            ProcessName = "Code"
        };

        var result = service.AddSelectedSource(source);

        Assert.True(result);
        Assert.Equal(1, service.SelectedSourceCount);
        Assert.Contains(source, service.SelectedSources);
    }

    [Fact]
    public void AddSelectedSource_DuplicateSource_ReturnsFalse()
    {
        var service = new PresentationStateService();
        var source1 = new WindowSource { Id = "win-1", Title = "Window 1" };
        var source2 = new WindowSource { Id = "win-1", Title = "Window 1 Duplicate" };

        service.AddSelectedSource(source1);
        var result = service.AddSelectedSource(source2);

        Assert.False(result);
        Assert.Equal(1, service.SelectedSourceCount);
    }

    [Fact]
    public void RemoveSelectedSource_ExistingSource_RemovesAndDecrementsCount()
    {
        var service = new PresentationStateService();
        var source = new WindowSource { Id = "win-1", Title = "Window 1" };
        service.AddSelectedSource(source);

        var removed = service.RemoveSelectedSource("win-1");

        Assert.True(removed);
        Assert.Equal(0, service.SelectedSourceCount);
        Assert.DoesNotContain(source, service.SelectedSources);
    }

    [Fact]
    public void RemoveSelectedSource_ActiveSource_PreservesActiveSourceForPresentationContinuity()
    {
        var service = new PresentationStateService();
        var source = new WindowSource { Id = "win-1", Title = "Window 1" };
        service.AddSelectedSource(source);
        service.SetActiveSource(source);

        service.RemoveSelectedSource("win-1");

        // Per Task Section 17:
        // Removing a source that is currently On Air must NOT unexpectedly stop the presentation.
        // It remains On Air while being removed from the future navigation queue.
        Assert.Equal(source, service.ActiveSource);
        Assert.DoesNotContain(source, service.SelectedSources);
    }

    [Fact]
    public void RemoveSelectedSource_SelectedCursor_AdvancesToNextQueuedSource()
    {
        var service = new PresentationStateService();
        var a = new WindowSource { Id = "a", Title = "A" };
        var b = new VideoMediaSource { Id = "b", Title = "B", FilePath = "b.mp4" };
        var c = new ImageMediaSource { Id = "c", Title = "C", FilePath = "c.png" };
        var d = new MonitorSource { Id = "d", Title = "D" };

        service.AddSelectedSource(a);
        service.AddSelectedSource(b);
        service.AddSelectedSource(c);
        service.AddSelectedSource(d);

        service.SetSelectedSource(b);
        Assert.Equal("b", service.SelectedSource?.Id);

        // Remove B: cursor should move deterministically to C (item at old index 1)
        service.RemoveSelectedSource("b");

        Assert.Equal("c", service.SelectedSource?.Id);
        Assert.Equal(3, service.SelectedSourceCount);
    }

    [Fact]
    public void RemoveSelectedSource_LastItem_MovesToPreviousQueuedSource()
    {
        var service = new PresentationStateService();
        var a = new WindowSource { Id = "a", Title = "A" };
        var b = new VideoMediaSource { Id = "b", Title = "B", FilePath = "b.mp4" };
        var c = new ImageMediaSource { Id = "c", Title = "C", FilePath = "c.png" };

        service.AddSelectedSource(a);
        service.AddSelectedSource(b);
        service.AddSelectedSource(c);

        service.SetSelectedSource(c);
        Assert.Equal("c", service.SelectedSource?.Id);

        // Remove C (last item): cursor should move deterministically to previous item (B)
        service.RemoveSelectedSource("c");

        Assert.Equal("b", service.SelectedSource?.Id);
        Assert.Equal(2, service.SelectedSourceCount);
    }

    [Fact]
    public void RemoveSelectedSource_NonSelectedSource_PreservesSelectedCursor()
    {
        var service = new PresentationStateService();
        var a = new WindowSource { Id = "a", Title = "A" };
        var b = new VideoMediaSource { Id = "b", Title = "B", FilePath = "b.mp4" };
        var c = new ImageMediaSource { Id = "c", Title = "C", FilePath = "c.png" };

        service.AddSelectedSource(a);
        service.AddSelectedSource(b);
        service.AddSelectedSource(c);

        service.SetSelectedSource(b);

        // Remove A: SelectedSource remains B
        service.RemoveSelectedSource("a");

        Assert.Equal("b", service.SelectedSource?.Id);
        Assert.Equal(2, service.SelectedSourceCount);
    }

    [Fact]
    public void RemoveSelectedSource_AllItems_ClearsSelectedCursorAndPreservesActiveSource()
    {
        var service = new PresentationStateService();
        var a = new WindowSource { Id = "a", Title = "A" };

        service.AddSelectedSource(a);
        service.SetSelectedSource(a);
        service.SetActiveSource(a);

        service.RemoveSelectedSource("a");

        Assert.Null(service.SelectedSource);
        Assert.Equal(0, service.SelectedSourceCount);
        Assert.Equal(a, service.ActiveSource);
    }

    [Fact]
    public void ClearSelectedSources_RemovesAll_AndResetsActiveSource()
    {
        var service = new PresentationStateService();
        var source1 = new WindowSource { Id = "win-1", Title = "Window 1" };
        var source2 = new MonitorSource { Id = "mon-1", Title = "Display 1" };
        service.AddSelectedSource(source1);
        service.AddSelectedSource(source2);
        service.SetActiveSource(source1);
        service.SetSelectedSource(source1);
        service.SetForegroundSource(source1);

        service.ClearSelectedSources();

        Assert.Empty(service.SelectedSources);
        Assert.Equal(0, service.SelectedSourceCount);
        Assert.Null(service.ActiveSource);
        Assert.Null(service.SelectedSource);
        Assert.Null(service.ForegroundSource);
    }

    [Fact]
    public void IsSourceSelected_ReturnsExpectedBoolean()
    {
        var service = new PresentationStateService();
        var source = new WindowSource { Id = "win-1", Title = "Window 1" };
        service.AddSelectedSource(source);

        Assert.True(service.IsSourceSelected("win-1"));
        Assert.False(service.IsSourceSelected("win-non-existent"));
    }

    [Fact]
    public void ToggleSourceSelection_TogglesStateCorrectly()
    {
        var service = new PresentationStateService();
        var source = new WindowSource { Id = "win-1", Title = "Window 1" };

        var added = service.ToggleSourceSelection(source);
        Assert.True(added);
        Assert.True(service.IsSourceSelected("win-1"));

        var removed = service.ToggleSourceSelection(source);
        Assert.False(removed);
        Assert.False(service.IsSourceSelected("win-1"));
    }

    [Fact]
    public void ReconcileAvailability_MarksMissingSourcesUnavailable()
    {
        var service = new PresentationStateService();
        var source1 = new WindowSource { Id = "win-1", Title = "Window 1", IsAvailable = true };
        var source2 = new WindowSource { Id = "win-2", Title = "Window 2", IsAvailable = true };
        service.AddSelectedSource(source1);
        service.AddSelectedSource(source2);

        // Discovered set only contains win-1; win-2 has disappeared/closed
        var activeIds = new HashSet<string> { "win-1" };
        service.ReconcileAvailability(activeIds);

        Assert.True(service.SelectedSources[0].IsAvailable);
        Assert.False(service.SelectedSources[1].IsAvailable);
        Assert.Equal(2, service.SelectedSourceCount); // selection retained
    }

    [Fact]
    public void ReconcileAvailability_ReenablesPresentSources()
    {
        var service = new PresentationStateService();
        var source1 = new WindowSource { Id = "win-1", Title = "Window 1", IsAvailable = false };
        service.AddSelectedSource(source1);

        var activeIds = new HashSet<string> { "win-1" };
        service.ReconcileAvailability(activeIds);

        Assert.True(service.SelectedSources[0].IsAvailable);
    }
}
