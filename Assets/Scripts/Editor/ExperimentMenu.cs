using System.IO;
using UnityEditor;
using UnityEngine;
using VRClass.Experiment;

namespace VRClass.EditorTools
{
    /// <summary>Tools/Experiment/... のメニュー。</summary>
    public static class ExperimentMenu
    {
        public const string ConfigFolder = "Assets/Config";
        public const string ConfigAssetPath = ConfigFolder + "/ExperimentConfig.asset";

        /// <summary>ExperimentConfig の asset を作る。既にあればそれを選択する。</summary>
        [MenuItem("Tools/Experiment/Create Config")]
        public static ExperimentConfig CreateConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ExperimentConfig>(ConfigAssetPath);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                Debug.Log($"ExperimentConfig は既にあります：{ConfigAssetPath}");
                return existing;
            }

            if (!AssetDatabase.IsValidFolder(ConfigFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Config");
            }

            var config = ScriptableObject.CreateInstance<ExperimentConfig>();
            AssetDatabase.CreateAsset(config, ConfigAssetPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
            Debug.Log($"ExperimentConfig を作りました：{ConfigAssetPath}");
            return config;
        }

        /// <summary>ログと participants.csv の置き場所（persistentDataPath）をエクスプローラーで開く。</summary>
        [MenuItem("Tools/Experiment/Open Data Folder")]
        public static void OpenDataFolder()
        {
            string path = Application.persistentDataPath;
            Directory.CreateDirectory(path);
            EditorUtility.RevealInFinder(path + Path.DirectorySeparatorChar);
            Debug.Log($"データの保存先：{path}");
        }

        /// <summary>participants.csv がなければ、見出しだけのひな形を persistentDataPath に作る。</summary>
        [MenuItem("Tools/Experiment/Create participants.csv Template")]
        public static void CreateParticipantsTemplate()
        {
            var config = AssetDatabase.LoadAssetAtPath<ExperimentConfig>(ConfigAssetPath);
            string fileName = config != null ? config.participantsFileName : "participants.csv";
            string path = Path.Combine(Application.persistentDataPath, fileName);
            if (File.Exists(path))
            {
                Debug.Log($"{fileName} は既にあります（上書きしません）：{path}");
            }
            else
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(path, "id\n# 1行に1つ、参加者番号（正の整数）を書く。氏名は書かない\n");
                Debug.Log($"{fileName} のひな形を作りました：{path}");
            }

            EditorUtility.RevealInFinder(path);
        }
    }
}
