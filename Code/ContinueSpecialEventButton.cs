using Godot;
using System;

public partial class ContinueSpecialEventButton : Button
{
    [Export] public ComputerTurnOn ComputerController;

    public override void _Ready()
    {
        // Connect the button's built-in click signal
        Pressed += OnButtonPressed;
    }

    private void OnButtonPressed()
    {
        if (ComputerController == null)
        {
            GD.PrintErr($"CRITICAL: ComputerController is not assigned in the Inspector on {Name}!");
            return;
        }

        var gm = GameManager.Instance;

        // Branch logic based on which special event is currently running
        switch (ComputerController.ActiveEventType)
        {
            case "firstTime":
                // Turn off firstTime flag so it won't repeat
                GameManager._firstTime = false;

                // Close special modal and boot into normal UI
                ComputerController.EndSpecialEvent();
                break;

            case "endingPopularity":
            case "endingPolution":
            case "endingPollution":
                // Reset game state and return to Home Screen
                gm.ResetGame();
                GetTree().ChangeSceneToFile("res://Scenes/HomeScreen.tscn");
                break;

            case "endDay":
            default:
                // End of Day sequence
                gm.AdvanceTurn();
                gm.RecalculateDailyYields();
                gm.NotifyStatsChanged();

                if (gm.Popularity <= 0)
                {
                    ComputerController.StartSpecialEvent("endingPopularity");
                    return;
                }

                if (gm.CurrentTurn == 15)
                {
                    ComputerController.StartSpecialEvent("endingPollution");
                    return;
                }

                ComputerController.EndSpecialEvent();
                break;
        }
    }
}