using Godot;
using System;

public partial class ComputerTurnOn : AnimatedSprite2D
{
    [Export] public Node2D NormalUI;
    [Export] public Node2D SpecialEventUI;

    [ExportGroup("Special Event References")]
    [Export] public Label TitleSpecialEvent;
    [Export] public Label MainTextSpecialEvent;
    [Export] public Button ContinueButton;

    [ExportGroup("Hide During Special Events")]
    [Export] public Label LabelToHide;
    [Export] public TextureButton TextureButtonToHide;

    private enum ScreenPhase
    {
        TurningOnNormal,
        NormalActive,
        TurningOffForEvent,
        TurningOnForEvent,
        EventActive,
        TurningOffToEndEvent
    }

    private ScreenPhase _currentPhase = ScreenPhase.TurningOnNormal;
    private string _activeEventType = "";

    public string ActiveEventType => _activeEventType;

    public override void _EnterTree()
    {
        base._EnterTree();
        SetUIState(showNormal: false, showSpecial: false);
    }

    public override void _Ready()
    {
        AnimationFinished += OnAnimationFinished;
        VisibilityChanged += OnVisibilityChanged;

        // Auto-find references inside SpecialEventUI if not assigned in Inspector
        if (SpecialEventUI != null)
        {
            TitleSpecialEvent ??= SpecialEventUI.FindChild("TitleSpecialEvent", recursive: true) as Label;
            MainTextSpecialEvent ??= SpecialEventUI.FindChild("MainTextSpecialEvent", recursive: true) as Label;
            ContinueButton ??= SpecialEventUI.FindChild("ContinueSpecialEventButton", recursive: true) as Button
                            ?? SpecialEventUI.FindChild("Button", recursive: true) as Button;
        }

        if (GameManager._firstTime)
        {
            StartSpecialEvent("firstTime");
        }
        else
        {
            StartNormalTurnOn();
        }
    }

    private void OnVisibilityChanged()
    {
        if (Visible && IsInsideTree())
        {
            if (GameManager._firstTime)
            {
                StartSpecialEvent("firstTime");
            }
            else
            {
                StartNormalTurnOn();
            }
        }
    }

    private void StartNormalTurnOn()
    {
        _currentPhase = ScreenPhase.TurningOnNormal;
        _activeEventType = "";
        SetUIState(showNormal: false, showSpecial: false);

        Frame = 0;
        Play();
    }

    public void StartSpecialEvent(string eventType = "endDay")
    {
        _activeEventType = eventType;

        // If computer is already active, turn off first before booting into event
        if (_currentPhase == ScreenPhase.NormalActive)
        {
            _currentPhase = ScreenPhase.TurningOffForEvent;
            SetUIState(showNormal: false, showSpecial: false);

            Frame = SpriteFrames.GetFrameCount(Animation) - 1;
            PlayBackwards();
        }
        // If computer is starting from cold boot (e.g. firstTime in _Ready)
        else
        {
            _currentPhase = ScreenPhase.TurningOnForEvent;
            SetUIState(showNormal: false, showSpecial: false);

            Frame = 0;
            Play();
        }
    }

    public void EndSpecialEvent()
    {
        if (_currentPhase != ScreenPhase.EventActive) return;

        _currentPhase = ScreenPhase.TurningOffToEndEvent;
        SetUIState(showNormal: false, showSpecial: false);

        Frame = SpriteFrames.GetFrameCount(Animation) - 1;
        PlayBackwards();
    }

    private void OnAnimationFinished()
    {
        switch (_currentPhase)
        {
            case ScreenPhase.TurningOnNormal:
                _currentPhase = ScreenPhase.NormalActive;
                SetUIState(showNormal: true, showSpecial: false);
                break;

            case ScreenPhase.TurningOffForEvent:
                _currentPhase = ScreenPhase.TurningOnForEvent;
                Frame = 0;
                Play();
                break;

            case ScreenPhase.TurningOnForEvent:
                _currentPhase = ScreenPhase.EventActive;

                switch (_activeEventType)
                {
                    case "firstTime":
                        SetupFirstTimeUI();
                        SetUIState(showNormal: false, showSpecial: true);
                        break;

                    case "endingPopularity":
                        SetupEndingPopularityUI();
                        SetUIState(showNormal: false, showSpecial: true);
                        break;

                    case "endingPolution":
                    case "endingPollution":
                        SetupEndingPolutionUI();
                        SetUIState(showNormal: false, showSpecial: true);
                        break;

                    case "endDay":
                    default:
                        SetupEndDayUI();
                        SetUIState(showNormal: false, showSpecial: true);
                        break;
                }
                break;

            case ScreenPhase.TurningOffToEndEvent:
                _currentPhase = ScreenPhase.TurningOnNormal;
                _activeEventType = "";
                Frame = 0;
                Play();
                break;
        }
    }

