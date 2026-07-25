using Godot;
using System;

public partial class SmogManager : Node
{
	// --- HIGH-CONTRAST SMOKE SETTINGS (Bottom Layers) ---
	// Modulates are kept balanced so background art stays readable
	[Export] public float MaxBackSmokeAlpha = 0.35f;
	[Export] public float MaxMidSmokeAlpha = 0.48f;
	[Export] public float MaxForeSmokeAlpha = 0.60f;

	// Dropped to an absolute crawl for a massive 10x speed difference!
	[Export] public float DeepAshBaseSpeed = 1.5f;
	[Export] public float AshCloudBaseSpeed = 3.0f;
	[Export] public float HeavyAshBaseSpeed = 5.0f;

	// --- WIDE-COVERAGE YELLOW SMOG SETTINGS (Top Layers) ---
	[Export] public float MaxGraySmogAlpha = 0.28f;
	[Export] public float MaxMidSmogAlpha = 0.42f;
	[Export] public float MaxYellowSmogAlpha = 0.58f;

	// Fast sweeping speeds to shear across the slow smoke underneath
	[Export] public float DeepFogBaseSpeed = 20.0f;
	[Export] public float LowMistBaseSpeed = 35.0f;
	[Export] public float YellowSmogBaseSpeed = 50.0f;

	// --- GENERAL SETTINGS ---
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
		windNoise.Frequency = 0.01f; 
		windNoise.Seed = (int)GD.Randi();
		
		masterWindTime = GD.Randf() * 500f;

		Vector2 screenSize = GetViewport().GetVisibleRect().Size;
		float maxDim = Mathf.Max(screenSize.X, screenSize.Y);
		Vector2 mirrorSize = new Vector2(maxDim, maxDim) * 3.0f; 

		// ==========================================================
		// PART 1: HIGH-CONTRAST, SLOW CRAWLING SMOKE (Bottom Layers)
		// Wider zero-alpha gaps + sharper ramps into darker charcoal RGBs
		// ==========================================================
		
		Gradient deepAshRamp = new Gradient();
		deepAshRamp.SetColor(0, new Color(0.20f, 0.22f, 0.25f, 0.0f)); 
		deepAshRamp.SetColor(1, new Color(0.12f, 0.14f, 0.16f, 0.80f)); // Punchy slate peak
		deepAshRamp.AddPoint(0.40f, new Color(0.20f, 0.22f, 0.25f, 0.0f)); // 40% transparent for distinct shapes
		deepAshLayer = CreateFogLayer("DeepAsh", 0.0035f, new Color(1.0f, 1.0f, 1.0f, 0.0f), deepAshRamp, new Vector2(0.80f, 0.80f), mirrorSize, 5, out deepAshRect);
		
		Gradient ashCloudRamp = new Gradient();
		ashCloudRamp.SetColor(0, new Color(0.18f, 0.20f, 0.23f, 0.0f)); 
		ashCloudRamp.SetColor(1, new Color(0.09f, 0.11f, 0.13f, 0.85f)); // Deep charcoal pockets
		ashCloudRamp.AddPoint(0.35f, new Color(0.18f, 0.20f, 0.23f, 0.0f)); 
		ashCloudLayer = CreateFogLayer("AshCloud", 0.0055f, new Color(1.0f, 1.0f, 1.0f, 0.0f), ashCloudRamp, new Vector2(0.90f, 0.90f), mirrorSize, 5, out ashCloudRect);

		Gradient heavyAshRamp = new Gradient();
		heavyAshRamp.SetColor(0, new Color(0.15f, 0.18f, 0.20f, 0.0f)); 
		heavyAshRamp.SetColor(1, new Color(0.06f, 0.08f, 0.10f, 0.90f)); // Sharp near-black contrast wisps
		heavyAshRamp.AddPoint(0.30f, new Color(0.15f, 0.18f, 0.20f, 0.0f)); 
		heavyAshLayer = CreateFogLayer("HeavyAsh", 0.0045f, new Color(1.0f, 1.0f, 1.0f, 0.0f), heavyAshRamp, new Vector2(0.98f, 0.98f), mirrorSize, 5, out heavyAshRect);

		// ==========================================================
		// PART 2: SWEEPING YELLOW SMOG WITH VIEWING WINDOWS (Top Layers)
		// Clean 30% skylights ensure the dark smoke below is always revealed
		// ==========================================================

		Gradient deepFogRamp = new Gradient();
		deepFogRamp.SetColor(0, new Color(0.20f, 0.22f, 0.25f, 0.0f)); 
		deepFogRamp.SetColor(1, new Color(0.25f, 0.27f, 0.30f, 0.70f)); 
		deepFogRamp.AddPoint(0.25f, new Color(0.20f, 0.22f, 0.25f, 0.0f)); 
		deepFogLayer = CreateFogLayer("DeepFog", 0.003f, new Color(1.0f, 1.0f, 1.0f, 0.0f), deepFogRamp, new Vector2(1.02f, 1.02f), mirrorSize, 4, out deepFogRect);
		
