using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using VRClass.Core;
using VRClass.Experiment;
using VRClass.Interaction;
using VRClass.Lesson;
using VRClass.Logging;

namespace VRClass.EditorTools
{
    /// <summary>
    /// Tools/Experiment/Build Lesson Test Scene：
    /// 仮の部品3つ（机の上に分解して並べた状態から組み立てる）で HDD の授業を動かすテスト用のシーン（Assets/Scenes/LessonTest.unity）と
    /// 授業データ（Assets/Lessons/HDD_Test.asset）を作る。
    /// ここにある寸法・位置はテスト用のシーンの配置で、研究のパラメータではない。
    /// </summary>
    public static class LessonTestSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/LessonTest.unity";
        public const string LessonPath = "Assets/Lessons/HDD_Test.asset";
        public const string MaterialFolder = "Assets/Models/Test";
        public const string XrOriginPrefabPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";

        // テスト用シーンの配置（m）
        private const float TableTopY = 0.8f;
        private const float TableCenterZ = 0.6f;
        private const float PartsZ = 0.55f;   // 組み立てる場所（ソケット）
        private const float LayoutZ = 0.42f;  // 分解した部品を並べる列（手前）

        // 部品の識別名（HDD_Test.asset の targetPartId と一致させる）
        public const string CoverId = "cover";
        public const string ArmId = "arm";
        public const string PlatterId = "platter";

        private sealed class PartSpec
        {
            public string Id;
            public string Label;
            public PrimitiveType Shape;
            public Vector3 Position;       // ソケット（組み立てた位置）
            public Vector3 LayoutPosition; // 初期位置（机の上に分解して並べた位置）
            public string[] Prerequisites; // 先に置くべきソケット（下の部品）
            public Vector3 Scale;
            public Color Color;
            public SymmetryAxis Symmetry;
        }

        private static readonly PartSpec[] Parts =
        {
            new PartSpec
            {
                Id = PlatterId, Label = "プラッタ", Shape = PrimitiveType.Cylinder,
                Position = new Vector3(-0.03f, TableTopY + 0.01f, PartsZ), Scale = new Vector3(0.095f, 0.0025f, 0.095f),
                LayoutPosition = new Vector3(-0.38f, TableTopY + 0.0035f, LayoutZ), Prerequisites = new string[0],
                Color = new Color(0.75f, 0.75f, 0.8f), Symmetry = SymmetryAxis.Y,
            },
            new PartSpec
            {
                Id = ArmId, Label = "アーム", Shape = PrimitiveType.Cube,
                Position = new Vector3(0.06f, TableTopY + 0.025f, PartsZ), Scale = new Vector3(0.10f, 0.012f, 0.02f),
                LayoutPosition = new Vector3(-0.22f, TableTopY + 0.0065f, LayoutZ), Prerequisites = new[] { PlatterId },
                Color = new Color(0.2f, 0.45f, 0.9f), Symmetry = SymmetryAxis.None,
            },
            new PartSpec
            {
                Id = CoverId, Label = "カバー", Shape = PrimitiveType.Cube,
                Position = new Vector3(0f, TableTopY + 0.05f, PartsZ), Scale = new Vector3(0.20f, 0.01f, 0.15f),
                LayoutPosition = new Vector3(0.30f, TableTopY + 0.0055f, LayoutZ), Prerequisites = new[] { PlatterId, ArmId },
                Color = new Color(0.55f, 0.55f, 0.55f), Symmetry = SymmetryAxis.None,
            },
        };

