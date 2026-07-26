using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class RoomScene : Control
{
    private static readonly int[] EventCountWeights = { 0, 45, 35, 20 };

    // --- ADDED: Exported array for the 5 advisor regions ---
    public Rect2[] AdvisorPortraitRegions = new Rect2[]
    {
        // A Rect2 takes 4 numbers: (X position, Y position, Width, Height)
        new Rect2(12, 24, 57, 72),   // Index 0: Activist
        new Rect2(117, 24, 54, 75), // Index 2: Rich Man
        new Rect2(207, 24, 63, 72),  // Index 1: Construction Worker
        new Rect2(9, 123, 63, 69), // Index 3: Anonymous
        new Rect2(114, 123, 63, 69)  // Index 4: Factory Owner
    };

    // --- CHANGED: Now a Sprite2D instead of a ColorRect ---
    private Sprite2D _advisorPortrait;
    private Label _advisorNameLabel;
    private Label _dialogueLabel;

    private TextureButton _acceptButton;
    private TextureButton _declineButton;

    private Label _acceptEffectsLabel;
    private Label _declineEffectsLabel;

    private Label _responseLabel;

    private TextureButton _nextButton;
    private Button _endTurnButton;

    //state
    private List<AdvisorEvent> _eventPool = new();
    private Queue<AdvisorEvent> _turnEvents = new();
    private AdvisorEvent _currentEvent;
    private bool _awaitingChoice;

    public override void _Ready()
    {
        GrabNodeReferences();
        BuildEventPool();
        CallDeferred(MethodName.InitializeRoom);
    }

    private void InitializeRoom()
    {
        if (GameManager.Instance == null)
        {
            GD.PrintErr("CRITICAL: RoomScene could not find GameManager.Instance!");
            return;
        }
        ConnectSignals();
        StartTurn();
    }

    private void GrabNodeReferences()
    {
        // --- CHANGED: Getting as Sprite2D ---
        _advisorPortrait = GetNode<Sprite2D>("Monitor/NormalUI/AdvisorPortrait");

        _advisorNameLabel = GetNode<Label>("Monitor/NormalUI/AdvisorNameLabel");
        _dialogueLabel = GetNode<Label>("Monitor/NormalUI/DialogueLabel");

        _acceptButton = GetNode<TextureButton>("Monitor/NormalUI/AcceptButton");
        _declineButton = GetNode<TextureButton>("Monitor/NormalUI/DeclineButton");

        _acceptEffectsLabel = GetNode<Label>("Monitor/NormalUI/AcceptEffectsLabel");
        _declineEffectsLabel = GetNode<Label>("Monitor/NormalUI/DeclineEffectsLabel");

        _responseLabel = GetNode<Label>("Monitor/NormalUI/ResponseLabel");

        _nextButton = GetNode<TextureButton>("Monitor/NormalUI/NextButton");
        _endTurnButton = GetNode<Button>("Monitor/NormalUI/EndTurnButton");
    }

    private void ConnectSignals()
    {
        _acceptButton.Pressed += OnAcceptPressed;
        _declineButton.Pressed += OnDeclinePressed;
        _nextButton.Pressed += OnNextPressed;
        _endTurnButton.Pressed += OnEndTurnPressed;

        GameManager.Instance.GameOver += OnGameOver;
    }

    private void StartTurn()
    {
        _turnEvents.Clear();
        var gm = GameManager.Instance;

        var eligible = _eventPool.Where(e => IsEligible(e, gm)).ToList();

        int count = RollEventCount();
        count = Mathf.Min(count, eligible.Count);

        ShuffleList(eligible);

        for (int i = 0; i < count; i++)
            _turnEvents.Enqueue(eligible[i]);

        if (count == 0)
        {
            ShowNoEvents();
            return;
        }

        ShowNextEvent();
    }

    private int RollEventCount()
    {
        int roll = (int)(GD.Randi() % 100);
        int cumulative = 0;
        for (int i = 0; i < EventCountWeights.Length; i++)
        {
            cumulative += EventCountWeights[i];
            if (roll < cumulative)
                return i;
        }
        return EventCountWeights.Length - 1;
    }

    private bool IsEligible(AdvisorEvent e, GameManager gm)
    {
        if (e.IsOneShot && gm.Memory.HasDecided(e.Id))
            return false;

        if (!string.IsNullOrEmpty(e.PrerequisiteId))
        {
            if (!gm.Memory.HasDecided(e.PrerequisiteId))
                return false;

            if (e.RequiresAccepted.HasValue)
            {
                bool wasAccepted = gm.Memory.WasAccepted(e.PrerequisiteId);
                if (wasAccepted != e.RequiresAccepted.Value)
                    return false;
            }

            int turnsSince = gm.Memory.TurnsSinceDecision(e.PrerequisiteId, gm.CurrentTurn);
            if (turnsSince < e.MinTurnDelay)
                return false;
        }
        return true;
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
        _dialogueLabel.Text = _currentEvent.DialogueText;

        // --- ADDED: Update the portrait region based on the speaker ---
        UpdateAdvisorPortrait(_currentEvent.AdvisorName);

        _acceptEffectsLabel.Text = GetEffectsString(
            _currentEvent.AcceptPopulation, _currentEvent.AcceptPollution,
            _currentEvent.AcceptPopularity, _currentEvent.AcceptMoney, _currentEvent.AcceptBuildingIndex,
            _currentEvent.AcceptBuildingCount, _currentEvent.AcceptModifierName, _currentEvent.AcceptModifierType, _currentEvent.AcceptModifierValue);

        _declineEffectsLabel.Text = GetEffectsString(
            _currentEvent.DeclinePopulation, _currentEvent.DeclinePollution,
            _currentEvent.DeclinePopularity, _currentEvent.DeclineMoney, _currentEvent.DeclineBuildingIndex,
            _currentEvent.DeclineBuildingCount, _currentEvent.DeclineModifierName, _currentEvent.DeclineModifierType, _currentEvent.DeclineModifierValue);

        _acceptButton.Visible = true;
        _declineButton.Visible = true;
        _acceptEffectsLabel.Visible = true;
        _declineEffectsLabel.Visible = true;

        _responseLabel.Text = "";
        _nextButton.Visible = false;
        _endTurnButton.Visible = false;
        _awaitingChoice = true;
    }

    // --- ADDED: Helper method to handle portrait swapping ---
    private void UpdateAdvisorPortrait(string advisorName)
    {
        if (_advisorPortrait == null || AdvisorPortraitRegions == null || AdvisorPortraitRegions.Length < 5)
            return;

        // Ensure region filtering is turned on
        _advisorPortrait.RegionEnabled = true;

        switch (advisorName)
        {
            case "Activist":
                _advisorPortrait.RegionRect = AdvisorPortraitRegions[0];
                break;
            case "Rich Man":
                _advisorPortrait.RegionRect = AdvisorPortraitRegions[1];
                break;
            case "Construction Worker":
                _advisorPortrait.RegionRect = AdvisorPortraitRegions[2];
                break;
            case "Anonymous":
                _advisorPortrait.RegionRect = AdvisorPortraitRegions[3];
                break;
            case "Factory Owner":
                _advisorPortrait.RegionRect = AdvisorPortraitRegions[4];
                break;
            default:
                // Set to 0,0,0,0 when no-one is speaking
                _advisorPortrait.RegionRect = new Rect2(0, 0, 0, 0);
                break;
        }
    }

    private string GetEffectsString(int pop, int pol, int popu, int mon, int bldIndex, int bldCount, string modName = "", GameManager.ModifierType modType = GameManager.ModifierType.Popularity, int modValue = 0)
    {
        List<string> effects = new List<string>();

        if (bldCount > 0 && bldIndex >= 0)
        {
            string buildingName = "Building";
            if (GameManager.Instance.BuildingInfo.TryGetValue(bldIndex, out string[] infoArray) && infoArray.Length > 0)
            {
                buildingName = infoArray[0];
            }
            effects.Add($"+{bldCount} {buildingName}");
        }

        if (pop != 0) effects.Add($"Pop: {(pop > 0 ? "+" : "")}{pop}");
        if (pol != 0) effects.Add($"Pollution: {(pol > 0 ? "+" : "")}{pol}");
        if (popu != 0) effects.Add($"Approval: {(popu > 0 ? "+" : "")}{popu}");

        if (mon != 0)
        {
            string sign = mon > 0 ? "+" : "";
            float absMon = Mathf.Abs(mon);
            if (absMon >= 1000)
                effects.Add($"Money: {sign}{(mon / 1000f).ToString("0.#")}k");
            else
                effects.Add($"Money: {sign}{mon}");
        }

        if (modValue != 0 && !string.IsNullOrEmpty(modName))
        {
            string sign = modValue > 0 ? "+" : "";
            string categoryName = modType switch
            {
                GameManager.ModifierType.Popularity => "Approval",
                GameManager.ModifierType.Pollution => "Pollution",
                GameManager.ModifierType.Money => "Income",
                GameManager.ModifierType.Population => "Pop Growth",
                _ => modType.ToString()
            };

            effects.Add($"Daily {categoryName}: {sign}{modValue}");
        }

        if (effects.Count > 0)
        {
            return $"[{string.Join(", ", effects)}]";
        }

        return "";
    }

    private void ShowNoEvents()
    {
        _advisorNameLabel.Text = "";
        UpdateAdvisorPortrait(""); // --- ADDED: Clears the portrait ---
        _dialogueLabel.Text = "A quiet day. Nothing demands your attention for better or worse.";
        HideChoiceUI();
        _responseLabel.Text = "";
        _nextButton.Visible = false;
        _endTurnButton.Visible = true;
    }

    private void ShowNoMoreEvents()
    {
        _advisorNameLabel.Text = "";
        UpdateAdvisorPortrait(""); // --- ADDED: Clears the portrait ---
        _dialogueLabel.Text = "Nothing else demands your attention today. End the day when you are ready.";
        HideChoiceUI();
        _responseLabel.Text = "";
        _nextButton.Visible = false;
        _endTurnButton.Visible = true;
    }

    private void OnAcceptPressed()
    {
        _acceptButton.ReleaseFocus();

        if (!_awaitingChoice || _currentEvent == null) return;
        _awaitingChoice = false;

        var gm = GameManager.Instance;

        gm.ApplyStatChanges(
            population: _currentEvent.AcceptPopulation,
            pollution: _currentEvent.AcceptPollution,
            popularity: _currentEvent.AcceptPopularity,
            money: _currentEvent.AcceptMoney
        );

        if (_currentEvent.AcceptBuildingCount > 0 && _currentEvent.AcceptBuildingIndex >= 0)
        {
            gm.AddBuildingToInventory(_currentEvent.AcceptBuildingIndex, _currentEvent.AcceptBuildingCount);
        }

        if (!string.IsNullOrEmpty(_currentEvent.AcceptModifierId) && _currentEvent.AcceptModifierValue != 0)
        {
            gm.AddPersistentModifier(_currentEvent.AcceptModifierId, _currentEvent.AcceptModifierName, _currentEvent.AcceptModifierType, _currentEvent.AcceptModifierValue);
        }

        if (!string.IsNullOrEmpty(_currentEvent.AcceptRemoveModifierId))
        {
            gm.RemovePersistentModifier(_currentEvent.AcceptRemoveModifierId);
        }

        gm.Memory.RecordDecision(_currentEvent.Id, accepted: true, gm.CurrentTurn);
        ExecuteTags(_currentEvent.AcceptTags);
        ShowResponse(_currentEvent.AcceptResponse);

        gm.RecalculateDailyYields();
        gm.NotifyStatsChanged();
    }

    private void OnDeclinePressed()
    {
        _declineButton.ReleaseFocus();

        if (!_awaitingChoice || _currentEvent == null) return;
        _awaitingChoice = false;

        var gm = GameManager.Instance;

        gm.ApplyStatChanges(
            population: _currentEvent.DeclinePopulation,
            pollution: _currentEvent.DeclinePollution,
            popularity: _currentEvent.DeclinePopularity,
            money: _currentEvent.DeclineMoney
        );

        if (_currentEvent.DeclineBuildingCount > 0 && _currentEvent.DeclineBuildingIndex >= 0)
        {
            gm.AddBuildingToInventory(_currentEvent.DeclineBuildingIndex, _currentEvent.DeclineBuildingCount);
        }

        if (!string.IsNullOrEmpty(_currentEvent.DeclineModifierId) && _currentEvent.DeclineModifierValue != 0)
        {
            gm.AddPersistentModifier(_currentEvent.DeclineModifierId, _currentEvent.DeclineModifierName, _currentEvent.DeclineModifierType, _currentEvent.DeclineModifierValue);
        }

        if (!string.IsNullOrEmpty(_currentEvent.DeclineRemoveModifierId))
        {
            gm.RemovePersistentModifier(_currentEvent.DeclineRemoveModifierId);
        }

        gm.Memory.RecordDecision(_currentEvent.Id, accepted: false, gm.CurrentTurn);
        ExecuteTags(_currentEvent.DeclineTags);
        ShowResponse(_currentEvent.DeclineResponse);

        gm.RecalculateDailyYields();
        gm.NotifyStatsChanged();
    }

    private void ExecuteTags(List<string> tags)
    {
        if (tags == null || tags.Count == 0) return;

        var gm = GameManager.Instance;
        foreach (string tag in tags)
        {
            GD.Print($"[AdvisorTag] {tag}");
            gm.EmitSignal(GameManager.SignalName.AdvisorTagExecuted, tag);
        }
    }

    private void ShowResponse(string text)
    {
        _responseLabel.Text = text;
        HideChoiceUI();

        if (_turnEvents.Count > 0)
        {
            _nextButton.Visible = true;
        }
        else
        {
            _endTurnButton.Visible = true;
        }
    }

    private void HideChoiceUI()
    {
        _acceptButton.Visible = false;
        _declineButton.Visible = false;
        _acceptEffectsLabel.Visible = false;
        _declineEffectsLabel.Visible = false;
    }

    private void OnNextPressed()
    {
        _nextButton.ReleaseFocus();
        ShowNextEvent();
    }

    private void OnEndTurnPressed()
    {
        _endTurnButton.ReleaseFocus();

        var gm = GameManager.Instance;

        gm.AdvanceTurn();
        gm.RecalculateDailyYields();
        gm.NotifyStatsChanged();

        if (gm.Popularity <= 0)
        {
            GD.Print("you lost due to popularity");
            gm.ResetGame();
            GetTree().ChangeSceneToFile("res://Scenes/HomeScreen.tscn");
            return;
        }

        if (gm.CurrentTurn == 15)
        {
            if (gm.Pollution <= 0)
            {
                GD.Print("u won");
            }
            else
            {
                GD.Print("u lose");
            }
            gm.ResetGame();
            GetTree().ChangeSceneToFile("res://Scenes/HomeScreen.tscn");
            return;
        }

        StartTurn();

        GD.Print(gm.CurrentTurn);
    }

    private void OnGameOver(string reason)
    {
        _advisorNameLabel.Text = "GAME OVER";
        UpdateAdvisorPortrait(""); // --- ADDED: Clears the portrait ---
        _dialogueLabel.Text = reason;
        HideChoiceUI();
        _responseLabel.Text = "";
        _nextButton.Visible = false;
        _endTurnButton.Visible = false;
    }

    private static void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = (int)(GD.Randi() % (uint)(i + 1));
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void BuildEventPool()
    {
        _eventPool = new List<AdvisorEvent>
    {
        // ==========================================
        // RECURRING: ACTIVIST
        // ==========================================
        new()
        {
            Id              = "act_clean_up",
            AdvisorName     = "Activist",
            DialogueText    = "Mayor, our people can barely breathe! We organized a volunteer cleanup crew, but we need city funds for supplies. Help us out!",
            AcceptText      = "Fund the cleanup",
            DeclineText     = "We lack the budget",
            AcceptResponse  = "Thank you! The streets look a bit brighter today.",
            DeclineResponse = "They protest outside city hall instead.",
            AcceptMoney     = -10000, AcceptPollution = -5, AcceptPopularity = 5,
            DeclinePopularity = -5
        },
        new()
        {
            Id              = "act_park_grant",
            AdvisorName     = "Activist",
            DialogueText    = "We secured a grassroots grant to build a Nature Park, but the city needs to approve the zoning and clear the concrete first.",
            AcceptText      = "Approve zoning",
            DeclineText     = "Keep it paved",
            AcceptResponse  = "The community celebrates as the saplings arrive.",
            DeclineResponse = "Concrete wins again. We won't forget this.",
            AcceptMoney     = -5000, AcceptPopularity = 8, AcceptPollution = -2,
            AcceptBuildingIndex = 6, AcceptBuildingCount = 1, // Nature Park
            DeclinePopularity = -3
        },
        new()
        {
            Id              = "act_protest",
            AdvisorName     = "Activist",
            DialogueText    = "The smog is suffocating our children! We are blocking the main industrial highway until you commit funds to clean air subsidies!",
            AcceptText      = "Pay the subsidies",
            DeclineText     = "Send the police",
            AcceptResponse  = "The blockade lifts, but the treasury takes a hit.",
            DeclineResponse = "The strike is broken up forcefully. The public is furious.",
            AcceptMoney     = -25000, AcceptPollution = -8, AcceptPopularity = 5,
            DeclinePopularity = -15
        },

        // ==========================================
        // RECURRING: CONSTRUCTION WORKER
        // ==========================================
        new()
        {
            Id              = "cw_surplus_houses",
            AdvisorName     = "Construction Worker",
            DialogueText    = "Hey boss, we got extra lumber and brick from the last job. Pay our overtime and my boys can throw up a couple houses real quick.",
            AcceptText      = "Pay overtime",
            DeclineText     = "No extra hours",
            AcceptResponse  = "The boys work through the night! Houses are ready to place.",
            DeclineResponse = "The materials just sit in the yard then.",
            AcceptMoney     = -15000, AcceptPopularity = 3,
            AcceptBuildingIndex = 0, AcceptBuildingCount = 2, // 2 Houses
            DeclinePopularity = -2
        },
        new()
        {
            Id              = "cw_apartments",
            AdvisorName     = "Construction Worker",
            DialogueText    = "There's a high demand for dense housing downtown. My crew is ready to build a massive Apartment complex if you greenlight the funds.",
            AcceptText      = "Greenlight it",
            DeclineText     = "Too expensive",
            AcceptResponse  = "A massive crane arrives downtown.",
            DeclineResponse = "Shame. The crew goes looking for private contracts instead.",
            AcceptMoney     = -35000, AcceptPollution = 5,
            AcceptBuildingIndex = 5, AcceptBuildingCount = 1, // Apartment
            DeclinePopularity = -4
        },
        new()
        {
            Id              = "cw_repair",
            AdvisorName     = "Construction Worker",
            DialogueText    = "Hey boss. The industrial pipes are crumbling. We either spend the money to fix them now, or the smog leaks are gonna get way worse.",
            AcceptText      = "Fix the pipes",
            DeclineText     = "Ignore it",
            AcceptResponse  = "Pipes patched. Crisis averted.",
            DeclineResponse = "The pipes burst a few days later, spewing thick black smoke.",
            AcceptMoney     = -20000,
            DeclinePollution = 15, DeclinePopularity = -5
        },

        // ==========================================
        // RECURRING: RICH MAN
        // ==========================================
        new()
        {
            Id              = "rm_donation",
            AdvisorName     = "Rich Man",
            DialogueText    = "Mayor. I'm hosting a charity gala and I'm willing to make a generous 'donation' to the city... provided you publicly praise my businesses.",
            AcceptText      = "Take the money",
            DeclineText     = "I cannot be bought",
            AcceptResponse  = "Your bank account swells, but the working class sees right through it.",
            DeclineResponse = "You keep your integrity. He smirks and leaves.",
            AcceptMoney     = 40000, AcceptPopularity = -8,
            DeclinePopularity = 5
        },
        new()
        {
            Id              = "rm_restaurant",
            AdvisorName     = "Rich Man",
            DialogueText    = "I want to open a high-end Restaurant downtown. Subsidize the permits, and it's yours to place. We need the *right* demographic in this city.",
            AcceptText      = "Subsidize it",
            DeclineText     = "No handouts",
            AcceptResponse  = "The elite have a new place to dine.",
            DeclineResponse = "He threatens to build it in the next town over.",
            AcceptMoney     = -15000,
            AcceptBuildingIndex = 9, AcceptBuildingCount = 1, // Restaurant
            DeclinePopularity = 2
        },
        new()
        {
            Id              = "rm_tax_break",
            AdvisorName     = "Rich Man",
            DialogueText    = "Mayor, my colleagues and I are feeling the squeeze. Grant us a corporate tax break, and we will stimulate the economy.",
            AcceptText      = "Grant the break",
            DeclineText     = "Pay your taxes",
            AcceptResponse  = "The wealthy get wealthier. The public notices the budget cuts.",
            DeclineResponse = "He huffs. 'We will remember this at re-election.'",
            AcceptMoney     = -30000, AcceptPopularity = -10, AcceptPollution = 5,
            DeclineMoney    = 15000, DeclinePopularity = 5
        },

        // ==========================================
        // RECURRING: ANONYMOUS
        // ==========================================
        new()
        {
            Id              = "anon_slush_fund",
            AdvisorName     = "Anonymous",
            DialogueText    = "Psst. Found a misplaced briefcase full of cash. It can go into the treasury, but people might ask where it came from. No names.",
            AcceptText      = "Deposit it quietly",
            DeclineText     = "Turn it over to police",
            AcceptResponse  = "The budget looks great. The public trust? Not so much.",
            DeclineResponse = "A boring, but safe, choice.",
            AcceptMoney     = 50000, AcceptPopularity = -15,
            DeclinePopularity = 2
        },
        new()
        {
            Id              = "anon_info_broker",
            AdvisorName     = "Anonymous",
            DialogueText    = "Information is power. I have dirt on a local union leader. Pay my fee, and I'll make sure a planned strike never happens.",
            AcceptText      = "Buy the dirt",
            DeclineText     = "I don't play dirty",
            AcceptResponse  = "The strike is quietly dismantled before it begins.",
            DeclineResponse = "The workers strike the next day, halting production.",
            AcceptMoney     = -15000, AcceptPopularity = -5,
            DeclineMoney    = -25000, DeclinePopularity = -5
        },

        // ==========================================
        // RECURRING: FACTORY OWNER
        // ==========================================
        new()
        {
            Id              = "fo_deregulation",
            AdvisorName     = "Factory Owner",
            DialogueText    = "These emission filters are killing my margins. Let me bypass them this week, and I'll kick a cut of the profits to the city.",
            AcceptText      = "Look the other way",
            DeclineText     = "Keep filters on",
            AcceptResponse  = "The smokestacks turn black. The treasury turns green.",
            DeclineResponse = "My costs go up, mayor. Don't forget who pays taxes here.",
            AcceptMoney     = 35000, AcceptPollution = 15, AcceptPopularity = -5,
            DeclinePollution = -2, DeclineMoney = -5000
        },
        new()
        {
            Id              = "fo_new_factory",
            AdvisorName     = "Factory Owner",
            DialogueText    = "I want to expand operations. Waive the environmental review, and I'll give you the deed to a brand new Factory. Look at the bottom line.",
            AcceptText      = "Waive the review",
            DeclineText     = "Follow the rules",
            AcceptResponse  = "Industry expands! Just try not to breathe too deeply.",
            DeclineResponse = "Bureaucracy slows progress once again.",
            AcceptMoney     = 15000, AcceptPollution = 10, AcceptPopularity = -8,
            AcceptBuildingIndex = 10, AcceptBuildingCount = 1 // Factory
        },
        new()
        {
            Id              = "fo_overtime",
            AdvisorName     = "Factory Owner",
            DialogueText    = "I need to run the coal plants through the night to meet a quota. The noise will keep the locals awake, but the revenue is huge.",
            AcceptText      = "Authorize night shift",
            DeclineText     = "Deny the shift",
            AcceptResponse  = "The gears grind all night long. The citizens are exhausted.",
            DeclineResponse = "We lose the contract. Bad for business.",
            AcceptMoney     = 45000, AcceptPollution = 12, AcceptPopularity = -12,
            DeclineMoney    = -10000
        },

        // ==========================================
        // STORYLINE 1: THE GREEN VS GREED WAR
        // Activist vs Factory Owner
        // ==========================================
        new()
        {
            Id              = "story_green_push",
            IsOneShot       = true,
            AdvisorName     = "Activist",
            DialogueText    = "Mayor, this is our chance. Commit to a permanent Green City Initiative. It will lower pollution every day, but industry leaders will fight you.",
            AcceptText      = "Commit to the Initiative",
            DeclineText     = "Industry comes first",
            AcceptResponse  = "The city goes green! Daily pollution will drop.",
            DeclineResponse = "The Activist leaves, vowing to fight you at the polls.",
            AcceptPopularity = 10, AcceptMoney = -25000,
            AcceptModifierId = "green_initiative",
            AcceptModifierName = "Green City",
            AcceptModifierType = GameManager.ModifierType.Pollution,
            AcceptModifierValue = -3, // -3 Pollution every day forever
            DeclinePopularity = -15
        },
        new()
        {
            Id               = "story_fo_retaliation",
            IsOneShot        = true,
            PrerequisiteId   = "story_green_push",
            RequiresAccepted = true, // If you chose the Activist
            MinTurnDelay     = 2,
            AdvisorName      = "Factory Owner",
            DialogueText     = "That 'Green Initiative' of yours is bleeding me dry. Compensate my losses out of the city treasury right now, or I'm shutting down a plant.",
            AcceptText       = "Pay him off",
            DeclineText      = "Call his bluff",
            AcceptResponse   = "You cave to his demands. The city loses a fortune, but the jobs stay.",
            DeclineResponse  = "He wasn't bluffing. A plant closes, jobs vanish, and the economy takes a massive hit.",
            AcceptMoney      = -60000, AcceptPopularity = -5,
            DeclineMoney     = -15000, DeclinePopulation = -25, DeclinePopularity = -15
        },
        new()
        {
            Id               = "story_fo_bribe",
            IsOneShot        = true,
            PrerequisiteId   = "story_green_push",
            RequiresAccepted = false, // If you rejected the Activist
            MinTurnDelay     = 1,
            AdvisorName      = "Factory Owner",
            DialogueText     = "I heard you told that dirt-hugging activist to take a hike. Smart business. Let's make this an industrial utopia. I'll boost your daily income.",
            AcceptText       = "Accept the partnership",
            DeclineText      = "I don't work for you",
            AcceptResponse   = "The factories roar to life. Money pours in, but the smog thickens.",
            DeclineResponse  = "He scoffs and walks away.",
            AcceptModifierId = "industrial_utopia",
            AcceptModifierName = "Industrial Utopia",
            AcceptModifierType = GameManager.ModifierType.Money,
            AcceptModifierValue = 2500, // +2500 Money every day forever
            AcceptPollution  = 20, AcceptPopularity = -10
        },
        new()
        {
            Id               = "story_act_sabotage",
            IsOneShot        = true,
            PrerequisiteId   = "story_fo_bribe",
            RequiresAccepted = true, // If you took the FO's bribe
            MinTurnDelay     = 2,
            AdvisorName      = "Activist",
            DialogueText     = "You sold our lungs for a quick buck! We are shutting down the Big Factory unless you build us a massive solar grid immediately!",
            AcceptText       = "Fund the solar grid",
            DeclineText      = "Let them protest",
            AcceptResponse   = "You pay a fortune to calm them down and go green. (Solar Panels added)",
            DeclineResponse  = "They break into the factory and smash the equipment.",
            AcceptMoney      = -75000, AcceptPollution = -15,
            AcceptBuildingIndex = 11, AcceptBuildingCount = 2, // 2 Solar Panels
            DeclineMoney     = -40000, DeclinePopularity = -20
        },

        // ==========================================
        // STORYLINE 2: GENTRIFICATION
        // Rich Man vs Construction Worker
        // ==========================================
        new()
        {
            Id              = "story_rm_buyout",
            IsOneShot       = true,
            AdvisorName     = "Rich Man",
            DialogueText    = "Mayor. This city is utterly devoid of class. Let me bulldoze a poor neighborhood for high-rises. It'll bring in the *right* kind of money permanently.",
            AcceptText      = "Bulldoze it",
            DeclineText     = "Protect the neighborhood",
            AcceptResponse  = "The bulldozers arrive. Your daily income rises, but approval crashes.",
            DeclineResponse = "He glares at you. 'You lack vision.'",
            AcceptMoney     = 20000, AcceptPopulation = -15, AcceptPopularity = -20,
            AcceptModifierId = "gentrification_buff",
            AcceptModifierName = "Elite Taxes",
            AcceptModifierType = GameManager.ModifierType.Money,
            AcceptModifierValue = 3000, // +3000 Money every day
            DeclinePopularity = 10
        },
        new()
        {
            Id               = "story_cw_pushback",
            IsOneShot        = true,
            PrerequisiteId   = "story_rm_buyout",
            RequiresAccepted = true, // If you let the RM bulldoze
            MinTurnDelay     = 1,
            AdvisorName      = "Construction Worker",
            DialogueText     = "Hey boss. That suit is kicking my guys out of their homes. Fund a massive Apartment block for us, or my entire crew is walking off the job forever.",
            AcceptText       = "Fund the Apartments",
            DeclineText      = "Let them walk",
            AcceptResponse   = "You spend heavily to keep the crew happy. (Big Apartment added)",
            DeclineResponse  = "The crew strikes. City construction grinds to a halt.",
            AcceptMoney      = -60000, AcceptPopularity = 15,
            AcceptBuildingIndex = 4, AcceptBuildingCount = 1, // Big Apartment
            DeclinePopularity = -25, DeclinePopulation = -20
        },
        new()
        {
            Id               = "story_anon_blackmail_rm",
            IsOneShot        = true,
            PrerequisiteId   = "story_rm_buyout",
            RequiresAccepted = true, // If you let the RM bulldoze
            MinTurnDelay     = 3,
            AdvisorName      = "Anonymous",
            DialogueText     = "Psst. That Rich Man? His zoning permits are completely fake. Pay me, and I bury the evidence. Don't, and he goes down... taking you with him.",
            AcceptText       = "Bury the evidence",
            DeclineText      = "Let the truth out",
            AcceptResponse   = "You pay the bribe. The secret is safe.",
            DeclineResponse  = "The scandal hits the news. The Rich Man is furious, and the public feels betrayed.",
            AcceptMoney      = -40000,
            DeclinePopularity = -35, DeclineMoney = -20000
        },

        // ==========================================
        // STORYLINE 3: THE SKETCHY COVERUP
        // Anonymous -> Activist / Factory Owner
        // ==========================================
        new()
        {
            Id              = "story_anon_coverup",
            IsOneShot       = true,
            AdvisorName     = "Anonymous",
            DialogueText    = "Your pollution numbers are scaring away investors. For a fee, I can wipe the servers. The smog will still be there, but the official records will say it's gone.",
            AcceptText      = "Wipe the servers",
            DeclineText     = "Absolutely not",
            AcceptResponse  = "The data vanishes. The city looks clean on paper... for now.",
            DeclineResponse = "You refuse the shady deal. The public appreciates the honesty.",
            AcceptMoney     = -20000, AcceptPollution = -30, AcceptPopularity = 5,
            DeclinePopularity = 10
        },
        new()
        {
            Id               = "story_act_leak",
            IsOneShot        = true,
            PrerequisiteId   = "story_anon_coverup",
            RequiresAccepted = true, // If you wiped the servers
            MinTurnDelay     = 2,
            AdvisorName      = "Activist",
            DialogueText     = "We found the real air quality reports! You lied to the whole city! We are going to the press unless you build us Wind Turbines immediately!",
            AcceptText       = "Build the Turbines",
            DeclineText      = "Call their bluff",
            AcceptResponse   = "You panic and fund the green energy. (Wind Turbines added)",
            DeclineResponse  = "The scandal breaks. Your approval rating plummets instantly and the real pollution returns.",
            AcceptMoney      = -60000, AcceptPollution = -10,
            AcceptBuildingIndex = 12, AcceptBuildingCount = 2, // 2 Wind Turbines
            DeclinePopularity = -40, DeclinePollution = 30 // Pollution jumps back up
        },
        new()
        {
            Id               = "story_fo_leverage",
            IsOneShot        = true,
            PrerequisiteId   = "story_anon_coverup",
            RequiresAccepted = true, // If you wiped the servers
            MinTurnDelay     = 4,
            AdvisorName      = "Factory Owner",
            DialogueText     = "Mayor, I know exactly what you did with those pollution records. Give me a permanent daily tax exemption, or I tell the press.",
            AcceptText       = "Grant the exemption",
            DeclineText      = "I will not be blackmailed",
            AcceptResponse   = "You are trapped. The city bleeds money every day to keep him quiet.",
            DeclineResponse  = "He leaks the documents. Your career takes a massive hit.",
            AcceptPopularity = -5,
            AcceptModifierId = "blackmail_tax",
            AcceptModifierName = "Blackmail Payout",
            AcceptModifierType = GameManager.ModifierType.Money,
            AcceptModifierValue = -2000, // Lose 2000 money every day
            DeclinePopularity = -35, DeclinePollution = 30
        },

        // ==========================================
        // STORYLINE 4: THE CONSTRUCTION BOOM
        // Construction Worker -> Rich Man / Factory Owner
        // ==========================================
        new()
        {
            Id              = "story_cw_deregulation",
            IsOneShot       = true,
            AdvisorName     = "Construction Worker",
            DialogueText    = "Hey boss, if we cut safety regulations, my crew can build twice as fast. We'll get a massive wave of people moving in immediately!",
            AcceptText      = "Cut the regulations",
            DeclineText     = "Safety is non-negotiable",
            AcceptResponse  = "Hammers swing 24/7. People are flooding into the city!",
            DeclineResponse = "Growth stays steady and safe.",
            AcceptPopularity = 5,
            AcceptPopulation = 40, // --- CHANGED: Instant net gain instead of daily modifier ---
            DeclinePopularity = 2
        },
        new()
        {
            Id               = "story_rm_crash",
            IsOneShot        = true,
            PrerequisiteId   = "story_cw_deregulation",
            RequiresAccepted = true, // If you cut regulations
            MinTurnDelay     = 3,
            AdvisorName      = "Rich Man",
            DialogueText     = "Those quickly-built houses are falling apart, and my real estate investments are tanking! I demand a city buyout to cover my losses!",
            AcceptText       = "Bail him out",
            DeclineText      = "Not my problem",
            AcceptResponse   = "You drain the treasury to save his portfolio. The public is furious.",
            DeclineResponse  = "His portfolio crashes. He uses his influence to ruin your daily approval.",
            AcceptMoney      = -75000, AcceptPopularity = -15,
            DeclinePopularity = -5,
            DeclineModifierId = "angry_elite_nerf",
            DeclineModifierName = "Elite Smear Campaign",
            DeclineModifierType = GameManager.ModifierType.Popularity,
            DeclineModifierValue = -2 // -2 Popularity every day forever
        },
        new()
        {
            Id               = "story_cw_union",
            IsOneShot        = true,
            PrerequisiteId   = "story_cw_deregulation",
            RequiresAccepted = false, // If you KEPT the regulations (Declined)
            MinTurnDelay     = 2,
            AdvisorName      = "Construction Worker",
            DialogueText     = "Since you stood up for our safety, the boys formed a union. We'll do higher quality work, boosting daily approval, but we cost a little more.",
            AcceptText       = "Recognize the Union",
            DeclineText      = "Bust the Union",
            AcceptResponse   = "The workers are thrilled. Daily approval goes up, but money goes down.",
            DeclineResponse  = "You send in the strike-breakers. Approval plummets.",
            AcceptModifierId = "union_quality",
            AcceptModifierName = "Union Quality",
            AcceptModifierType = GameManager.ModifierType.Popularity,
            AcceptModifierValue = 2, // +2 Popularity per day
            AcceptMoney      = -15000,
            DeclinePopularity = -20
        }
    };
    }
}