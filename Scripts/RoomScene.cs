using Godot;
using System.Collections.Generic;


public partial class RoomScene : Control
{
	private const int EventsPerTurn = 2;

	private Label _turnLabel;
	private Label _populationLabel;
	private Label _pollutionLabel;
	private Label _popularityLabel;
	private Label _moneyLabel;

	private ColorRect _advisorPortrait;
	private Label _advisorNameLabel;
	private Label _dialogueLabel;

	private Button _acceptButton;
	private Button _declineButton;
	private Label _responseLabel;

	private Button _nextButton;
	private Button _endTurnButton;

	//state
	private List<AdvisorEvent> _eventPool = new();
	private Queue<AdvisorEvent> _turnEvents = new();
	private AdvisorEvent _currentEvent;
	private bool _awaitingChoice;

	public override void _Ready()
	{
		GrabNodeReferences();
		ConnectSignals();
		BuildEventPool();

		UpdateStatsDisplay();
		StartTurn();
	}

	private void GrabNodeReferences()
	{
		var root = "MarginContainer/MainVBox";

		_turnLabel       = GetNode<Label>($"{root}/StatsBar/TurnLabel");
		_populationLabel = GetNode<Label>($"{root}/StatsBar/PopulationLabel");
		_pollutionLabel  = GetNode<Label>($"{root}/StatsBar/PollutionLabel");
		_popularityLabel = GetNode<Label>($"{root}/StatsBar/PopularityLabel");
		_moneyLabel      = GetNode<Label>($"{root}/StatsBar/MoneyLabel");

		_advisorPortrait  = GetNode<ColorRect>($"{root}/ContentHBox/AdvisorPortrait");
		_advisorNameLabel = GetNode<Label>($"{root}/ContentHBox/ContentVBox/AdvisorNameLabel");
		_dialogueLabel    = GetNode<Label>($"{root}/ContentHBox/ContentVBox/DialogueLabel");

		_acceptButton  = GetNode<Button>($"{root}/ChoiceHBox/AcceptButton");
		_declineButton = GetNode<Button>($"{root}/ChoiceHBox/DeclineButton");
		_responseLabel = GetNode<Label>($"{root}/ResponseLabel");

		_nextButton    = GetNode<Button>($"{root}/BottomHBox/NextButton");
		_endTurnButton = GetNode<Button>($"{root}/BottomHBox/EndTurnButton");
	}

	private void ConnectSignals()
	{
		_acceptButton.Pressed  += OnAcceptPressed;
		_declineButton.Pressed += OnDeclinePressed;
		_nextButton.Pressed    += OnNextPressed;
		_endTurnButton.Pressed += OnEndTurnPressed;

		GameManager.Instance.StatsChanged += UpdateStatsDisplay;
		GameManager.Instance.GameOver     += OnGameOver;
	}


	private void UpdateStatsDisplay()
	{
		var gm = GameManager.Instance;
		_turnLabel.Text       = $"Day {gm.CurrentTurn} / {gm.MaxTurns}";
		_populationLabel.Text = $"Pop: {gm.Population}";
		_pollutionLabel.Text  = $"Pollution: {gm.Pollution}";
		_popularityLabel.Text = $"Approval: {gm.Popularity}%";
		_moneyLabel.Text      = $"${gm.Money}";
	}


