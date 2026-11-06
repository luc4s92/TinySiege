using System;
using TinySiege.AR;
using TinySiege.Board;
using UnityEngine;

namespace TinySiege.Gameplay
{
    /// <summary>
    /// Bridges placement and gameplay: when the board is placed, spawns the catapult under
    /// <see cref="PlacedBoard.CatapultSocket"/>. Gameplay only sees <see cref="PlacedBoard"/>,
    /// never AR Foundation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardGameplaySpawner : MonoBehaviour
    {
        [SerializeField] PlacementController placementController;
        [SerializeField] CatapultController catapultPrefab;
        [Tooltip("Camera used for catapult input. Defaults to Camera.main.")]
        [SerializeField] Camera viewCamera;

        public PlacedBoard Board { get; private set; }
        public CatapultController Catapult { get; private set; }

        /// <summary>Raised once, after the catapult has been spawned on the placed board.</summary>
        public event Action<PlacedBoard, CatapultController> GameplaySpawned;

        void OnEnable()
        {
            if (placementController == null || catapultPrefab == null)
            {
                Debug.LogError($"{nameof(BoardGameplaySpawner)}: missing Inspector references; gameplay will not spawn.", this);
                enabled = false;
                return;
            }

            placementController.ObjectPlaced += OnObjectPlaced;
            if (placementController.PlacedObject != null)
                OnObjectPlaced(placementController.PlacedObject);
        }

        void OnDisable()
        {
            if (placementController != null)
                placementController.ObjectPlaced -= OnObjectPlaced;
        }

        void OnObjectPlaced(GameObject placed)
        {
            if (Catapult != null)
                return;

            var board = placed != null ? placed.GetComponentInChildren<PlacedBoard>() : null;
            if (board == null || board.CatapultSocket == null)
            {
                Debug.LogError($"{nameof(BoardGameplaySpawner)}: placed object has no PlacedBoard with a CatapultSocket.", this);
                return;
            }

            Board = board;
            Catapult = Instantiate(catapultPrefab, board.CatapultSocket, false);
            Catapult.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (viewCamera != null)
                Catapult.SetCamera(viewCamera);

            GameplaySpawned?.Invoke(board, Catapult);
        }
    }
}
