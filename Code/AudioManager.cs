using Godot;

public partial class AudioManager : Node
{
	public static AudioManager Instance { get; private set; }

	//the actual players need to be separate
	private AudioStreamPlayer _musicPlayer;
	private AudioStreamPlayer _ambiancePlayer;
	private AudioStreamPlayer _sfxBuildPlace;
	private AudioStreamPlayer _sfxBuildDestroy;
	private AudioStreamPlayer _sfxCannotAfford;
	private AudioStreamPlayer _sfxClick;
	private AudioStreamPlayer _sfxEmailRead;

	//plz change these to actually be good this is just a quick one
	private const float CityMusicDb = -6f;
	private const float RoomMusicDb = -14f; //this one might need ti be tweeked but if u do before u do that try changing its fade in time
	private const float AmbianceDb = -6f;
	private const float SilentDb = -80f;
	private const float FadeDuration = 1.0f;
	private const float MusicStartFadeDuration = 2.0f;

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
-
		_sfxBuildPlace = CreateSfxPlayer("res://Sound/Building place thump.wav");
		_sfxBuildDestroy = CreateSfxPlayer("res://Sound/building destroyed crumble.wav");
		_sfxCannotAfford = CreateSfxPlayer("res://Sound/Cannot afford bloop.wav");
		_sfxClick = CreateSfxPlayer("res://Sound/PopSFXForClicking.wav");
		_sfxEmailRead = CreateSfxPlayer("res://Sound/SFX that plays when you read an email (EDIT IT).wav");
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
}
