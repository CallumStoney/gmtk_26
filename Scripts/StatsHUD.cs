using Godot;

public partial class StatsHUD : HBoxContainer
{
    // -----------------------------------------
    // UI NODE REFERENCES
    // -----------------------------------------
    private Control _dayBarRoot;
    private Control _dayBarArea;
    private Label _turnLabel;

    private RichTextLabel _populationLabel;
    private RichTextLabel _pollutionLabel;
    private RichTextLabel _popularityLabel;
    private RichTextLabel _moneyLabel;

    private ColorRect _dayBarFill;
    private ColorRect _pollutionBarFill;
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

        // Subscribe to updates
        gm.StatsChanged += UpdateDisplay;
        gm.TurnAdvanced += _ => UpdateDisplay();

        // Force initial display
        UpdateDisplay();
    }

    private void GrabNodeReferences()
    {
        _dayBarRoot = GetNode<Control>("DayBar");
        _dayBarArea = GetNode<Control>("DayBar/DayBarArea");
        _dayBarFill = GetNode<ColorRect>("DayBar/DayBarArea/Fill");

        _turnLabel = GetNode<Label>("DayBar/TurnLabel");

        _populationLabel = GetNode<RichTextLabel>("PopulationLabel");
        _pollutionLabel = GetNode<RichTextLabel>("HBoxContainer/PollutionLabel");
        _popularityLabel = GetNode<RichTextLabel>("HBoxContainer2/PopularityLabel");
        _moneyLabel = GetNode<RichTextLabel>("HBoxContainer3/MoneyLabel");
    }

    // -----------------------------------------
    // UI UPDATES
    // -----------------------------------------
    private void UpdateDisplay()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        // 1. Update Turn Text
        int daysLeft = Mathf.Max(0, gm.MaxTurns - gm.CurrentTurn);
        _turnLabel.Text = daysLeft == 0 ? "Final Day!" : $"{daysLeft} Days Left";

        // 2. Update Main Labels
        string popColor = gm.Capacity < gm.Population ? "#e57373" : "white";
        _populationLabel.Text = $"[color={popColor}]Pop: {gm.Population}/{gm.Capacity}[/color]";

        _pollutionLabel.Text = $"[color=white]Pollution: {gm.Pollution}[/color] [color={GetInvertedDeltaColor(gm.ExpectedDailyPollution)}]({FormatDelta(gm.ExpectedDailyPollution)})[/color]";
        _popularityLabel.Text = $"[color=white]Approval: {gm.Popularity}[/color] [color={GetDeltaColor(gm.ExpectedDailyPopularity)}]({FormatDelta(gm.ExpectedDailyPopularity)})[/color]";

        string moneyDeltaSign = gm.ExpectedDailyMoney >= 0 ? "+" : "-";
        string moneyDeltaFormatted = FormatMoney(Mathf.Abs(gm.ExpectedDailyMoney));
        string moneyDeltaColor = GetDeltaColor(gm.ExpectedDailyMoney);

        _moneyLabel.Text = $"[color=#ffd54f]${FormatMoney(gm.Money)}[/color] [color={moneyDeltaColor}]({moneyDeltaSign}${moneyDeltaFormatted})[/color]";

        // 3. Update Day Bar Fill (Bypassing Layout Engine with Scale)
        float dayRatio = gm.MaxTurns > 1 ? (float)daysLeft / (gm.MaxTurns - 1) : 0f;

        // Scale.X shrinks the visual width perfectly without triggering container layout recalculations
        _dayBarFill.Scale = new Vector2(dayRatio, 1f);
    }

    // -----------------------------------------
    // HELPER METHODS
    // -----------------------------------------
    private string GetDeltaColor(int value)
    {
        if (value > 0) return "#81c784";
        if (value < 0) return "#e57373";
        return "#e0e0e0";
    }

    private string GetInvertedDeltaColor(int value)
    {
        if (value > 0) return "#e57373";
        if (value < 0) return "#81c784";
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