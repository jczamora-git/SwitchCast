using CommunityToolkit.Mvvm.ComponentModel;

namespace SwitchCast.Models;

/// <summary>
/// Represents a selectable source category option with a stable identity and observable count/label.
/// Used for ComboBox and category tab binding in Sources management.
/// </summary>
public partial class SourceCategoryOption : ObservableObject
{
    public int CategoryIndex { get; }
    public string Name { get; }

    [ObservableProperty]
    private int _count;

    [ObservableProperty]
    private string _displayText = string.Empty;

    public SourceCategoryOption(int categoryIndex, string name, int initialCount = 0)
    {
        CategoryIndex = categoryIndex;
        Name = name;
        _count = initialCount;
        _displayText = $"{name} ({initialCount})";
    }

    public void UpdateCount(int newCount)
    {
        Count = newCount;
        DisplayText = $"{Name} ({newCount})";
    }

    public override string ToString() => DisplayText;
}
