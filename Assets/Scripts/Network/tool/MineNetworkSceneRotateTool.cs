#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MineVent.EditorTools
{
    [InitializeOnLoad]
    public static class MineNetworkSceneRotateTool
    {
        private static bool isDragging;
        private static Vector2 lastMousePosition;

        static MineNetworkSceneRotateTool()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            Event e = Event.current;

            if (Selection.activeGameObject == null)
                return;

            var visualizer = Selection.activeGameObject.GetComponentInParent<MineVent.Visualization.MineNetworkVisualizer>();
            if (visualizer == null)
                return;

            Transform target = visualizer.transform;

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                isDragging = true;
                lastMousePosition = e.mousePosition;
                e.Use();
                return;
            }

            if (e.type == EventType.MouseDrag && isDragging && e.button == 0)
            {
                Vector2 delta = e.mousePosition - lastMousePosition;
                lastMousePosition = e.mousePosition;

                target.Rotate(Vector3.up, -delta.x * 0.8f, Space.World);
                target.Rotate(Vector3.right, delta.y * 0.8f, Space.World);

                e.Use();
                HandleUtility.Repaint();
                sceneView.Repaint();
                return;
            }

            if (e.type == EventType.MouseUp && e.button == 0)
            {
                isDragging = false;
                e.Use();
            }
        }
    }
}
#endif