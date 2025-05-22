using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum AudioName
{
    GameBG,

    Hit1,
    Hit2,
    Hit3,

    Win,
    Lose
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private AudioSource musicSource;

    [SerializeField] private List<Sound> soundEffects;
    [SerializeField] private List<Sound> bgmList;

    [SerializeField] private GameObject audioPrefab;

    private Dictionary<AudioName, List<AudioSource>> _soundEffectSources = new();

    private Dictionary<AudioName, Sound> _musicDic;
    private Dictionary<AudioName, Sound> _soundEffectDic;

    private AudioName _currentMusicName;

    public float MasterSoundVolume { get; set; } = 1f;
    public float MasterMusicVolume { get; set; } = 1f;

    private const string MasterSoundVolumeKey = "MasterSoundVolume";
    private const string MasterMusicVolumeKey = "MasterMusicVolume";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Initialize();
    }

    private void Initialize()
    {
        MasterSoundVolume = PlayerPrefs.GetFloat(MasterSoundVolumeKey, 1f);
        MasterMusicVolume = PlayerPrefs.GetFloat(MasterMusicVolumeKey, 1f);
        _musicDic = bgmList.ToDictionary(x => x.name);
        _soundEffectDic = soundEffects.ToDictionary(x => x.name);
    }

    private AudioSource CreateSoundEffect(AudioName name)
    {
        var audio = Instantiate(audioPrefab, transform);
        var source = audio.GetComponent<AudioSource>();
        if (_soundEffectDic.TryGetValue(name, out var sound))
        {
            source.clip = sound.clip;
            source.volume = sound.volume * MasterSoundVolume;
            source.pitch = sound.pitch;
            source.loop = sound.loop;

            source.Play();

            if (_soundEffectSources.ContainsKey(sound.name))
            {
                _soundEffectSources[sound.name].Add(source);
            }
            else
            {
                _soundEffectSources.Add(sound.name, new List<AudioSource>() { source });
            }

            return source;
        }
        else
        {
            Debug.Log($"Audio {name} not found!");
            return null;
        }
    }

    public void PlayMusic(AudioName name)
    {
        _musicDic.TryGetValue(name, out var sound);
        if (sound != null)
        {
            _currentMusicName = name;
            musicSource.clip = sound.clip;
            musicSource.volume = sound.volume * MasterMusicVolume;
            musicSource.Play();
        }
    }

    public void StopMusic()
    {
        musicSource.Stop();
    }

    public AudioSource PlaySoundEffect(AudioName name)
    {
        if (_soundEffectSources.TryGetValue(name, out var sources))
        {
            for (var i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                if (source.isPlaying) continue;
                source.Play();
                return source;
            }
            return CreateSoundEffect(name);
        }
        else
        {
            return CreateSoundEffect(name);
        }
    }

    public void StopSoundEffect(AudioName name, AudioSource source)
    {
        if (source == null) return;
        if (_soundEffectSources.TryGetValue(name, out var sources))
        {
            var index = sources.IndexOf(source);
            if (index >= 0)
            {
                sources[index].Stop();
            }
        }
    }

    public void UpdateSoundVolume()
    {
        PlayerPrefs.SetFloat(MasterSoundVolumeKey, MasterSoundVolume);
        PlayerPrefs.Save();
        foreach (var (name, sources) in _soundEffectSources)
        {
            for (var i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                source.volume = _soundEffectDic[name].volume * MasterSoundVolume;
            }
        }
    }

    public void UpdateMusicVolume()
    {
        PlayerPrefs.SetFloat(MasterMusicVolumeKey, MasterMusicVolume);
        PlayerPrefs.Save();
        if (_musicDic.ContainsKey(_currentMusicName))
        {
            musicSource.volume = _musicDic[_currentMusicName].volume * MasterMusicVolume;
        }
    }
}

[System.Serializable]
public class Sound
{
    public AudioName name;
    public AudioClip clip;
    public bool loop;
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0.1f, 3f)] public float pitch = 1f;
}


