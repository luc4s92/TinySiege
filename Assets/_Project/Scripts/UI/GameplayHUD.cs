using TinySiege.Board;
using TinySiege.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace TinySiege.UI
{
    /// <summary>
    /// Gameplay HUD inside the existing canvas: shot power, ammunition, remaining targets and the
    /// final Victory/Defeat banner. Pure view: it only listens to <see cref="MatchController"/> and
    /// to the catapult spawned by <see cref="BoardGameplaySpawner"/>. All graphics must keep
    /// raycastTarget off so the HUD never blocks placement or catapult input.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayHUD : MonoBehaviour
    {
        [Header("Gameplay")]
        [SerializeField] BoardGameplaySpawner gameplay;
        [SerializeField] MatchController match;

        [Header("Stats")]
        [Tooltip("Hidden until the board is placed and gameplay has spawned.")]
        [SerializeField] GameObject statsRoot;
        [SerializeField] Text ammoText;
        [SerializeField] Text targetsText;

        [Header("Power meter")]
        [SerializeField] GameObject powerMeterRoot;
        [Tooltip("Grows from the bottom of the meter: its anchorMax.y follows the charge (0..1).")]
        [SerializeField] Image powerFill;
        [SerializeField] Color lowChargeColor = new Color(0.98f, 0.85f, 0.25f, 1f);
        [SerializeField] Color highChargeColor = new Color(0.95f, 0.3f, 0.15f, 1f);

        [Header("Result")]
        [SerializeField] GameObject resultPanel;
        [SerializeField] Text resultText;
        [SerializeField] Color victoryColor = new Color(1f, 0.84f, 0.3f, 1f);
        [SerializeField] Color defeatColor = new Color(0.95f, 0.35f, 0.3f, 1f);

        CatapultController catapult;

        void OnEnable()
        {
            if (gameplay == null || match == null || statsRoot == null || ammoText == null || targetsText == null ||
                powerMeterRoot == null || powerFill == null || resultPanel == null || resultText == null)
            {
                Debug.LogError($"{nameof(GameplayHUD)}: missing Inspector references; HUD disabled.", this);
                enabled = false;
                return;
            }

            match.AmmoChanged += OnAmmoChanged;
            match.TargetsChanged += OnTargetsChanged;
            match.StateChanged += OnStateChanged;
            gameplay.GameplaySpawned += OnGameplaySpawned;

            if (gameplay.Catapult != null)
                BindCatapult(gameplay.Catapult);
            Refresh();
        }

        void OnDisable()
        {
            if (match != null)
            {
                match.AmmoChanged -= OnAmmoChanged;
                match.TargetsChanged -= OnTargetsChanged;
                match.StateChanged -= OnStateChanged;
            }

            if (gameplay != null)
                gameplay.GameplaySpawned -= OnGameplaySpawned;
            UnbindCatapult();
        }

        void OnGameplaySpawned(PlacedBoard board, CatapultController spawnedCatapult)
        {
            BindCatapult(spawnedCatapult);
            Refresh();
        }

        void BindCatapult(CatapultController spawnedCatapult)
        {
            UnbindCatapult();
            catapult = spawnedCatapult;
            catapult.ChargeChanged += OnChargeChanged;
            OnChargeChanged(catapult.Charge01);
        }

        void UnbindCatapult()
        {
            if (catapult != null)
                catapult.ChargeChanged -= OnChargeChanged;
            catapult = null;
        }

        void Refresh()
        {
            statsRoot.SetActive(catapult != null);
            OnAmmoChanged(match.AmmoRemaining);
            OnTargetsChanged(match.TargetsRemaining);
            OnStateChanged(match.State);
        }

        void OnAmmoChanged(int ammo) => ammoText.text = $"Ammo: {ammo}";

        void OnTargetsChanged(int targets) => targetsText.text = $"Targets: {targets}";

        void OnChargeChanged(float charge01)
        {
            charge01 = Mathf.Clamp01(charge01);
            RectTransform fill = powerFill.rectTransform;
            fill.anchorMax = new Vector2(fill.anchorMax.x, charge01);
            powerFill.color = Color.Lerp(lowChargeColor, highChargeColor, charge01);
        }

        void OnStateChanged(MatchState state)
        {
            bool playing = state == MatchState.Playing;
            powerMeterRoot.SetActive(catapult != null && playing);
            resultPanel.SetActive(!playing);
            if (playing)
                return;

            bool won = state == MatchState.Win;
            resultText.text = won ? "VICTORY" : "DEFEAT";
            resultText.color = won ? victoryColor : defeatColor;
        }
    }
}
