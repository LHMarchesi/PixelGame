// Land of Fire · Global Combo System.
// Tracks valid impacts received by each fighter, independently from the
// attack system that produced them. The shared resolver calls ProcessTick
// only after every contact for the current simulation tick has resolved.

using System.Collections.Generic;
using UnityEngine;

namespace LandOfFire.BunnyStep
{
    internal sealed class FighterComboSystem
    {
        private sealed class FighterHistory
        {
            public readonly List<long> receivedHitTicks =
                new List<long>(8);

            public FighterState lastState;
            public bool comboClosedByKnockdown;
        }

        private struct PendingImpact
        {
            public FighterMotor2D target;
            public int direction;
        }

        private readonly Dictionary<FighterMotor2D, FighterHistory> histories =
            new Dictionary<FighterMotor2D, FighterHistory>();

        // A list is intentional: two distinct valid impacts in the same tick
        // must both be counted. Duplicate hitboxes from one attack are already
        // filtered by FighterCombat2D before this event is raised.
        private readonly List<PendingImpact> pendingImpacts =
            new List<PendingImpact>(8);

        private readonly HashSet<FighterMotor2D> knockdownCandidates =
            new HashSet<FighterMotor2D>();

        private readonly Dictionary<FighterMotor2D, int> knockdownDirections =
            new Dictionary<FighterMotor2D, int>();

        public void Register(FighterMotor2D fighter)
        {
            if (fighter == null)
                return;

            if (!histories.ContainsKey(fighter))
            {
                histories.Add(fighter, new FighterHistory
                {
                    lastState = fighter.Machine.State
                });
            }
        }

        public void Unregister(FighterMotor2D fighter)
        {
            if (fighter == null)
                return;

            histories.Remove(fighter);
            knockdownCandidates.Remove(fighter);
            knockdownDirections.Remove(fighter);

            for (int i = pendingImpacts.Count - 1; i >= 0; i--)
            {
                if (pendingImpacts[i].target == fighter)
                    pendingImpacts.RemoveAt(i);
            }
        }

        public void QueueValidImpact(FighterMotor2D target, int direction)
        {
            if (target == null)
                return;

            pendingImpacts.Add(new PendingImpact
            {
                target = target,
                direction = direction >= 0 ? 1 : -1
            });
        }

        public void ProcessTick(
            IList<FighterMotor2D> fighters,
            long currentTick,
            int maxComboHits,
            int comboHitWindowTicks,
            bool forceKnockdownOnLimit)
        {
            int hitLimit = Mathf.Max(1, maxComboHits);
            int hitWindow = Mathf.Max(1, comboHitWindowTicks);

            // Expire old impacts and close a combo as soon as its defender
            // enters a knockdown sequence. A later return to Idle starts clean.
            if (fighters != null)
            {
                for (int i = 0; i < fighters.Count; i++)
                {
                    FighterMotor2D fighter = fighters[i];
                    if (fighter == null)
                        continue;

                    Register(fighter);
                    FighterHistory history = histories[fighter];
                    FighterState currentState = fighter.Machine.State;

                    if (currentState == FighterState.Knockdown ||
                        currentState == FighterState.ComboLimitKnockdown)
                    {
                        // The combo is closed on knockdown, but the recorded
                        // ticks are cleared when Reset finishes and control
                        // returns to neutral. No new impacts can be received
                        // during Knockdown or Reset.
                        history.comboClosedByKnockdown = true;
                    }
                    else if (history.comboClosedByKnockdown &&
                             !fighter.Machine.IsTimed &&
                             (history.lastState == FighterState.Reset ||
                              history.lastState == FighterState.Knockdown))
                    {
                        history.receivedHitTicks.Clear();
                        history.comboClosedByKnockdown = false;
                    }
                    else if (!history.comboClosedByKnockdown)
                    {
                        PruneExpiredHits(history, currentTick, hitWindow);
                    }

                    history.lastState = currentState;
                }
            }

            // First register every valid impact from this tick. No knockdown
            // is applied until this complete batch has been recorded, so the
            // result cannot depend on fighter update order.
            knockdownCandidates.Clear();
            knockdownDirections.Clear();

            for (int i = 0; i < pendingImpacts.Count; i++)
            {
                PendingImpact impact = pendingImpacts[i];
                if (impact.target == null || !impact.target.isActiveAndEnabled)
                    continue;

                Register(impact.target);
                FighterHistory history = histories[impact.target];

                // Knockdown/Reset closes the current combo. Contacts that
                // were queued earlier in a tick cannot reopen it afterward.
                if (history.comboClosedByKnockdown ||
                    impact.target.Machine.IsComboLimitKnockdown ||
                    impact.target.Machine.IsKnockdown ||
                    impact.target.Machine.IsResetting)
                {
                    continue;
                }

                history.receivedHitTicks.Add(currentTick);

                if (!knockdownDirections.ContainsKey(impact.target))
                    knockdownDirections.Add(impact.target, impact.direction);
                else
                    knockdownDirections[impact.target] = impact.direction;

                if (history.receivedHitTicks.Count >= hitLimit)
                    knockdownCandidates.Add(impact.target);
            }

            pendingImpacts.Clear();

            if (forceKnockdownOnLimit)
            {
                foreach (FighterMotor2D fighter in knockdownCandidates)
                {
                    if (fighter == null || !fighter.isActiveAndEnabled)
                        continue;

                    int direction = knockdownDirections.TryGetValue(
                        fighter,
                        out int resolvedDirection)
                        ? resolvedDirection
                        : 1;

                    // This deliberately runs after all contacts and damage for
                    // the tick. It overrides Hurt/FlyingHurt/GuardBreak when
                    // the global hit limit wins on this same tick.
                    if (fighter.ReceiveComboLimitKnockdown(direction))
                    {
                        FighterHistory history = histories[fighter];
                        history.comboClosedByKnockdown = true;
                        history.lastState = FighterState.ComboLimitKnockdown;
                    }
                }
            }
        }

        public void ClearPendingImpacts()
        {
            pendingImpacts.Clear();
        }

        private static void PruneExpiredHits(
            FighterHistory history,
            long currentTick,
            int windowTicks)
        {
            for (int i = history.receivedHitTicks.Count - 1; i >= 0; i--)
            {
                if (currentTick - history.receivedHitTicks[i] > windowTicks)
                    history.receivedHitTicks.RemoveAt(i);
            }
        }
    }
}
