using Godot;
using System;

public partial class ComputerTurnOn : AnimatedSprite2D
{
    [Export] public Node2D NormalUI;
    [Export] public Node2D SpecialEventUI;

    [ExportGroup("Special Event References")]
    [Export] public Label TitleSpecialEvent;
    [Export] public Label MainTextSpecialEvent;

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

        // Auto-find labels inside SpecialEventUI if not assigned in Inspector
        if (SpecialEventUI != null)
        {
            TitleSpecialEvent ??= SpecialEventUI.FindChild("TitleSpecialEvent", recursive: true) as Label;
            MainTextSpecialEvent ??= SpecialEventUI.FindChild("MainTextSpecialEvent", recursive: true) as Label;
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
        if (TitleSpecialEvent != null)
        {
            TitleSpecialEvent.Text = "New Mayor";
        }

        if (MainTextSpecialEvent != null)
        {
            MainTextSpecialEvent.Text = "Instructions:";
        }
    }

    private void SetupEndDayUI()
    {
        int currentDay = GameManager.Instance.CurrentTurn;

        // Pull yields from GameManager
        int pollutionChange = GameManager.Instance.BuildingDailyPollution;
        int approvalChange = GameManager.Instance.BuildingDailyPopularity;
        int moneyChange = GameManager.Instance.BuildingDailyMoney;

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
    }
}