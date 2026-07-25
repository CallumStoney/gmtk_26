using Godot;
using System.Collections.Generic;

public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; }

    //signals let u sub from any scene
    [Signal] public delegate void StatsChangedEventHandler();
    [Signal] public delegate void TurnAdvancedEventHandler(int currentTurn);
    [Signal] public delegate void GameOverEventHandler(string reason);
    [Signal] public delegate void AdvisorTagExecutedEventHandler(string tag);

    public AdvisorMemory Memory { get; private set; }

    // --- INVENTORY & MAP STATE ---
    public Godot.Collections.Array<int> BuildingInventory = new Godot.Collections.Array<int>();
    public int[] PlacedBuildingCounts;
    public int[,] PlacedBuildingLevelCounts;

    // The raw text from the inspector (passed from BuildingManager)
    public Godot.Collections.Dictionary<int, string[]> BuildingInfo = new Godot.Collections.Dictionary<int, string[]>();

    // NEW: The clean, pre-calculated math numbers
    public Dictionary<int, Dictionary<int, BuildingYieldStats>> ParsedBuildingStats = new Dictionary<int, Dictionary<int, BuildingYieldStats>>();


    private int _population = 100;
    private int _pollution = 80;   //this might be a tad high idk
    private int _popularity = 50;   //they start not giving a shit abt u
    private int _money = 50000000;
    private int _capacity = 0; //have at defaultvalue

    private int _expectedDailyPopulation = 0;
    private int _expectedDailyPollution = 0;
    private int _expectedDailyPopularity = 0;
    private int _expectedDailyMoney = 0;
    private int _expectedDailyCapacity = 0;
    private int _lastTurnCapacity = 0;

    // --- ACTUAL STATS ---
    public int Population
    {
        get => _population;
        set { _population = Mathf.Max(value, 0); EmitSignal(SignalName.StatsChanged); }
    }

    public int Pollution
    {
        get => _pollution;
        // THE FIX: Changed from Mathf.Clamp to Mathf.Max so it can climb above 100!
        set { _pollution = Mathf.Max(value, 0); EmitSignal(SignalName.StatsChanged); }
    }

    public int Popularity
    {
        get => _popularity;
        // THE FIX: Changed from Mathf.Clamp to Mathf.Max so it can climb above 100!
        set { _popularity = Mathf.Max(value, 0); EmitSignal(SignalName.StatsChanged); }
    }

    public int Money
    {
        get => _money;
        set { _money = Mathf.Max(value, 0); EmitSignal(SignalName.StatsChanged); }
    }

    public int Capacity
    {
        get => _capacity;
        set { _capacity = Mathf.Max(value, 0); EmitSignal(SignalName.StatsChanged); }
    }

    // --- EXPECTED YIELDS ---
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
    public int CurrentTurn { get; private set; } = 1;

    public int PollutionTarget { get; set; } = 30;   //must get pollution <= this
    public int PopularityFloor { get; set; } = 15;   //loose if popularity drops below


    public override void _Ready()
    {
        Instance = this;
        Memory = new AdvisorMemory();
    }

    /// <summary>
    /// Call this immediately after loading the initial map state so that Day 1 starts with
    /// the accurate starting map capacity, without displaying a fake daily delta.
    /// </summary>
    public void SyncInitialCapacity()
    {
        _lastTurnCapacity = Capacity;
        ExpectedDailyCapacity = 0;
        EmitSignal(SignalName.StatsChanged);
    }

    public void ApplyStatChanges(int population = 0, int pollution = 0, int popularity = 0, int money = 0, int capacity = 0)
    {
        _population = Mathf.Max(_population + population, 0);

        // THE FIX: Allows infinite growth instead of stopping at 100
        _pollution = Mathf.Max(_pollution + pollution, 0);
        _popularity = Mathf.Max(_popularity + popularity, 0);

        _money = Mathf.Max(_money + money, 0);
        _capacity = Mathf.Max(_capacity + capacity, 0);

        EmitSignal(SignalName.StatsChanged);
    }

    public void AdvanceTurn()
    {
        RecalculateDailyYields();

        GD.Print($"\n=== END OF DAY {CurrentTurn} YIELD REPORT ===");
        GD.Print($"Population: {(ExpectedDailyPopulation >= 0 ? "+" : "")}{ExpectedDailyPopulation}");
        GD.Print($"Pollution:  {(ExpectedDailyPollution >= 0 ? "+" : "")}{ExpectedDailyPollution}");
        GD.Print($"Approval:   {(ExpectedDailyPopularity >= 0 ? "+" : "")}{ExpectedDailyPopularity}");
        GD.Print($"Money:      {(ExpectedDailyMoney >= 0 ? "+" : "")}{ExpectedDailyMoney}");

        // Prints the difference (e.g. +12)
        GD.Print($"Capacity:   {(ExpectedDailyCapacity >= 0 ? "+" : "")}{ExpectedDailyCapacity}");
        GD.Print("===================================\n");

        ApplyStatChanges(
            population: ExpectedDailyPopulation,
            pollution: ExpectedDailyPollution,
            popularity: ExpectedDailyPopularity,
            money: ExpectedDailyMoney
        // Note: capacity is still intentionally left out here!
        );

        CurrentTurn++;

        // NEW: Remember today's capacity so we can calculate the difference tomorrow!
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

        // --- DEBUG BLOCK START ---
        if (BuildingInfo == null || BuildingInfo.Count == 0)
        {
            GD.PrintErr("DEBUG FAILED: BuildingInfo is NULL or EMPTY! The GameManager is not receiving the dictionary from the Inspector.");
            return;
        }

        GD.Print("\n--- DEBUG: RAW BUILDING INFO FROM INSPECTOR ---");
        foreach (var kvp in BuildingInfo)
        {
            int typeIndex = kvp.Key;
            string[] infoArray = kvp.Value;

            GD.Print($"Building Type [{typeIndex}]: Found {infoArray.Length} strings.");
            for (int i = 0; i < infoArray.Length; i++)
            {
                GD.Print($"   Line {i}: \"{infoArray[i]}\"");
            }
        }
        GD.Print("-----------------------------------------------\n");
        // --- DEBUG BLOCK END ---


        // (The rest of the parsing code continues below)
        foreach (var kvp in BuildingInfo)
        {
            int typeIndex = kvp.Key;
            string[] infoArray = kvp.Value;

            var levelDict = new Dictionary<int, BuildingYieldStats>();

            // Assuming max 4 upgrades (so 5 levels total: 0, 1, 2, 3, 4)
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
                        rawStats = infoArray[1]; // Fallback to level 0 stats if missing
                }

                // Parse it once and store it!
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

        // Split by comma to separate the different stats
        string[] stats = statsString.Split(',', System.StringSplitOptions.RemoveEmptyEntries);

        foreach (string stat in stats)
        {
            // Split by colon to separate the name (e.g., "Money") from the value (e.g., "+$5k/day")
            string[] kv = stat.Split(':');

            if (kv.Length == 2)
            {
                string statName = kv[0].Trim().ToLower();
                string valStr = kv[1].Trim().ToLower();

                // 1. Check if the value contains 'k' (thousands)
                bool isK = valStr.Contains("k");

                // 2. Aggressively clean out all symbols and letters, leaving only numbers, decimals, and minus signs
                valStr = valStr.Replace("+", "")
                               .Replace("$", "")
                               .Replace("k", "")
                               .Replace("/day", "")
                               .Trim();

                // 3. Parse the number as a float (so it safely handles decimals like "2.5")
                if (float.TryParse(valStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float parsedVal))
                {
                    // Convert 'k' to thousands if needed
                    int finalValue = (int)(isK ? parsedVal * 1000f : parsedVal);

                    // 4. Map the extracted value to the correct stat!
                    if (statName.Contains("upkeep"))
                    {
                        result.Money -= finalValue; // Upkeep subtracts money!
                    }
                    else if (statName.Contains("money"))
                    {
                        result.Money += finalValue;
                    }
                    else if (statName.Contains("capacity"))
                    {
                        result.Capacity += finalValue;
                    }
                    else if (statName.Contains("approval")) // THE FIX: Correctly maps "Approval: +4" to the Popularity variable
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
                // Skip if this building type isn't in our pre-calculated dictionary
                if (!ParsedBuildingStats.TryGetValue(type, out var levelDict)) continue;

                for (int lvl = 0; lvl < levelCount; lvl++)
                {
                    int countOnMap = PlacedBuildingLevelCounts[type, lvl];
                    if (countOnMap <= 0) continue;

                    // Grab the pre-calculated math struct! No strings involved.
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

        // (This goes at the very bottom of RecalculateDailyYields)
        ExpectedDailyPopulation = totalPop;
        ExpectedDailyPollution = totalPol;
        ExpectedDailyPopularity = totalPopu;
        ExpectedDailyMoney = totalMon;

        // 1. Set the absolute map capacity (No base 100 filler)
        Capacity = totalCap;

        // 2. The "Daily" capacity is just whatever changed today!
        ExpectedDailyCapacity = Capacity - _lastTurnCapacity;
    }

    public void ResetGame()
    {
        _population = 100;
        _pollution = 80;
        _popularity = 50;
        _money = 500;
        _capacity = 0; // Removed 100 filler

        _expectedDailyPopulation = 0;
        _expectedDailyPollution = 0;
        _expectedDailyPopularity = 0;
        _expectedDailyMoney = 0;
        _expectedDailyCapacity = 0;

        _lastTurnCapacity = 0; // Reset this too!

        CurrentTurn = 1;
        Memory.Reset();

        RecalculateDailyYields();
        EmitSignal(SignalName.StatsChanged);
    }

    // The container for our math
    public struct BuildingYieldStats
    {
        public int Population;
        public int Pollution;
        public int Popularity;
        public int Money;
        public int Capacity;
    }
}