using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class RoomScene : Control
{
    private static readonly int[] EventCountWeights = { 0, 45, 35, 20 };

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
        var root = "MarginContainer/MainVBox";

        _advisorPortrait = GetNode<ColorRect>("AdvisorPortrait");
        _advisorNameLabel = GetNode<Label>("AdvisorNameLabel");
        _dialogueLabel = GetNode<Label>("DialogueLabel");

        _acceptButton = GetNode<Button>("AcceptButton");
        _declineButton = GetNode<Button>("DeclineButton");
        _responseLabel = GetNode<Label>("ResponseLabel");

        _nextButton = GetNode<Button>("BottomHBox/NextButton");
        _endTurnButton = GetNode<Button>("BottomHBox/EndTurnButton");
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

        // --- PASS BUILDING REWARDS TO BUTTON TEXT FORMATTER ---
        _acceptButton.Text = AppendEffectsToButtonText(_currentEvent.AcceptText, _currentEvent.AcceptPopulation, _currentEvent.AcceptPollution, _currentEvent.AcceptPopularity, _currentEvent.AcceptMoney, _currentEvent.AcceptBuildingIndex, _currentEvent.AcceptBuildingCount);
        _declineButton.Text = AppendEffectsToButtonText(_currentEvent.DeclineText, _currentEvent.DeclinePopulation, _currentEvent.DeclinePollution, _currentEvent.DeclinePopularity, _currentEvent.DeclineMoney, _currentEvent.DeclineBuildingIndex, _currentEvent.DeclineBuildingCount);

        _acceptButton.Visible = true;
        _declineButton.Visible = true;
        _responseLabel.Text = "";
        _nextButton.Visible = false;
        _endTurnButton.Visible = false;
        _awaitingChoice = true;
    }

    // --- NEW FORMATTING HELPER METHOD (WITH BUILDINGS) ---
    private string AppendEffectsToButtonText(string baseText, int pop, int pol, int popu, int mon, int bldIndex, int bldCount)
    {
        List<string> effects = new List<string>();

        // Fetch building name dynamically from GameManager
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

        if (effects.Count > 0)
        {
            return $"{baseText}\n[{string.Join(", ", effects)}]";
        }

        return baseText;
    }

    private void ShowNoEvents()
    {
        _advisorNameLabel.Text = "";
        _dialogueLabel.Text = "A quiet day. Nothing demands your attention for better or worse.";
        _acceptButton.Visible = false;
        _declineButton.Visible = false;
        _responseLabel.Text = "";
        _nextButton.Visible = false;
        _endTurnButton.Visible = true;
    }

    private void ShowNoMoreEvents()
    {
        _advisorNameLabel.Text = "";
        _dialogueLabel.Text = "Nothing else demands your attention today. End the day when you are ready.";
        _acceptButton.Visible = false;
        _declineButton.Visible = false;
        _responseLabel.Text = "";
        _nextButton.Visible = false;
        _endTurnButton.Visible = true;
    }

    private void OnAcceptPressed()
    {
        if (!_awaitingChoice || _currentEvent == null) return;
        _awaitingChoice = false;

        var gm = GameManager.Instance;

        gm.ApplyStatChanges(
            population: _currentEvent.AcceptPopulation,
            pollution: _currentEvent.AcceptPollution,
            popularity: _currentEvent.AcceptPopularity,
            money: _currentEvent.AcceptMoney
        );

        // --- NEW: ADD BUILDINGS TO INVENTORY ---
        if (_currentEvent.AcceptBuildingCount > 0 && _currentEvent.AcceptBuildingIndex >= 0)
        {
            gm.AddBuildingToInventory(_currentEvent.AcceptBuildingIndex, _currentEvent.AcceptBuildingCount);
        }

        gm.Memory.RecordDecision(_currentEvent.Id, accepted: true, gm.CurrentTurn);
        ExecuteTags(_currentEvent.AcceptTags);
        ShowResponse(_currentEvent.AcceptResponse);
    }

    private void OnDeclinePressed()
    {
        if (!_awaitingChoice || _currentEvent == null) return;
        _awaitingChoice = false;

        var gm = GameManager.Instance;

        gm.ApplyStatChanges(
            population: _currentEvent.DeclinePopulation,
            pollution: _currentEvent.DeclinePollution,
            popularity: _currentEvent.DeclinePopularity,
            money: _currentEvent.DeclineMoney
        );

        // --- NEW: ADD BUILDINGS TO INVENTORY ---
        if (_currentEvent.DeclineBuildingCount > 0 && _currentEvent.DeclineBuildingIndex >= 0)
        {
            gm.AddBuildingToInventory(_currentEvent.DeclineBuildingIndex, _currentEvent.DeclineBuildingCount);
        }

        gm.Memory.RecordDecision(_currentEvent.Id, accepted: false, gm.CurrentTurn);
        ExecuteTags(_currentEvent.DeclineTags);
        ShowResponse(_currentEvent.DeclineResponse);
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
        _acceptButton.Visible = false;
        _declineButton.Visible = false;

        if (_turnEvents.Count > 0)
        {
            _nextButton.Text = "Next";
            _nextButton.Visible = true;
        }
        else
        {
            _endTurnButton.Visible = true;
        }
    }

    private void OnNextPressed() => ShowNextEvent();

    private void OnEndTurnPressed()
    {
        GameManager.Instance.AdvanceTurn();
        StartTurn();
    }

    private void OnGameOver(string reason)
    {
        _advisorNameLabel.Text = "GAME OVER";
        _dialogueLabel.Text = reason;
        _acceptButton.Visible = false;
        _declineButton.Visible = false;
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
            // BUILDING REWARD EVENTS
            // ==========================================
            new()
            {
                Id              = "surplus_materials",
                AdvisorName     = "Head of Construction",
                DialogueText    = "Mayor, we ended up with surplus concrete from the highway expansion. If you can cover the labor costs, my crew can throw up a couple of extra houses this week.",
                AcceptText      = "Build the houses",
                DeclineText     = "Too expensive right now",
                AcceptResponse  = "The crew works overtime. The houses are ready to place!",
                DeclineResponse = "The surplus concrete is left to rot in the yard.",
                AcceptMoney = -25000, AcceptPollution = 4,
                AcceptBuildingIndex = 0, AcceptBuildingCount = 2, // Gives 2 Houses
                DeclinePopularity = -2
            },
            new()
            {
                Id              = "urban_renewal",
                AdvisorName     = "Local Environmentalist",
                DialogueText    = "The concrete jungle is suffocating us! Please dedicate some space for a Nature Park. My organization will provide the saplings if the city helps clear the land.",
                AcceptText      = "Clear the land",
                DeclineText     = "We need the space for industry",
                AcceptResponse  = "The community celebrates as the saplings arrive. Place your new park wisely.",
                DeclineResponse = "The activist leaves in frustration.",
                AcceptMoney = -15000, AcceptPopularity = 10,
                AcceptBuildingIndex = 6, AcceptBuildingCount = 1, // Gives 1 Nature Park
                DeclinePopularity = -5
            },
            new()
            {
                Id              = "clean_energy_grant",
                AdvisorName     = "Clean Energy Rep",
                DialogueText    = "The state is offering a grant for green infrastructure. We can secure a shipment of solar panels if you cover the heavy transport fees.",
                AcceptText      = "Pay for transport",
                DeclineText     = "Pass on the grant",
                AcceptResponse  = "The shipment arrives safely. Time to go green.",
                DeclineResponse = "Another city scoops up the grant money.",
                AcceptMoney = -50000, AcceptPopularity = 5,
                AcceptBuildingIndex = 11, AcceptBuildingCount = 1, // Gives 1 Solar Panel
                DeclinePollution = 2, DeclinePopularity = -3
            },

            // ==========================================
            // ORIGINAL EVENTS (Cleaned of hyphens)
            // ==========================================
            new()
            {
                Id              = "solar_panels",
                AdvisorName     = "Environment Minister",
                DialogueText    = "Mayor, we can install solar panels across the industrial district. It will cost us, but pollution will drop significantly.",
                AcceptText      = "Fund the project",
                DeclineText     = "Too expensive right now",
                AcceptResponse  = "Workers begin installing panels. Pollution drops, but it was not cheap.",
                DeclineResponse = "The minister leaves, clearly disappointed.",
                AcceptPollution = -10, AcceptPopularity = 5, AcceptMoney = -15000,
                DeclinePopularity = -3
            },
            new()
            {
                Id              = "factory_lease",
                AdvisorName     = "City Treasurer",
                DialogueText    = "There is an opportunity to lease public land to a factory. Big money, but the residents will not like the extra smoke.",
                AcceptText      = "Sign the lease",
                DeclineText     = "Protect the land",
                AcceptResponse  = "The factory moves in. Cash flows, but so does the smog.",
                DeclineResponse = "The land stays green. People are pleased.",
                AcceptPollution = 15, AcceptPopularity = -10, AcceptMoney = 30000, AcceptPopulation = 10,
                DeclinePopularity = 5
            },
            new()
            {
                Id              = "build_apartments",
                AdvisorName     = "Housing Director",
                DialogueText    = "We have families waiting for affordable housing. Building apartments will attract more people but costs money.",
                AcceptText      = "Build apartments",
                DeclineText     = "We cannot afford it",
                AcceptResponse  = "Construction begins. New residents are moving in!",
                DeclineResponse = "The housing crisis continues. People are not happy.",
                AcceptPopulation = 20, AcceptPopularity = 5, AcceptMoney = -20000, AcceptPollution = 5,
                DeclinePopulation = -5, DeclinePopularity = -5
            },
            new()
            {
                Id              = "relax_standards",
                AdvisorName     = "Industry Lobbyist",
                DialogueText    = "I represent several manufacturing firms. Relax emission standards and we will bring jobs and revenue to your city.",
                AcceptText      = "Relax the standards",
                DeclineText     = "Standards stay as they are",
                AcceptResponse  = "Factories ramp up production. The air quality drops but the economy booms.",
                DeclineResponse = "The lobbyist storms out. You will regret this, he mutters.",
                AcceptPollution = 20, AcceptMoney = 25000, AcceptPopularity = -8, AcceptPopulation = 10,
                DeclinePopularity = 3, DeclineMoney = -5000
            },
            new()
            {
                Id              = "electric_buses",
                AdvisorName     = "Public Transit Chief",
                DialogueText    = "Mayor, investing in electric buses would reduce commuter emissions and boost your public image.",
                AcceptText      = "Fund electric buses",
                DeclineText     = "Not a priority",
                AcceptResponse  = "New electric buses hit the roads. Citizens approve!",
                DeclineResponse = "Diesel buses keep rumbling along.",
                AcceptPollution = -8, AcceptPopularity = 8, AcceptMoney = -18000,
                DeclinePopularity = -2, DeclinePollution = 2
            },
            new()
            {
                Id              = "shady_deal",
                IsOneShot       = true,
                AdvisorName     = "Shady Businessman",
                DialogueText    = "Psst. I can make your pollution numbers look better on paper. Nobody will know. For a small fee, of course.",
                AcceptText      = "Take the deal",
                DeclineText     = "Get out of my office",
                AcceptResponse  = "The numbers look better for now. Let us hope nobody audits.",
                DeclineResponse = "You feel good about your integrity. The people notice.",
                AcceptPollution = -5, AcceptMoney = -10000, AcceptPopularity = -15,
                DeclinePopularity = 5
            },
            new()
            {
                Id              = "community_park",
                AdvisorName     = "Community Organizer",
                DialogueText    = "The citizens are demanding a park in the old industrial lot. It would be a symbol of change!",
                AcceptText      = "Build the park",
                DeclineText     = "We need that lot for industry",
                AcceptResponse  = "A beautiful green space springs up. The people love it!",
                DeclineResponse = "Protests erupt at the industrial lot.",
                AcceptPollution = -5, AcceptPopularity = 12, AcceptMoney = -10000,
                DeclinePopularity = -8
            },
            new()
            {
                Id              = "sell_power",
                AdvisorName     = "Foreign Trade Minister",
                DialogueText    = "A neighbouring city wants to buy our excess power at a premium. It would mean running our coal plants harder.",
                AcceptText      = "Sell the power",
                DeclineText     = "Decline the offer",
                AcceptResponse  = "The coal plants roar. Money pours in, but at what cost?",
                DeclineResponse = "A missed opportunity, but the skies stay clearer.",
                AcceptPollution = 12, AcceptMoney = 20000, AcceptPopularity = -5,
                DeclinePopularity = 2, DeclinePollution = -2
            },

            // ==========================================
            // CONSEQUENCE CHAIN EVENTS
            // ==========================================
            new()
            {
                Id               = "corruption_scandal",
                IsOneShot        = true,
                PrerequisiteId   = "shady_deal",
                RequiresAccepted = true,
                MinTurnDelay     = 2,
                AdvisorName      = "Investigative Journalist",
                DialogueText     = "Mayor! We have uncovered falsified pollution records. The public is outraged, someone leaked the paper trail.",
                AcceptText       = "Come clean publicly",
                DeclineText      = "Deny everything",
                AcceptResponse   = "Your honesty costs you politically, but the cover up ends. Pollution data is corrected.",
                DeclineResponse  = "The scandal deepens. Trust plummets, and the real pollution numbers resurface anyway.",
                AcceptPopularity = -10, AcceptPollution = 10,
                DeclinePopularity = -20, DeclinePollution = 8
            },

            new()
            {
                Id               = "factory_strike",
                IsOneShot        = true,
                PrerequisiteId   = "relax_standards",
                RequiresAccepted = true,
                MinTurnDelay     = 3,
                AdvisorName      = "Workers Union Rep",
                DialogueText     = "The factory workers are on strike! Poor air quality is making them sick. They demand safer conditions or they walk.",
                AcceptText       = "Tighten standards back up",
                DeclineText      = "Bring in replacement workers",
                AcceptResponse   = "Standards are restored. The factories slow down, but workers return.",
                DeclineResponse  = "Replacement workers keep the factories going, but the city reputation takes a hit.",
                AcceptPollution  = -12, AcceptMoney = -10000, AcceptPopularity = 8,
                DeclinePopularity = -15, DeclineMoney = 5000, DeclinePopulation = -10
            },

            new()
            {
                Id               = "green_award",
                IsOneShot        = true,
                PrerequisiteId   = "solar_panels",
                RequiresAccepted = true,
                MinTurnDelay     = 2,
                AdvisorName      = "International Observer",
                DialogueText     = "Your solar initiative has been nominated for a Global Green City award! Winning would attract ecotourists and investors.",
                AcceptText       = "Campaign for the award",
                DeclineText      = "We do not need recognition",
                AcceptResponse   = "You win the award! Ecotourism booms and investors take notice.",
                DeclineResponse  = "A humble choice. The citizens appreciate the focus on substance over spectacle.",
                AcceptMoney = 20000, AcceptPopularity = 10, AcceptPopulation = 15, AcceptPollution = -3,
                DeclinePopularity = 5
            },

            new()
            {
                Id               = "housing_crisis_worsens",
                IsOneShot        = true,
                PrerequisiteId   = "build_apartments",
                RequiresAccepted = false,
                MinTurnDelay     = 2,
                AdvisorName      = "Emergency Dispatcher",
                DialogueText     = "Families are camping in the streets. The housing shortage has become a full blown crisis. Media coverage is brutal.",
                AcceptText       = "Emergency housing fund",
                DeclineText      = "Issue a public statement",
                AcceptResponse   = "Temporary shelters go up. It is not a fix, but it stops the bleeding.",
                DeclineResponse  = "Words without action. The protests intensify.",
                AcceptPopulation = 10, AcceptPopularity = 5, AcceptMoney = -25000,
                DeclinePopularity = -12, DeclinePopulation = -15
            },

            new()
            {
                Id               = "lobbyist_returns",
                IsOneShot        = true,
                PrerequisiteId   = "relax_standards",
                RequiresAccepted = false,
                MinTurnDelay     = 1,
                AdvisorName      = "Industry Lobbyist",
                DialogueText     = "I told you you would regret it. Three factories are threatening to leave the city entirely. Last chance, sweeten the deal or lose the jobs.",
                AcceptText       = "Offer tax incentives",
                DeclineText      = "Let them leave",
                AcceptResponse   = "The factories stay, but the tax break hurts the budget. At least jobs are safe.",
                DeclineResponse  = "The factories pack up. Jobs are lost, but the air is cleaner.",
                AcceptMoney = -15000, AcceptPopularity = -3, AcceptPollution = 8,
                DeclinePopulation = -20, DeclinePollution = -10, DeclinePopularity = -5, DeclineMoney = -8000
            },
        };
    }
}