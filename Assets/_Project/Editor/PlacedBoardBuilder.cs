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
        const string k_TowerModelPath = k_KenneyDir + "tower-square.fbx";
        const string k_WallModelPath = k_KenneyDir + "wall.fbx";
        const string k_KenneyMaterialPath = "Assets/_Project/Materials/KenneyColormap.mat";
        const string k_BoardMaterialPath = "Assets/_Project/Materials/BoardBase.mat";

        const string k_PreviewPrefabPath = "Assets/_Project/Prefabs/PlacementPreview.prefab";
        const string k_PreviewMaterialPath = "Assets/_Project/Materials/PlacementPreview.mat";

        // Table-top footprint in meters. X = width, Z = depth (+Z points away from the player).
        // The placement preview is generated from the same footprint so both always match.
        static readonly Vector3 k_BoardSize = new Vector3(0.50f, 0.012f, 0.60f);
        const float k_PreviewThickness = 0.002f;
        const float k_PhysicsThickness = 0.04f;
        const float k_TargetTowerHeight = 0.17f;
        const float k_MaxFortressWidthRatio = 0.85f;
        const float k_FortressDepthRatio = 0.25f;
        const float k_CatapultDepthRatio = -0.35f;

        [MenuItem("Tiny Siege/Build PlacedBoard Prefab")]
        static void Build()
        {
            var towerModel = AssetDatabase.LoadAssetAtPath<GameObject>(k_TowerModelPath);
            var wallModel = AssetDatabase.LoadAssetAtPath<GameObject>(k_WallModelPath);
            var kenneyMaterial = AssetDatabase.LoadAssetAtPath<Material>(k_KenneyMaterialPath);
            var boardMaterial = AssetDatabase.LoadAssetAtPath<Material>(k_BoardMaterialPath);
            var previewMaterial = AssetDatabase.LoadAssetAtPath<Material>(k_PreviewMaterialPath);
            if (towerModel == null || wallModel == null || kenneyMaterial == null || boardMaterial == null || previewMaterial == null)
            {
                Debug.LogError("PlacedBoardBuilder: missing Kenney models or board/preview materials; nothing was built.");
                return;
            }

            var root = new GameObject("PlacedBoard");
            GameObject prefab;
            try
            {
                prefab = BuildPrefab(root, towerModel, wallModel, kenneyMaterial, boardMaterial);
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

        static GameObject BuildPrefab(GameObject root, GameObject towerModel, GameObject wallModel,
            Material kenneyMaterial, Material boardMaterial)
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
            ConfigureRenderers(baseVisual, boardMaterial, ShadowCastingMode.Off);

            // Gameplay collision: a single box, top flush with the board surface, independent from visuals.
            var physicsSurface = CreateChild(root.transform, "PhysicsSurface", Vector3.zero);
            var box = physicsSurface.gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(k_BoardSize.x, k_PhysicsThickness, k_BoardSize.z);
            box.center = new Vector3(0f, top - k_PhysicsThickness * 0.5f, 0f);

            // Fortress visuals: one tower flanked by two walls, all sharing one material.
            var fortressRoot = CreateChild(root.transform, "FortressRoot", new Vector3(0f, top, k_BoardSize.z * k_FortressDepthRatio));
            Bounds towerBounds = MeasureModel(towerModel);
            Bounds wallBounds = MeasureModel(wallModel);
            bool rotateWall = wallBounds.size.z > wallBounds.size.x;
            float wallLength = rotateWall ? wallBounds.size.z : wallBounds.size.x;
            float scale = Mathf.Min(
                k_TargetTowerHeight / Mathf.Max(towerBounds.size.y, 1e-4f),
                k_BoardSize.x * k_MaxFortressWidthRatio / Mathf.Max(towerBounds.size.x + 2f * wallLength, 1e-4f));
            float wallOffsetX = (towerBounds.size.x * 0.5f + wallLength * 0.5f) * scale;
            float wallYaw = rotateWall ? 90f : 0f;

            PlaceModel(towerModel, fortressRoot, "Tower", towerBounds, scale, Vector3.zero, 0f, kenneyMaterial);
            PlaceModel(wallModel, fortressRoot, "Wall_Left", wallBounds, scale, new Vector3(-wallOffsetX, 0f, 0f), wallYaw, kenneyMaterial);
            PlaceModel(wallModel, fortressRoot, "Wall_Right", wallBounds, scale, new Vector3(wallOffsetX, 0f, 0f), wallYaw, kenneyMaterial);

            // Integration points only; no systems yet.
            var catapultSocket = CreateChild(root.transform, "CatapultSocket", new Vector3(0f, top, k_BoardSize.z * k_CatapultDepthRatio));
            var targetsRoot = CreateChild(root.transform, "TargetsRoot", new Vector3(0f, top, k_BoardSize.z * k_FortressDepthRatio));
            var shadowReceiverSocket = CreateChild(root.transform, "ShadowReceiverSocket", Vector3.zero);

            var serialized = new SerializedObject(board);
            serialized.FindProperty("boardRoot").objectReferenceValue = boardRoot;
            serialized.FindProperty("physicsSurface").objectReferenceValue = box;
            serialized.FindProperty("fortressRoot").objectReferenceValue = fortressRoot;
            serialized.FindProperty("catapultSocket").objectReferenceValue = catapultSocket;
            serialized.FindProperty("targetsRoot").objectReferenceValue = targetsRoot;
            serialized.FindProperty("shadowReceiverSocket").objectReferenceValue = shadowReceiverSocket;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, k_PrefabPath, out bool success);
            Debug.Log($"PlacedBoardBuilder: saved {k_PrefabPath} (kit scale {scale:0.###}, tower height {towerBounds.size.y * scale:0.###} m).");
            return success ? prefab : null;
        }

        static Transform CreateChild(Transform parent, string name, Vector3 localPosition)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            return child;
        }

        static void PlaceModel(GameObject model, Transform parent, string name, Bounds unitBounds, float scale,
            Vector3 bottomCenter, float yaw, Material material)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            instance.name = name;
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 pivotToBottomCenter = new Vector3(unitBounds.center.x, unitBounds.min.y, unitBounds.center.z);
            instance.transform.localRotation = rotation;
            instance.transform.localScale = Vector3.one * scale;
            instance.transform.localPosition = bottomCenter - rotation * (pivotToBottomCenter * scale);
            ConfigureRenderers(instance, material, ShadowCastingMode.On);

            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
        }

        /// <summary>Mesh bounds of a model in its own root space, at unit scale.</summary>
        static Bounds MeasureModel(GameObject model)
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
            int renderers = 0, triangles = 0, colliders = prefab.GetComponentsInChildren<Collider>(true).Length;
            var meshes = new HashSet<Mesh>();
            var materials = new HashSet<Material>();
            foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderers++;
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
            Debug.Log($"PlacedBoardBuilder: {renderers} renderers, {meshes.Count} unique meshes, {materials.Count} materials, {triangles} triangles, {colliders} collider(s).");
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
