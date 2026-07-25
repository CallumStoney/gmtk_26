using Godot;
using System;

public partial class HomeScreen : Control
{
	private Button _play;
	private Button _quit;

	public override void _Ready()
	{
		_play = GetNode<Button>("CenterContainer/VBoxContainer/PlayButton");
		_quit = GetNode<Button>("CenterContainer/VBoxContainer/QuitButton");

		_play.Pressed += OnPlayPressed;
		_quit.Pressed += OnQuitPressed;
	}

	private void OnPlayPressed()
	{
		GetTree().ChangeSceneToFile("res://Scenes/CityScene.tscn");
	}

	private void OnQuitPressed()
	{
		GetTree().Quit();
	}
}
