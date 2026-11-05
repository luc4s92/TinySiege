using System;
using System.Collections.Generic;
using System.Diagnostics;
using TinySiege.Board;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace TinySiege.Gameplay
{
    public enum MatchState
    {
        Playing,
        Win,
        Lose
    }

    /// <summary>
    /// Match rules for one placed board: ammunition, remaining targets and Win/Lose.
    /// Binds to the gameplay spawned by <see cref="BoardGameplaySpawner"/> on the same GameObject and
    /// discovers targets under <see cref="PlacedBoard.TargetsRoot"/>. Knows nothing about AR Foundation.
    /// </summary>
    [RequireComponent(typeof(BoardGameplaySpawner))]
    [DisallowMultipleComponent]
    public sealed class MatchController : MonoBehaviour
    {
        // 4 sectors x 3 hits = 12 minimum shots, plus 3 spare.
        [SerializeField, Min(1)] int startingAmmo = 15;

        readonly List<DestructibleTarget> targets = new List<DestructibleTarget>();
        BoardGameplaySpawner spawner;
        CatapultController catapult;
        bool started;

        public int AmmoRemaining { get; private set; }
        public int TargetsRemaining { get; private set; }
        public MatchState State { get; private set; } = MatchState.Playing;

        public event Action<int> AmmoChanged;
        public event Action<int> TargetsChanged;
        public event Action<MatchState> StateChanged;

        void Awake() => spawner = GetComponent<BoardGameplaySpawner>();

        void OnEnable()
        {
            spawner.GameplaySpawned += OnGameplaySpawned;
            if (spawner.Board != null && spawner.Catapult != null)
                Bind(spawner.Board, spawner.Catapult);
        }

        void OnDisable()
        {
            spawner.GameplaySpawned -= OnGameplaySpawned;
            Unbind();
        }

        void OnGameplaySpawned(PlacedBoard board, CatapultController spawnedCatapult) => Bind(board, spawnedCatapult);

        void Bind(PlacedBoard board, CatapultController spawnedCatapult)
        {
            Unbind();
            if (board.TargetsRoot == null)
            {
                Debug.LogError($"{nameof(MatchController)}: placed board has no TargetsRoot; match not started.", this);
                return;
            }

            catapult = spawnedCatapult;
            catapult.ProjectileLaunched += OnProjectileLaunched;
            catapult.ShotResolved += OnShotResolved;

            board.TargetsRoot.GetComponentsInChildren(true, targets);
            int alive = 0;
            foreach (var target in targets)
            {
                if (target.IsDestroyed)
                    continue;
                target.Destroyed += OnTargetDestroyed;
                alive++;
            }

            // A rebind (component re-enabled) keeps the current match; only the first bind starts it.
            if (!started)
            {
                started = true;
                AmmoRemaining = startingAmmo;
                TargetsRemaining = alive;
                State = MatchState.Playing;
                catapult.FiringEnabled = true;
                Log($"match started: ammo {AmmoRemaining}, targets {TargetsRemaining}");
                AmmoChanged?.Invoke(AmmoRemaining);
                TargetsChanged?.Invoke(TargetsRemaining);
                StateChanged?.Invoke(State);
            }
            else
            {
                if (State == MatchState.Playing && alive != TargetsRemaining)
                {
                    TargetsRemaining = alive;
                    TargetsChanged?.Invoke(TargetsRemaining);
                }
                if (State != MatchState.Playing || AmmoRemaining == 0)
                    catapult.FiringEnabled = false;
            }

            if (State == MatchState.Playing && TargetsRemaining == 0)
                End(MatchState.Win);
        }

        void Unbind()
        {
            if (catapult != null)
            {
                catapult.ProjectileLaunched -= OnProjectileLaunched;
                catapult.ShotResolved -= OnShotResolved;
                catapult = null;
            }

            foreach (var target in targets)
            {
                if (target != null)
                    target.Destroyed -= OnTargetDestroyed;
            }
            targets.Clear();
        }

        void OnProjectileLaunched(Projectile projectile)
        {
            if (State != MatchState.Playing || AmmoRemaining == 0)
                return;

            AmmoRemaining--;
            Log($"projectile launched, ammo {AmmoRemaining}/{startingAmmo}");
            AmmoChanged?.Invoke(AmmoRemaining);

            // No reload once the last stone is out; Lose is decided when that shot resolves.
            if (AmmoRemaining == 0)
            {
                catapult.FiringEnabled = false;
                Log("last projectile in flight, waiting for it to resolve");
            }
        }

        void OnTargetDestroyed(DestructibleTarget target)
        {
            target.Destroyed -= OnTargetDestroyed;
            if (State != MatchState.Playing || TargetsRemaining == 0)
                return;

            TargetsRemaining--;
            Log($"{target.name} destroyed, targets {TargetsRemaining}");
            TargetsChanged?.Invoke(TargetsRemaining);

            if (TargetsRemaining == 0)
                End(MatchState.Win);
        }

        void OnShotResolved(Projectile projectile)
        {
            // A resolved projectile can no longer destroy targets, so the last shot's outcome is final here.
            if (State == MatchState.Playing && AmmoRemaining == 0 && TargetsRemaining > 0)
                End(MatchState.Lose);
        }

        void End(MatchState result)
        {
            State = result;
            if (catapult != null)
                catapult.FiringEnabled = false;
            Log($"match over: {result} (ammo {AmmoRemaining}, targets {TargetsRemaining}), firing disabled");
            StateChanged?.Invoke(State);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        void Log(string message) => Debug.Log($"{nameof(MatchController)}: {message}.", this);
    }
}
