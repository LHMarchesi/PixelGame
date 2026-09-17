// Land of Fire · Mapeo serializable de una fase lógica a un estado Animator.
// FitPhase o Loop controlan el muestreo visual sin cambiar la duración en ticks.
using System;
using UnityEngine;
namespace LandOfFire.BunnyStep
{
    public enum PhasePlayback { FitPhase, Loop }
    [Serializable]
    public sealed class PhaseAnimation
    {
        [Tooltip("Ruta completa del estado dentro del Animator.")]
        public string stateName;
        public PhasePlayback playback = PhasePlayback.FitPhase;
        [Min(1), Tooltip("Ticks por repetición cuando Playback es Loop.")]
        public int loopTicks = 30;
        public PhaseAnimation(string name) { stateName = "Base Layer." + name; }

        public float Sample(int elapsed, int duration)
        {
            int index = Math.Max(0, elapsed - 1);
            if (playback == PhasePlayback.Loop) return (index % Math.Max(1, loopTicks)) / (float)Math.Max(1, loopTicks);
            return duration <= 1 ? 0 : Mathf.Min(.99999f, index / (float)(duration - 1));
        }
    }
}