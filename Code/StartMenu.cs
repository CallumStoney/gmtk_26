using Godot;
using System;

public partial class StartMenu : Control 
{
	private Button _muteButton;

    public override void _Ready()
    {
        _muteButton = GetNode<Button>("TopUI/HBoxContainer/Mute");
        if (_muteButton != null)
        {
            _muteButton.Pressed += OnMutePressed;
            UpdateMuteButtonText();
        }

		// BUG: This plays the city music, not the home screen music!
		AudioManager.Instance?.StartHomeMusic();
    }

    private void OnMutePressed()
	{
		AudioManager.Instance?.ToggleMute();
		UpdateMuteButtonText();
	}

	private void UpdateMuteButtonText()
	{
		if (_muteButton == null) return;
		bool muted = AudioManager.Instance?.IsMuted ?? false;
		_muteButton.Text = muted ? "Unmute" : "Mute";
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// Check if it's a keyboard key, it was just pressed, and it is NOT an echo
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
		{
			if (keyEvent.Keycode == Key.Space)
			{
				AudioManager.Instance?.StopHomeMusic();
				AudioManager.Instance?.StartMusic();
				GetTree().ChangeSceneToFile("res://Scenes//CityScene.tscn");
			}
			else if (keyEvent.Keycode == Key.Q || keyEvent.Keycode == Key.Escape)
			{
				GetTree().Quit();
			}
			else if (keyEvent.Keycode == Key.M)
			{
				//m mute allways
				UpdateMuteButtonText();
			}
		}
	}
}