    private void SetupFirstTimeUI()
    {
        SetButtonText("Continue");

        if (TitleSpecialEvent != null)
        {
            TitleSpecialEvent.Text = "New Mayor";
        }

        if (MainTextSpecialEvent != null)
        {
            MainTextSpecialEvent.Text = "Instructions:\n" +
                                        "The last mayor got kicked out of office because his approval reached 0 from adding too many factories." +
                                        " Make sure that you don't make the same mistake. Although it might already be over for you, I've heard whispers that your" +
                                        " getting kicked out in 15 days if the pollution level isn't 0 on your 15th day.\n\n" +
                                        "Answer the Public's proposals and try rebuilding the city if you don't want to get the boot.";
        }
    }

    private void SetupEndingPopularityUI()
    {
        SetButtonText("End");

        if (TitleSpecialEvent != null)
        {
            TitleSpecialEvent.Text = "Ousted from Office";
        }

        if (MainTextSpecialEvent != null)
        {
            MainTextSpecialEvent.Text = "Your approval rating dropped to zero!\n\n" +
                                        "The citizens have completely lost faith in your leadership and voted to recall you from office effective immediately. Tough luck, but I kinda expected it.";
        }
    }

    private void SetupEndingPolutionUI()
    {
        SetButtonText("End");

        bool hasWon = GameManager.Instance.Pollution <= 0;

        if (TitleSpecialEvent != null)
        {
            TitleSpecialEvent.Text = hasWon ? "Victory!" : "Term Expired";
        }

        if (MainTextSpecialEvent != null)
        {
            if (hasWon)
            {
                MainTextSpecialEvent.Text = "Day 15 has arrived and you successfully brought pollution down to 0!\n\n" +
                                            "The city is saved and you have proven to be a truly visionary mayor. I ALWAYS believed in you!";
            }
            else
            {
                MainTextSpecialEvent.Text = "Your 15 days are up, but pollution levels remain above 0!\n\n" +
                                            "As promised, you have been kicked out of office for failing to clean up the city. Expected to be honest, but still disappointing.";
            }
        }
    }

    private void SetupEndDayUI()
    {
        SetButtonText("Continue");

        int currentDay = GameManager.Instance.CurrentTurn;

        // Pull total yields including modifiers
        int pollutionChange = GameManager.Instance.ExpectedDailyPollution;
        int approvalChange = GameManager.Instance.ExpectedDailyPopularity;
        int moneyChange = GameManager.Instance.ExpectedDailyMoney;

        // Set Title to "Day #"
        if (TitleSpecialEvent != null)
        {
            TitleSpecialEvent.Text = $"Day {currentDay + 1}";
        }

        // Set Main Text Yield Report
        if (MainTextSpecialEvent != null)
        {
            MainTextSpecialEvent.Text =
                $"=== END OF DAY {currentDay + 1} YIELD REPORT ===\n" +
                $"Pollution:  {FormatStat(pollutionChange)}\n" +
                $"Approval:   {FormatStat(approvalChange)}\n" +
                $"Money:      {FormatMoney(moneyChange)}\n" +
                $"===================================";
        }
    }

    private void SetButtonText(string text)
    {
        if (ContinueButton != null)
        {
            ContinueButton.Text = text;
        }
    }

    private string FormatStat(int value)
    {
        return value >= 0 ? $"+{value}" : $"{value}";
    }

    private string FormatMoney(int value)
    {
        string sign = value >= 0 ? "+" : "-";
        int absVal = Math.Abs(value);

        if (absVal >= 1000000)
        {
            return $"{sign}${absVal / 1000000}M";
        }
        else if (absVal >= 1000)
        {
            return $"{sign}${absVal / 1000}k";
        }

        return $"{sign}${absVal}";
    }

    private void SetUIState(bool showNormal, bool showSpecial)
    {
        if (NormalUI != null)
        {
            NormalUI.Visible = showNormal;
            NormalUI.ProcessMode = showNormal ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
        }

        if (SpecialEventUI != null)
        {
            SpecialEventUI.Visible = showSpecial;
            SpecialEventUI.ProcessMode = showSpecial ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
        }

        // Hide nodes during special events or transitions, show only when normal active
        bool isNormalActive = showNormal && !showSpecial;

        if (LabelToHide != null)
        {
            LabelToHide.Visible = isNormalActive;
        }

        if (TextureButtonToHide != null)
        {
            TextureButtonToHide.Visible = isNormalActive;
        }
    }
}