using CodMovementShooter.UI;
using UnityEditor;
using UnityEngine;

namespace CodMovementShooter.UI.Editor
{
    public static class FpsHudMenu
    {
        [MenuItem("GameObject/UI/FPS HUD Canvas", false, 10)]
        public static void CreateFpsHudCanvas(MenuCommand command)
        {
            GameObject canvas = FpsHudHierarchyBuilder.CreateRootCanvas();
            GameObjectUtility.SetParentAndAlign(canvas, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(canvas, "Create FPS HUD Canvas");
            Selection.activeGameObject = canvas;
        }

        [MenuItem("Assets/Create/UI/FPS HUD Canvas Prefab", false, 120)]
        public static void CreateFpsHudPrefabAsset()
        {
            string folder = "Assets/UI/Prefabs";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/UI"))
                    AssetDatabase.CreateFolder("Assets", "UI");
                AssetDatabase.CreateFolder("Assets/UI", "Prefabs");
            }

            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/FpsHudCanvas.prefab");
            GameObject canvas = FpsHudHierarchyBuilder.CreateRootCanvas();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(canvas, path);
            Object.DestroyImmediate(canvas);
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }
    }
}
