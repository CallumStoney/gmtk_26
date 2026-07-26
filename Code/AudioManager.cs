using Godot;

public partial class AudioManager : Node
{
	public static AudioManager Instance { get; private set; }

	//the actual players need to be separate
	private AudioStreamPlayer _musicPlayer;
	private AudioStreamPlayer _ambiancePlayer;
	private AudioStreamPlayer _homeMusicPlayer;
	private AudioStreamPlayer _sfxBuildPlace;
	private AudioStreamPlayer _sfxBuildDestroy;
	private AudioStreamPlayer _sfxCannotAfford;
	private AudioStreamPlayer _sfxClick;
	private AudioStreamPlayer _sfxEmailRead;

	private bool _isMuted = false;

	private CanvasLayer _muteOverlay;
	private Label _muteLabel;

	//plz change these to actually be good this is just a quick one
	private const float CityMusicDb = -6f;
	private const float RoomMusicDb = -14f; //this one might need ti be tweeked but if u do before u do that try changing its fade in time
	private const float AmbianceDb = -6f;
	private const float SilentDb = -80f;
	private const float FadeDuration = 1.0f;
	private const float MusicStartFadeDuration = 2.0f;

	private const float HomeMusicDb = -10f;
	private const float HomeMusicFadeDuration = 3.0f;

	private bool _musicStarted = false;

	public override void _Ready()
	{
		Instance = this;

		_musicPlayer = new AudioStreamPlayer();
		var musicStream = GD.Load<AudioStream>("res://Sound/calm city music loop.mp3");
		if (musicStream is AudioStreamMP3 mp3Music) mp3Music.Loop = true;
		_musicPlayer.Stream = musicStream;
		_musicPlayer.VolumeDb = SilentDb;
		AddChild(_musicPlayer);

		// ambicace
		_ambiancePlayer = new AudioStreamPlayer();
		var ambianceStream = GD.Load<AudioStream>("res://Sound/office ambiance sound (play over a quieted city music loop).mp3");
		if (ambianceStream is AudioStreamMP3 mp3Ambiance) mp3Ambiance.Loop = true;
		_ambiancePlayer.Stream = ambianceStream;
		_ambiancePlayer.VolumeDb = SilentDb;
		AddChild(_ambiancePlayer);

		//home screen music
		_homeMusicPlayer = new AudioStreamPlayer();
		var homeStream = GD.Load<AudioStream>("res://Sound/Homescreen Sound.wav");
		if (homeStream is AudioStreamWav wavHome) wavHome.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
		_homeMusicPlayer.Stream = homeStream;
		_homeMusicPlayer.VolumeDb = SilentDb;
		AddChild(_homeMusicPlayer);

		_sfxBuildPlace = CreateSfxPlayer("res://Sound/Building place thump.wav");
		_sfxBuildDestroy = CreateSfxPlayer("res://Sound/building destroyed crumble.wav");
		_sfxCannotAfford = CreateSfxPlayer("res://Sound/Cannot afford bloop.wav");
		_sfxClick = CreateSfxPlayer("res://Sound/PopSFXForClicking.wav");
		_sfxEmailRead = CreateSfxPlayer("res://Sound/SFX that plays when you read an email (EDIT IT).wav");

		//persistant btw
		CreateMuteIndicator();
	}

	private void CreateMuteIndicator()
	{
		//I Like allways seeing ts
		_muteOverlay = new CanvasLayer();
		_muteOverlay.Layer = 100;
		AddChild(_muteOverlay);

		_muteLabel = new Label();
		_muteLabel.Text = "";
		_muteLabel.HorizontalAlignment = HorizontalAlignment.Right;
		_muteLabel.VerticalAlignment = VerticalAlignment.Top;
	
		_muteLabel.AnchorLeft = 1.0f;
		_muteLabel.AnchorRight = 1.0f;
		_muteLabel.AnchorTop = 0.0f;
		_muteLabel.AnchorBottom = 0.0f;
		_muteLabel.OffsetLeft = -120;
		_muteLabel.OffsetRight = -8;
		_muteLabel.OffsetTop = 8;
		_muteLabel.OffsetBottom = 32;
		_muteLabel.GrowHorizontal = Control.GrowDirection.Begin;

		_muteLabel.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.4f, 1f));
		_muteLabel.AddThemeFontSizeOverride("font_size", 14);

		_muteOverlay.AddChild(_muteLabel);

		UpdateMuteIndicator();
	}

	private void UpdateMuteIndicator()
	{
		if (_muteLabel == null) return;

		if (_isMuted)
		{
			_muteLabel.Text = "MUTED [M]";
			_muteLabel.Visible = true;
		}
		else
		{
			_muteLabel.Visible = false;
		}
	}

	private AudioStreamPlayer CreateSfxPlayer(string path)
	{
		var player = new AudioStreamPlayer();
		player.Stream = GD.Load<AudioStream>(path);
		AddChild(player);
		return player;
	}

	public void StartMusic()
	{
		if (_musicStarted) 
			return;
		_musicStarted = true;

		_musicPlayer.VolumeDb = SilentDb;
		_musicPlayer.Play();
		TweenVolume(_musicPlayer, CityMusicDb, MusicStartFadeDuration);
	}

	public void SetScene(string scenePath)
	{
		if (!_musicStarted) return;

		if (scenePath.Contains("Room"))
		{
			//this should be like decreaseing music and fading in office sounds
			TweenVolume(_musicPlayer, RoomMusicDb, FadeDuration);

			if (!_ambiancePlayer.Playing)
				_ambiancePlayer.Play();
			TweenVolume(_ambiancePlayer, AmbianceDb, FadeDuration);
		}
		else{
			//and inverse of above
			TweenVolume(_musicPlayer, CityMusicDb, FadeDuration);
			TweenVolume(_ambiancePlayer, SilentDb, FadeDuration);
		}
	}


	private void TweenVolume(AudioStreamPlayer player, float targetDb, float duration)
	{
		var tween = CreateTween();
		tween.TweenProperty(player, "volume_db", targetDb, duration)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.InOut);
	}

	public void PlayBuildingPlace() => _sfxBuildPlace.Play();
	public void PlayBuildingDestroy() => _sfxBuildDestroy.Play();
	public void PlayCannotAfford() => _sfxCannotAfford.Play();
	public void PlayClick() => _sfxClick.Play();
	public void PlayEmailRead() => _sfxEmailRead.Play();

	// Global M key shortcut for mute toggle
	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
		{
			if (keyEvent.Keycode == Key.M)
			{
				ToggleMute();
				GetViewport().SetInputAsHandled();
			}
		}
	}

	public void StartHomeMusic()
	{
		_homeMusicPlayer.VolumeDb = SilentDb;
		_homeMusicPlayer.Play();
		TweenVolume(_homeMusicPlayer, HomeMusicDb, HomeMusicFadeDuration);
	}

	public void StopHomeMusic(){
		TweenVolume(_homeMusicPlayer, SilentDb, 0.5f);
	}

	public bool IsMuted => _isMuted;

	public void ToggleMute()
	{
		_isMuted = !_isMuted;
		AudioServer.SetBusMute(AudioServer.GetBusIndex("Master"), _isMuted);
		UpdateMuteIndicator();
	}
}
