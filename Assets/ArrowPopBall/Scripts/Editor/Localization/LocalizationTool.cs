using System;
using System.Collections.Generic;
using Game.Localization;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization.Components;

namespace Game.Editor.Localization
{
    /// <summary>
    /// Editor window that scans the TextMeshPro components of the currently open prefab and lets you
    /// add &amp; connect localization events in one click:
    ///  - <see cref="LocalizeStringEvent"/> (Unity Localization) wired to the TMP <c>text</c> setter.
    ///  - <see cref="LocalizeFontEvent"/> (this project) wired to the TMP <c>font</c> setter.
    ///
    /// Works on the active Prefab Stage (open a prefab to edit it). Uses IMGUI to match the project's
    /// existing editor tooling.
    /// </summary>
    public class LocalizationTool : EditorWindow
    {
        const string SetTextMethod = "set_text";
        const string SetFontMethod = "set_font";

        readonly List<TextComponentInfo> _components = new List<TextComponentInfo>();
        PrefabStage _lastStage;
        Vector2 _scroll;

        [MenuItem("Tools/Arrow Pop/Localization Tool")]
        public static void ShowWindow()
        {
            var window = GetWindow<LocalizationTool>();
            window.titleContent = new GUIContent("Localization Tool");
            window.minSize = new Vector2(400, 500);
        }

        void OnEnable()
        {
            _lastStage = PrefabStageUtility.GetCurrentPrefabStage();
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.hierarchyChanged += Refresh;
            Undo.undoRedoPerformed += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.hierarchyChanged -= Refresh;
            Undo.undoRedoPerformed -= Refresh;
        }