		Gradient lowMistRamp = new Gradient();
		lowMistRamp.SetColor(0, new Color(0.30f, 0.30f, 0.30f, 0.0f)); 
		lowMistRamp.SetColor(1, new Color(0.35f, 0.35f, 0.35f, 0.75f)); 
		lowMistRamp.AddPoint(0.25f, new Color(0.30f, 0.30f, 0.30f, 0.0f)); 
		lowMistLayer = CreateFogLayer("LowMist", 0.006f, new Color(1.0f, 1.0f, 1.0f, 0.0f), lowMistRamp, new Vector2(1.08f, 1.08f), mirrorSize, 4, out lowMistRect);

		Gradient naturalSmogRamp = new Gradient();
		naturalSmogRamp.SetColor(0, new Color(0.68f, 0.62f, 0.48f, 0.0f)); 
		naturalSmogRamp.SetColor(1, new Color(0.78f, 0.68f, 0.42f, 0.85f)); 
		naturalSmogRamp.AddPoint(0.30f, new Color(0.68f, 0.62f, 0.48f, 0.0f)); // 30% clean viewing window
		naturalSmogRamp.AddPoint(0.65f, new Color(0.74f, 0.66f, 0.45f, 0.50f)); 
		yellowSmogLayer = CreateFogLayer("NaturalSmog", 0.0045f, new Color(1.0f, 1.0f, 1.0f, 0.0f), naturalSmogRamp, new Vector2(1.15f, 1.15f), mirrorSize, 4, out yellowSmogRect);

		ApplyPollutionVisuals();
		GD.Print("High-contrast crawling smoke layered under sweeping smog initialized!");
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
		texture.Width = 1024;  
		texture.Height = 1024;
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
		masterWindTime += fDelta * 0.02f;

		// 1. Base wind vector for the slow, crawling smoke underneath
		Vector2 smokeWind = GetSmoothWindVector(masterWindTime);
		
		// 2. Offset wind vector for the top smog so it shears across at a different angle
		Vector2 smogWind = GetSmoothWindVector(masterWindTime * 1.2f + 300f);

		// Apply ULTRA-SLOW crawling movement to the bottom High-Contrast Smoke layers
		deepAshLayer.MotionOffset += smokeWind * DeepAshBaseSpeed * fDelta;
		ashCloudLayer.MotionOffset += smokeWind * AshCloudBaseSpeed * fDelta;
		heavyAshLayer.MotionOffset += smokeWind * HeavyAshBaseSpeed * fDelta;

		// Apply FAST sweeping movement to the top Yellow Smog layers
		deepFogLayer.MotionOffset += smogWind * DeepFogBaseSpeed * fDelta;
		lowMistLayer.MotionOffset += smogWind * LowMistBaseSpeed * fDelta;
		yellowSmogLayer.MotionOffset += smogWind * YellowSmogBaseSpeed * fDelta;

		if (Mathf.Abs(currentDensity - targetDensity) > 0.001f)
		{
			currentDensity = Mathf.Lerp(currentDensity, targetDensity, LerpSpeed * fDelta);
			ApplyPollutionVisuals();
		}

		if (Input.IsActionJustPressed("ui_up"))
		{
			SetPollutionDensity(Mathf.Clamp(targetDensity + 0.2f, 0.0f, 1.0f));
		}
		else if (Input.IsActionJustPressed("ui_down"))
		{
			SetPollutionDensity(Mathf.Clamp(targetDensity - 0.2f, 0.0f, 1.0f));
		}
	}

	private Vector2 GetSmoothWindVector(float time)
	{
		float noiseX = windNoise.GetNoise2D(time * 10.0f, 0.0f);
		float noiseY = windNoise.GetNoise2D(0.0f, time * 10.0f);
		
		Vector2 rawDir = new Vector2(noiseX, noiseY);
		
		if (rawDir.Length() < 0.2f)
		{
			rawDir = rawDir.Normalized() * 0.5f;
		}

		return rawDir;
	}

	public void SetPollutionDensity(float density0to1)
	{
		targetDensity = Mathf.Clamp(density0to1, 0.0f, 1.0f);
	}

	private void ApplyPollutionVisuals()
	{
		// Apply alphas to High-Contrast Crawling Smoke (Bottom)
		if (deepAshRect != null)
			deepAshRect.Modulate = new Color(1.0f, 1.0f, 1.0f, currentDensity * MaxBackSmokeAlpha); 
		if (ashCloudRect != null)
			ashCloudRect.Modulate = new Color(1.0f, 1.0f, 1.0f, currentDensity * MaxMidSmokeAlpha);
		if (heavyAshRect != null)
			heavyAshRect.Modulate = new Color(1.0f, 1.0f, 1.0f, currentDensity * MaxForeSmokeAlpha);

		// Apply alphas to Wide-Coverage Yellow Smog (Top)
		if (deepFogRect != null)
			deepFogRect.Modulate = new Color(1.0f, 1.0f, 1.0f, currentDensity * MaxGraySmogAlpha); 
		if (lowMistRect != null)
			lowMistRect.Modulate = new Color(1.0f, 1.0f, 1.0f, currentDensity * MaxMidSmogAlpha);
		if (yellowSmogRect != null)
			yellowSmogRect.Modulate = new Color(1.0f, 1.0f, 1.0f, currentDensity * MaxYellowSmogAlpha);
	}
}
