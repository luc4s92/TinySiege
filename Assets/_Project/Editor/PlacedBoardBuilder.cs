using System.Collections.Generic;
using TinySiege.AR;
using TinySiege.Board;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace TinySiege.EditorTools
{
    /// <summary>
    /// Builds Assets/_Project/Prefabs/PlacedBoard.prefab from primitives and two Kenney Castle Kit
    /// models plus a matching footprint preview (PlacementPreview.prefab), then assigns both in AR_Main.
    /// Safe to run again: both prefabs are overwritten in place and keep their GUIDs.
    /// </summary>
    static class PlacedBoardBuilder
    {
        const string k_PrefabPath = "Assets/_Project/Prefabs/PlacedBoard.prefab";
        const string k_ScenePath = "Assets/Scenes/AR_Main.unity";
        const string k_KenneyDir = "Assets/Models/kenney_castle-kit/Models/FBX format/";
        const string k_KenneyMaterialPath = "Assets/_Project/Materials/KenneyColormap.mat";
        const string k_BoardMaterialPath = "Assets/_Project/Materials/BoardBase.mat";

        const string k_PreviewPrefabPath = "Assets/_Project/Prefabs/PlacementPreview.prefab";
        const string k_PreviewMaterialPath = "Assets/_Project/Materials/PlacementPreview.mat";
        const string k_ShadowCatcherMaterialPath = "Assets/_Project/Materials/ShadowCatcher.mat";

        // Kenney Castle Kit models used by the fortress (file names under k_KenneyDir, without extension).
        // Kenney walls and gates run along their local Z axis; towers stack bottom to top.
        const string k_Tower = "tower-square";
        const string k_Wall = "wall";
        const string k_TowerBase = "tower-square-base";
        const string k_TowerBaseColor = "tower-square-base-color";
        const string k_TowerMidWindows = "tower-square-mid-windows";
        const string k_TowerTop = "tower-square-top";
        const string k_TowerTopColor = "tower-square-top-color";
        const string k_TowerTopRoof = "tower-square-top-roof";
        const string k_GateWall = "wall-narrow-gate";
        const string k_Gate = "gate";
        const string k_Flag = "flag-pennant";
        const string k_Debris = "rocks-small";
        const string k_DebrisLarge = "rocks-large";
        static readonly string[] k_FortressModels =
        {
            k_Tower, k_Wall, k_TowerBase, k_TowerBaseColor, k_TowerMidWindows, k_TowerTop, k_TowerTopColor,
            k_TowerTopRoof, k_GateWall, k_Gate, k_Flag, k_Debris, k_DebrisLarge
        };

        // Victory ruins (FortressFinalDebris), shown when the match is won in place of the back row and gatehouse.
        // Relative to FortressRoot's origin, in meters: x, z, largest footprint side, yaw. The back row spans
        // x -0.2..0.2 and z -0.04..0.04; the gatehouse stands at the target row (z = targetZ, added at build time).
        static readonly (string model, float x, float z, float footprint, float yaw)[] k_FinalRubble =
        {
            (k_DebrisLarge, 0f, 0f, 0.12f, 15f),
            (k_DebrisLarge, -0.16f, 0.005f, 0.085f, 140f),
            (k_DebrisLarge, 0.16f, -0.005f, 0.085f, 250f),
            (k_DebrisLarge, -0.085f, 0.012f, 0.07f, 70f),
            (k_DebrisLarge, 0.085f, -0.01f, 0.07f, 200f),
            (k_Debris, -0.04f, -0.035f, 0.05f, 30f),
            (k_Debris, 0.045f, 0.032f, 0.045f, 110f),
            (k_Debris, -0.125f, -0.03f, 0.04f, 300f),
            (k_Debris, 0.125f, 0.03f, 0.04f, 160f),
            (k_Debris, -0.2f, 0.02f, 0.035f, 45f),
            (k_Debris, 0.2f, -0.02f, 0.035f, 220f),
        };
        // Rubble left where the gatehouse stood (x = gateX, z = targetZ).
        const float k_GateRubbleFootprint = 0.05f;
        // Broken tower stumps under the rubble: x, footprint width, height (meters).
        static readonly (float x, float width, float height)[] k_FinalStumps =
        {
            (0f, 0.07f, 0.025f), (-0.16f, 0.06f, 0.02f), (0.16f, 0.06f, 0.02f),
        };

        // Back row (global, never destroyed): side tower - wall - keep - wall - side tower, one kit tile each.
        const float k_KeepTile = 0.08f;

        // Destructible weak points in front of the fortress, as fractions of the board width.
        static readonly float[] k_TargetXRatios = { -0.3f, -0.1f, 0.1f, 0.3f };
        const float k_TargetSize = 0.04f;
        const float k_TargetFrontGap = 0.005f;
        // Hitbox footprint relative to the sector tower; its height follows the tower stack (board to top).
        const float k_TargetHitboxRatio = 0.75f;
        // Valid hits per sector: hit 1 drops the tower top, hit 2 the walls, hit 3 destroys the rest.
        const int k_SectorHits = 3;
        // Sector half-walls, relative to k_TargetSize: lower and thinner than the tower.
        const float k_SectorWallHeightRatio = 0.75f;
        const float k_SectorWallThicknessRatio = 0.5f;
        // Rubble shown where a destroyed target stood: footprint in meters (height follows the model).
        const float k_DebrisFootprint = 0.036f;

        // Shadow receiver: slightly larger than the board so shadows cast past its edges land on the table.
        const float k_ShadowReceiverScale = 1.4f;
        const float k_ShadowReceiverLift = 0.001f;

        // Table-top footprint in meters. X = width, Z = depth (+Z points away from the player).
        // The placement preview is generated from the same footprint so both always match.
        static readonly Vector3 k_BoardSize = new Vector3(0.50f, 0.012f, 0.60f);
        const float k_PreviewThickness = 0.002f;
        const float k_PhysicsThickness = 0.04f;
        // Footprint of the original keep (tower-square at 17 cm, max 85% of the board width). Only used to keep
        // the target row exactly where it was validated; the visual keep is now built from k_KeepTile.
        const float k_TargetTowerHeight = 0.17f;
        const float k_MaxFortressWidthRatio = 0.85f;
        const float k_FortressDepthRatio = 0.25f;
        const float k_CatapultDepthRatio = -0.35f;

        [MenuItem("Tiny Siege/Build PlacedBoard Prefab")]
        static void Build()
        {
            var kenneyMaterial = AssetDatabase.LoadAssetAtPath<Material>(k_KenneyMaterialPath);
            var boardMaterial = AssetDatabase.LoadAssetAtPath<Material>(k_BoardMaterialPath);
            var previewMaterial = AssetDatabase.LoadAssetAtPath<Material>(k_PreviewMaterialPath);
            var shadowCatcherMaterial = AssetDatabase.LoadAssetAtPath<Material>(k_ShadowCatcherMaterialPath);
            var models = new Dictionary<string, GameObject>();
            var missing = new List<string>();
            foreach (string model in k_FortressModels)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(k_KenneyDir + model + ".fbx");
                if (asset != null)
                    models[model] = asset;
                else
                    missing.Add(model);
            }

            if (missing.Count > 0 || kenneyMaterial == null || boardMaterial == null ||
                previewMaterial == null || shadowCatcherMaterial == null)
            {
                Debug.LogError("PlacedBoardBuilder: missing Kenney models (" + string.Join(", ", missing) +
                               ") or board/preview/shadow materials; nothing was built.");
                return;
            }

            var root = new GameObject("PlacedBoard");
            GameObject prefab;
            try
            {
                prefab = BuildPrefab(root, models, kenneyMaterial, boardMaterial, shadowCatcherMaterial);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            if (prefab == null)
            {
                Debug.LogError("PlacedBoardBuilder: saving " + k_PrefabPath + " failed.");
                return;
            }

            var previewRoot = new GameObject("PlacementPreview");
            GameObject preview;
            try
            {
                preview = BuildPreview(previewRoot, previewMaterial);
            }
            finally
            {
                Object.DestroyImmediate(previewRoot);
            }

            if (preview == null)
            {
                Debug.LogError("PlacedBoardBuilder: saving " + k_PreviewPrefabPath + " failed.");
                return;
            }

            LogRenderStats(prefab);
            AssignToScene(prefab, preview);
        }

        /// <summary>Semi-transparent footprint of the board; no collider, no shadows.</summary>
        static GameObject BuildPreview(GameObject root, Material previewMaterial)
        {
            var footprint = GameObject.CreatePrimitive(PrimitiveType.Cube);
            footprint.name = "Footprint";
            Object.DestroyImmediate(footprint.GetComponent<Collider>());
            footprint.transform.SetParent(root.transform, false);
            // Lifted by its half thickness so it sits on the plane without z-fighting.
            footprint.transform.localPosition = new Vector3(0f, k_PreviewThickness * 0.5f, 0f);
            footprint.transform.localScale = new Vector3(k_BoardSize.x, k_PreviewThickness, k_BoardSize.z);
            ConfigureRenderers(footprint, previewMaterial, ShadowCastingMode.Off);
            foreach (var renderer in footprint.GetComponentsInChildren<MeshRenderer>(true))
                renderer.receiveShadows = false;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, k_PreviewPrefabPath, out bool success);
            Debug.Log($"PlacedBoardBuilder: saved {k_PreviewPrefabPath} ({k_BoardSize.x * 100f:0} x {k_BoardSize.z * 100f:0} cm footprint).");
            return success ? prefab : null;
        }

        static GameObject BuildPrefab(GameObject root, Dictionary<string, GameObject> models,
            Material kenneyMaterial, Material boardMaterial, Material shadowCatcherMaterial)
        {
            float top = k_BoardSize.y;
            var board = root.AddComponent<PlacedBoard>();

            // Visual base: one primitive, no collider, receives but does not cast shadows.
            var boardRoot = CreateChild(root.transform, "BoardRoot", Vector3.zero);
            var baseVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseVisual.name = "BoardBase";
            Object.DestroyImmediate(baseVisual.GetComponent<Collider>());
            baseVisual.transform.SetParent(boardRoot, false);
            baseVisual.transform.localPosition = new Vector3(0f, top * 0.5f, 0f);
            baseVisual.transform.localScale = k_BoardSize;
            // Casts so the board leaves a contact shadow on the real table through the shadow receiver.
            ConfigureRenderers(baseVisual, boardMaterial, ShadowCastingMode.On);

            // Gameplay collision: a single box, top flush with the board surface, independent from visuals.
            var physicsSurface = CreateChild(root.transform, "PhysicsSurface", Vector3.zero);
            var box = physicsSurface.gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(k_BoardSize.x, k_PhysicsThickness, k_BoardSize.z);
            box.center = new Vector3(0f, top - k_PhysicsThickness * 0.5f, 0f);

            var measured = new Dictionary<string, Bounds>();
            Bounds Measure(string model)
            {
                if (!measured.TryGetValue(model, out Bounds b))
                    measured[model] = b = MeasureModel(models[model]);
                return b;
            }

            // Stacks kit pieces bottom to top under a new group; returns the group and the stack height (parent units).
            Transform PlaceStack(Transform parent, string name, Vector3 position, float unit, out float height, params string[] pieces)
            {
                var group = CreateChild(parent, name, position);
                height = 0f;
                foreach (string piece in pieces)
                {
                    Bounds b = Measure(piece);
                    PlaceModel(models[piece], group, piece, b, unit, new Vector3(0f, height, 0f), 0f, kenneyMaterial);
                    height += b.size.y * unit;
                }
                return group;
            }

            // Fortress back row: global decoration (no colliders, never destroyed), one kit tile per piece:
            // side tower - wall - keep - wall - side tower. Walls run along local Z, so they turn 90 degrees to face -Z.
            var fortressRoot = CreateChild(root.transform, "FortressRoot", new Vector3(0f, top, k_BoardSize.z * k_FortressDepthRatio));
            float keepUnit = k_KeepTile / Mathf.Max(Measure(k_Tower).size.x, 1e-4f);
            var keep = PlaceStack(fortressRoot, "Keep", Vector3.zero, keepUnit, out float keepHeight,
                k_TowerBase, k_TowerMidWindows, k_TowerTop);
            PlaceModel(models[k_Flag], keep, "Flag", Measure(k_Flag), keepUnit, new Vector3(0f, keepHeight, 0f), 0f, kenneyMaterial);
            for (int side = -1; side <= 1; side += 2)
            {
                string suffix = side < 0 ? "_Left" : "_Right";
                PlaceModel(models[k_Wall], fortressRoot, "Wall" + suffix, Measure(k_Wall), keepUnit,
                    new Vector3(side * k_KeepTile, 0f, 0f), 90f, kenneyMaterial);
                PlaceStack(fortressRoot, "SideTower" + suffix, new Vector3(side * 2f * k_KeepTile, 0f, 0f), keepUnit, out _,
                    k_Tower, k_TowerTopRoof);
            }

            // Integration points only; no systems yet.
            var catapultSocket = CreateChild(root.transform, "CatapultSocket", new Vector3(0f, top, k_BoardSize.z * k_CatapultDepthRatio));
            var targetsRoot = CreateChild(root.transform, "TargetsRoot", new Vector3(0f, top, k_BoardSize.z * k_FortressDepthRatio));

            // Targets stand on the board in front of the fortress (towards the catapult, -Z). The row stays exactly
            // where it was validated, computed from the original keep footprint (tower-square at 17 cm + 2 walls).
            Bounds legacyTower = Measure(k_Tower);
            Bounds legacyWall = Measure(k_Wall);
            bool legacyRotateWall = legacyWall.size.z > legacyWall.size.x;
            float legacyWallLength = legacyRotateWall ? legacyWall.size.z : legacyWall.size.x;
            float legacyScale = Mathf.Min(
                k_TargetTowerHeight / Mathf.Max(legacyTower.size.y, 1e-4f),
                k_BoardSize.x * k_MaxFortressWidthRatio / Mathf.Max(legacyTower.size.x + 2f * legacyWallLength, 1e-4f));
            float legacyWallDepth = legacyRotateWall ? legacyWall.size.x : legacyWall.size.z;
            float fortressFrontZ = -0.5f * Mathf.Max(legacyTower.size.z, legacyWallDepth) * legacyScale;
            float targetZ = fortressFrontZ - k_TargetFrontGap - k_TargetSize * 0.5f;

            // Each target is a fortress sector: its tower (aligned with the hitbox) plus its walls, all under
            // Target_N/Visual so the whole sector vanishes with the target.
            Bounds targetBounds = Measure(k_TowerBaseColor);
            Bounds wallBounds = Measure(k_Wall);
            Bounds gateWallBounds = Measure(k_GateWall);
            Bounds debrisBounds = Measure(k_Debris);
            // Meters per model unit for the sector pieces: the tower base's largest side spans k_TargetSize.
            float sectorUnit = k_TargetSize / Mathf.Max(targetBounds.size.x, targetBounds.size.y, targetBounds.size.z, 1e-4f);
            float towerWidth = targetBounds.size.x * sectorUnit;
            float towerDepth = targetBounds.size.z * sectorUnit;
            float gatehouseLength = gateWallBounds.size.z * sectorUnit; // runs along local Z, turned 90 degrees
            int gateGap = k_TargetXRatios.Length / 2 - 1; // the gap between the two central targets
            float debrisScale = k_DebrisFootprint / Mathf.Max(debrisBounds.size.x, debrisBounds.size.z, 1e-4f);
            // Outer sectors close the courtyard with a wall back to the side towers.
            float backRowFrontZ = -0.5f * k_KeepTile;
            float backWallLength = Mathf.Max(0f, backRowFrontZ - (targetZ + towerDepth * 0.5f));

            float HalfWallLength(int gap)
            {
                float width = k_BoardSize.x * (k_TargetXRatios[gap + 1] - k_TargetXRatios[gap]) - towerWidth;
                if (gap == gateGap)
                    width -= gatehouseLength;
                return Mathf.Max(0f, width * 0.5f);
            }

            Debug.Log($"PlacedBoardBuilder: keep {keepHeight:0.###} m tall, back row {5f * k_KeepTile:0.##} m wide; " +
                      $"sector tower {targetBounds.size * sectorUnit} m, hitbox {k_TargetSize * k_TargetHitboxRatio:0.###} m wide " +
                      $"(height = tower stack), {k_SectorHits} hits per sector; " +
                      $"half-wall {HalfWallLength(0):0.###} m (next to gate {HalfWallLength(gateGap):0.###} m), " +
                      $"back wall {backWallLength:0.###} m, gatehouse {gateWallBounds.size * sectorUnit} m, " +
                      $"debris {debrisBounds.size * debrisScale} m; target row z {targetZ:0.####} m.");

            int lastTarget = k_TargetXRatios.Length - 1;
            for (int i = 0; i < k_TargetXRatios.Length; i++)
            {
                float targetX = k_BoardSize.x * k_TargetXRatios[i];
                var target = GameObject.CreatePrimitive(PrimitiveType.Cube); // keeps its BoxCollider
                target.name = $"Target_{i + 1}";
                target.transform.SetParent(targetsRoot, false);
                target.transform.localPosition = new Vector3(targetX, k_TargetSize * 0.5f, targetZ);
                target.transform.localScale = Vector3.one * k_TargetSize;
                var destructible = target.AddComponent<TinySiege.Gameplay.DestructibleTarget>();
                var destructibleSettings = new SerializedObject(destructible);
                destructibleSettings.FindProperty("hitsToDestroy").intValue = k_SectorHits;
                destructibleSettings.ApplyModifiedPropertiesWithoutUndo();

                // Swap the placeholder cube's rendering for Kenney models; the root keeps collider and logic.
                Object.DestroyImmediate(target.GetComponent<MeshRenderer>());
                Object.DestroyImmediate(target.GetComponent<MeshFilter>());
                // Sector group at the bottom face of the unit cube (the board surface), in target units.
                var visual = CreateChild(target.transform, "Visual", new Vector3(0f, -0.5f, 0f));
                bool outer = i == 0 || i == lastTarget;
                string towerTop = outer ? k_TowerTopColor : k_TowerTop;
                var tower = PlaceStack(visual, "Tower", Vector3.zero, sectorUnit / k_TargetSize, out float towerHeight,
                    k_TowerBaseColor, towerTop);

                // Hitbox: the tower's footprint (not the neighbouring walls), from the board to the tower top.
                var hitbox = target.GetComponent<BoxCollider>();
                hitbox.size = new Vector3(k_TargetHitboxRatio, towerHeight, k_TargetHitboxRatio);
                hitbox.center = new Vector3(0f, towerHeight * 0.5f - 0.5f, 0f);

                var walls = new List<GameObject>();
                if (i > 0)
                {
                    float length = HalfWallLength(i - 1);
                    walls.Add(PlaceSectorWall(models[k_Wall], wallBounds, visual, "Wall_Left",
                        new Vector3(-(towerWidth + length) * 0.5f, 0f, 0f), length, true, kenneyMaterial));
                }
                if (i < lastTarget)
                {
                    float length = HalfWallLength(i);
                    walls.Add(PlaceSectorWall(models[k_Wall], wallBounds, visual, "Wall_Right",
                        new Vector3((towerWidth + length) * 0.5f, 0f, 0f), length, true, kenneyMaterial));
                }
                if (outer)
                    walls.Add(PlaceSectorWall(models[k_Wall], wallBounds, visual, "Wall_Back",
                        new Vector3(0f, 0f, (towerDepth + backWallLength) * 0.5f), backWallLength, false, kenneyMaterial));
                walls.RemoveAll(wall => wall == null);

                // Damage stages (the destroying hit needs none: the target hides itself, Debris_N shows the rubble).
                var damage = new SerializedObject(target.AddComponent<TinySiege.Gameplay.FortressSectorDamage>());
                SerializedProperty stages = damage.FindProperty("stages");
                stages.arraySize = k_SectorHits - 1;
                SetStagePieces(stages.GetArrayElementAtIndex(0), tower.Find(towerTop).gameObject); // stage 1: tower top
                SetStagePieces(stages.GetArrayElementAtIndex(1), walls.ToArray());                   // stage 2: sector walls
                damage.FindProperty("punchVisual").objectReferenceValue = visual;                      // hit feedback
                damage.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"PlacedBoardBuilder: {target.name} hitbox {hitbox.size.y * k_TargetSize:0.###} m tall, stage 1 hides {towerTop}, " +
                    $"stage 2 hides {walls.Count} wall(s).");

                // Rubble: a sibling of the target (which deactivates itself), hidden until the target is destroyed.
                var debrisRoot = CreateChild(targetsRoot, $"Debris_{i + 1}", new Vector3(targetX, 0f, targetZ));
                var rocks = PlaceModel(models[k_Debris], debrisRoot, "Rocks", debrisBounds, debrisScale, Vector3.zero, 0f, kenneyMaterial);
                rocks.SetActive(false);
                var debris = new SerializedObject(debrisRoot.gameObject.AddComponent<TinySiege.Gameplay.TargetDebris>());
                debris.FindProperty("target").objectReferenceValue = destructible;
                debris.FindProperty("debris").objectReferenceValue = rocks;
                debris.ApplyModifiedPropertiesWithoutUndo();
            }

            // Gatehouse: global decoration between the two central targets. The gate fits the wall's 66-unit opening.
            // It lives under FortressRoot (same origin as TargetsRoot) so it collapses with the back row on victory.
            float gateX = k_BoardSize.x * 0.5f * (k_TargetXRatios[gateGap] + k_TargetXRatios[gateGap + 1]);
            var gatehouse = CreateChild(fortressRoot, "Gatehouse", new Vector3(gateX, 0f, targetZ));
            PlaceModel(models[k_GateWall], gatehouse, "GateWall", gateWallBounds, sectorUnit, Vector3.zero, 90f, kenneyMaterial);
            PlaceModel(models[k_Gate], gatehouse, "Gate", Measure(k_Gate), sectorUnit, Vector3.zero, 90f, kenneyMaterial);

            // Victory ruins: a big rubble heap over the back row and the gatehouse, hidden until the match is won.
            var finalDebris = CreateChild(root.transform, "FortressFinalDebris", fortressRoot.localPosition);
            Bounds stumpBounds = Measure(k_TowerBase);
            for (int s = 0; s < k_FinalStumps.Length; s++)
            {
                var stump = k_FinalStumps[s];
                var stumpScale = new Vector3(stump.width / stumpBounds.size.x, stump.height / stumpBounds.size.y,
                    stump.width / stumpBounds.size.z);
                PlaceModel(models[k_TowerBase], finalDebris, $"Stump_{s + 1}", stumpBounds, stumpScale,
                    new Vector3(stump.x, 0f, 0f), 0f, kenneyMaterial);
            }
            for (int r = 0; r < k_FinalRubble.Length; r++)
            {
                var rubble = k_FinalRubble[r];
                Bounds b = Measure(rubble.model);
                PlaceModel(models[rubble.model], finalDebris, $"Rubble_{r + 1}", b,
                    rubble.footprint / Mathf.Max(b.size.x, b.size.z, 1e-4f), new Vector3(rubble.x, 0f, rubble.z), rubble.yaw, kenneyMaterial);
            }
            Bounds gateRubble = Measure(k_Debris);
            PlaceModel(models[k_Debris], finalDebris, "Rubble_Gate", gateRubble,
                k_GateRubbleFootprint / Mathf.Max(gateRubble.size.x, gateRubble.size.z, 1e-4f),
                new Vector3(gateX, 0f, targetZ), 300f, kenneyMaterial);
            finalDebris.gameObject.SetActive(false);
            var shadowReceiverSocket = CreateChild(root.transform, "ShadowReceiverSocket", Vector3.zero);

            // Shadow receiver: an upward-facing quad on the table plane, rendered only where shadows fall.
            // Under the board it is hidden by the board's depth; around it, it darkens the camera image.
            var receiver = GameObject.CreatePrimitive(PrimitiveType.Quad);
            receiver.name = "ShadowReceiver";
            Object.DestroyImmediate(receiver.GetComponent<Collider>());
            receiver.transform.SetParent(shadowReceiverSocket, false);
            receiver.transform.localPosition = new Vector3(0f, k_ShadowReceiverLift, 0f);
            receiver.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            receiver.transform.localScale = new Vector3(k_BoardSize.x * k_ShadowReceiverScale, k_BoardSize.z * k_ShadowReceiverScale, 1f);
            ConfigureRenderers(receiver, shadowCatcherMaterial, ShadowCastingMode.Off);

            var serialized = new SerializedObject(board);
            serialized.FindProperty("boardRoot").objectReferenceValue = boardRoot;
            serialized.FindProperty("physicsSurface").objectReferenceValue = box;
            serialized.FindProperty("fortressRoot").objectReferenceValue = fortressRoot;
            serialized.FindProperty("fortressFinalDebris").objectReferenceValue = finalDebris.gameObject;
            serialized.FindProperty("catapultSocket").objectReferenceValue = catapultSocket;
            serialized.FindProperty("targetsRoot").objectReferenceValue = targetsRoot;
            serialized.FindProperty("shadowReceiverSocket").objectReferenceValue = shadowReceiverSocket;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, k_PrefabPath, out bool success);
            Debug.Log($"PlacedBoardBuilder: saved {k_PrefabPath} (kit scale {keepUnit:0.###}, tower height {keepHeight:0.###} m).");
            return success ? prefab : null;
        }

        static Transform CreateChild(Transform parent, string name, Vector3 localPosition)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            return child;
        }

        internal static GameObject PlaceModel(GameObject model, Transform parent, string name, Bounds unitBounds, float scale,
            Vector3 bottomCenter, float yaw, Material material)
            => PlaceModel(model, parent, name, unitBounds, Vector3.one * scale, bottomCenter, yaw, material);

        /// <summary>Non-uniform variant: <paramref name="scale"/> is expressed in the model's own axes.</summary>
        internal static GameObject PlaceModel(GameObject model, Transform parent, string name, Bounds unitBounds, Vector3 scale,
            Vector3 bottomCenter, float yaw, Material material)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            instance.name = name;
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 pivotToBottomCenter = new Vector3(unitBounds.center.x, unitBounds.min.y, unitBounds.center.z);
            instance.transform.localRotation = rotation;
            instance.transform.localScale = scale;
            instance.transform.localPosition = bottomCenter - rotation * Vector3.Scale(pivotToBottomCenter, scale);
            ConfigureRenderers(instance, material, ShadowCastingMode.On);

            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
            return instance;
        }

        /// <summary>
        /// Low, thin wall segment of a fortress sector. Kenney walls run along their local Z axis;
        /// <paramref name="alongX"/> turns the segment 90 degrees so it runs along the target row.
        /// <paramref name="visual"/> is in target units (1 = k_TargetSize); <paramref name="center"/> (bottom center,
        /// relative to the target) and <paramref name="length"/> are in meters.
        /// </summary>
        static GameObject PlaceSectorWall(GameObject wallModel, Bounds wallBounds, Transform visual, string name,
            Vector3 center, float length, bool alongX, Material material)
        {
            if (length <= 1e-4f)
                return null;

            // Length fills the gap; height and thickness stay below the tower so the tower reads as the target.
            var scale = new Vector3(
                k_SectorWallThicknessRatio / Mathf.Max(wallBounds.size.x, 1e-4f),
                k_SectorWallHeightRatio / Mathf.Max(wallBounds.size.y, 1e-4f),
                length / k_TargetSize / Mathf.Max(wallBounds.size.z, 1e-4f));
            return PlaceModel(wallModel, visual, name, wallBounds, scale, center / k_TargetSize, alongX ? 90f : 0f, material);
        }

        /// <summary>Fills one FortressSectorDamage stage (its <c>hide</c> array) with the given pieces.</summary>
        static void SetStagePieces(SerializedProperty stage, params GameObject[] pieces)
        {
            SerializedProperty hide = stage.FindPropertyRelative("hide");
            hide.arraySize = pieces.Length;
            for (int i = 0; i < pieces.Length; i++)
                hide.GetArrayElementAtIndex(i).objectReferenceValue = pieces[i];
        }

        /// <summary>Mesh bounds of a model in its own root space, at unit scale.</summary>
        internal static Bounds MeasureModel(GameObject model)
        {
            var temp = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                temp.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                temp.transform.localScale = Vector3.one;
                bool hasBounds = false;
                var bounds = new Bounds();
                foreach (var filter in temp.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null)
                        continue;
                    Bounds mb = filter.sharedMesh.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        Vector3 point = temp.transform.InverseTransformPoint(filter.transform.TransformPoint(corner));
                        if (!hasBounds) { bounds = new Bounds(point, Vector3.zero); hasBounds = true; }
                        else bounds.Encapsulate(point);
                    }
                }
                return bounds;
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }

        static void ConfigureRenderers(GameObject go, Material material, ShadowCastingMode shadows)
        {
            foreach (var renderer in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                var materials = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = material;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = shadows;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }

        static void LogRenderStats(GameObject prefab)
        {
            int renderers = 0, hiddenRenderers = 0, triangles = 0;
            var colliders = prefab.GetComponentsInChildren<Collider>(true);
            int targets = prefab.GetComponentsInChildren<TinySiege.Gameplay.DestructibleTarget>(true).Length;
            var meshes = new HashSet<Mesh>();
            var materials = new HashSet<Material>();
            foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderers++;
                // Hidden until needed (e.g. debris); not rendered at start.
                if (!renderer.gameObject.activeInHierarchy)
                    hiddenRenderers++;
                foreach (var m in renderer.sharedMaterials)
                    if (m != null) materials.Add(m);
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    meshes.Add(filter.sharedMesh);
                    for (int s = 0; s < filter.sharedMesh.subMeshCount; s++)
                        triangles += (int)filter.sharedMesh.GetIndexCount(s) / 3;
                }
            }

            // Kenney model instances per source file.
            var modelUse = new SortedDictionary<string, int>();
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
            {
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))
                    continue;
                var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(t.gameObject);
                string key = source != null ? source.name : t.name;
                modelUse[key] = modelUse.TryGetValue(key, out int n) ? n + 1 : 1;
            }

            var colliderNames = new List<string>();
            foreach (var c in colliders)
                colliderNames.Add(c.name);

            Debug.Log($"PlacedBoardBuilder: {renderers} renderers ({hiddenRenderers} hidden at start), {meshes.Count} unique meshes, " +
                      $"{materials.Count} materials, {triangles} triangles, {colliders.Length} collider(s) [{string.Join(", ", colliderNames)}], " +
                      $"{targets} DestructibleTarget(s).");
            var uses = new List<string>();
            foreach (var pair in modelUse)
                uses.Add($"{pair.Key} x{pair.Value}");
            Debug.Log("PlacedBoardBuilder: models used: " + string.Join(", ", uses) + ".");
        }

        static void AssignToScene(GameObject prefab, GameObject preview)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != k_ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    Debug.LogWarning("PlacedBoardBuilder: prefab built, but AR_Main was not opened; assign it manually.");
                    return;
                }
                scene = EditorSceneManager.OpenScene(k_ScenePath, OpenSceneMode.Single);
            }

            var controller = Object.FindAnyObjectByType<PlacementController>(FindObjectsInactive.Include);
            if (controller == null)
            {
                Debug.LogError("PlacedBoardBuilder: no PlacementController found in " + k_ScenePath + ".");
                return;
            }

            bool changed = false;
            var serialized = new SerializedObject(controller);
            var property = serialized.FindProperty("placementPrefab");
            if (property.objectReferenceValue != prefab)
            {
                property.objectReferenceValue = prefab;
                changed = true;
            }

            var previewProperty = serialized.FindProperty("previewPrefab");
            if (previewProperty.objectReferenceValue != preview)
            {
                previewProperty.objectReferenceValue = preview;
                changed = true;
            }

            if (changed)
                serialized.ApplyModifiedProperties();

            // Keep the on-screen instruction consistent with what is placed now.
            var instructionObject = GameObject.Find("Instruction Text");
            var instruction = instructionObject != null ? instructionObject.GetComponent<UnityEngine.UI.Text>() : null;
            if (instruction != null && instruction.text.Contains("el cubo"))
            {
                var textSerialized = new SerializedObject(instruction);
                textSerialized.FindProperty("m_Text").stringValue = instruction.text.Replace("el cubo", "el tablero");
                textSerialized.ApplyModifiedProperties();
                changed = true;
            }

            // Victory collapse: visual companion of the MatchController (it owns no match state).
            var match = Object.FindAnyObjectByType<TinySiege.Gameplay.MatchController>(FindObjectsInactive.Include);
            if (match == null)
                Debug.LogWarning("PlacedBoardBuilder: no MatchController in " + k_ScenePath + "; victory collapse not added.");
            else if (match.GetComponent<TinySiege.Gameplay.FortressVictoryCollapse>() == null)
            {
                match.gameObject.AddComponent<TinySiege.Gameplay.FortressVictoryCollapse>();
                changed = true;
                Debug.Log($"PlacedBoardBuilder: added FortressVictoryCollapse to {match.name}.");
            }

            if (!changed)
            {
                Debug.Log("PlacedBoardBuilder: AR_Main already places PlacedBoard.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("PlacedBoardBuilder: PlacementController now places PlacedBoard; AR_Main saved.");
        }
    }
}
