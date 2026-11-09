using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace VRClass.EditorTools
{
    /// <summary>
    /// Tools/Experiment/Setup Japanese Font：
    /// Assets/Fonts/ に置いた Noto Sans JP（.ttf / .otf）から、TextMeshPro の動的フォントアセットを作り、
    /// TMP Settings のフォールバックに登録する（どの TMP の文字でも日本語が出るようにする）。
    /// 動的フォントアセットは、使った文字だけを実行時にアトラスへ追加するので、文字の一覧を用意しなくてよい。
    /// </summary>
    public static class JapaneseFontSetup
    {
        public const string FontFolder = "Assets/Fonts";
        public const string FontAssetPath = FontFolder + "/NotoSansJP SDF.asset";
        public const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        [MenuItem("Tools/Experiment/Setup Japanese Font")]
        public static void Setup()
        {
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (fontAsset == null)
            {
                Font source = FindSourceFont();
                if (source == null)
                {
                    EditorUtility.DisplayDialog("Setup Japanese Font",
                        "Assets/Fonts/ に Noto Sans JP のフォントファイル（例：NotoSansJP-Regular.ttf）がありません。\n" +
                        "Google Fonts から Noto Sans JP をダウンロードし、static フォルダの NotoSansJP-Regular.ttf を Assets/Fonts/ に置いてから、もう一度実行してください。",
                        "OK");
                    return;
                }

                fontAsset = TMP_FontAsset.CreateFontAsset(source); // 動的（Dynamic）
                if (fontAsset == null)
                {
                    Debug.LogError("フォントアセットを作れませんでした。フォントファイルの Include Font Data がオンか確認してください。");
                    return;
                }

                fontAsset.name = Path.GetFileNameWithoutExtension(FontAssetPath);
                AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
                foreach (Texture2D tex in fontAsset.atlasTextures)
                {
                    if (tex != null)
                    {
                        tex.name = fontAsset.name + " Atlas";
                        AssetDatabase.AddObjectToAsset(tex, fontAsset);
                    }
                }

                if (fontAsset.material != null)
                {
                    fontAsset.material.name = fontAsset.name + " Material";
                    AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
                }

                AssetDatabase.SaveAssets();
                Debug.Log($"日本語のフォントアセットを作りました：{FontAssetPath}（元：{AssetDatabase.GetAssetPath(source)}）");
            }

            AddToTmpFallback(fontAsset);
            Selection.activeObject = fontAsset;
        }

        /// <summary>作成済みの日本語フォントアセット。なければ null。</summary>
        public static TMP_FontAsset FindJapaneseFontAsset()
        {
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        }

        private static Font FindSourceFont()
        {
            if (!AssetDatabase.IsValidFolder(FontFolder))
            {
                return null;
            }

            Font fallback = null;
            foreach (string guid in AssetDatabase.FindAssets("t:Font", new[] { FontFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileName(path);
                if (!file.StartsWith("NotoSansJP"))
                {
                    continue;
                }

                var font = AssetDatabase.LoadAssetAtPath<Font>(path);
                if (file.Contains("Regular"))
                {
                    return font;
                }

                fallback = fallback != null ? fallback : font;
            }

            return fallback;
        }

        private static void AddToTmpFallback(TMP_FontAsset fontAsset)
        {
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (settings == null)
            {
                Debug.LogWarning($"TMP Settings が見つかりません：{TmpSettingsPath}。フォールバックへの登録は飛ばします。");
                return;
            }

            var so = new SerializedObject(settings);
            SerializedProperty list = so.FindProperty("m_fallbackFontAssets");
            if (list == null)
            {
                Debug.LogWarning("TMP Settings にフォールバックの項目が見つかりません。手で登録してください。");
                return;
            }

            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == fontAsset)
                {
                    Debug.Log("日本語フォントは既に TMP Settings のフォールバックに登録されています。");
                    return;
                }
            }

            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = fontAsset;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("日本語フォントを TMP Settings のフォールバックに登録しました。");
        }
    }
}
