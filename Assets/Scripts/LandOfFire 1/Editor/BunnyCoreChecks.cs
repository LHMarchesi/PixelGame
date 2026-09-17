// Land of Fire · Comprobaciones desde el menú Land of Fire en el Editor.
// Valida la máquina real de Bunny y L sin entrar en Play; no forma parte del build.
using System;
using UnityEditor;
using UnityEngine;

namespace LandOfFire.BunnyStep.Editor
{
    // Pruebas del núcleo real, sin dependencia de NUnit ni asmdefs.
    public static class BunnyCoreChecks
    {
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("Bunny check: " + message);
        }
        private static void Press(BunnyStateMachine m, int direction, long tick)
        { m.Press(direction, tick, 12, 12, 1.2f, .3f, 14, .9f, .25f); }

        [MenuItem("Land of Fire/Verificar núcleo Bunny y golpe")]
        public static void Run()
        {
            var m = new BunnyStateMachine();
            int transitions = 0;
            m.StateChanged += (from, to) => transitions++;
            m.BeginTick(1, 1); Press(m, 1, 0);
            Check(m.State == FighterState.BunnyForward, "entrada adelante");
            float distance = 0, peak = 0;
            for (int i = 0; i < 12; i++)
            {
                distance += m.Advance(); peak = Math.Max(peak, m.VisualHeight);
                Press(m, -1, i + 1); // No puede interrumpir el paso.
            }
            Check(Math.Abs(distance - 1.2f) < .0001f, "distancia total");
            Check(Math.Abs(peak - .3f) < .0001f, "pico del arco");
            Check(m.VisualHeight == 0, "aterrizaje exacto");
            m.BeginTick(1, 1);
            Check(m.State == FighterState.BunnyForwardLoop, "hold adelante repite sin anticipation");
            m.Advance();
            Check(m.Phase == BunnyPhase.Flight, "hold entra directo a flight");
            for (int i = 1; i < m.Duration; i++) m.Advance();
            m.BeginTick(1, 0);
            Check(m.State == FighterState.Idle && m.Advance() == 0, "soltar sale al final del ciclo");
            Check(transitions == 3, "transiciones emitidas una vez");
            m.BeginTick(1, -1); Press(m, -1, 20);
            Check(m.State == FighterState.Guard, "primer atrás solicita guardia");
            m.BeginTick(1, 0); Check(m.State == FighterState.Idle, "soltar libera guardia");
            m.BeginTick(1, -1); Press(m, -1, 32);
            Check(m.State == FighterState.BunnyBackward, "doble tap inclusivo");
            distance = 0;
            for (int i = 0; i < 14; i++) distance += m.Advance();
            Check(Math.Abs(distance + .9f) < .0001f, "distancia atrás");
            m.Reset(); m.BeginTick(1, -1); Press(m, -1, 0); Press(m, -1, 13);
            Check(!m.IsStepping, "tap vencido");
            m.ForgetTap(); Press(m, -1, 14); Check(!m.IsStepping, "limpieza hitstop");
            m.BeginTick(-1, 1); Press(m, 1, 15); Check(!m.IsStepping, "cambio facing limpia historial");
            m.Reset(); m.BeginTick(-1, -1); Press(m, -1, 16);
            Check(m.State == FighterState.BunnyForward, "entrada adelante relativa al facing");
            m.Advance(); m.Advance();
            Check(m.Advance() < 0, "flight avanza según facing después de preparation");

            var light = ScriptableObject.CreateInstance<AttackMoveData>();
            try
            {
                light.anticipationTicks = 3;
                light.smearTicks = 1;
                light.poseTicks = 2;
                light.recoveryTicks = 2;
                m.Reset(); m.BeginTick(1, 0);
                Check(m.TryStartAttack(light), "L comienza desde Idle");
                Check(m.Duration == 8, "duración del ataque por ticks");
                light.anticipationTicks = 6;
                Check(m.Duration == 8, "editar el asset no cambia el ataque en curso");
                light.anticipationTicks = 3;
                AttackPhase[] expected = {
                    AttackPhase.Anticipation, AttackPhase.Anticipation, AttackPhase.Anticipation,
                    AttackPhase.Smear, AttackPhase.Pose, AttackPhase.Pose,
                    AttackPhase.Recovery, AttackPhase.Recovery
                };
                for (int i = 0; i < expected.Length; i++)
                {
                    Check(m.Advance() == 0 && m.CurrentAttackPhase == expected[i], "orden de fases L tick " + i);
                }
                m.BeginTick(1, 0);
                Check(m.State == FighterState.Idle && m.CurrentAttack == null, "miss retorna a Idle");

                m.BeginTick(1, 0); Check(m.TryStartAttack(light), "segundo L inicia");
                m.Advance(); m.EnterHurt(4);
                Check(m.IsHurt && m.CurrentAttack == null && m.CurrentAttackPhase == AttackPhase.None,
                    "Hurt interrumpe el ataque y apaga su hitbox");
                for (int i = 0; i < 4; i++) m.Advance();
                m.BeginTick(1, 0);
                Check(m.State == FighterState.Idle, "Hurt termina en Idle");

                m.BeginTick(1, 1); Press(m, 1, 99);
                Check(m.IsStepping && !m.TryStartAttack(light), "Bunny no cancela sin autorización");
                light.allowBunnyCancel = true;
                Check(m.TryStartAttack(light) && m.IsAttacking, "Bunny cancela al habilitar L");
            }
            finally { UnityEngine.Object.DestroyImmediate(light); }

            var random = new System.Random(7);
            for (int i = 0; i < 10000; i++)
            {
                float wa = .1f + (float)random.NextDouble(), wb = .1f + (float)random.NextDouble();
                float oldA = -4, oldB = 4;
                float a = oldA + (float)random.NextDouble() * 40 - 20;
                float b = oldB + (float)random.NextDouble() * 40 - 20;
                ArenaSeparation.Resolve(oldA, oldB, wa, wb, -7, 7, ref a, ref b);
                Check(a >= -7 + wa - .0001f && b <= 7 - wb + .0001f && b - a >= wa + wb - .0001f,
                    "límites y separación");
            }
            float nearA = 0, nearB = .8f;
            ArenaSeparation.Resolve(0, .8f, .4f, .4f, -7, 7, ref nearA, ref nearB);
            nearA += 1;
            ArenaSeparation.Resolve(0, .8f, .4f, .4f, -7, 7, ref nearA, ref nearB);
            Check(Math.Abs(nearB - .8f) < .0001f, "no empujar rival quieto");
            Debug.Log("Core Checks: OK (Bunny, L por ticks, interrupción Hurt, cancel y límites).");
        }
    }
}
