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

        // Branch logic based on which special event is currently running
        switch (ComputerController.ActiveEventType)
        {
            case "firstTime":
                // Turn off firstTime flag so it won't repeat
                GameManager._firstTime = false;

                // Close special modal and boot into normal UI
                ComputerController.EndSpecialEvent();
                break;

            case "endDay":
            default:
                // End of Day sequence
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

                ComputerController.EndSpecialEvent();
                break;
        }
    }
}