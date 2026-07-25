using Godot;
using System;

public partial class ChangeScenesButton : TextureButton
{
    private string _cityScenePath = "res://Scenes/CityScene.tscn";
    private string _roomScenePath = "res://Scenes/RoomScene.tscn";

    public override void _Ready()
    {
        // Connect the Godot 'Pressed' signal to our custom method
        Pressed += OnButtonPressed;
    }

    private void OnButtonPressed()
    {
        ReleaseFocus();
        // Get the file path of the scene we are currently standing in
        string currentPath = GetTree().CurrentScene.SceneFilePath;

        string targetPath = "";

        // Toggle logic: if in City, go to Room. If in Room, go to City.
        if (currentPath == _cityScenePath)
        {
            targetPath = _roomScenePath;
        }
        else if (currentPath == _roomScenePath)
        {
            targetPath = _cityScenePath;
        }
        else
        {
            // Fallback just in case you put this button in a 3rd scene
            targetPath = _cityScenePath;
        }

        // Call the Autoload to handle the stash and swap
        SceneManager.Instance.SwapToScene(targetPath);
    }
}