	private void StartTurn()
	{
		_turnEvents.Clear();

		//shuffle the pool and queue N events for it
		var shuffled = new List<AdvisorEvent>(_eventPool);
		for (int i = shuffled.Count - 1; i > 0; i--)
		{
			int j = (int)(GD.Randi() % (uint)(i + 1));
			(shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
		}

		int count = Mathf.Min(EventsPerTurn, shuffled.Count);
		for (int i = 0; i < count; i++)
			_turnEvents.Enqueue(shuffled[i]);

		ShowNextEvent();
	}

	private void ShowNextEvent()
	{
		if (_turnEvents.Count == 0)
		{
			ShowNoMoreEvents();
			return;
		}

		_currentEvent = _turnEvents.Dequeue();

		_advisorNameLabel.Text = _currentEvent.AdvisorName;
		_dialogueLabel.Text    = _currentEvent.DialogueText;
		_acceptButton.Text     = _currentEvent.AcceptText;
		_declineButton.Text    = _currentEvent.DeclineText;

		_acceptButton.Visible  = true;
		_declineButton.Visible = true;
		_responseLabel.Text    = "";
		_nextButton.Visible    = false;
		_endTurnButton.Visible = false;
		_awaitingChoice        = true;
	}

	private void ShowNoMoreEvents()
	{
		_advisorNameLabel.Text = "";
		_dialogueLabel.Text    = "No more advisors today. End the day when you're ready.";
		_acceptButton.Visible  = false;
		_declineButton.Visible = false;
		_responseLabel.Text    = "";
		_nextButton.Visible    = false;
		_endTurnButton.Visible = true;
	}


	private void OnAcceptPressed()
	{
		if (!_awaitingChoice || _currentEvent == null) return;
		_awaitingChoice = false;

		GameManager.Instance.ApplyStatChanges(
			population: _currentEvent.AcceptPopulation,
			pollution:  _currentEvent.AcceptPollution,
			popularity: _currentEvent.AcceptPopularity,
			money:      _currentEvent.AcceptMoney
		);

		ShowResponse(_currentEvent.AcceptResponse);
	}

	private void OnDeclinePressed()
	{
		if (!_awaitingChoice || _currentEvent == null) return;
		_awaitingChoice = false;

		GameManager.Instance.ApplyStatChanges(
			population: _currentEvent.DeclinePopulation,
			pollution:  _currentEvent.DeclinePollution,
			popularity: _currentEvent.DeclinePopularity,
			money:      _currentEvent.DeclineMoney
		);

		ShowResponse(_currentEvent.DeclineResponse);
	}

	private void ShowResponse(string text)
	{
		_responseLabel.Text    = text;
		_acceptButton.Visible  = false;
		_declineButton.Visible = false;

		if (_turnEvents.Count > 0)
		{
			_nextButton.Text    = "Next Advisor";
			_nextButton.Visible = true;
		}
		else
		{
			_endTurnButton.Visible = true;
		}
	}


	private void OnNextPressed()  => ShowNextEvent();

	private void OnEndTurnPressed()
	{
		GameManager.Instance.AdvanceTurn();
		UpdateStatsDisplay();
		StartTurn();
	}


	private void OnGameOver(string reason)
	{
		_advisorNameLabel.Text = "GAME OVER";
		_dialogueLabel.Text    = reason;
		_acceptButton.Visible  = false;
		_declineButton.Visible = false;
		_responseLabel.Text    = "";
		_nextButton.Visible    = false;
		_endTurnButton.Visible = false;
	}

	// User requested placeholder eventpool
	private void BuildEventPool()
	{
		_eventPool = new List<AdvisorEvent>
		{
			new()
			{
				AdvisorName     = "Environment Minister",
				DialogueText    = "Mayor, we can install solar panels across the industrial district. It'll cost us, but pollution will drop significantly.",
				AcceptText      = "Fund the project",
				DeclineText     = "Too expensive right now",
				AcceptResponse  = "Workers begin installing panels. Pollution drops, but it wasn't cheap.",
				DeclineResponse = "The minister leaves, clearly disappointed.",
				AcceptPollution = -10, AcceptPopularity = 5, AcceptMoney = -150,
				DeclinePopularity = -3
			},
			new()
			{
				AdvisorName     = "City Treasurer",
				DialogueText    = "There's an opportunity to lease public land to a factory. Big money, but the residents won't like the extra smoke.",
				AcceptText      = "Sign the lease",
				DeclineText     = "Protect the land",
				AcceptResponse  = "The factory moves in. Cash flows, but so does the smog.",
				DeclineResponse = "The land stays green. People are pleased.",
				AcceptPollution = 15, AcceptPopularity = -10, AcceptMoney = 300, AcceptPopulation = 10,
				DeclinePopularity = 5
			},
			new()
			{
				AdvisorName     = "Housing Director",
				DialogueText    = "We have families waiting for affordable housing. Building apartments will attract more people but costs money.",
				AcceptText      = "Build apartments",
				DeclineText     = "We can't afford it",
				AcceptResponse  = "Construction begins. New residents are moving in!",
				DeclineResponse = "The housing crisis continues. People are not happy.",
				AcceptPopulation = 20, AcceptPopularity = 5, AcceptMoney = -200, AcceptPollution = 5,
				DeclinePopulation = -5, DeclinePopularity = -5
			},
			new()
			{
				AdvisorName     = "Industry Lobbyist",
				DialogueText    = "I represent several manufacturing firms. Relax emission standards and we'll bring jobs and revenue to your city.",
				AcceptText      = "Relax the standards",
				DeclineText     = "Standards stay as they are",
				AcceptResponse  = "Factories ramp up production. The air quality drops but the economy booms.",
				DeclineResponse = "The lobbyist storms out. 'You'll regret this,' he mutters.",
				AcceptPollution = 20, AcceptMoney = 250, AcceptPopularity = -8, AcceptPopulation = 10,
				DeclinePopularity = 3, DeclineMoney = -50
			},
			new()
			{
				AdvisorName     = "Public Transit Chief",
				DialogueText    = "Mayor, investing in electric buses would reduce commuter emissions and boost your public image.",
				AcceptText      = "Fund electric buses",
				DeclineText     = "Not a priority",
				AcceptResponse  = "New electric buses hit the roads. Citizens approve!",
				DeclineResponse = "Diesel buses keep rumbling along...",
				AcceptPollution = -8, AcceptPopularity = 8, AcceptMoney = -180,
				DeclinePopularity = -2, DeclinePollution = 2
			},
			new()
			{
				AdvisorName     = "Shady Businessman",
				DialogueText    = "Psst... I can make your pollution numbers look better on paper. Nobody will know. For a small fee, of course.",
				AcceptText      = "Take the deal",
				DeclineText     = "Get out of my office",
				AcceptResponse  = "The numbers look better... for now. Let's hope nobody audits.",
				DeclineResponse = "You feel good about your integrity. The people notice.",
				AcceptPollution = -5, AcceptMoney = -100, AcceptPopularity = -15,
				DeclinePopularity = 5
			},
			new()
			{
				AdvisorName     = "Community Organizer",
				DialogueText    = "The citizens are demanding a park in the old industrial lot. It would be a symbol of change!",
				AcceptText      = "Build the park",
				DeclineText     = "We need that lot for industry",
				AcceptResponse  = "A beautiful green space springs up. The people love it!",
				DeclineResponse = "Protests erupt at the industrial lot.",
				AcceptPollution = -5, AcceptPopularity = 12, AcceptMoney = -100,
				DeclinePopularity = -8
			},
			new()
			{
				AdvisorName     = "Foreign Trade Minister",
				DialogueText    = "A neighbouring city wants to buy our excess power at a premium. It would mean running our coal plants harder.",
				AcceptText      = "Sell the power",
				DeclineText     = "Decline the offer",
				AcceptResponse  = "The coal plants roar. Money pours in, but at what cost?",
				DeclineResponse = "A missed opportunity, but the skies stay clearer.",
				AcceptPollution = 12, AcceptMoney = 200, AcceptPopularity = -5,
				DeclinePopularity = 2, DeclinePollution = -2
			},
		};
	}
}
