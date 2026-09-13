using UnityEngine;

namespace LandOfFire.BunnyStep
{
    public sealed class MovementLab : MonoBehaviour
    {
        public BunnyFighter left, right;
        public float minX = -7, maxX = 7;
        [Min(1)] public int ticksPerSecond = 60;
        public bool hitstop;
        private double accumulated;
        private long tick;
        private float spawnA, spawnB;
        private bool ready;

        private void Start()
        {
            if (left == null || right == null || left == right || left.X >= right.X ||
                maxX - minX < 2 * (left.Width + right.Width))
            {
                Debug.LogError("MovementLab: revisar referencias, orden izquierda/derecha y ancho de arena.", this);
                enabled = false;
                return;
            }
            spawnA = left.X; spawnB = right.X;
            ready = true;
            ResetLab();
        }

        [ContextMenu("Reiniciar laboratorio")]
        public void ResetLab()
        {
            if (!ready) return;
            tick = 0; accumulated = 0;
            left.ResetFighter(1); right.ResetFighter(-1);
            Resolve(spawnA, spawnB);
        }

        private void Update()
        {
            if (!ready) return;
            if (!left.isActiveAndEnabled || !right.isActiveAndEnabled) { accumulated = 0; return; }
            if (maxX - minX < 2 * (left.Width + right.Width)) return;
            double interval = 1.0 / Mathf.Max(1, ticksPerSecond);
            accumulated += Time.deltaTime;
            int budget = 8;
            while (accumulated >= interval && budget-- > 0)
            {
                accumulated -= interval;
                float a = left.Propose(tick, 1, hitstop);
                float b = right.Propose(tick, -1, hitstop);
                Resolve(a, b);
                if (!hitstop) tick++;
            }
        }

        private void Resolve(float a, float b)
        {
            ArenaSeparation.Resolve(left.X, right.X, left.Width, right.Width, minX, maxX, ref a, ref b);
            left.ApplyPosition(a); right.ApplyPosition(b);
        }
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(new Vector3(minX, 0, 0), new Vector3(minX, 3, 0));
            Gizmos.DrawLine(new Vector3(maxX, 0, 0), new Vector3(maxX, 3, 0));
            Gizmos.DrawLine(new Vector3(minX, 0, 0), new Vector3(maxX, 0, 0));
        }
    }
}
