using Godot;
using System;

public partial class SmogManager : Node
{
    // --- PERFORMANCE & RESOLUTION ---
    [Export] public int TextureResolution = 256;

    // --- GAMEPLAY LINK ---
    // At what Pollution level should the smog be 100% dense? (Adjust in Inspector)
    [Export] public float MaxPollutionForVisuals = 100f;

    // --- PITCH-BLACK SMOKE SETTINGS (Bottom Layers - Slow & Heavy) ---
    [Export] public float MaxBackSmokeAlpha = 0.45f;
    [Export] public float MaxMidSmokeAlpha = 0.60f;
    [Export] public float MaxForeSmokeAlpha = 0.75f;

    [Export] public float DeepAshBaseSpeed = 1.0f;
    [Export] public float AshCloudBaseSpeed = 2.0f;
    [Export] public float HeavyAshBaseSpeed = 3.5f;

    // --- PATCHY YELLOW SMOG SETTINGS (Top Layers - Fast Shearing) ---
    [Export] public float MaxGraySmogAlpha = 0.35f;
    [Export] public float MaxMidSmogAlpha = 0.50f;
    [Export] public float MaxYellowSmogAlpha = 0.65f;

    [Export] public float DeepFogBaseSpeed = 45.0f;
    [Export] public float LowMistBaseSpeed = 65.0f;
    [Export] public float YellowSmogBaseSpeed = 85.0f;

    // --- WIND & GENERAL SETTINGS ---
    [Export] public Vector2 BaseWindDirection = new Vector2(1.0f, -0.15f);
    [Export] public float LerpSpeed = 2.0f;

    private ParallaxBackground combinedLayer;

    // Smoke Layers
    private ParallaxLayer deepAshLayer;
    private ParallaxLayer ashCloudLayer;
    private ParallaxLayer heavyAshLayer;
    private TextureRect deepAshRect;
    private TextureRect ashCloudRect;
    private TextureRect heavyAshRect;

    // Smog Layers
    private ParallaxLayer deepFogLayer;
    private ParallaxLayer lowMistLayer;
    private ParallaxLayer yellowSmogLayer;
    private TextureRect deepFogRect;
    private TextureRect lowMistRect;
    private TextureRect yellowSmogRect;

    private float currentDensity = 0.0f;
    private float targetDensity = 0.0f;

    private FastNoiseLite windNoise;
    private float masterWindTime;

