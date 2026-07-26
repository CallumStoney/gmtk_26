using Godot;

public partial class StatsHUD : HBoxContainer
{
    // -----------------------------------------
    // UI NODE REFERENCES
    // -----------------------------------------
    private Label _turnLabel;

    private RichTextLabel _populationLabel;
    private RichTextLabel _pollutionLabel;
    private RichTextLabel _popularityLabel;
    private RichTextLabel _moneyLabel;

    // Bar Fills
    private ColorRect _dayBarFill;
    private ColorRect _pollutionBarFill;

    // Thresholds
    private ColorRect _pollutionThreshold;

    // -----------------------------------------
    // INITIALIZATION
    // -----------------------------------------
    public override void _Ready()
    {
        GrabNodeReferences();
        CallDeferred(MethodName.InitializeHUD);
    }

    private void InitializeHUD()
    {
        var gm = GameManager.Instance;

        if (gm == null)
        {
            GD.PrintErr("CRITICAL: StatsHUD could not find GameManager.Instance!");
            return;
        }

        gm.StatsChanged += UpdateDisplay;
        gm.TurnAdvanced += _ => UpdateDisplay();

        UpdateDisplay();
    }

    private void GrabNodeReferences()
    {
        _turnLabel = GetNode<Label>("DayBar/TurnLabel");

        _populationLabel = GetNode<RichTextLabel>("PopulationLabel");
        _pollutionLabel = GetNode<RichTextLabel>("HBoxContainer/PollutionLabel");
        _popularityLabel = GetNode<RichTextLabel>("HBoxContainer2/PopularityLabel");
        _moneyLabel = GetNode<RichTextLabel>("HBoxContainer3/MoneyLabel");

        _dayBarFill = GetNode<ColorRect>("DayBar/DayBarArea/Fill");
    }

    // -----------------------------------------
    // UI UPDATES
    // -----------------------------------------
    private void UpdateDisplay()
    {
        var gm = GameManager.Instance;
        if (gm == null) return; // Failsafe

        // 1. Update Turn
        int daysLeft = gm.MaxTurns - gm.CurrentTurn;
        _turnLabel.Text = daysLeft == 0 ? "Final Day!" : $"{daysLeft} Days Left";

        // 2. Update Main Labels
        // Over-capacity Check: Turns red (#e57373) if capacity is less than population
        string popColor = gm.Capacity < gm.Population ? "#e57373" : "white";
        _populationLabel.Text = $"[color={popColor}]Pop: {gm.Population}/{gm.Capacity}[/color]";

        // --- BBCode colored text for Pollution and Approval ---
        _pollutionLabel.Text = $"[color=white]Pollution: {gm.Pollution}[/color] [color={GetInvertedDeltaColor(gm.ExpectedDailyPollution)}]({FormatDelta(gm.ExpectedDailyPollution)})[/color]";
        _popularityLabel.Text = $"[color=white]Approval: {gm.Popularity}[/color] [color={GetDeltaColor(gm.ExpectedDailyPopularity)}]({FormatDelta(gm.ExpectedDailyPopularity)})[/color]";

        // --- Custom formatting for Money ---
        string moneyDeltaSign = gm.ExpectedDailyMoney >= 0 ? "+" : "-";
        string moneyDeltaFormatted = FormatMoney(Mathf.Abs(gm.ExpectedDailyMoney));
        string moneyDeltaColor = GetDeltaColor(gm.ExpectedDailyMoney);

        _moneyLabel.Text = $"[color=#ffd54f]${FormatMoney(gm.Money)}[/color] [color={moneyDeltaColor}]({moneyDeltaSign}${moneyDeltaFormatted})[/color]";

        // 3. Update Bars
        float dayRatio = gm.MaxTurns > 1 ? 1f - ((float)(gm.CurrentTurn - 1) / (gm.MaxTurns - 1)) : 0f;
        _dayBarFill.AnchorRight = dayRatio;
    }

    // -----------------------------------------
    // HELPER METHODS
    // -----------------------------------------
    private string GetDeltaColor(int value)
    {
        if (value > 0) return "#81c784"; // Soft pastel green
        if (value < 0) return "#e57373"; // Soft muted red
        return "#e0e0e0"; // Soft off-white
    }

    private string GetInvertedDeltaColor(int value)
    {
        if (value > 0) return "#e57373"; // Soft muted red
        if (value < 0) return "#81c784"; // Soft pastel green
        return "#e0e0e0";
    }

    private string FormatDelta(int value)
    {
        if (value > 0) return $"+{value}";
        if (value < 0) return $"{value}";
        return "+0";
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