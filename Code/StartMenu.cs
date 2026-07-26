using Godot;
using System;

public partial class StartMenu : Control 
{
	public override void _UnhandledInput(InputEvent @event)
	{
		// Check if it's a keyboard key, it was just pressed, and it is NOT an echo
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
		{
			if (keyEvent.Keycode == Key.Space)
			{
				AudioManager.Instance?.StartMusic();
				GetTree().ChangeSceneToFile("res://Scenes//CityScene.tscn");
			}
			else if (keyEvent.Keycode == Key.Q || keyEvent.Keycode == Key.Escape)
			{
				GetTree().Quit();
			}
		}
	}
}
