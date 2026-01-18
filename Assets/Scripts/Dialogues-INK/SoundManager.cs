using System.Collections.Generic;
using UnityEngine;

public class SoundManager: MonoBehaviour
{
    public static SoundManager Instance;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Sound Library")]
    [SerializeField] private List<SoundData> sounds = new List<SoundData>();

    private Dictionary<string, SoundData> soundDictionary = new Dictionary<string, SoundData>();

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Inicializa el diccionario de sonidos
        foreach (var sound in sounds)
        {
            if (!soundDictionary.ContainsKey(sound.name))
                soundDictionary.Add(sound.name, sound);
        }
    }

    /// <summary>
    /// Reproduce un efecto de sonido por nombre.
    /// </summary>
    public void PlaySound(string name)
    {
        if (!soundDictionary.ContainsKey(name))
        {
            Debug.LogWarning($"SoundManager: No se encontró el sonido '{name}'");
            return;
        }

        SoundData data = soundDictionary[name];
        sfxSource.PlayOneShot(data.clip, data.volume);
    }

    /// <summary>
    /// Reproduce música de fondo.
    /// Si ya está sonando la misma, no la reinicia.
    /// </summary>
    public void PlayMusic(string name)
    {
        if (!soundDictionary.ContainsKey(name))
        {
            Debug.LogWarning($"SoundManager: No se encontró la música '{name}'");
            return;
        }

        SoundData data = soundDictionary[name];
        if (musicSource.clip == data.clip && musicSource.isPlaying)
            return; // ya está sonando

        musicSource.clip = data.clip;
        musicSource.volume = data.volume;
        musicSource.loop = true;
        musicSource.Play();
    }

    /// <summary>
    /// Detiene la música actual.
    /// </summary>
    public void StopMusic()
    {
        musicSource.Stop();
    }

    /// <summary>
    /// Cambia el volumen general (0.0 - 1.0)
    /// </summary>
    public void SetMasterVolume(float volume)
    {
        
            musicSource.volume = volume;
            sfxSource.volume = volume;
    }

    /// <summary>
    /// Cambia el volumen de los efectos de sonido (solo SFX)
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        sfxSource.volume = volume;
    }

    /// <summary>
    /// Cambia el volumen de la música (solo música)
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        musicSource.volume = volume;
    }
}
