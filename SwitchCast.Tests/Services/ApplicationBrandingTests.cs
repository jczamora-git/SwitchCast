using System.IO;
using Xunit;

namespace SwitchCast.Tests.Services;

public class ApplicationBrandingTests
{
    private static string GetProjectRoot()
    {
        string current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "SwitchCast.csproj")))
            {
                return current;
            }
            var parent = Directory.GetParent(current);
            if (parent is null)
            {
                break;
            }
            current = parent.FullName;
        }

        // Fallback relative to tests
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }

    [Fact]
    public void SwitchCastIco_FileExistsInAssetsFolder()
    {
        string projectRoot = GetProjectRoot();
        string icoPath = Path.Combine(projectRoot, "Assets", "SwitchCast.ico");

        Assert.True(File.Exists(icoPath), $"Expected SwitchCast.ico at: {icoPath}");
    }

    [Fact]
    public void SwitchCastIco_ContainsValidIcoHeaderAndMultiResolutions()
    {
        string projectRoot = GetProjectRoot();
        string icoPath = Path.Combine(projectRoot, "Assets", "SwitchCast.ico");

        Assert.True(File.Exists(icoPath));

        using var fs = new FileStream(icoPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var br = new BinaryReader(fs);

        ushort reserved = br.ReadUInt16();
        ushort type = br.ReadUInt16();
        ushort count = br.ReadUInt16();

        Assert.Equal(0, reserved);
        Assert.Equal(1, type); // 1 = ICO
        Assert.True(count >= 5, $"Expected at least 5 icon resolutions, found {count}");

        var widths = new List<int>();
        var heights = new List<int>();

        for (int i = 0; i < count; i++)
        {
            byte w = br.ReadByte();
            byte h = br.ReadByte();
            byte colorCount = br.ReadByte();
            byte reservedEntry = br.ReadByte();
            ushort planes = br.ReadUInt16();
            ushort bitCount = br.ReadUInt16();
            uint bytesInRes = br.ReadUInt32();
            uint imageOffset = br.ReadUInt32();

            int actualWidth = w == 0 ? 256 : w;
            int actualHeight = h == 0 ? 256 : h;

            widths.Add(actualWidth);
            heights.Add(actualHeight);

            Assert.True(bytesInRes > 0);
            Assert.True(imageOffset >= 6 + (16 * count));
        }

        // Verify standard Windows icon resolutions are included
        Assert.Contains(16, widths);
        Assert.Contains(32, widths);
        Assert.Contains(48, widths);
        Assert.Contains(64, widths);
        Assert.Contains(128, widths);
        Assert.Contains(256, widths);
    }

    [Fact]
    public void SwitchCastCsproj_ConfiguresApplicationIconAndContentDeployment()
    {
        string projectRoot = GetProjectRoot();
        string csprojPath = Path.Combine(projectRoot, "SwitchCast.csproj");

        Assert.True(File.Exists(csprojPath));
        string content = File.ReadAllText(csprojPath);

        Assert.Contains("<ApplicationIcon>Assets\\SwitchCast.ico</ApplicationIcon>", content);
        Assert.Contains("<Content Include=\"Assets\\SwitchCast.ico\">", content);
        Assert.Contains("<Content Include=\"Assets\\SwitchCast_1.png\">", content);
        Assert.Contains("<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>", content);
    }

    [Fact]
    public void SwitchCastPng_FileExistsInAssetsFolder()
    {
        string projectRoot = GetProjectRoot();
        string pngPath = Path.Combine(projectRoot, "Assets", "SwitchCast_1.png");

        Assert.True(File.Exists(pngPath), $"Expected SwitchCast_1.png at: {pngPath}");
    }
}
