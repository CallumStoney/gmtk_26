using Godot;
using System;

public partial class ButtonStatMoneyChanges : TextureButton
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

        bool isOpen = GameManager.Instance?.IsMoneyDropdownOpen ?? false;
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
                GameManager.Instance.IsMoneyDropdownOpen = _dropdownText.Visible;
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

        int buildingMoney = gm.BuildingDailyMoney;
        string buildingColor = GetDeltaColor(buildingMoney);
        string buildingSign = buildingMoney >= 0 ? "+" : "-";
        string formattedBuilding = FormatMoney(Mathf.Abs(buildingMoney));

        string text = $"[color=white]Buildings:[/color] [color={buildingColor}]({buildingSign}${formattedBuilding})[/color]";

        foreach (var mod in gm.GetActiveModifiers())
        {
            if (mod.Type == GameManager.ModifierType.Money)
            {
                string modColor = GetDeltaColor(mod.Value);
                string modSign = mod.Value >= 0 ? "+" : "-";
                string formattedMod = FormatMoney(Mathf.Abs(mod.Value));

                text += $"\n[color=white]{mod.SourceName}:[/color] [color={modColor}]({modSign}${formattedMod})[/color]";
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

    private string FormatMoney(int value)
    {
        float absVal = Mathf.Abs(value);

        if (absVal >= 1_000_000)
            return (absVal / 1_000_000f).ToString("0.#") + "M";
        else if (absVal >= 1_000)
            return (absVal / 1_000f).ToString("0.#") + "k";
        else
            return absVal.ToString();
    }
}