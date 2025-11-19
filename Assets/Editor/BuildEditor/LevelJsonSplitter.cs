using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

public class LevelJsonSplitter : EditorWindow
{
    private TextAsset inputJson;
    private string outputFolder = "Assets/FourWordIdiom/MultipleData/StageDatas/StageInfos/chineseStage";

    [MenuItem("Tools/Level JSON Splitter")]
    public static void ShowWindow()
    {
        GetWindow<LevelJsonSplitter>("Level JSON Splitter");
    }

    private void OnGUI()
    {
        GUILayout.Label("Level JSON Splitter", EditorStyles.boldLabel);
        
        inputJson = (TextAsset)EditorGUILayout.ObjectField("Input JSON", inputJson, typeof(TextAsset), false);
        
        EditorGUILayout.Space();
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);
        
        EditorGUILayout.Space();
        if (GUILayout.Button("Split JSON Files"))
        {
            if (inputJson != null)
            {
                SplitJsonFile();
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Please select an input JSON file", "OK");
            }
        }
    }

    private void SplitJsonFile()
    {
        try
        {
            // Create output directory if it doesn't exist
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
                AssetDatabase.Refresh();
            }

            string fullJson = inputJson.text;
            int levelStartIndex = fullJson.IndexOf("\"1_pass\"");
            int levelNumber = 1;

            while (levelStartIndex >= 0)
            {
                // Find the start of the next level or end of file
                int nextLevelStart = fullJson.IndexOf($"\"{levelNumber + 1}_pass\"", levelStartIndex);
                int levelEnd = (nextLevelStart >= 0) ? nextLevelStart : fullJson.Length;

                // Extract the level JSON
                string levelJson = fullJson.Substring(levelStartIndex, levelEnd - levelStartIndex);
                levelJson = "{\n" + levelJson.Trim().TrimEnd(',') + "\n}";

                // Write to file
                string fileName = $"hexlevel_{levelNumber}.json";
                string filePath = Path.Combine(outputFolder, fileName);
                File.WriteAllText(filePath, levelJson, Encoding.UTF8);

                Debug.Log($"Created level file: {filePath}");

                // Prepare for next level
                levelStartIndex = nextLevelStart;
                levelNumber++;
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Success", $"Successfully split {levelNumber - 1} levels", "OK");
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("Error", $"Failed to split JSON: {e.Message}", "OK");
            Debug.LogError(e);
        }
    }
}