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

        public const string TitlePath = Folder + "/Letreiro3D.prefab";
        const string TitleMeshes = Folder + "/Letras";

        /// <summary>
        /// Titulo "SHOW DO / PROCON" em letras 3D extrudadas, feitas com a
        /// ferramenta de letras deformaveis do llm-tools: cada letra ganha uma
        /// deformacao de volume propria (abre em cima, curva e torce alternando) e
        /// as letras ficam em arco, como logotipo de programa de auditorio.
        /// </summary>
        [MenuItem("Missao PROCON/5 - Gerar titulo 3D (precisa do llm-tools)")]
        public static void GenerateTitle()
        {
            var letters = Type.GetType("LlmTools.DeformableTextTools, LlmTools.Editor");
            var json = Type.GetType("Newtonsoft.Json.Linq.JObject, Newtonsoft.Json");
            if (letters == null || json == null)
            {
                EditorUtility.DisplayDialog("Missão PROCON", "Instale o pacote llm-tools-unity só para gerar o título; depois pode remover.", "OK");
                return;
            }

            Directory(Folder);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, Workshop);
            if (AssetDatabase.IsValidFolder(GeneratedByTool)) AssetDatabase.DeleteAsset(GeneratedByTool);

            var root = new GameObject("Letreiro 3D");
            var top = Word(letters, json, "SHOW DO", 0.62f, 4.2f, 0.22f, (i, n) =>
                "{\"op\":\"warp\",\"twist\":" + (i % 2 == 0 ? 10 : -10) + ",\"bend\":0,\"taper\":0.88,\"slices\":10}");
            var bottom = Word(letters, json, "PROCON", 1.25f, 6.4f, 0.38f, (i, n) =>
                "{\"op\":\"warp\",\"twist\":" + (i % 2 == 0 ? 6 : -6) + ",\"bend\":" + (i % 2 == 0 ? 12 : -12) + ",\"taper\":1.28,\"slices\":14}");
            top.transform.SetParent(root.transform, false);
            bottom.transform.SetParent(root.transform, false);
            top.transform.localPosition = new Vector3(0f, 1.5f, 0f);

            Arch(top.transform, 0.12f, 6f);
            Arch(bottom.transform, 0.32f, 9f);

            StripToolComponents(root);
            Paint(top, ShowBuilder.Material("Letra prata", new Color(0.95f, 0.96f, 1f), 0.45f), 0.6f);
            Paint(bottom, ShowBuilder.Material("Letra dourada", new Color(1f, 0.74f, 0.2f), 0.35f), 0.85f);

            if (AssetDatabase.IsValidFolder(GeneratedByTool))
            {
                if (AssetDatabase.IsValidFolder(TitleMeshes)) AssetDatabase.DeleteAsset(TitleMeshes);
                var error = AssetDatabase.MoveAsset(GeneratedByTool, TitleMeshes);
                if (!string.IsNullOrEmpty(error)) Debug.LogError("Missao PROCON: " + error);
            }
            PrefabUtility.SaveAsPrefabAsset(root, TitlePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(Workshop);
            AssetDatabase.SaveAssets();
            Debug.Log("Missao PROCON: titulo salvo em " + TitlePath);
        }

        /// <summary>Cria uma palavra e aplica a deformacao 3D de cada letra pela propria ferramenta.</summary>
        static GameObject Word(Type tools, Type json, string text, float height, float width, float depth, Func<int, int, string> warp)
        {
            var result = Invoke(tools, json, "llm_create_deformable_text",
                "{\"text\":\"" + text + "\",\"name\":\"" + text + "\",\"height\":" + F(height) + ",\"width\":" + F(width) +
                ",\"depth\":" + F(depth) + ",\"spacing\":0.06,\"samples\":10,\"color\":\"#FFFFFF\"}");
            var raw = result.ToString();
            var rows = System.Text.RegularExpressions.Regex.Matches(raw.Substring(raw.IndexOf("\"Rows\"")),
                "\\[\\s*(\\d+),\\s*\"(.*?)\",\\s*\"(r:\\d+)\"");
            var visible = 0;
            foreach (System.Text.RegularExpressions.Match row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.Groups[2].Value)) continue;
                Invoke(Type.GetType("LlmTools.DeformableTextTools, LlmTools.Editor"), json, "llm_deform_letter",
                    "{\"id\":\"" + row.Groups[3].Value + "\",\"operations\":[" + warp(visible, rows.Count) + "]}");
                visible++;
            }
            var created = FindNewest(text);
            if (created == null) throw new InvalidOperationException("Nao achei a palavra " + text);
            return created;
        }

        /// <summary>Sobe o meio da palavra e inclina as pontas: arco de logotipo.</summary>
        static void Arch(Transform word, float rise, float tilt)
        {
            float extent = 0f;
            foreach (Transform letter in word) extent = Mathf.Max(extent, Mathf.Abs(letter.localPosition.x));
            if (extent <= 0f) return;
            foreach (Transform letter in word)
            {
                var k = letter.localPosition.x / extent;
                letter.localPosition += Vector3.up * rise * (1f - k * k);
                letter.localRotation = Quaternion.Euler(0f, 0f, -k * tilt) * letter.localRotation;
            }
        }

        static void Paint(GameObject word, Material material, float metallic)
        {
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", 0.72f);
            EditorUtility.SetDirty(material);
            foreach (var renderer in word.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.sharedMaterial = material;
                renderer.SetPropertyBlock(null);
            }
        }

        static object Invoke(Type tools, Type json, string tool, string arguments)
        {
            var parse = json.GetMethod("Parse", new[] { typeof(string) });
            var handle = tools.GetMethod("Handle", BindingFlags.Public | BindingFlags.Static);
            return handle.Invoke(null, new[] { tool, parse.Invoke(null, new object[] { arguments }) });
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
