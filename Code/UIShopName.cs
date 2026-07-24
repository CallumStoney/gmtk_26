using Godot;
using System;

public partial class UIShopName : Label
{
    [Export] public BuildingManager Manager;
    [Export] public int BuildingIndex = 0;

    // --- Font Scaling Settings ---
    [Export] public int BaseFontSize = 28;      // Starting font size
    [Export] public float ShrinkPerChar = 0.8f; // How much font size to drop per character
    // -----------------------------

    public override void _Ready()
    {
        CallDeferred(nameof(RefreshName));
    }

    public void RefreshName()
    {
        if (Manager == null)
        {
            Text = "Error: Manager not assigned";
            return;
        }

        if (Manager.BuildingInfo.TryGetValue(BuildingIndex, out string[] infoArray))
        {
            if (infoArray.Length > 0)
            {
                string buildingName = infoArray[0];
                Text = buildingName;

                ScaleFontToFit(buildingName);
            }
            else
            {
                Text = "Error: Name missing";
            }
        }
        else
        {
            Text = "Unknown Building";
        }
    }

    private void ScaleFontToFit(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        // Calculate size based directly on string length
        int calculatedSize = Mathf.RoundToInt(BaseFontSize - (text.Length * ShrinkPerChar));

        // Prevent font size from hitting 0 or negative numbers, capped at BaseFontSize
        int newFontSize = Mathf.Clamp(calculatedSize, 1, BaseFontSize);

        AddThemeFontSizeOverride("font_size", newFontSize);
    }
}