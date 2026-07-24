using Godot;


public partial class GameManager : Node
{
	public static GameManager Instance { get; private set; }

	//signals let u sub from any scene
	[Signal] public delegate void StatsChangedEventHandler();
	[Signal] public delegate void TurnAdvancedEventHandler(int currentTurn);
	[Signal] public delegate void GameOverEventHandler(string reason);
	[Signal] public delegate void AdvisorTagExecutedEventHandler(string tag);

	public AdvisorMemory Memory { get; private set; }

	private int _population  = 100;
	private int _pollution   = 80;   //this might be a tad high idk
	private int _popularity  = 50;   //they start not giving a shit abt u
	private int _money       = 500;

	public int Population
	{
		get => _population;
		set { _population = Mathf.Max(value, 0); EmitSignal(SignalName.StatsChanged); 
		}
	}

	public int Pollution
	{
		get => _pollution;
		set { _pollution = Mathf.Clamp(value, 0, 100); EmitSignal(SignalName.StatsChanged); }
	}

	public int Popularity{
		get => _popularity;
		set { _popularity = Mathf.Clamp(value, 0, 100); EmitSignal(SignalName.StatsChanged); }
	}
	public int Money
	{
		get => _money;
		set { _money = Mathf.Max(value, 0); EmitSignal(SignalName.StatsChanged); }
	}

	public int MaxTurns    		 { get; set; } = 15;
	public int CurrentTurn 		 { get; private set; } = 1;

	public int PollutionTarget   { get; set; } = 30;   //must get pollution <= this
	public int PopularityFloor   { get; set; } = 15;   //loose if popularity drops below

	//idk
	public override void _Ready()
	{
		Instance = this;
		Memory = new AdvisorMemory();
	}

	//pub helps
	//Idk if u were saying i needed to do this but its not hard so whatever

	public void ApplyStatChanges(int population = 0, int pollution = 0, int popularity = 0, int money = 0)
	{
		_population	 = Mathf.Max(_population  	+ population, 0);
		_pollution   = Mathf.Clamp(_pollution 	+ pollution, 0, 100);
		_popularity  = Mathf.Clamp(_popularity 	+ popularity, 0, 100);
		_money       = Mathf.Max(_money       	+ money, 0);

		EmitSignal(SignalName.StatsChanged);
	}

	public void AdvanceTurn()
	{
		CurrentTurn++;
		EmitSignal(SignalName.TurnAdvanced, CurrentTurn);

		if (_popularity <= PopularityFloor)
		{
			EmitSignal(SignalName.GameOver, "Gtfo!");
			return;
		}

		if (CurrentTurn > MaxTurns)
		{
			if (_pollution <= PollutionTarget)
				EmitSignal(SignalName.GameOver, "pluh"); 
			else
				EmitSignal(SignalName.GameOver, "fuck you everyone is dead");
		}
	}

	public void ResetGame()
	{
		_population = 100;
		_pollution  = 80;
		_popularity = 50;
		_money      = 500;
		CurrentTurn = 1;
		Memory.Reset();

		EmitSignal(SignalName.StatsChanged);
	}
}
