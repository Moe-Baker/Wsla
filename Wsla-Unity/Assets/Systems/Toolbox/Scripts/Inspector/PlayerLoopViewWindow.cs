#if UNITY_EDITOR

using System;
using System.Collections.Generic;

using UnityEditor;

using UnityEngine;
using UnityEngine.LowLevel;

namespace Toolbox
{
    public class PlayerLoopViewWindow : EditorWindow
    {
        [MenuItem("Window/Player Loop View")]
        static void Open()
        {
            var window = CreateWindow<PlayerLoopViewWindow>();

            window.titleContent = new GUIContent($"Player Loop View");

            window.Show();
        }

        Vector2 scrollPosition;

        Dictionary<Type, bool> TypeFoldoutCollection = new();

        void OnGUI()
        {
            var loop = PlayerLoop.GetCurrentPlayerLoop();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            foreach (var sub in loop.subSystemList)
                Draw(sub, 0);

            EditorGUILayout.EndScrollView();
        }

        void Draw(PlayerLoopSystem system, int depth)
        {
            EditorGUI.indentLevel = depth * 2;

            var content = system.type == null ? new GUIContent("ROOT") : new GUIContent(system.type.Name);

            if (system.subSystemList is null || system.subSystemList.Length is 0)
            {
                EditorGUILayout.LabelField(content);
            }
            else
            {
                TypeFoldoutCollection.TryGetValue(system.type, out var isExpanded);
                isExpanded = EditorGUILayout.Foldout(isExpanded, content, true);
                TypeFoldoutCollection[system.type] = isExpanded;

                if (isExpanded is false)
                    return;

                foreach (var sub in system.subSystemList)
                    Draw(sub, depth + 1);
            }
        }
    }
}
#endif