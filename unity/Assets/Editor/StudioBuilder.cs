using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Procon.EditorTools
{
    /// <summary>
    /// Gera a geometria do estudio de TV com o pacote llm-tools-unity e guarda o
    /// resultado como prefab comum (Assets/Art/Estudio/Estudio.prefab).
    ///
    /// O pacote so precisa estar instalado na hora de gerar: os componentes dele
    /// sao retirados e ficam so malhas, materiais URP e luzes. Por isso o projeto
    /// abre e compila sem o pacote, e o prefab ja gerado continua valendo.
    /// </summary>
    public static class StudioBuilder
    {
        const string Folder = "Assets/Art/Estudio";
        public const string PrefabPath = Folder + "/Estudio.prefab";
        const string Workshop = Folder + "/_oficina.unity";
        const string GeneratedByTool = "Assets/LLMToolsGenerated";
        const string GeneratedFolder = Folder + "/Gerado";

        [MenuItem("Missao PROCON/4 - Gerar estudio de TV (precisa do llm-tools)")]
        public static void Generate()
        {
            var stage = Type.GetType("LlmTools.StageTools, LlmTools.Editor");
            var lamps = Type.GetType("LlmTools.LampTools, LlmTools.Editor");
            var json = Type.GetType("Newtonsoft.Json.Linq.JObject, Newtonsoft.Json");
            if (stage == null || lamps == null || json == null)
            {
                EditorUtility.DisplayDialog("Missão PROCON",
                    "O pacote llm-tools-unity não está instalado. Instale pelo Package Manager " +
                    "(Install package from disk) só para gerar o estúdio; depois pode remover.", "OK");
                return;
            }

            Directory(Folder);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, Workshop);

            // palco, telao principal, trelica, paineis laterais, pulpito e duas luzes
            var root = Call(stage, json, "llm_create_tv_set",
                "{\"style\":\"stage\",\"name\":\"Estudio\",\"width\":12,\"depth\":8,\"height\":7.5," +
                "\"color\":\"#1B1446\",\"accentColor\":\"#7B3CFF\",\"emission\":1.4,\"title\":\"\"," +
                "\"cameras\":false,\"lighting\":true}");

            // tubos de LED nas laterais do telao
            foreach (var x in new[] { -4.55f, 4.55f })
            {
                var tube = Call(lamps, json, "llm_create_lamp",
                    "{\"style\":\"tube\",\"name\":\"Tubo LED\",\"color\":\"#C9A2FF\",\"intensity\":1.2," +
                    "\"emission\":4,\"range\":6,\"position\":[" + F(x) + ",0.9,2.55],\"angles\":[0,0,90]}");
                tube.transform.SetParent(root.transform, true);
            }

            // o pulpito vai para a esquerda: e o lugar do apresentador, fora do telao
            var lectern = root.transform.Find("Presentation lectern");
            if (lectern != null) lectern.localPosition = new Vector3(-5.05f, lectern.localPosition.y, -0.55f);

            StripToolComponents(root);
            EditorSceneManager.SaveScene(scene);

            if (AssetDatabase.IsValidFolder(GeneratedByTool))
            {
                if (AssetDatabase.IsValidFolder(GeneratedFolder)) AssetDatabase.DeleteAsset(GeneratedFolder);
                var error = AssetDatabase.MoveAsset(GeneratedByTool, GeneratedFolder);
                if (!string.IsNullOrEmpty(error)) Debug.LogError("Missao PROCON: " + error);
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(Workshop);
            AssetDatabase.SaveAssets();
            Debug.Log("Missao PROCON: estudio salvo em " + PrefabPath);
        }

        static GameObject Call(Type tools, Type json, string tool, string arguments)
        {
            var parse = json.GetMethod("Parse", new[] { typeof(string) });
            var handle = tools.GetMethod("Handle", BindingFlags.Public | BindingFlags.Static);
            var result = handle.Invoke(null, new[] { tool, parse.Invoke(null, new object[] { arguments }) });
            // o resultado traz a referencia "r:N" do objeto criado; o nome resolve igual
            var name = result.GetType().GetProperty("Item", new[] { typeof(string) }).GetValue(result, new object[] { "Name" }).ToString();
            var created = Selection.activeGameObject != null && Selection.activeGameObject.name == name
                ? Selection.activeGameObject
                : FindNewest(name);
            if (created == null) throw new InvalidOperationException("Nao achei o objeto criado por " + tool);
            return created;
        }

        static GameObject FindNewest(string name)
        {
            GameObject found = null;
            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID))
                if (go.name == name && go.transform.parent == null) found = go;
            return found;
        }

        /// <summary>Tira os componentes do pacote (receitas, vinculos de texto).</summary>
        static void StripToolComponents(GameObject root)
        {
            foreach (var component in root.GetComponentsInChildren<Component>(true))
                if (component != null && component.GetType().Namespace == "LlmTools")
                    UnityEngine.Object.DestroyImmediate(component);
        }

        static void Directory(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            Directory(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        static string F(float value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
