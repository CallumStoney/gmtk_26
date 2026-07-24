using Godot;

public partial class StatsHUD : HBoxContainer
{
	//lables
	private Label _turnLabel;
	private Label _populationLabel;
	private Label _pollutionLabel;
	private Label _popularityLabel;
	private Label _moneyLabel;

	//bar fills (sized via AnchorRight)
	private ColorRect _dayBarFill;
	private ColorRect _pollutionBarFill;
	private ColorRect _approvalBarFill;

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


		int daysLeft = gm.MaxTurns - gm.CurrentTurn;
		_turnLabel.Text       = daysLeft == 0 ? "Final Day!" : $"{daysLeft} Days Left";
		_populationLabel.Text = $"Pop: {gm.Population}";
		_pollutionLabel.Text  = $"Pollution: {gm.Pollution}";
		_popularityLabel.Text = $"Approval: {gm.Popularity}%";
		_moneyLabel.Text      = $"${gm.Money}";


		float dayRatio = gm.MaxTurns > 1 ? 1f - ((float)(gm.CurrentTurn - 1) / (gm.MaxTurns - 1)) : 0f;
		_dayBarFill.AnchorRight = dayRatio;

		_pollutionBarFill.AnchorRight = gm.Pollution / 100f;
		_approvalBarFill.AnchorRight  = gm.Popularity / 100f;


		float pollThresh = gm.PollutionTarget / 100f;
		_pollutionThreshold.AnchorLeft  = pollThresh;
		_pollutionThreshold.AnchorRight = pollThresh;

		float apprThresh = gm.PopularityFloor / 100f;
		_approvalThreshold.AnchorLeft  = apprThresh;
		_approvalThreshold.AnchorRight = apprThresh;
	}
}
