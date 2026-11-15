// Land of Fire · Mapeo serializable de una fase lógica a un estado Animator.
// Permite muestrear un tramo concreto de una timeline y ajustarlo a ticks deterministas.
using System;
using UnityEngine;

namespace LandOfFire.BunnyStep
{
    public enum PhasePlayback
    {
        FitPhase,
        Loop
    }

    [Serializable]
    public sealed class PhaseAnimation
    {
        [Tooltip("Ruta completa del estado dentro del Animator.")]
        public string stateName;

        [Header("Timeline range")]
        [Tooltip("Cuando está desactivado, conserva el muestreo completo del clip como antes. Activarlo permite reproducir un rango de frames concreto de la timeline.")]
        public bool useTimelineRange;

        [Min(1), Tooltip("Cantidad total de frames de la timeline referenciada.")]
        public int timelineFrameCount = 20;

        [Min(1), Tooltip("Primer frame del tramo que se reproduce (inclusive). Puede ser mayor que End Frame para reproducir hacia atrás.")]
        public int startFrame = 1;

        [Min(1), Tooltip("Último frame del tramo que se reproduce (inclusive). Puede ser menor que Start Frame para reproducir hacia atrás.")]
        public int endFrame = 20;

        [Header("Tick playback")]
        public PhasePlayback playback = PhasePlayback.FitPhase;

        [Min(1), Tooltip("Ticks de duración de esta timeline cuando Playback es FitPhase. La máquina usa este valor para avanzar Intro/Outro.")]
        public int timelineTicks = 20;

        [Min(1), Tooltip("Ticks por repetición cuando Playback es Loop.")]
        public int loopTicks = 30;

        public PhaseAnimation(string name)
        {
            stateName = "Base Layer." + name;
        }

        public int TimelineDurationTicks => Mathf.Max(1, timelineTicks);

        public float Sample(int elapsed, int duration)
        {
            int index = Math.Max(0, elapsed - 1);

            // Keep the established full-clip behavior for every existing
            // PhaseAnimation. Only directional Walk phases opt into frame ranges.
            if (!useTimelineRange)
            {
                if (playback == PhasePlayback.Loop)
                    return (index % Mathf.Max(1, loopTicks)) / (float)Mathf.Max(1, loopTicks);

                return duration <= 1
                    ? 0f
                    : Mathf.Min(.99999f, index / (float)(duration - 1));
            }

            float progress;
            if (playback == PhasePlayback.Loop)
            {
                int ticks = Mathf.Max(1, loopTicks);
                progress = ticks <= 1
                    ? 0f
                    : (index % ticks) / (float)(ticks - 1);
            }
            else
            {
                int ticks = Mathf.Max(1, duration);
                progress = ticks <= 1
                    ? 0f
                    : Mathf.Clamp01(index / (float)(ticks - 1));
            }

            int frameCount = Mathf.Max(1, timelineFrameCount);
            float firstFrame = Mathf.Clamp(startFrame, 1, frameCount);
            float lastFrame = Mathf.Clamp(endFrame, 1, frameCount);
            float frame = Mathf.Lerp(firstFrame, lastFrame, progress);

            // Animator normalized time 1 wraps to the beginning for looping states.
            // Frame N is sampled at the start of its frame, so divide by frameCount.
            float normalizedTime = (frame - 1f) / frameCount;
            return Mathf.Clamp(normalizedTime, 0f, .99999f);
        }
    }
}
