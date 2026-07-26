using Godot;
using System.Collections.Generic;

public partial class SceneManager : Node
{
    // This allows us to access the SceneManager from ANY script
    public static SceneManager Instance { get; private set; }

    // This dictionary holds onto our loaded scenes so they don't get deleted
    private Dictionary<string, Node> _stashedScenes = new Dictionary<string, Node>();

    public override void _Ready()
    {
        Instance = this;
    }

    public void SwapToScene(string nextScenePath)
    {
        Node currentScene = GetTree().CurrentScene;

        // 1. Remove the current scene from the active game, but DO NOT delete it
        GetTree().Root.RemoveChild(currentScene);

        // Stash it in our dictionary using its file path as the key
        _stashedScenes[currentScene.SceneFilePath] = currentScene;

        // 2. Check if the scene we want to go to is already stashed
        Node nextScene;
        if (_stashedScenes.ContainsKey(nextScenePath))
        {
            // It exists! Pull it out of the waiting room
            nextScene = _stashedScenes[nextScenePath];
        }
        else
        {
            // First time going to this scene, so load it normally
            PackedScene packedScene = GD.Load<PackedScene>(nextScenePath);
            nextScene = packedScene.Instantiate();
        }

        // 3. Add the next scene to the active game and set it as current
        GetTree().Root.AddChild(nextScene);
        GetTree().CurrentScene = nextScene;

        GameManager.Instance.RecalculateDailyYields();
        GameManager.Instance.NotifyStatsChanged();
    }
}