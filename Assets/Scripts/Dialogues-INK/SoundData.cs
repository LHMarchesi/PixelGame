using System;
using UnityEngine;

[Serializable]
public class SoundData
{
    public string name;
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;
}