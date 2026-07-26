using Godot;
using System.Collections.Generic;

public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; }

    // Signals let u sub from any scene
    [Signal] public delegate void StatsChangedEventHandler();
    [Signal] public delegate void TurnAdvancedEventHandler(int currentTurn);
    [Signal] public delegate void GameOverEventHandler(string reason);
    [Signal] public delegate void AdvisorTagExecutedEventHandler(string tag);


    // --- UI STATE PERSISTENCE ---
    public bool IsPollutionDropdownOpen { get; set; } = false;
    public bool IsPopularityDropdownOpen { get; set; } = false;
    public bool IsMoneyDropdownOpen { get; set; } = false;

    public AdvisorMemory Memory { get; private set; }

    // --- INVENTORY & MAP STATE ---
    public Godot.Collections.Array<int> BuildingInventory = new Godot.Collections.Array<int>();
    public int[] PlacedBuildingCounts;
    public int[,] PlacedBuildingLevelCounts;

    // The raw text from the inspector (passed from BuildingManager)
    public Godot.Collections.Dictionary<int, string[]> BuildingInfo = new Godot.Collections.Dictionary<int, string[]>();

    // Clean, pre-calculated math numbers
    public Dictionary<int, Dictionary<int, BuildingYieldStats>> ParsedBuildingStats = new Dictionary<int, Dictionary<int, BuildingYieldStats>>();

    // --- STAT MODIFIER SYSTEM (NERFS / BUFFS) ---
    public enum ModifierType
    {
        Popularity,
        Pollution,
        Money,
        Population // Added Population so advisors can boost growth
    }

    public struct StatModifier
    {
        public string SourceName;  // e.g., "Smog & Pollution", "Overcrowding"
        public ModifierType Type;  // Popularity, Pollution, Money, or Population
        public int Value;          // Negative for Nerfs, Positive for Buffs
    }

    // --- NEW: PERSISTENT MODIFIERS DICTIONARY ---
    // Stores ongoing buffs/nerfs granted by advisors (Key: Unique ID)
    public Dictionary<string, StatModifier> PersistentModifiers = new Dictionary<string, StatModifier>();

    // --- RAW BUILDING YIELDS (For UI Dropdowns) ---
    public int BuildingDailyPopulation { get; private set; } = 0;
    public int BuildingDailyPollution { get; private set; } = 0;
    public int BuildingDailyPopularity { get; private set; } = 0;
    public int BuildingDailyMoney { get; private set; } = 0;

    // --- CORE STAT FIELDS ---
    private int _population = 100;
    private int _pollution = 80;    // Starts high
    private int _popularity = 50000;   // Default approval
    private int _money = 50000;
    private int _capacity = 0;

    private int _expectedDailyPopulation = 0;
    private int _expectedDailyPollution = 0;
    private int _expectedDailyPopularity = 0;
    private int _expectedDailyMoney = 0;
    private int _expectedDailyCapacity = 0;
    private int _lastTurnCapacity = 0;

    public static bool _firstTime = true;

    // --- ACTUAL STAT PROPERTIES ---
    public int Population
    {
        get => _population;
        set { _population = Mathf.Max(value, 0); EmitSignal(SignalName.StatsChanged); }
    }

    public int Pollution
    {
        get => _pollution;
        set { _pollution = Mathf.Max(value, 0); EmitSignal(SignalName.StatsChanged); }
    }

    public int Popularity
    {
        get => _popularity;
        set { _popularity = Mathf.Max(value, 0); EmitSignal(SignalName.StatsChanged); }
    }

    public int Money
    {
        get => _money;
        // REMOVED Mathf.Max so money can drop below 0!
        set { _money = value; EmitSignal(SignalName.StatsChanged); }
    }

    public int Capacity
    {
        get => _capacity;
        set { _capacity = Mathf.Max(value, 0); EmitSignal(SignalName.StatsChanged); }
    }

    // --- EXPECTED TOTAL YIELDS (Buildings + Modifiers) ---
    public int ExpectedDailyPopulation
    {
        get => _expectedDailyPopulation;
        set { _expectedDailyPopulation = value; EmitSignal(SignalName.StatsChanged); }
    }

    public int ExpectedDailyPollution
    {
        get => _expectedDailyPollution;
        set { _expectedDailyPollution = value; EmitSignal(SignalName.StatsChanged); }
    }

    public int ExpectedDailyPopularity
    {
        get => _expectedDailyPopularity;
        set { _expectedDailyPopularity = value; EmitSignal(SignalName.StatsChanged); }
    }

    public int ExpectedDailyMoney
    {
        get => _expectedDailyMoney;
        set { _expectedDailyMoney = value; EmitSignal(SignalName.StatsChanged); }
    }

    public int ExpectedDailyCapacity
    {
        get => _expectedDailyCapacity;
        set { _expectedDailyCapacity = value; EmitSignal(SignalName.StatsChanged); }
    }

    public int MaxTurns { get; set; } = 15;
    public int CurrentTurn { get; private set; } = 0;

    public int PollutionTarget { get; set; } = 30;   // Must get pollution <= this
    public int PopularityFloor { get; set; } = 15;   // Lose if popularity drops below

    public override void _Ready()
    {
        Instance = this;
        Memory = new AdvisorMemory();
    }

    // --- STACKABLE DAILY BUFFS ---
    public void AddPersistentModifier(string id, string sourceName, ModifierType type, int value)
    {
        // If the buff already exists, add the new value to the old one so it stacks!
        if (PersistentModifiers.ContainsKey(id))
        {
            StatModifier existingMod = PersistentModifiers[id];
            existingMod.Value += value;
            PersistentModifiers[id] = existingMod; // Save it back into the dictionary
        }
        else
        {
            // Otherwise, create a brand new one
            PersistentModifiers[id] = new StatModifier { SourceName = sourceName, Type = type, Value = value };
        }

        RecalculateDailyYields();
    }

    public void RemovePersistentModifier(string id)
    {
        if (PersistentModifiers.ContainsKey(id))
        {
            PersistentModifiers.Remove(id);
            RecalculateDailyYields();
        }
    }

    // -----------------------------------------
    // DYNAMIC MODIFIER CALCULATIONS
    // -----------------------------------------
    public List<StatModifier> GetActiveModifiers()
    {
        var modifiers = new List<StatModifier>();

        // 1. Pollution Penalty
        if (Pollution > 0)
        {
            int pollutionPenalty = -(Pollution / 10);

            if (pollutionPenalty != 0)
            {
                modifiers.Add(new StatModifier
                {
                    SourceName = "Smog & Pollution",
                    Type = ModifierType.Popularity,
                    Value = pollutionPenalty
                });
            }
        }

        // 2. Overcrowding Penalty
        int overflow = Population - Capacity;
        if (overflow > 0)
        {
            modifiers.Add(new StatModifier
            {
                SourceName = "Overcrowding",
                Type = ModifierType.Money,
                Value = -(overflow * 1000)
            });

            modifiers.Add(new StatModifier
            {
                SourceName = "Overcrowding",
                Type = ModifierType.Popularity,
                Value = -overflow
            });
        }

        // --- 3. NEW: DEBT PENALTY ---
        // --- 3. NEW: DEBT PENALTY ---
        if (Money < 0)
        {
            int debtPenalty = Money / 5000;
            if (debtPenalty == 0) debtPenalty = -1;

            modifiers.Add(new StatModifier
            {
                SourceName = "City Debt", // <--- CHANGED HERE
                Type = ModifierType.Popularity,
                Value = debtPenalty
            });
        }

        // Add the persistent advisor buffs to the list
        modifiers.AddRange(PersistentModifiers.Values);

        return modifiers;
    }

    public void SyncInitialCapacity()
    {
        _lastTurnCapacity = Capacity;
        ExpectedDailyCapacity = 0;
        EmitSignal(SignalName.StatsChanged);
    }

    public void ApplyStatChanges(int population = 0, int pollution = 0, int popularity = 0, int money = 0, int capacity = 0)
    {
        _population = Mathf.Max(_population + population, 0);
        _pollution = Mathf.Max(_pollution + pollution, 0);
        _popularity = Mathf.Max(_popularity + popularity, 0);

        // REMOVED Mathf.Max here so money can drop negative from events
        _money = _money + money;

        _capacity = Mathf.Max(_capacity + capacity, 0);

        EmitSignal(SignalName.StatsChanged);
    }

    public void AddBuildingToInventory(int buildingIndex, int amount)
    {
        if (buildingIndex >= 0 && buildingIndex < BuildingInventory.Count)
        {
            BuildingInventory[buildingIndex] += amount;
            EmitSignal(SignalName.StatsChanged); // Notifies UI that inventory changed
        }
        else
        {
            GD.PrintErr($"Failed to add building: Index {buildingIndex} is out of bounds!");
        }
    }

    public void AdvanceTurn()
    {
        RecalculateDailyYields();

        GD.Print($"\n=== END OF DAY {CurrentTurn} YIELD REPORT ===");
        GD.Print($"Population: {(ExpectedDailyPopulation >= 0 ? "+" : "")}{ExpectedDailyPopulation}");
        GD.Print($"Pollution:  {(ExpectedDailyPollution >= 0 ? "+" : "")}{ExpectedDailyPollution}");
        GD.Print($"Approval:   {(ExpectedDailyPopularity >= 0 ? "+" : "")}{ExpectedDailyPopularity}");
        GD.Print($"Money:      {(ExpectedDailyMoney >= 0 ? "+" : "")}{ExpectedDailyMoney}");
        GD.Print($"Capacity:   {(ExpectedDailyCapacity >= 0 ? "+" : "")}{ExpectedDailyCapacity}");
        GD.Print("===================================\n");

        ApplyStatChanges(
            population: ExpectedDailyPopulation,
            pollution: ExpectedDailyPollution,
            popularity: ExpectedDailyPopularity,
            money: ExpectedDailyMoney
        );

        CurrentTurn++;

        _lastTurnCapacity = Capacity;

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
                EmitSignal(SignalName.GameOver, "because of you, everyone is dead");
        }
    }

    // ==========================================
    // THE OPTIMIZED STAT PARSING SYSTEM
    // ==========================================

    public void InitializeBuildingStats()
    {
        ParsedBuildingStats.Clear();

        if (BuildingInfo == null || BuildingInfo.Count == 0)
        {
            GD.PrintErr("DEBUG FAILED: BuildingInfo is NULL or EMPTY! The GameManager is not receiving the dictionary from the Inspector.");
            return;
        }

        foreach (var kvp in BuildingInfo)
        {
            int typeIndex = kvp.Key;
            string[] infoArray = kvp.Value;

            var levelDict = new Dictionary<int, BuildingYieldStats>();

            for (int lvl = 0; lvl <= 4; lvl++)
            {
                string rawStats = "";

                if (lvl == 0 && infoArray.Length >= 2)
                {
                    rawStats = infoArray[1];
                }
                else if (lvl > 0)
                {
                    int targetStatsIndex = 4 + lvl;
                    if (infoArray.Length > targetStatsIndex)
                        rawStats = infoArray[targetStatsIndex];
                    else if (infoArray.Length >= 2)
                        rawStats = infoArray[1];
                }

                levelDict[lvl] = ParseYieldStats(rawStats);
            }

            ParsedBuildingStats[typeIndex] = levelDict;
        }
        GD.Print("SUCCESS: GameManager pre-calculated all building stats!");
    }

    private BuildingYieldStats ParseYieldStats(string statsString)
    {
        BuildingYieldStats result = new BuildingYieldStats();
        if (string.IsNullOrWhiteSpace(statsString)) return result;

        string[] stats = statsString.Split(',', System.StringSplitOptions.RemoveEmptyEntries);

        foreach (string stat in stats)
        {
            string[] kv = stat.Split(':');

            if (kv.Length == 2)
            {
                string statName = kv[0].Trim().ToLower();
                string valStr = kv[1].Trim().ToLower();

                bool isK = valStr.Contains("k");

                valStr = valStr.Replace("+", "")
                               .Replace("$", "")
                               .Replace("k", "")
                               .Replace("/day", "")
                               .Trim();

                if (float.TryParse(valStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float parsedVal))
                {
                    int finalValue = (int)(isK ? parsedVal * 1000f : parsedVal);

                    if (statName.Contains("upkeep"))
                    {
                        result.Money -= finalValue;
                    }
                    else if (statName.Contains("money"))
                    {
                        result.Money += finalValue;
                    }
                    else if (statName.Contains("capacity"))
                    {
                        result.Capacity += finalValue;
                    }
                    else if (statName.Contains("approval"))
                    {
                        result.Popularity += finalValue;
                    }
                    else if (statName.Contains("pollution"))
                    {
                        result.Pollution += finalValue;
                    }
                    else if (statName.Contains("population"))
                    {
                        result.Population += finalValue;
                    }
                }
            }
        }
        return result;
    }

    public void RecalculateDailyYields()
    {
        int totalPop = 0, totalPol = 0, totalPopu = 0, totalMon = 0, totalCap = 0;

        if (PlacedBuildingLevelCounts != null)
        {
            int typeCount = PlacedBuildingLevelCounts.GetLength(0);
            int levelCount = PlacedBuildingLevelCounts.GetLength(1);

            for (int type = 0; type < typeCount; type++)
            {
                if (!ParsedBuildingStats.TryGetValue(type, out var levelDict)) continue;

                for (int lvl = 0; lvl < levelCount; lvl++)
                {
                    int countOnMap = PlacedBuildingLevelCounts[type, lvl];
                    if (countOnMap <= 0) continue;

                    if (levelDict.TryGetValue(lvl, out BuildingYieldStats stats))
                    {
                        totalPop += (stats.Population * countOnMap);
                        totalPol += (stats.Pollution * countOnMap);
                        totalPopu += (stats.Popularity * countOnMap);
                        totalMon += (stats.Money * countOnMap);
                        totalCap += (stats.Capacity * countOnMap);
                    }
                }
            }
        }

        BuildingDailyPopulation = totalPop;
        BuildingDailyPollution = totalPol;
        BuildingDailyPopularity = totalPopu;
        BuildingDailyMoney = totalMon;

        Capacity = totalCap;
        ExpectedDailyCapacity = Capacity - _lastTurnCapacity;

        int modPop = 0, modPopu = 0, modPol = 0, modMon = 0;
        foreach (var mod in GetActiveModifiers())
        {
            if (mod.Type == ModifierType.Population) modPop += mod.Value;
            if (mod.Type == ModifierType.Popularity) modPopu += mod.Value;
            if (mod.Type == ModifierType.Pollution) modPol += mod.Value;
            if (mod.Type == ModifierType.Money) modMon += mod.Value;
        }

        ExpectedDailyPopulation = totalPop + modPop;
        ExpectedDailyPollution = totalPol + modPol;
        ExpectedDailyPopularity = totalPopu + modPopu;
        ExpectedDailyMoney = totalMon + modMon;
    }

    public void NotifyStatsChanged()
    {
        EmitSignal(SignalName.StatsChanged);
    }

    public void ResetGame()
    {
        _population = 100;
        _pollution = 80;
        _popularity = 50;
        _money = 500;
        _capacity = 0;

        _expectedDailyPopulation = 0;
        _expectedDailyPollution = 0;
        _expectedDailyPopularity = 0;
        _expectedDailyMoney = 0;
        _expectedDailyCapacity = 0;

        _lastTurnCapacity = 0;

        BuildingDailyPopulation = 0;
        BuildingDailyPollution = 0;
        BuildingDailyPopularity = 0;
        BuildingDailyMoney = 0;

        IsPollutionDropdownOpen = false;
        IsPopularityDropdownOpen = false;
        IsMoneyDropdownOpen = false;
        _firstTime = true;

        CurrentTurn = 0;
        Memory.Reset();

        PersistentModifiers.Clear();

        RecalculateDailyYields();
        EmitSignal(SignalName.StatsChanged);
    }

    public struct BuildingYieldStats
    {
        public int Population;
        public int Pollution;
        public int Popularity;
        public int Money;
        public int Capacity;
    }
}