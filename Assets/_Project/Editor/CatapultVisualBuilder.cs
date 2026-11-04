using TinySiege.Gameplay;
using UnityEditor;
using UnityEngine;

namespace TinySiege.EditorTools
{
    /// <summary>
    /// Replaces the placeholder visuals of Assets/_Project/Prefabs/Catapult.prefab (Base/Arm primitives)
    /// with the Kenney Castle Kit siege catapult. Visual only: the root, CatapultController,
    /// LaunchPoint and Aim Arrow are kept untouched. Safe to run again: the prefab keeps its GUID.
    /// </summary>
    static class CatapultVisualBuilder
    {
        const string k_PrefabPath = "Assets/_Project/Prefabs/Catapult.prefab";
        const string k_ModelPath = "Assets/Models/kenney_castle-kit/Models/FBX format/siege-catapult.fbx";
        const string k_KenneyMaterialPath = "Assets/_Project/Materials/KenneyColormap.mat";
        const string k_VisualName = "Visual";
        static readonly string[] k_PlaceholderNames = { "Base", "Arm" };

        // Footprint/height budget in meters, close to the placeholder (6 x 8 cm base, ~5 cm tall).
        const float k_MaxFootprint = 0.09f;
        const float k_MaxHeight = 0.07f;
        // Rotation around Y so the model's front faces the catapult's +Z (towards the targets).
        // The model's long axis is X: +90 turns its -X side towards +Z, -90 turns its +X side towards +Z.
        // Use -90 and run again if the model appears backwards.
        const float k_ModelYaw = 90f;

        [MenuItem("Tiny Siege/Build Catapult Visual")]
        static void Build()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(k_ModelPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(k_KenneyMaterialPath);
            if (model == null || material == null)
            {
                Debug.LogError("CatapultVisualBuilder: missing siege-catapult model or KenneyColormap material; nothing was changed.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(k_PrefabPath);
            try
            {
                if (root.GetComponent<CatapultController>() == null || root.transform.Find("LaunchPoint") == null)
                {
                    Debug.LogError("CatapultVisualBuilder: Catapult.prefab has no CatapultController or LaunchPoint; nothing was changed.");
                    return;
                }

                RemoveChild(root.transform, k_VisualName);
                foreach (string placeholder in k_PlaceholderNames)
                    RemoveChild(root.transform, placeholder);

                Bounds bounds = PlacedBoardBuilder.MeasureModel(model);
                float footprint = Mathf.Max(bounds.size.x, bounds.size.z);
                float scale = Mathf.Min(
                    k_MaxFootprint / Mathf.Max(footprint, 1e-4f),
                    k_MaxHeight / Mathf.Max(bounds.size.y, 1e-4f));

                // Bottom-center on the catapult origin, i.e. resting on the board surface.
                PlacedBoardBuilder.PlaceModel(model, root.transform, k_VisualName, bounds, scale, Vector3.zero, k_ModelYaw, material);
                root.transform.Find(k_VisualName).SetAsFirstSibling();

                PrefabUtility.SaveAsPrefabAsset(root, k_PrefabPath, out bool success);
                if (!success)
                {
                    Debug.LogError("CatapultVisualBuilder: saving " + k_PrefabPath + " failed.");
                    return;
                }

                Transform launchPoint = root.transform.Find("LaunchPoint");
                Debug.Log($"CatapultVisualBuilder: saved {k_PrefabPath}. Model size at unit scale {bounds.size}, " +
                          $"scale {scale:0.####}, yaw {k_ModelYaw:0}, final size {bounds.size * scale} m " +
                          $"(height {bounds.size.y * scale:0.###} m vs LaunchPoint y {launchPoint.localPosition.y:0.###} m).");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void RemoveChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
                Object.DestroyImmediate(child.gameObject);
        }
    }
}
