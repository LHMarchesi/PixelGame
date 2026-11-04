// Land of Fire · Shared deterministic combat tick resolver.
// Advances every registered fighter on the same 60 Hz timeline, collects
// all active hitbox contacts, then resolves reciprocal attacks together.
// Eligible Clash contacts can wait up to the configured tick tolerance so
// attacks landing a few ticks apart still produce one deterministic Clash.

using System.Collections.Generic;
using UnityEngine;

namespace LandOfFire.BunnyStep
{
    internal struct FighterHitCandidate
    {
        public FighterCombat2D Combat;
        public FighterMotor2D Attacker;
        public FighterMotor2D Target;
        public FighterHealth Health;
        public AttackMoveData Move;
        public int Facing;
        public int AttackElapsedTick;
        public bool IsMovesetSpecial;
        public bool ForceLethal;
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10000)]
    public sealed class FighterCombatResolver : MonoBehaviour
    {
        private const double TickSeconds = 1.0 / 60.0;

        private struct PendingClashCandidate
        {
            public FighterHitCandidate Candidate;
            public long CreatedTick;
        }

        private static FighterCombatResolver instance;

        private readonly List<FighterMotor2D> fighters =
            new List<FighterMotor2D>(4);

        private readonly List<FighterHitCandidate> candidates =
            new List<FighterHitCandidate>(8);

        private readonly List<bool> consumedCandidates =
            new List<bool>(8);

        private readonly List<PendingClashCandidate> pendingClashCandidates =
            new List<PendingClashCandidate>(8);

        private double accumulatedTime;
        private long simulationTick;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        public static void Register(FighterMotor2D fighter)
        {
            if (!Application.isPlaying || fighter == null)
                return;

            EnsureInstance();

            if (!instance.fighters.Contains(fighter))
                instance.fighters.Add(fighter);
        }

        public static void Unregister(FighterMotor2D fighter)
        {
            if (fighter == null || instance == null)
                return;

            instance.fighters.Remove(fighter);
            RemovePendingForFighter(fighter);
        }

        private static void RemovePendingForFighter(FighterMotor2D fighter)
        {
            if (instance == null || fighter == null)
                return;

            for (int i = instance.pendingClashCandidates.Count - 1; i >= 0; i--)
            {
                FighterHitCandidate candidate =
                    instance.pendingClashCandidates[i].Candidate;

                if (candidate.Attacker == fighter || candidate.Target == fighter)
                    instance.pendingClashCandidates.RemoveAt(i);
            }
        }

        private static void EnsureInstance()
        {
            if (instance != null)
                return;

            instance = UnityEngine.Object.FindObjectOfType<FighterCombatResolver>();
            if (instance != null)
                return;

            GameObject coordinator =
                new GameObject("[Land of Fire] Combat Tick Resolver");

            coordinator.hideFlags = HideFlags.HideInHierarchy;
            UnityEngine.Object.DontDestroyOnLoad(coordinator);
            instance = coordinator.AddComponent<FighterCombatResolver>();
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void FixedUpdate()
        {
            RemoveInvalidFighters();
            if (fighters.Count == 0)
            {
                accumulatedTime = 0d;
                pendingClashCandidates.Clear();
                return;
            }

            // Stable ordering prevents scene/component enumeration order from
            // changing which contacts are inspected first.
            fighters.Sort(CompareFighters);

            for (int i = 0; i < fighters.Count; i++)
                fighters[i].BeginCoordinatedFrame();

            accumulatedTime += Time.fixedDeltaTime;

            while (accumulatedTime + 1e-9 >= TickSeconds)
            {
                accumulatedTime -= TickSeconds;
                SimulateSharedTick();
            }

            float fixedDelta = Mathf.Max(.000001f, Time.fixedDeltaTime);
            for (int i = 0; i < fighters.Count; i++)
                fighters[i].EndCoordinatedFrame(fixedDelta);
        }

        private void SimulateSharedTick()
        {
            simulationTick++;

            // Phase 1: advance every fighter before examining any hitbox.
            for (int i = 0; i < fighters.Count; i++)
                fighters[i].AdvanceCoordinatedTick();

            // Phase 2: collect the full set of contacts for this tick.
            candidates.Clear();
            for (int i = 0; i < fighters.Count; i++)
            {
                FighterMotor2D fighter = fighters[i];
                if (!fighter.CanCollectAttackThisTick ||
                    fighter.combat == null ||
                    !fighter.combat.isActiveAndEnabled)
                {
                    continue;
                }

                fighter.combat.CollectHitCandidates(
                    fighter.Machine,
                    fighter.Facing,
                    candidates);
            }

            candidates.Sort(CompareCandidates);
            consumedCandidates.Clear();
            for (int i = 0; i < candidates.Count; i++)
                consumedCandidates.Add(false);

            // A valid Parry is resolved before Clash and category priority.
            // Failed attempts remain ordinary incoming contacts.
            ResolveParryAttempts();

            // Contacts that occur in the same tick retain their existing
            // exact-tick behavior: Clash, category priority, or normal trade.
            ResolveSameTickReciprocalExchanges();

            // Contacts from different ticks can still Clash inside the
            // configured tolerance. Only eligible Clash moves are buffered.
            ProcessUnresolvedCandidates();

            // Give an opposite attack landing on the final tolerance tick a
            // chance to Clash before releasing the original pending impact.
            FlushExpiredPendingClashes();
        }

        private void ResolveParryAttempts()
        {
            for (int fighterIndex = 0; fighterIndex < fighters.Count; fighterIndex++)
            {
                FighterMotor2D defender = fighters[fighterIndex];
                if (defender == null || !defender.isActiveAndEnabled)
                    continue;

                if (!defender.TryGetActiveParry(out ParryInputType inputType))
                    continue;

                FighterCombat2D defenderCombat = defender.combat;
                int incomingIndex = FindIncomingCandidate(defender);
                if (incomingIndex < 0 || defenderCombat == null)
                    continue;

                FighterHitCandidate incoming = candidates[incomingIndex];
                bool perfect = inputType == ParryInputType.PerfectLight ||
                               inputType == ParryInputType.PerfectMedium ||
                               inputType == ParryInputType.PerfectHeavy;

                int slotCost = perfect ? 1 : 2;
                bool validInput = inputType != ParryInputType.Invalid;
                bool correctCategory = !perfect ||
                    PerfectParryMatches(inputType, incoming.Move.category);
                bool insideWindow = incoming.Move.IsParryWindowActive(
                    incoming.AttackElapsedTick);

                bool success = validInput && correctCategory && insideWindow &&
                               defenderCombat.TrySpendParrySlots(slotCost);

                if (!success)
                    continue;

                consumedCandidates[incomingIndex] = true;

                if (!incoming.IsMovesetSpecial)
                {
                    incoming.Attacker.CancelAttackForParry(
                        incoming.Move.parryStunTicks);
                }
                else
                {
                    // A special Moveset strike is prevented from dealing
                    // damage, but the sequence and attacker continue.
                    incoming.Combat.RegisterParriedTarget(defender);
                }

                defender.ReceiveParrySuccess(
                    defenderCombat.parryRecoveryTicks,
                    perfect);
            }
        }

        private int FindIncomingCandidate(FighterMotor2D defender)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (consumedCandidates[i])
                    continue;

                if (candidates[i].Target == defender)
                    return i;
            }

            return -1;
        }

        private static bool PerfectParryMatches(
            ParryInputType inputType,
            AttackCategory category)
        {
            switch (inputType)
            {
                case ParryInputType.PerfectLight:
                    return category == AttackCategory.Light;
                case ParryInputType.PerfectMedium:
                    return category == AttackCategory.Medium;
                case ParryInputType.PerfectHeavy:
                    return category == AttackCategory.Heavy;
                default:
                    return false;
            }
        }

        private void ResolveSameTickReciprocalExchanges()
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (consumedCandidates[i])
                    continue;

                FighterHitCandidate first = candidates[i];
                int reverseIndex = FindReverseCandidate(i, first);
                if (reverseIndex < 0 || consumedCandidates[reverseIndex])
                    continue;

                FighterHitCandidate second = candidates[reverseIndex];

                // If a prior-tick contact is already pending in the opposite
                // direction, let that earlier contact pair with this tick's
                // reverse strike before considering the newer same-tick pair.
                if (TryResolveAgainstPending(first, i) ||
                    TryResolveAgainstPending(second, reverseIndex))
                {
                    consumedCandidates[i] = true;
                    consumedCandidates[reverseIndex] = true;
                    continue;
                }

                if (first.IsMovesetSpecial || second.IsMovesetSpecial)
                {
                    first.Combat.ResolveContact(first, true);
                    second.Combat.ResolveContact(second, true);
                    consumedCandidates[i] = true;
                    consumedCandidates[reverseIndex] = true;
                    continue;
                }

                bool sameCategory = first.Move.category == second.Move.category;
                bool sameClashGroup = AreClashCompatible(first, second);

                if (sameCategory && sameClashGroup)
                {
                    CancelClash(first, second);
                    consumedCandidates[i] = true;
                    consumedCandidates[reverseIndex] = true;
                    continue;
                }

                if (!sameCategory)
                {
                    // Different categories on the exact same tick use the
                    // agreed Heavy > Medium > Light priority.
                    int firstPriority = CategoryPriority(first.Move.category);
                    int secondPriority = CategoryPriority(second.Move.category);
                    FighterHitCandidate winner =
                        firstPriority > secondPriority ? first : second;

                    bool accepted = winner.Combat.ResolveContact(winner, false);
                    if (!accepted)
                    {
                        // If the winning contact cannot be accepted for a
                        // non-combat reason, fall back to ordinary contacts.
                        first.Combat.ResolveContact(first, true);
                        second.Combat.ResolveContact(second, true);
                    }

                    consumedCandidates[i] = true;
                    consumedCandidates[reverseIndex] = true;
                    continue;
                }

                // Same category without a matching Clash group: both hits
                // resolve normally. The Clash tolerance does not override
                // an explicit ClashGroup.None or a custom group mismatch.
                first.Combat.ResolveContact(first, true);
                second.Combat.ResolveContact(second, true);
                consumedCandidates[i] = true;
                consumedCandidates[reverseIndex] = true;
            }
        }

        private bool TryResolveAgainstPending(
            FighterHitCandidate current,
            int currentIndex)
        {
            int pendingIndex = FindReversePendingCandidate(current);
            if (pendingIndex < 0)
                return false;

            PendingClashCandidate pending = pendingClashCandidates[pendingIndex];
            FighterHitCandidate earlier = pending.Candidate;
            long tickDifference = simulationTick - pending.CreatedTick;

            pendingClashCandidates.RemoveAt(pendingIndex);

            if (AreClashCompatible(earlier, current) &&
                tickDifference <= PairClashTolerance(earlier, current))
            {
                CancelClash(earlier, current);
                consumedCandidates[currentIndex] = true;
                return true;
            }

            // Different categories or non-clashable moves do not receive
            // timing tolerance. They resolve as separate impacts because
            // their actual contact ticks were different.
            earlier.Combat.ResolveContact(earlier, true);
            return false;
        }

        private void ProcessUnresolvedCandidates()
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (consumedCandidates[i])
                    continue;

                FighterHitCandidate candidate = candidates[i];

                // If a newer attack from the same fighter reaches the same
                // target, settle the older pending hit before buffering the
                // new attack. This keeps pending entries tied to one attack.
                FlushSupersededPendingCandidates(candidate);

                int reversePendingIndex = FindReversePendingCandidate(candidate);
                if (reversePendingIndex >= 0)
                {
                    PendingClashCandidate pending =
                        pendingClashCandidates[reversePendingIndex];
                    FighterHitCandidate earlier = pending.Candidate;
                    long tickDifference = simulationTick - pending.CreatedTick;
                    pendingClashCandidates.RemoveAt(reversePendingIndex);

                    if (AreClashCompatible(earlier, candidate) &&
                        tickDifference <= PairClashTolerance(earlier, candidate))
                    {
                        CancelClash(earlier, candidate);
                    }
                    else
                    {
                        // No Clash is possible: resolve both distinct-tick
                        // contacts normally, without same-tick category priority.
                        earlier.Combat.ResolveContact(earlier, true);
                        candidate.Combat.ResolveContact(candidate, true);
                    }

                    consumedCandidates[i] = true;
                    continue;
                }

                if (CanBeBufferedForClash(candidate))
                {
                    candidate.Combat.RegisterPendingClashTarget(candidate.Target);
                    pendingClashCandidates.Add(new PendingClashCandidate
                    {
                        Candidate = candidate,
                        CreatedTick = simulationTick
                    });
                }
                else
                {
                    candidate.Combat.ResolveContact(candidate, true);
                }

                consumedCandidates[i] = true;
            }
        }

        private void FlushSupersededPendingCandidates(FighterHitCandidate current)
        {
            for (int i = pendingClashCandidates.Count - 1; i >= 0; i--)
            {
                FighterHitCandidate earlier =
                    pendingClashCandidates[i].Candidate;

                bool sameDirectionAndActors =
                    earlier.Attacker == current.Attacker &&
                    earlier.Target == current.Target;

                if (!sameDirectionAndActors)
                    continue;

                pendingClashCandidates.RemoveAt(i);
                earlier.Combat.ResolveContact(earlier, true);
            }
        }

        private void FlushExpiredPendingClashes()
        {
            for (int i = pendingClashCandidates.Count - 1; i >= 0; i--)
            {
                PendingClashCandidate pending = pendingClashCandidates[i];
                FighterCombat2D combat = pending.Candidate.Combat;
                int tolerance = combat != null
                    ? combat.ClashTimingToleranceTicks
                    : 0;

                long age = simulationTick - pending.CreatedTick;
                if (age < tolerance)
                    continue;

                pendingClashCandidates.RemoveAt(i);
                if (combat != null)
                    combat.ResolveContact(pending.Candidate, true);
            }
        }

        private int FindReversePendingCandidate(FighterHitCandidate candidate)
        {
            for (int i = 0; i < pendingClashCandidates.Count; i++)
            {
                FighterHitCandidate pending = pendingClashCandidates[i].Candidate;
                if (pending.Attacker == candidate.Target &&
                    pending.Target == candidate.Attacker)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool CanBeBufferedForClash(FighterHitCandidate candidate)
        {
            return candidate.Combat != null &&
                   candidate.Combat.CanClash(candidate);
        }

        private bool AreClashCompatible(
            FighterHitCandidate first,
            FighterHitCandidate second)
        {
            return first.Combat != null &&
                   second.Combat != null &&
                   !first.IsMovesetSpecial &&
                   !second.IsMovesetSpecial &&
                   first.Move != null &&
                   second.Move != null &&
                   first.Move.category == second.Move.category &&
                   first.Combat.CanClash(first) &&
                   second.Combat.CanClash(second) &&
                   first.Move.ResolvedClashGroup == second.Move.ResolvedClashGroup;
        }

        private static int PairClashTolerance(
            FighterHitCandidate first,
            FighterHitCandidate second)
        {
            int firstTolerance = first.Combat != null
                ? first.Combat.ClashTimingToleranceTicks
                : 0;
            int secondTolerance = second.Combat != null
                ? second.Combat.ClashTimingToleranceTicks
                : 0;

            // Both players must allow the tolerance. Using the lower setting
            // keeps the result symmetric and deterministic.
            return Mathf.Min(firstTolerance, secondTolerance);
        }

        private static void CancelClash(
            FighterHitCandidate first,
            FighterHitCandidate second)
        {
            if (first.Attacker != null)
                first.Attacker.CancelAttackForClash();

            if (second.Attacker != null && second.Attacker != first.Attacker)
                second.Attacker.CancelAttackForClash();
        }

        private int FindReverseCandidate(
            int currentIndex,
            FighterHitCandidate candidate)
        {
            for (int j = currentIndex + 1; j < candidates.Count; j++)
            {
                if (consumedCandidates[j])
                    continue;

                FighterHitCandidate other = candidates[j];
                if (other.Attacker == candidate.Target &&
                    other.Target == candidate.Attacker)
                {
                    return j;
                }
            }

            return -1;
        }

        private static int CategoryPriority(AttackCategory category)
        {
            switch (category)
            {
                case AttackCategory.Heavy: return 3;
                case AttackCategory.Medium: return 2;
                default: return 1;
            }
        }

        private void RemoveInvalidFighters()
        {
            for (int i = fighters.Count - 1; i >= 0; i--)
            {
                FighterMotor2D fighter = fighters[i];
                if (fighter == null || !fighter.isActiveAndEnabled)
                {
                    if (fighter != null)
                        RemovePendingForFighter(fighter);
                    fighters.RemoveAt(i);
                }
            }
        }

        private static int CompareFighters(FighterMotor2D a, FighterMotor2D b)
        {
            if (a == b) return 0;
            if (a == null) return -1;
            if (b == null) return 1;
            return a.GetInstanceID().CompareTo(b.GetInstanceID());
        }

        private static int CompareCandidates(
            FighterHitCandidate a,
            FighterHitCandidate b)
        {
            int attackerCompare = CompareFighters(a.Attacker, b.Attacker);
            if (attackerCompare != 0)
                return attackerCompare;

            return CompareFighters(a.Target, b.Target);
        }
    }
}
