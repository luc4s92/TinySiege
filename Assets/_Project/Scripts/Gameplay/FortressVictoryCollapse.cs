using TinySiege.Board;
using UnityEngine;

namespace TinySiege.Gameplay
{
    /// <summary>
    /// Visual-only victory effect: when the match is won, hides the fortress back row
    /// (<see cref="PlacedBoard.FortressRoot"/>) and shows <see cref="PlacedBoard.FortressFinalDebris"/>.
    /// Runs once per match; Lose does nothing. Sits next to <see cref="MatchController"/>, which keeps
    /// owning the match state.
    /// </summary>
    [RequireComponent(typeof(MatchController), typeof(BoardGameplaySpawner))]
    [DisallowMultipleComponent]
    public sealed class FortressVictoryCollapse : MonoBehaviour
    {
        MatchController match;
        BoardGameplaySpawner spawner;
        bool collapsed;

        void Awake()
        {
            match = GetComponent<MatchController>();
            spawner = GetComponent<BoardGameplaySpawner>();
        }

        void OnEnable()
        {
            match.StateChanged += OnStateChanged;
            OnStateChanged(match.State);
        }

        void OnDisable() => match.StateChanged -= OnStateChanged;

        void OnStateChanged(MatchState state)
        {
            if (collapsed || state != MatchState.Win)
                return;

            PlacedBoard board = spawner.Board;
            if (board == null)
                return;

            collapsed = true;
            if (board.FortressRoot != null)
                board.FortressRoot.gameObject.SetActive(false);
            if (board.FortressFinalDebris != null)
                board.FortressFinalDebris.SetActive(true);
            else
                Debug.LogWarning($"{nameof(FortressVictoryCollapse)}: placed board has no FortressFinalDebris.", this);
        }
    }
}