    public override void _Ready()
    {
        combinedLayer = new ParallaxBackground();
        combinedLayer.Name = "CombinedAtmosphereLayer";
        combinedLayer.Layer = 5;
        AddChild(combinedLayer);

        windNoise = new FastNoiseLite();
        windNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        windNoise.Frequency = 0.015f;
        windNoise.Seed = (int)GD.Randi();

        masterWindTime = GD.Randf() * 500f;

        Vector2 screenSize = GetViewport().GetVisibleRect().Size;
        float maxDim = Mathf.Max(screenSize.X, screenSize.Y);
        Vector2 mirrorSize = new Vector2(maxDim, maxDim) * 3.0f;

        // ==========================================================
        // PART 1: PITCH-BLACK, SMALL-CLOUD SMOKE (Bottom Layers)
        // ==========================================================

        Gradient deepAshRamp = new Gradient();
        deepAshRamp.SetColor(0, new Color(0.08f, 0.09f, 0.10f, 0.0f));
        deepAshRamp.SetColor(1, new Color(0.0f, 0.0f, 0.0f, 0.85f));
        deepAshRamp.AddPoint(0.35f, new Color(0.08f, 0.09f, 0.10f, 0.0f));
        deepAshLayer = CreateFogLayer("DeepAsh", 0.007f, new Color(1.0f, 1.0f, 1.0f, 0.0f), deepAshRamp, new Vector2(0.80f, 0.80f), mirrorSize, 3, out deepAshRect);

        Gradient ashCloudRamp = new Gradient();
        ashCloudRamp.SetColor(0, new Color(0.05f, 0.05f, 0.06f, 0.0f));
        ashCloudRamp.SetColor(1, new Color(0.0f, 0.0f, 0.0f, 0.90f));
        ashCloudRamp.AddPoint(0.30f, new Color(0.05f, 0.05f, 0.06f, 0.0f));
        ashCloudLayer = CreateFogLayer("AshCloud", 0.010f, new Color(1.0f, 1.0f, 1.0f, 0.0f), ashCloudRamp, new Vector2(0.90f, 0.90f), mirrorSize, 3, out ashCloudRect);

        Gradient heavyAshRamp = new Gradient();
        heavyAshRamp.SetColor(0, new Color(0.02f, 0.02f, 0.03f, 0.0f));
        heavyAshRamp.SetColor(1, new Color(0.0f, 0.0f, 0.0f, 0.95f));
        heavyAshRamp.AddPoint(0.25f, new Color(0.02f, 0.02f, 0.03f, 0.0f));
        heavyAshLayer = CreateFogLayer("HeavyAsh", 0.009f, new Color(1.0f, 1.0f, 1.0f, 0.0f), heavyAshRamp, new Vector2(0.98f, 0.98f), mirrorSize, 3, out heavyAshRect);

        // ==========================================================
        // PART 2: PATCHY, SEPARATED YELLOW SMOG (Top Layers)
        // ==========================================================

        Gradient deepFogRamp = new Gradient();
        deepFogRamp.SetColor(0, new Color(0.20f, 0.22f, 0.25f, 0.0f));
        deepFogRamp.SetColor(1, new Color(0.25f, 0.27f, 0.30f, 0.70f));
        deepFogRamp.AddPoint(0.45f, new Color(0.20f, 0.22f, 0.25f, 0.0f));
        deepFogLayer = CreateFogLayer("DeepFog", 0.008f, new Color(1.0f, 1.0f, 1.0f, 0.0f), deepFogRamp, new Vector2(1.02f, 1.02f), mirrorSize, 3, out deepFogRect);

        Gradient lowMistRamp = new Gradient();
        lowMistRamp.SetColor(0, new Color(0.30f, 0.30f, 0.30f, 0.0f));
        lowMistRamp.SetColor(1, new Color(0.35f, 0.35f, 0.35f, 0.75f));
        lowMistRamp.AddPoint(0.48f, new Color(0.30f, 0.30f, 0.30f, 0.0f));
        lowMistLayer = CreateFogLayer("LowMist", 0.014f, new Color(1.0f, 1.0f, 1.0f, 0.0f), lowMistRamp, new Vector2(1.08f, 1.08f), mirrorSize, 3, out lowMistRect);

        Gradient naturalSmogRamp = new Gradient();
        naturalSmogRamp.SetColor(0, new Color(0.68f, 0.62f, 0.48f, 0.0f));
        naturalSmogRamp.SetColor(1, new Color(0.78f, 0.68f, 0.42f, 0.85f));
        naturalSmogRamp.AddPoint(0.50f, new Color(0.68f, 0.62f, 0.48f, 0.0f));
        naturalSmogRamp.AddPoint(0.75f, new Color(0.74f, 0.66f, 0.45f, 0.50f));
        yellowSmogLayer = CreateFogLayer("NaturalSmog", 0.012f, new Color(1.0f, 1.0f, 1.0f, 0.0f), naturalSmogRamp, new Vector2(1.15f, 1.15f), mirrorSize, 3, out yellowSmogRect);

        ApplyPollutionVisuals();

        // Use CallDeferred so we guarantee GameManager.Instance is ready
        CallDeferred(MethodName.ConnectToGameManager);
    }

