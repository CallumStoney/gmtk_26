using Godot;

public partial class StatsHUD : HBoxContainer
{
    // -----------------------------------------
    // UI NODE REFERENCES
    // -----------------------------------------
    // Main Labels
    private Label _turnLabel;
    private Label _populationLabel;
    private Label _pollutionLabel;
    private Label _popularityLabel;
    private Label _moneyLabel;

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
        // Existing nodes
        _turnLabel = GetNode<Label>("DayBar/TurnLabel");
        _populationLabel = GetNode<Label>("PopulationLabel");
        _pollutionLabel = GetNode<Label>("PollutionBar/PollutionLabel");

        // NOTE: Path updated since the ApprovalBar is gone. 
        // Ensure this matches where the label is now in your Godot Scene Tree!
        _popularityLabel = GetNode<Label>("ApprovalBar/PopularityLabel");

        _moneyLabel = GetNode<Label>("MoneyLabel");

        // Bar nodes
        _dayBarFill = GetNode<ColorRect>("DayBar/DayBarArea/Fill");
        _pollutionBarFill = GetNode<ColorRect>("PollutionBar/PollutionBarArea/Fill");
        _pollutionThreshold = GetNode<ColorRect>("PollutionBar/PollutionBarArea/Threshold");
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
        // THE FIX: Removed the daily delta from the Population/Capacity label!
        _populationLabel.Text = $"Pop: {gm.Population}/{gm.Capacity}";

        _pollutionLabel.Text = $"Pollution: {gm.Pollution} ({FormatDelta(gm.ExpectedDailyPollution)})";
        _popularityLabel.Text = $"Approval: {gm.Popularity} ({FormatDelta(gm.ExpectedDailyPopularity)})";

        // --- Custom formatting for Money ---
        string moneyDeltaSign = gm.ExpectedDailyMoney >= 0 ? "+" : "-";
        string moneyDeltaFormatted = FormatMoney(Mathf.Abs(gm.ExpectedDailyMoney));
        _moneyLabel.Text = $"${FormatMoney(gm.Money)} ({moneyDeltaSign}${moneyDeltaFormatted})";

        // 3. Update Bars
        float dayRatio = gm.MaxTurns > 1 ? 1f - ((float)(gm.CurrentTurn - 1) / (gm.MaxTurns - 1)) : 0f;
        _dayBarFill.AnchorRight = dayRatio;

        _pollutionBarFill.AnchorRight = gm.Pollution / 100f;

        // 4. Update Thresholds
        float pollThresh = gm.PollutionTarget / 100f;
        _pollutionThreshold.AnchorLeft = pollThresh;
        _pollutionThreshold.AnchorRight = pollThresh;
    }

    // Helper method to format the delta with a + or - sign for standard integers
    private string FormatDelta(int value)
    {
        if (value > 0) return $"+{value}";
        if (value < 0) return $"{value}"; // Negative numbers already include the minus sign automatically
        return "+0"; // Shows +0 when nothing is changing
    }

    // Helper method to format large money values into k and M
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