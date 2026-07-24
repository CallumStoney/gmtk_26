using Godot;

/// <summary>
/// Reusable PPPE stats HUD — drop this scene into any UI and it self-updates
/// via GameManager signals. Day bar counts DOWN to fit the countdown theme.
/// </summary>
public partial class StatsHUD : HBoxContainer
{
	// labels
	private Label _turnLabel;
	private Label _populationLabel;
	private Label _pollutionLabel;
	private Label _popularityLabel;
	private Label _moneyLabel;

	// bar fills (sized via AnchorRight)
	private ColorRect _dayBarFill;
	private ColorRect _pollutionBarFill;
	private ColorRect _approvalBarFill;

	// threshold markers
	private ColorRect _pollutionThreshold;
	private ColorRect _approvalThreshold;

	public override void _Ready()
	{
		GrabNodeReferences();

		var gm = GameManager.Instance;
		gm.StatsChanged  += UpdateDisplay;
		gm.TurnAdvanced  += _ => UpdateDisplay();

		UpdateDisplay();
	}

	private void GrabNodeReferences()
	{
		_turnLabel       = GetNode<Label>("DayBar/TurnLabel");
		_populationLabel = GetNode<Label>("PopulationLabel");
		_pollutionLabel  = GetNode<Label>("PollutionBar/PollutionLabel");
		_popularityLabel = GetNode<Label>("ApprovalBar/PopularityLabel");
		_moneyLabel      = GetNode<Label>("MoneyLabel");

		_dayBarFill         = GetNode<ColorRect>("DayBar/DayBarArea/Fill");
		_pollutionBarFill   = GetNode<ColorRect>("PollutionBar/PollutionBarArea/Fill");
		_approvalBarFill    = GetNode<ColorRect>("ApprovalBar/ApprovalBarArea/Fill");

		_pollutionThreshold = GetNode<ColorRect>("PollutionBar/PollutionBarArea/Threshold");
		_approvalThreshold  = GetNode<ColorRect>("ApprovalBar/ApprovalBarArea/Threshold");
	}

	private void UpdateDisplay()
	{
		var gm = GameManager.Instance;

		// --- Labels ---
		int daysLeft = gm.MaxTurns - gm.CurrentTurn;
		_turnLabel.Text       = daysLeft == 0 ? "Final Day!" : $"{daysLeft} Days Left";
		_populationLabel.Text = $"Pop: {gm.Population}";
		_pollutionLabel.Text  = $"Pollution: {gm.Pollution}";
		_popularityLabel.Text = $"Approval: {gm.Popularity}%";
		_moneyLabel.Text      = $"${gm.Money}";

		// --- Day bar COUNTS DOWN (full on day 1, empty on last day) ---
		float dayRatio = gm.MaxTurns > 1
			? 1f - ((float)(gm.CurrentTurn - 1) / (gm.MaxTurns - 1))
			: 0f;
		_dayBarFill.AnchorRight = dayRatio;

		// --- Pollution & Approval bars ---
		_pollutionBarFill.AnchorRight = gm.Pollution / 100f;
		_approvalBarFill.AnchorRight  = gm.Popularity / 100f;

		// --- Threshold markers ---
		float pollThresh = gm.PollutionTarget / 100f;
		_pollutionThreshold.AnchorLeft  = pollThresh;
		_pollutionThreshold.AnchorRight = pollThresh;

		float apprThresh = gm.PopularityFloor / 100f;
		_approvalThreshold.AnchorLeft  = apprThresh;
		_approvalThreshold.AnchorRight = apprThresh;
	}
}