    private void ConnectToGameManager()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StatsChanged += UpdateSmogFromStats;
            UpdateSmogFromStats(); // Force an update right away so the smog matches starting pollution
        }
        else
        {
            GD.PrintErr("SmogManager could not find GameManager.Instance!");
        }
    }

    private void UpdateSmogFromStats()
    {
        // Translate the raw integer pollution into a 0.0 to 1.0 float scale
        float rawPollution = GameManager.Instance.Pollution;
        float calculatedDensity = Mathf.Clamp(rawPollution / MaxPollutionForVisuals, 0.0f, 1.0f);

        SetPollutionDensity(calculatedDensity);
    }

    private ParallaxLayer CreateFogLayer(string name, float frequency, Color baseColor, Gradient customGradient, Vector2 parallaxScale, Vector2 mirrorSize, int octaves, out TextureRect rect)
    {
        ParallaxLayer pLayer = new ParallaxLayer();
        pLayer.Name = name + "Layer";
        pLayer.MotionScale = parallaxScale;
        pLayer.MotionMirroring = mirrorSize;
        combinedLayer.AddChild(pLayer);

        rect = new TextureRect();
        rect.Name = name;
        rect.Size = mirrorSize;
        rect.MouseFilter = Control.MouseFilterEnum.Ignore;
        rect.TextureFilter = CanvasItem.TextureFilterEnum.Linear;
        rect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;

        FastNoiseLite noise = new FastNoiseLite();
        noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        noise.Frequency = frequency;
        noise.Seed = (int)GD.Randi();
        noise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        noise.FractalOctaves = octaves;

        NoiseTexture2D texture = new NoiseTexture2D();
        texture.Width = TextureResolution;
        texture.Height = TextureResolution;
        texture.Seamless = true;
        texture.Noise = noise;

        if (customGradient != null)
        {
            texture.ColorRamp = customGradient;
        }

        rect.Texture = texture;
        rect.Modulate = baseColor;

        pLayer.AddChild(rect);
        return pLayer;
    }

    public override void _Process(double delta)
    {
        float fDelta = (float)delta;
        masterWindTime += fDelta * 0.05f;

        Vector2 smokeWind = GetReliableWindVector(masterWindTime);
        Vector2 smogWind = GetReliableWindVector(masterWindTime * 1.5f + 300f);

        deepAshLayer.MotionOffset += smokeWind * DeepAshBaseSpeed * fDelta;
        ashCloudLayer.MotionOffset += smokeWind * AshCloudBaseSpeed * fDelta;
        heavyAshLayer.MotionOffset += smokeWind * HeavyAshBaseSpeed * fDelta;

        deepFogLayer.MotionOffset += smogWind * DeepFogBaseSpeed * fDelta;
        lowMistLayer.MotionOffset += smogWind * LowMistBaseSpeed * fDelta;
        yellowSmogLayer.MotionOffset += smogWind * YellowSmogBaseSpeed * fDelta;

        if (Mathf.Abs(currentDensity - targetDensity) > 0.001f)
        {
            currentDensity = Mathf.Lerp(currentDensity, targetDensity, LerpSpeed * fDelta);
            ApplyPollutionVisuals();
        }
    }

    private Vector2 GetReliableWindVector(float time)
    {
        float swirlX = windNoise.GetNoise2D(time * 10.0f, 0.0f) * 0.5f;
        float swirlY = windNoise.GetNoise2D(0.0f, time * 10.0f) * 0.5f;

        Vector2 combinedWind = BaseWindDirection.Normalized() + new Vector2(swirlX, swirlY);

        if (combinedWind.Length() < 0.4f)
        {
            combinedWind = BaseWindDirection.Normalized() * 0.4f;
        }

        return combinedWind;
    }

    public void SetPollutionDensity(float density0to1)
    {
        targetDensity = Mathf.Clamp(density0to1, 0.0f, 1.0f);
    }

    private void ApplyPollutionVisuals()
    {
        if (deepAshRect != null)
            deepAshRect.Modulate = new Color(1.0f, 1.0f, 1.0f, currentDensity * MaxBackSmokeAlpha);
        if (ashCloudRect != null)
            ashCloudRect.Modulate = new Color(1.0f, 1.0f, 1.0f, currentDensity * MaxMidSmokeAlpha);
        if (heavyAshRect != null)
            heavyAshRect.Modulate = new Color(1.0f, 1.0f, 1.0f, currentDensity * MaxForeSmokeAlpha);

        if (deepFogRect != null)
            deepFogRect.Modulate = new Color(1.0f, 1.0f, 1.0f, currentDensity * MaxGraySmogAlpha);
        if (lowMistRect != null)
            lowMistRect.Modulate = new Color(1.0f, 1.0f, 1.0f, currentDensity * MaxMidSmogAlpha);
        if (yellowSmogRect != null)
            yellowSmogRect.Modulate = new Color(1.0f, 1.0f, 1.0f, currentDensity * MaxYellowSmogAlpha);
    }
}