        void OnEditorUpdate()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != _lastStage)
            {
                _lastStage = stage;
                Refresh();
                Repaint();
            }
        }

        void Refresh()
        {
            _components.Clear();

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null || stage.prefabContentsRoot == null)
                return;

            Search(stage.prefabContentsRoot.transform, "");
        }

        void Search(Transform current, string parentPath)
        {
            if (current == null) return;

            var path = string.IsNullOrEmpty(parentPath) ? current.name : parentPath + "/" + current.name;

            var tmp = current.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                _components.Add(new TextComponentInfo
                {
                    GameObject = current.gameObject,
                    Text = tmp,
                    Path = path,
                    StringEvent = current.GetComponent<LocalizeStringEvent>(),
                    FontEvent = current.GetComponent<LocalizeFontEvent>(),
                });
            }

            foreach (Transform child in current)
                Search(child, path);
        }

        void OnGUI()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                var title = stage != null ? "Prefab: " + stage.prefabContentsRoot.name : "No prefab opened";
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                if (GUILayout.Button("Refresh", GUILayout.Width(70)))
                    Refresh();
            }

            if (stage == null)
            {
                EditorGUILayout.HelpBox("Open a prefab (double-click it) to scan its TextMeshPro components.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"Found {_components.Count} TextMeshProUGUI component(s)", EditorStyles.miniLabel);
            EditorGUILayout.Space();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _components.Count; i++)
                DrawComponent(_components[i]);
            EditorGUILayout.EndScrollView();
        }

        void DrawComponent(TextComponentInfo info)
        {
            if (info.GameObject == null || info.Text == null)
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(info.GameObject.name, EditorStyles.boldLabel);
                    var preview = (info.Text.text ?? "").Replace("\n", " ").Replace("\r", "");
                    if (preview.Length > 30) preview = preview.Substring(0, 30) + "…";
                    EditorGUILayout.LabelField("\"" + preview + "\"", EditorStyles.miniLabel);
                    if (GUILayout.Button("Ping", GUILayout.Width(50)))
                    {
                        Selection.activeGameObject = info.GameObject;
                        EditorGUIUtility.PingObject(info.GameObject);
                    }
                }
                EditorGUILayout.LabelField("Path: " + info.Path, EditorStyles.miniLabel);

                DrawStringSection(info);
                DrawFontSection(info);
            }
            EditorGUILayout.Space(2);
        }

        void DrawStringSection(TextComponentInfo info)
        {
            EditorGUILayout.LabelField("LocalizeStringEvent", EditorStyles.boldLabel);

            if (info.StringEvent == null)
            {
                if (GUILayout.Button("Add & Connect"))
                {
                    var ev = Undo.AddComponent<LocalizeStringEvent>(info.GameObject);
                    ConnectString(ev, info.Text);
                    Refresh();
                }
                return;
            }

            var so = new SerializedObject(info.StringEvent);
            so.Update();
            var prop = so.FindProperty("m_StringReference");
            if (prop != null)
                EditorGUILayout.PropertyField(prop, new GUIContent("String Reference"), true);
            so.ApplyModifiedProperties();

            bool connected = IsConnected(info.StringEvent.OnUpdateString, info.Text, SetTextMethod);
            DrawConnectionRow("OnUpdateString → text", connected, () =>
            {
                ConnectString(info.StringEvent, info.Text);
                Refresh();
            });
        }

        void DrawFontSection(TextComponentInfo info)
        {
            EditorGUILayout.LabelField("LocalizeFontEvent", EditorStyles.boldLabel);

            if (info.FontEvent == null)
            {
                if (GUILayout.Button("Add & Connect"))
                {
                    var ev = Undo.AddComponent<LocalizeFontEvent>(info.GameObject);
                    ConnectFont(ev, info.Text);
                    Refresh();
                }
                return;
            }

            var so = new SerializedObject(info.FontEvent);
            so.Update();
            var fontProp = so.FindProperty("m_LocalizedAssetReference");
            if (fontProp != null)
                EditorGUILayout.PropertyField(fontProp, new GUIContent("Ref Font"), true);
            var matProp = so.FindProperty("m_RefMaterial");
            if (matProp != null)
                EditorGUILayout.PropertyField(matProp, new GUIContent("Ref Material (optional)"), true);
            so.ApplyModifiedProperties();

            bool connected = IsConnected(info.FontEvent.OnUpdateAsset, info.Text, SetFontMethod);
            DrawConnectionRow("OnUpdateAsset → font", connected, () =>
            {
                ConnectFont(info.FontEvent, info.Text);
                Refresh();
            });
        }

        static void DrawConnectionRow(string label, bool connected, Action onConnect)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var prev = GUI.color;
                GUI.color = connected ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.6f, 0.6f);
                EditorGUILayout.LabelField(label + (connected ? ": Connected" : ": Not Connected"));
                GUI.color = prev;

                if (!connected && GUILayout.Button("Connect", GUILayout.Width(80)))
                    onConnect();
            }
        }

        static void ConnectString(LocalizeStringEvent ev, TextMeshProUGUI text)
        {
            if (ev == null || text == null) return;
            if (IsConnected(ev.OnUpdateString, text, SetTextMethod)) return;

            Undo.RecordObject(ev, "Connect Localize String Event");
            var setter = (UnityAction<string>)Delegate.CreateDelegate(
                typeof(UnityAction<string>), text, SetTextMethod);
            UnityEventTools.AddPersistentListener(ev.OnUpdateString, setter);
            SetLastListenerState(ev, "m_UpdateString", UnityEventCallState.EditorAndRuntime);
            EditorUtility.SetDirty(ev);
        }

        static void ConnectFont(LocalizeFontEvent ev, TextMeshProUGUI text)
        {
            if (ev == null || text == null) return;
            if (IsConnected(ev.OnUpdateAsset, text, SetFontMethod)) return;

            Undo.RecordObject(ev, "Connect Localize Font Event");
            var setter = (UnityAction<TMP_FontAsset>)Delegate.CreateDelegate(
                typeof(UnityAction<TMP_FontAsset>), text, SetFontMethod);
            UnityEventTools.AddPersistentListener(ev.OnUpdateAsset, setter);
            SetLastListenerState(ev, "m_UpdateAsset", UnityEventCallState.EditorAndRuntime);
            EditorUtility.SetDirty(ev);
        }

        // UnityEventTools has no SetPersistentListenerState in this Unity version, so set the
        // call state of the most recently added listener directly through the serialized event.
        static void SetLastListenerState(UnityEngine.Object owner, string eventField, UnityEventCallState state)
        {
            var so = new SerializedObject(owner);
            var calls = so.FindProperty(eventField + ".m_PersistentCalls.m_Calls");
            if (calls != null && calls.arraySize > 0)
            {
                var last = calls.GetArrayElementAtIndex(calls.arraySize - 1);
                var callState = last.FindPropertyRelative("m_CallState");
                if (callState != null)
                {
                    callState.intValue = (int)state;
                    so.ApplyModifiedProperties();
                }
            }
        }

        static bool IsConnected(UnityEventBase ev, UnityEngine.Object target, string method)
        {
            if (ev == null) return false;
            int count = ev.GetPersistentEventCount();
            for (int i = 0; i < count; i++)
            {
                if (ev.GetPersistentTarget(i) == target && ev.GetPersistentMethodName(i) == method)
                    return true;
            }
            return false;
        }

        class TextComponentInfo
        {
            public GameObject GameObject;
            public TextMeshProUGUI Text;
            public string Path;
            public LocalizeStringEvent StringEvent;
            public LocalizeFontEvent FontEvent;
        }
    }
}