        [MenuItem("Tools/Experiment/Build Lesson Test Scene")]
        public static void Build()
        {
            var xrOriginPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(XrOriginPrefabPath);
            if (xrOriginPrefab == null)
            {
                EditorUtility.DisplayDialog("Build Lesson Test Scene",
                    "XR Origin のプレハブが見つかりません：\n" + XrOriginPrefabPath +
                    "\n\nPackage Manager で XR Interaction Toolkit の Starter Assets を取り込んでください。", "OK");
                return;
            }

            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Build Lesson Test Scene", ScenePath + " は既にあります。作り直しますか？", "作り直す", "やめる"))
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            // 先に asset を作って保存し、新しいシーンを開いた後に読み込み直す。
            // （シーンを切り替えると、読み込んでいた asset が解放されて参照が切れることがあるため）
            ExperimentMenu.CreateConfig();
            CreateLessonDataIfMissing();
            AssetDatabase.SaveAssets();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var config = AssetDatabase.LoadAssetAtPath<ExperimentConfig>(ExperimentMenu.ConfigAssetPath);
            var lesson = AssetDatabase.LoadAssetAtPath<LessonData>(LessonPath);
            TMP_FontAsset font = JapaneseFontSetup.FindJapaneseFontAsset();
            if (config == null || lesson == null)
            {
                EditorUtility.DisplayDialog("Build Lesson Test Scene",
                    $"設定か授業データを読み込めませんでした。\n{ExperimentMenu.ConfigAssetPath}\n{LessonPath}\nが Project ウィンドウにあるか確認してください。", "OK");
                return;
            }

            // 光と床・実験台
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject floor = CreateBox("Floor", new Vector3(0f, -0.05f, 0f), new Vector3(6f, 0.1f, 6f), GetMaterial("Floor", new Color(0.8f, 0.8f, 0.78f)));
            floor.AddComponent<FloorZone>(); // 部品が触れたら机の上に戻す
            CreateBox("Table", new Vector3(0f, TableTopY / 2f, TableCenterZ), new Vector3(1.2f, TableTopY, 0.6f), GetMaterial("Table", new Color(0.15f, 0.17f, 0.25f)));
            CreateBox("HDD Base", new Vector3(0f, TableTopY + 0.0025f, PartsZ), new Vector3(0.22f, 0.005f, 0.17f), GetMaterial("Base", new Color(0.3f, 0.3f, 0.3f)));

            // XR
            new GameObject("XR Interaction Manager").AddComponent<XRInteractionManager>();
            var xrOrigin = (GameObject)PrefabUtility.InstantiatePrefab(xrOriginPrefab, scene);
            xrOrigin.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Camera cam = xrOrigin.GetComponentInChildren<Camera>(true);
            if (cam != null)
            {
                cam.tag = "MainCamera";
            }

            // 授業の管理
            var lessonGo = new GameObject("Lesson");
            var inputMode = lessonGo.AddComponent<InputModeController>();
            var sessionLog = lessonGo.AddComponent<SessionLog>();
            var headLogger = lessonGo.AddComponent<HeadLogger>();
            var narration = lessonGo.AddComponent<AudioSource>();
            narration.playOnAwake = false;
            narration.spatialBlend = 0f;
            var controller = lessonGo.AddComponent<LessonController>();
            var assemblyLock = lessonGo.AddComponent<AssemblyLock>();
            var bootstrap = lessonGo.AddComponent<LessonTestBootstrap>();
            var overlay = lessonGo.AddComponent<LessonDebugOverlay>();
            LessonMessageView message = CreateMessageView(font);
            var experimenter = lessonGo.AddComponent<ExperimenterControls>();
            var presence = lessonGo.AddComponent<PresenceMonitor>();
            var hint = lessonGo.AddComponent<HintPresenter>();

            // UI（学習者パネル）：コントローラーの光線とマウスの両方で押せるように XRUIInputModule を使う
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<XRUIInputModule>();
            LearnerPanel panel = CreateLearnerPanel(font, cam, inputMode);

            Set(inputMode, "config", config);
            Set(assemblyLock, "config", config);
            Set(headLogger, "head", cam != null ? cam.transform : null);
            Set(headLogger, "config", config);
            Set(controller, "config", config);
            Set(controller, "sessionLog", sessionLog);
            Set(controller, "narrationSource", narration);
            Set(controller, "messageView", message);
            Set(bootstrap, "lesson", lesson);
            Set(bootstrap, "config", config);
            Set(bootstrap, "controller", controller);
            Set(bootstrap, "sessionLog", sessionLog);
            Set(bootstrap, "headLogger", headLogger);
            Set(bootstrap, "inputMode", inputMode);
            Set(overlay, "controller", controller);
            Set(overlay, "inputMode", inputMode);
            Set(controller, "learnerPanel", panel);
            Set(controller, "hintPresenter", hint);
            Set(hint, "hintMaterial", GetHintMaterial());
            Set(experimenter, "controller", controller);
            Set(experimenter, "config", config);
            Set(presence, "controller", controller);
            Set(presence, "config", config);

            // デバッグ用のキーマウ操作
            var mouseGo = new GameObject("Debug Mouse Interactor");
            var mouse = mouseGo.AddComponent<DebugMouseInteractor>();
            Set(mouse, "inputMode", inputMode);
            Set(mouse, "config", config);
            Set(mouse, "rayCamera", cam);
            var camControl = lessonGo.AddComponent<DebugCameraControl>();
            Set(camControl, "cameraTransform", cam != null ? cam.transform : null);
            Set(camControl, "rigRoot", xrOrigin.transform);
            Set(camControl, "inputMode", inputMode);
            Set(camControl, "config", config);

            // 部品とソケット
            var partsRoot = new GameObject("Parts").transform;
            var socketsRoot = new GameObject("Sockets").transform;
            foreach (PartSpec spec in Parts)
            {
                CreatePart(spec, partsRoot, inputMode, sessionLog, config);
                CreateSocket(spec, socketsRoot, config, sessionLog);
            }

            if (new SerializedObject(bootstrap).FindProperty("lesson").objectReferenceValue == null)
            {
                Debug.LogError("LessonTestBootstrap に授業データを割り当てられませんでした。");
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log($"テスト用のシーンを作りました：{ScenePath}（授業データ：{LessonPath}）" +
                      (font == null ? "\n日本語フォントがないため、日本語は □ で表示されます（Tools/Experiment/Setup Japanese Font）。" : string.Empty));
        }

        private static XRGrabInteractable CreatePart(PartSpec spec, Transform parent, InputModeController inputMode, SessionLog sessionLog, ExperimentConfig config)
        {
            GameObject go = GameObject.CreatePrimitive(spec.Shape);
            go.name = $"Part {spec.Label} ({spec.Id})";
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(spec.LayoutPosition, Quaternion.identity); // 分解した状態で机に置く
            go.transform.localScale = spec.Scale;
            go.GetComponent<Renderer>().sharedMaterial = GetMaterial("Part_" + spec.Id, spec.Color);

            // 円柱の当たり判定はカプセルなので、箱に替える
            if (spec.Shape != PrimitiveType.Cube)
            {
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.AddComponent<BoxCollider>();
            }

            var grab = go.AddComponent<XRGrabInteractable>(); // Rigidbody も自動で付く
            var rb = go.GetComponent<Rigidbody>();
            rb.mass = 0.2f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            grab.movementType = XRBaseInteractable.MovementType.Kinematic;
            grab.throwOnDetach = false;
            grab.useDynamicAttach = true;

            var part = go.AddComponent<PartInteraction>();
            Set(part, "partId", spec.Id);
            SetEnum(part, "symmetryAxis", (int)spec.Symmetry);
            Set(part, "inputMode", inputMode);
            Set(part, "sessionLog", sessionLog);
            Set(part, "config", config);
            return grab;
        }

        private static void CreateSocket(PartSpec spec, Transform parent, ExperimentConfig config, SessionLog sessionLog)
        {
            var go = new GameObject($"Socket {spec.Label} ({spec.Id})");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(spec.Position, Quaternion.identity);

            var trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = config.socketSnapDistance;

            var socket = go.AddComponent<XRSocketInteractor>();
            socket.showInteractableHoverMeshes = true;

            var partSocket = go.AddComponent<PartSocket>();
            Set(partSocket, "socketId", spec.Id);
            Set(partSocket, "acceptedPartId", spec.Id);
            Set(partSocket, "config", config);
            Set(partSocket, "sessionLog", sessionLog);
            SetStrings(partSocket, "prerequisiteSocketIds", spec.Prerequisites);
        }

        /// <summary>学習者パネル（リセット・ヒント）。机の左手前に、少し奥へ傾けて置く（位置はシーンで動かしてよい）。</summary>
        private static LearnerPanel CreateLearnerPanel(TMP_FontAsset font, Camera cam, InputModeController inputMode)
        {
            var canvasGo = new GameObject("Learner Panel", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            canvasGo.AddComponent<GraphicRaycaster>();             // マウス
            canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>(); // コントローラー
            var rect = (RectTransform)canvasGo.transform;
            rect.sizeDelta = new Vector2(320f, 150f);
            rect.localScale = Vector3.one * 0.001f; // 0.32 m × 0.15 m
            rect.SetPositionAndRotation(new Vector3(-0.45f, TableTopY + 0.12f, 0.36f), Quaternion.Euler(35f, -15f, 0f));

            var bg = new GameObject("Background", typeof(RectTransform)).AddComponent<Image>();
            bg.transform.SetParent(canvasGo.transform, false);
            bg.color = new Color(0.1f, 0.1f, 0.12f, 0.9f);
            Stretch((RectTransform)bg.transform);

            Button reset = CreateButton(canvasGo.transform, "Reset Button", "リセット", new Vector2(-80f, 0f), font);
            Button hint = CreateButton(canvasGo.transform, "Hint Button", "ヒント", new Vector2(80f, 0f), font);

            var panel = canvasGo.AddComponent<LearnerPanel>();
            Set(panel, "resetButton", reset);
            Set(panel, "hintButton", hint);
            Set(panel, "inputMode", inputMode);
            return panel;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 position, TMP_FontAsset font)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.sizeDelta = new Vector2(140f, 100f);
            r.anchoredPosition = position;
            var image = go.AddComponent<Image>();
            image.color = new Color(0.25f, 0.45f, 0.85f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            var text = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            text.transform.SetParent(go.transform, false);
            Stretch((RectTransform)text.transform);
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 32f;
            text.color = Color.white;
            text.text = label;
            if (font != null)
            {
                text.font = font;
            }

            return button;
        }

        /// <summary>ヒント用の半透明の黄色（URP Unlit、Transparent）。</summary>
        private static Material GetHintMaterial()
        {
            EnsureFolder(MaterialFolder);
            string path = $"{MaterialFolder}/Hint.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
            {
                return mat;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Transparent");
            }

            mat = new Material(shader);
            mat.SetColor("_BaseColor", new Color(1f, 0.85f, 0.1f, 0.5f));
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend", 0f);   // Alpha
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static LessonMessageView CreateMessageView(TMP_FontAsset font)
        {
            var canvasGo = new GameObject("Message Canvas", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)canvasGo.transform;
            rect.sizeDelta = new Vector2(900f, 250f);
            rect.localScale = Vector3.one * 0.001f; // 0.9 m × 0.25 m
            rect.position = new Vector3(0f, 1.45f, 1.2f);

            var bg = new GameObject("Background", typeof(RectTransform)).AddComponent<Image>();
            bg.transform.SetParent(canvasGo.transform, false);
            bg.color = new Color(0f, 0f, 0f, 0.75f);
            Stretch((RectTransform)bg.transform);

            var text = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            text.transform.SetParent(canvasGo.transform, false);
            Stretch((RectTransform)text.transform);
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 56f;
            text.color = Color.white;
            text.text = LessonController.RemoveHmdMessage;
            if (font != null)
            {
                text.font = font;
            }

            var view = canvasGo.AddComponent<LessonMessageView>();
            Set(view, "text", text);
            Set(view, "root", canvasGo);
            canvasGo.SetActive(false);
            return view;
        }

        private static void CreateLessonDataIfMissing()
        {
            var existing = AssetDatabase.LoadAssetAtPath<LessonData>(LessonPath);
            if (existing != null &&
                !EditorUtility.DisplayDialog("Build Lesson Test Scene",
                    LessonPath + " を初期の内容（組み立て中心の5段階）で上書きしますか？\n手で直した内容は消えます。", "上書きする", "そのまま使う"))
            {
                return;
            }

            // 組み立てが中心。触れる段階は、まだ置いていない（固定されていない）部品を対象にする
            var stages = new List<StageData>
            {
                Stage("プラッタを付ける", PlatterId, CompletionType.Attach, false, ""),
                Stage("アームに触れる", ArmId, CompletionType.Touch, false, ""),
                Stage("アームを付ける", ArmId, CompletionType.Attach, true, "アームはプラッタの上を動く（仮）"),
                Stage("カバーを付ける", CoverId, CompletionType.Attach, true, "カバーで中を密閉する（仮）"),
                Stage("まとめ", "", CompletionType.None, false, ""),
            };

            if (existing != null)
            {
                existing.lessonId = LessonId.HDD;
                existing.stages = stages;
                EditorUtility.SetDirty(existing);
                return;
            }

            EnsureFolder("Assets/Lessons");
            var data = ScriptableObject.CreateInstance<LessonData>();
            data.lessonId = LessonId.HDD;
            data.stages = stages;
            AssetDatabase.CreateAsset(data, LessonPath);
        }

        private static StageData Stage(string name, string target, CompletionType completion, bool key, string telop)
        {
            return new StageData
            {
                stageName = name,
                targetPartId = target,
                completion = completion,
                isKeyPoint = key,
                telopText = telop,
            };
        }

        // ---- 小さな道具 ----
        private static GameObject CreateBox(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.isStatic = true;
            return go;
        }

        private static Material GetMaterial(string name, Color color)
        {
            EnsureFolder(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>private の [SerializeField] に値を入れる。</summary>
        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field) ?? throw new System.ArgumentException($"{target.GetType().Name} に {field} がありません。");
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field) ?? throw new System.ArgumentException($"{target.GetType().Name} に {field} がありません。");
            p.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetStrings(Object target, string field, string[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field) ?? throw new System.ArgumentException($"{target.GetType().Name} に {field} がありません。");
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                p.GetArrayElementAtIndex(i).stringValue = values[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field) ?? throw new System.ArgumentException($"{target.GetType().Name} に {field} がありません。");
            p.enumValueIndex = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
