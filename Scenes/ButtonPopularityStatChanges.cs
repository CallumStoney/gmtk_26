using Godot;
using System;

public partial class ButtonPopularityStatChanges : TextureButton
{
    private RichTextLabel _dropdownText;

    public override void _Ready()
    {
        _dropdownText = GetNode<RichTextLabel>("DropdownText");
        SyncState();
    }

    public override void _EnterTree()
    {
        Pressed -= OnButtonPressed;
        Pressed += OnButtonPressed;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.StatsChanged -= OnStatsChanged;
            GameManager.Instance.StatsChanged += OnStatsChanged;
        }

        if (_dropdownText != null)
        {
            SyncState();
        }
    }

    public override void _ExitTree()
    {
        Pressed -= OnButtonPressed;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.StatsChanged -= OnStatsChanged;
        }
    }

    private void SyncState()
    {
        if (_dropdownText == null) return;

        bool isOpen = GameManager.Instance?.IsPopularityDropdownOpen ?? false;
        _dropdownText.Visible = isOpen;

        if (isOpen)
        {
            UpdateDropdownText();
        }
    }

    private void OnStatsChanged()
    {
        if (_dropdownText != null && _dropdownText.Visible)
        {
            UpdateDropdownText();
        }
    }

    private void OnButtonPressed()
    {
        ReleaseFocus();

        if (_dropdownText != null)
        {
            _dropdownText.Visible = !_dropdownText.Visible;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.IsPopularityDropdownOpen = _dropdownText.Visible;
            }

            if (_dropdownText.Visible)
            {
                UpdateDropdownText();
            }
        }
    }

    private void UpdateDropdownText()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        int buildingPop = gm.BuildingDailyPopularity;
        string buildingColor = GetDeltaColor(buildingPop);
        string formattedBuilding = FormatDelta(buildingPop);

        string text = $"[color=white]Buildings:[/color] [color={buildingColor}]({formattedBuilding})[/color]";

        foreach (var mod in gm.GetActiveModifiers())
        {
            if (mod.Type == GameManager.ModifierType.Popularity)
            {
                string modColor = GetDeltaColor(mod.Value);
                string formattedMod = FormatDelta(mod.Value);
                text += $"\n[color=white]{mod.SourceName}:[/color] [color={modColor}]({formattedMod})[/color]";
            }
        }

        _dropdownText.Text = text;
    }

    private string GetDeltaColor(int value)
    {
        if (value > 0) return "#81c784";
        if (value < 0) return "#e57373";
        return "#e0e0e0";
    }

    private string FormatDelta(int value)
    {
        if (value > 0) return $"+{value}";
        if (value < 0) return $"{value}";
        return "+0";
    }
}