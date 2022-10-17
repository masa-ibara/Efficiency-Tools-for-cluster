using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ClusterVR.CreatorKit.Gimmick.Supplements;
using ClusterVR.CreatorKit.Gimmick.Implements;

namespace MSAi.Editor.JsonGenerator
{
    public class JsonGenerator : EditorWindow
    {
        Toggle showConfirmDialogToggle;
        string oldJsonPath;

        [System.Serializable]
        public class Triggers
        {
            public Trigger[] triggers;
        }

        [System.Serializable]
        public class Trigger
        {
            public string displayName;
            public string category;
            public bool showConfirmDialog;
            public float[] color;
            public State[] state;
        }

        [System.Serializable]
        public class State
        {
            public string key;
            public string type;
        }

        [MenuItem("Tools/WebTrigger Json Generater")]
        public static void Init()
        {
            var window = GetWindow<JsonGenerator>();
            window.titleContent = new GUIContent("WebTrigger Json Generater");
            window.minSize = new Vector2(236, 122);
            window.Show();
        }

        void GetJsonPath()
        {
            var path = EditorUtility.OpenFilePanelWithFilters(
                "Select Json",
                Application.dataPath,
                new[] { "Trigger Json files", "json" }
            );

            if (!string.IsNullOrEmpty(path))
                oldJsonPath = path;
        }

        Triggers ReadJson(string path)
        {
            string datastr = "";
            StreamReader reader;
            reader = new StreamReader(path);
            datastr = reader.ReadToEnd();
            reader.Close();

            return JsonUtility.FromJson<Triggers>(datastr);
        }

        Dictionary<string, string> MakeNameReferenceDictionary()
        {
            var d = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(oldJsonPath))
            {
                var triggers = ReadJson(oldJsonPath);
                foreach (var trigger in triggers.triggers)
                {
                    d[trigger.state[0].key] = trigger.displayName;
                }
            }
            return d;
        }

        Triggers GenerateJsonData()
        {
            var jsonData = new Triggers();
            var triggers = new Dictionary<string, Trigger>();
            var nameReference = MakeNameReferenceDictionary();

            var sceneObjects = Resources
                .FindObjectsOfTypeAll<GameObject>()
                .Where(
                    c =>
                        c.hideFlags != HideFlags.NotEditable
                        && c.hideFlags != HideFlags.HideAndDontSave
                );

            foreach (var gameObject in sceneObjects)
            {
                if (gameObject.GetComponent<PlayableSwitch>())
                {
                    foreach (
                        var playTimeline in gameObject.transform.GetComponentsInChildren<PlayTimelineGimmick>(
                            true
                        )
                    )
                    {
                        var key = GetPlayTimeLineKey(playTimeline);
                        triggers[key] = CreateTrigger(key, nameReference, gameObject.name);
                    }
                }
            }
            foreach (var gameObject in sceneObjects)
            {
                if (gameObject.GetComponent<PlayTimelineGimmick>())
                {
                    var key = GetPlayTimeLineKey(gameObject.GetComponent<PlayTimelineGimmick>());
                    try
                    {
                        triggers.Add(key, CreateTrigger(key, nameReference));
                    }
                    catch (ArgumentException) { }
                }
            }

            jsonData.triggers = triggers.Values.ToArray();
            return jsonData;
        }

        Trigger CreateTrigger(
            string key,
            Dictionary<string, string> nameReference,
            string categoryName = "none"
        )
        {
            var state = new State();
            state.key = key;
            state.type = "signal";
            var states = new State[1];
            states[0] = state;

            var trigger = new Trigger();

            if (nameReference.TryGetValue(key, out var displayName))
            {
                trigger.displayName = displayName;
            }
            else
            {
                trigger.displayName = key;
            }

            trigger.category = categoryName;

            if (categoryName != "none")
            {
                var color = new float[3];
                for (int i = 0; i < categoryName.Length; i++)
                    color[i % 3] += (float)categoryName[i];

                color[0] = color[0] % 64 / 64;
                color[1] = color[1] % 64 / 64;
                color[2] = color[2] % 64 / 64;
                trigger.color = color;
            }
            else
            {
                trigger.color = new float[] { 0.5f, 0.5f, 0.5f };
            }

            trigger.showConfirmDialog = showConfirmDialogToggle.value;
            trigger.state = states;

            return trigger;
        }

        string GetPlayTimeLineKey(PlayTimelineGimmick component)
        {
            var fieldInfo = component
                .GetType()
                .GetField("globalGimmickKey", BindingFlags.NonPublic | BindingFlags.Instance);
            var obj = fieldInfo.GetValue(component);
            var globalGimmickKey = obj as GlobalGimmickKey;
            var key = globalGimmickKey.Key.Key;
            return key;
        }

        void GenerateJson()
        {
            var dir = $"Assets/TriggerJson/";
            var dataPath = $"{dir}{SceneManager.GetActiveScene().name}.json";
            var jsondata = GenerateJsonData();
            var jsonstr = JsonUtility.ToJson(jsondata, true);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            
            var writer = new StreamWriter(dataPath, false);
            writer.WriteLine(jsonstr);
            writer.Flush();
            writer.Close();

            AssetDatabase.Refresh();
        }

        public void OnEnable()
        {
            var root = rootVisualElement;

            var toolMenu = Resources.Load<VisualTreeAsset>("JsonGeneratorWindow");
            toolMenu.CloneTree(root);

            showConfirmDialogToggle = root.Q<Toggle>("ShowConfirmDialog");

            root.Q<Button>("ReadJson").clickable.clicked += () =>
                GetJsonPath();

            root.Q<Button>("GenerateJson").clickable.clicked += () =>
                GenerateJson();
                
        }
    }
}
