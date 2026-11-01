using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Procon.EditorTools
{
    /// <summary>
    /// Monta a cena do Show do PROCON: estudio 3D (prefab gerado pelo StudioBuilder),
    /// camera, luzes, lampadas do telao, apresentador e o telao com a interface 2D
    /// do jogo num Canvas em World Space, clicavel pela camera do estudio.
    /// </summary>
    public static class ShowBuilder
    {
        const string ArtFolder = "Assets/Art/Estudio";
        const string ShowArtFolder = "Assets/Art/Show";
        const string VolumePath = "Assets/Settings/EstudioVolume.asset";
        const int StudioRenderer = 1;   // Estudio3D na lista do UniversalRP.asset; o 0 e o 2D

        public struct StudioRig
        {
            public Transform screen;        // Canvas do telao (World Space): pergunta e ilustracao
            public Transform overlay;       // Canvas da tela: botoes, placar, resultado
            public Presenter presenter;
        }

        // ================================================================== estudio

        public static StudioRig BuildStudioScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var rig = new StudioRig();

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.15f, 0.17f, 0.27f);
            RenderSettings.skybox = null;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StudioBuilder.PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("Missao PROCON: falta " + StudioBuilder.PrefabPath + ". Rode 'Gerar estudio de TV' com o llm-tools instalado.");
                return rig;
            }
            var studio = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

            DimLedWalls(studio);

            // o telao principal: o Canvas do telao vai na frente dele, do mesmo tamanho
            var wall = studio.transform.Find("Main LED backdrop");
            var wallBounds = Bounds(wall);
            var front = wallBounds.min.z - 0.015f;
            var screenCenter = new Vector3(wallBounds.center.x, wallBounds.center.y, front);

            // enquadramento: telao na parte de cima e a direita (60% da largura),
            // apresentador inteiro no palco a esquerda e o chao livre embaixo para os botoes
            var target = new Vector3(screenCenter.x - 2.49f, 2f, front);
            var camera = BuildCamera(target, 10.65f, 0.35f, 40f);

            // apresentador na frente do palco, mais perto da camera, fora da area do telao
            var podium = new Vector3(target.x - 2.7f, 0.22f, -0.98f);
            HostDesk(studio, podium);
            rig.presenter = BuildPresenter(new Vector3(podium.x, 0.22f, podium.z + 0.5f));
            DecorateStudio(studio, wallBounds, front, false);
            StageLighting(podium, wallBounds, false);
            EnsureRenderQuality();

            BuildLights(wallBounds, podium);
            BuildBulbs(wallBounds, front);
            BuildVolume();

            rig.screen = BuildScreenCanvas(camera, screenCenter, wallBounds.size.x * 0.965f);
            rig.overlay = BuildOverlay();

            var events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
            events.AddComponent<InputSystemUIInputModule>();
            return rig;
        }

        /// <summary>
        /// Os paineis de LED do estudio saem do gerador num roxo claro e forte, que
        /// estoura atras do titulo e das letras. Aqui ficam num roxo fundo.
        /// </summary>
        static void DimLedWalls(GameObject studio)
        {
            var led = Material("LED do telao", new Color(0.03f, 0.08f, 0.22f), 0.6f);
            led.SetColor("_BaseColor", new Color(0.03f, 0.08f, 0.22f));
            led.SetColor("_EmissionColor", new Color(0.03f, 0.08f, 0.22f) * 0.6f);
            led.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(led);
            // "Studioaccent" (frente do pulpito e faixa do palco) sai do gerador em
            // roxo claro com emissao 1,4: com o bloom estoura e fica rosa/lilas
            var accent = Material("Destaque do palco", new Color(0.08f, 0.28f, 0.7f), 0.35f);
            accent.SetColor("_BaseColor", new Color(0.08f, 0.28f, 0.7f));
            accent.SetColor("_EmissionColor", new Color(0.08f, 0.28f, 0.7f) * 0.35f);
            EditorUtility.SetDirty(accent);
            var deck = Material("Piso do palco", new Color(0.04f, 0.06f, 0.13f));
            deck.SetFloat("_Smoothness", 0.82f);
            deck.SetFloat("_Metallic", 0.2f);
            EditorUtility.SetDirty(deck);
            foreach (var renderer in studio.GetComponentsInChildren<MeshRenderer>(true))
            {
                var materials = renderer.sharedMaterials;
                var changed = false;
                for (var i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null) continue;
                    if (materials[i].name.StartsWith("LED")) { materials[i] = led; changed = true; }
                    else if (materials[i].name.StartsWith("Studioaccent")) { materials[i] = accent; changed = true; }
                    else if (materials[i].name.StartsWith("Studiobody")) { materials[i] = deck; changed = true; }
                }
                if (changed) renderer.sharedMaterials = materials;
            }
        }

        // ================================================================== menu em 3D

        /// <summary>
        /// Palco do menu: o mesmo estudio, com o titulo em letras 3D flutuando na
        /// frente do telao, quatro holofotes coloridos com feixe visivel varrendo o
        /// palco e as lampadas acesas. A interface do menu fica por cima.
        /// </summary>
        public static void BuildMenuStage()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.13f, 0.15f, 0.25f);
            RenderSettings.skybox = null;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StudioBuilder.PrefabPath);
            if (prefab == null) return;
            var studio = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            DimLedWalls(studio);
            var wall = Bounds(studio.transform.Find("Main LED backdrop"));
            var front = wall.min.z - 0.015f;

            var podium = new Vector3(wall.min.x - 0.56f, 0.22f, 1.3f);
            HostDesk(studio, podium);
            BuildPresenter(new Vector3(podium.x, 0.22f, podium.z + 0.5f));
            DecorateStudio(studio, wall, front, true);
            StageLighting(podium, wall, true);
            EnsureRenderQuality();
            Spot("Luz do apresentador", new Vector3(podium.x + 1.2f, 4.8f, podium.z - 3.5f), podium + Vector3.up * 1.5f,
                 new Color(1f, 0.9f, 0.75f), 18f, 26f);

            // camera aberta: o palco inteiro, o telao e o titulo na frente dele
            var target = new Vector3(0f, wall.center.y - 0.2f, front);
            var node = new GameObject("Main Camera");
            node.tag = "MainCamera";
            var camera = node.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.01f, 0.05f);
            camera.fieldOfView = 50f;
            node.AddComponent<AudioListener>();
            var data = node.AddComponent<UniversalAdditionalCameraData>();
            data.SetRenderer(StudioRenderer);
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            node.transform.position = target + new Vector3(0f, 0.6f, -10.5f);
            node.transform.LookAt(target);
            var drift = node.AddComponent<CameraDrift>();
            drift.target = target;
            drift.amplitude = new Vector3(0.35f, 0.1f, 0.15f);
            drift.period = 16f;

            BuildBulbs(wall, front);
            BuildVolume();

            // titulo 3D na metade de cima do telao, balancando devagar
            var titlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StudioBuilder.TitlePath);
            if (titlePrefab != null)
            {
                var title = (GameObject)PrefabUtility.InstantiatePrefab(titlePrefab);
                title.transform.position = new Vector3(0f, wall.center.y + 0.15f, front - 0.2f);
                title.transform.localScale = Vector3.one * 1.05f;
                // luz larga e mais fraca, vinda de longe: ilumina o titulo por igual. A antiga
                // (estreita e forte, no centro) estourava o "W" de branco
                var titleLight = Spot("Luz do titulo", new Vector3(0f, 7.5f, front - 9f), title.transform.position + Vector3.up * 0.9f,
                     new Color(1f, 0.94f, 0.85f), 9f, 70f);
                titleLight.range = 18f;
                // prata menos espelhada e sem brilho proprio, para nao virar mancha branca
                var silver = AssetDatabase.LoadAssetAtPath<Material>(ArtFolder + "/Materiais/Letra prata.mat");
                if (silver != null)
                {
                    silver.SetColor("_BaseColor", new Color(0.86f, 0.89f, 0.95f));
                    silver.SetFloat("_Metallic", 0.25f);
                    silver.SetFloat("_Smoothness", 0.45f);
                    silver.SetColor("_EmissionColor", new Color(0.86f, 0.89f, 0.95f) * 0.08f);
                    EditorUtility.SetDirty(silver);
                }
            }

            // holofotes de show: luz real com cone de luz visivel, varrendo o palco
            var colors = new[] { new Color(0.25f, 0.55f, 1f), new Color(0.3f, 0.85f, 1f), new Color(1f, 0.8f, 0.3f), new Color(0.85f, 0.9f, 1f) };
            var xs = new[] { -5.2f, -1.9f, 1.9f, 5.2f };
            for (var i = 0; i < xs.Length; i++)
                MovingSpot(i, new Vector3(xs[i], 7.2f, -0.6f), colors[i]);
        }

        static void MovingSpot(int index, Vector3 position, Color color)
        {
            var node = new GameObject("Holofote " + (index + 1));
            node.transform.position = position;
            node.transform.rotation = Quaternion.LookRotation(new Vector3(-position.x * 0.35f, -position.y, 1.5f));
            var light = node.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = color;
            light.intensity = 40f;
            light.spotAngle = 24f;
            light.range = 14f;
            light.shadows = LightShadows.None;

            var housing = Material("Lata do refletor", new Color(0.05f, 0.06f, 0.08f));
            var lens = Material("Lente do refletor", color, 3f);
            var can = Block(node.transform, "Lata", new Vector3(0.32f, 0.32f, 0.42f), new Vector3(0f, 0f, -0.18f), housing);
            Block(node.transform, "Lente", new Vector3(0.26f, 0.26f, 0.02f), new Vector3(0f, 0f, 0.035f), lens);
            Block(node.transform, "Aleta", new Vector3(0.36f, 0.02f, 0.1f), new Vector3(0f, 0.17f, 0.06f), housing);
            can.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

            var beam = new GameObject("Feixe");
            beam.transform.SetParent(node.transform, false);
            beam.transform.localScale = new Vector3(1.45f, 1.45f, 8.5f);
            beam.AddComponent<MeshFilter>().sharedMesh = ConeMesh();
            var renderer = beam.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = BeamMaterial("Feixe " + (index + 1), color);
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            var sweep = node.AddComponent<LightSweep>();
            sweep.phase = index * 1.7f;
            sweep.yaw = 22f;
            sweep.pitch = 10f;
            sweep.period = 5.5f + index * 0.8f;
        }

        /// <summary>Cone aberto que nasce na lampada (origem) e abre para +Z.</summary>
        static Mesh ConeMesh()
        {
            const string path = ArtFolder + "/Feixe.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            const int segments = 28;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (var i = 0; i <= segments; i++)
            {
                var a = i * Mathf.PI * 2f / segments;
                vertices.Add(new Vector3(Mathf.Cos(a) * 0.04f, Mathf.Sin(a) * 0.04f, 0f));
                uvs.Add(new Vector2((float)i / segments, 0f));
                vertices.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 1f));
                uvs.Add(new Vector2((float)i / segments, 1f));
            }
            for (var i = 0; i < segments; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                triangles.AddRange(new[] { a, b, c, c, b, d });
            }
            mesh = new Mesh { name = "Feixe de luz" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        /// <summary>Material aditivo, sem profundidade: o feixe so soma luz, forte na lampada e sumindo na ponta.</summary>
        static Material BeamMaterial(string name, Color color)
        {
            var path = ArtFolder + "/Materiais/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                material.SetColor("_BaseColor", color * 0.28f);   // a cor do feixe acompanha a do holofote
                EditorUtility.SetDirty(material);
                return material;
            }
            if (!AssetDatabase.IsValidFolder(ArtFolder + "/Materiais")) AssetDatabase.CreateFolder(ArtFolder, "Materiais");

            var texturePath = ArtFolder + "/feixe3d.png";
            if (!File.Exists(texturePath))
            {
                var gradient = new Texture2D(8, 256, TextureFormat.RGBA32, false);
                for (var y = 0; y < 256; y++)
                {
                    var k = Mathf.Pow(1f - y / 255f, 1.8f);
                    for (var x = 0; x < 8; x++) gradient.SetPixel(x, y, new Color(k, k, k, 1f));
                }
                File.WriteAllBytes(texturePath, gradient.EncodeToPNG());
                Object.DestroyImmediate(gradient);
                AssetDatabase.ImportAsset(texturePath);
            }

            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_BaseColor", color * 0.28f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static Bounds Bounds(Transform node)
        {
            var renderers = node.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        static Camera BuildCamera(Vector3 target, float distance, float rise, float fov)
        {
            var node = new GameObject("Main Camera");
            node.tag = "MainCamera";
            var camera = node.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.02f, 0.07f);
            camera.fieldOfView = fov;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 60f;
            node.AddComponent<AudioListener>();

            var data = node.AddComponent<UniversalAdditionalCameraData>();
            data.SetRenderer(StudioRenderer);
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            node.transform.position = target + new Vector3(0f, rise, -distance);
            node.transform.LookAt(target);
            var drift = node.AddComponent<CameraDrift>();
            drift.target = target;
            drift.amplitude = new Vector3(0.06f, 0.02f, 0f);
            return camera;
        }

        static void BuildLights(Bounds wall, Vector3 podium)
        {
            Spot("Luz azul", new Vector3(-6.5f, 6.5f, -1.5f), new Vector3(wall.min.x, wall.center.y, wall.center.z),
                 new Color(0.3f, 0.55f, 1f), 1.2f, 40f);
            Spot("Luz ciano", new Vector3(6.5f, 6.5f, -1.5f), new Vector3(wall.max.x, wall.center.y, wall.center.z),
                 new Color(0.3f, 0.85f, 1f), 1.2f, 40f);
            var hostLight = Spot("Luz do apresentador", new Vector3(podium.x + 1.2f, 4.8f, podium.z - 3.5f), podium + Vector3.up * 1.5f,
                 new Color(1f, 0.9f, 0.75f), 15f, 26f);
            hostLight.shadows = LightShadows.Soft;
        }

        static Light Spot(string name, Vector3 position, Vector3 target, Color color, float intensity, float angle)
        {
            var node = new GameObject(name);
            node.transform.position = position;
            node.transform.LookAt(target);
            var light = node.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = color;
            light.intensity = intensity;
            light.spotAngle = angle;
            light.range = 16f;
            light.shadows = LightShadows.None;
            return light;
        }

        /// <summary>Lampadas em volta do telao, acendendo em sequencia.</summary>
        static void BuildBulbs(Bounds wall, float front)
        {
            var root = new GameObject("Lampadas do telao");
            var material = Material("Lampada", new Color(1f, 0.85f, 0.45f), 4f);
            var bulbs = new List<Renderer>();
            const float spacing = 0.32f, margin = 0.16f;
            float x0 = wall.min.x - margin, x1 = wall.max.x + margin, y0 = wall.min.y - margin, y1 = wall.max.y + margin;

            void Line(Vector2 from, Vector2 to)
            {
                var count = Mathf.Max(1, Mathf.RoundToInt(Vector2.Distance(from, to) / spacing));
                for (var i = 0; i < count; i++)
                {
                    var p = Vector2.Lerp(from, to, (float)i / count);
                    var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Object.DestroyImmediate(bulb.GetComponent<Collider>());
                    bulb.name = "Lampada";
                    bulb.transform.SetParent(root.transform, false);
                    bulb.transform.position = new Vector3(p.x, p.y, front - 0.04f);
                    bulb.transform.localScale = Vector3.one * 0.1f;
                    var renderer = bulb.GetComponent<Renderer>();
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    bulbs.Add(renderer);
                }
            }
            // sentido horario a partir do canto de baixo a esquerda
            Line(new Vector2(x0, y0), new Vector2(x0, y1));
            Line(new Vector2(x0, y1), new Vector2(x1, y1));
            Line(new Vector2(x1, y1), new Vector2(x1, y0));
            Line(new Vector2(x1, y0), new Vector2(x0, y0));

            var marquee = root.AddComponent<Marquee>();
            marquee.bulbs = bulbs.ToArray();
        }

        static void BuildVolume()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumePath);
                var bloom = profile.Add<Bloom>(true);
                bloom.threshold.Override(0.95f);
                bloom.intensity.Override(0.8f);
                bloom.scatter.Override(0.6f);
                var vignette = profile.Add<Vignette>(true);
                vignette.intensity.Override(0.28f);
                foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
                AssetDatabase.SaveAssets();
            }
            if (!profile.Has<Tonemapping>())
            {
                var tone = profile.Add<Tonemapping>(true);
                tone.mode.Override(TonemappingMode.ACES);
                AssetDatabase.AddObjectToAsset(tone, profile);
            }
            if (!profile.Has<ColorAdjustments>())
            {
                var color = profile.Add<ColorAdjustments>(true);
                color.postExposure.Override(0.35f);
                color.contrast.Override(14f);
                color.saturation.Override(12f);
                AssetDatabase.AddObjectToAsset(color, profile);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            var node = new GameObject("Pos-processamento");
            var volume = node.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        // ================================================================== apresentador

        /// <summary>
        /// Boneco de blocos no mesmo espirito dos personagens em pixel do jogo:
        /// paleto vinho, gravata borboleta dourada e microfone na mao.
        /// A frente fica para -Z (para a camera).
        /// </summary>
        static Presenter BuildPresenter(Vector3 feet)
        {
            var jacket = Material("Paleto", new Color(0.58f, 0.09f, 0.2f));
            var lapel = Material("Lapela", new Color(0.36f, 0.05f, 0.12f));
            var pants = Material("Calca", new Color(0.08f, 0.09f, 0.18f));
            var skin = Material("Pele", new Color(0.76f, 0.52f, 0.34f));
            var skinShade = Material("Pele sombra", new Color(0.62f, 0.4f, 0.25f));
            var hair = Material("Cabelo", new Color(0.17f, 0.1f, 0.07f));
            var white = Material("Camisa", new Color(0.95f, 0.95f, 0.92f));
            var gold = Material("Dourado", new Color(1f, 0.78f, 0.24f), 0.6f);
            var dark = Material("Detalhe", new Color(0.05f, 0.05f, 0.08f));
            var metal = Material("Microfone", new Color(0.7f, 0.72f, 0.76f));
            jacket.SetFloat("_Smoothness", 0.45f);
            gold.SetFloat("_Metallic", 0.8f);
            metal.SetFloat("_Metallic", 0.9f);

            var root = new GameObject("Apresentador");
            root.transform.position = feet;
            var presenter = root.AddComponent<Presenter>();

            var body = new GameObject("Corpo").transform;
            body.SetParent(root.transform, false);
            presenter.body = body;

            // pernas, sapatos e cinto
            foreach (var side in new[] { -1f, 1f })
            {
                Block(body, "Perna", new Vector3(0.17f, 0.86f, 0.2f), new Vector3(side * 0.11f, 0.49f, 0f), pants);
                Block(body, "Sapato", new Vector3(0.19f, 0.08f, 0.3f), new Vector3(side * 0.11f, 0.04f, -0.04f), dark);
            }
            Block(body, "Cinto", new Vector3(0.5f, 0.05f, 0.27f), new Vector3(0f, 0.92f, 0f), dark);
            Block(body, "Fivela", new Vector3(0.07f, 0.045f, 0.02f), new Vector3(0f, 0.92f, -0.14f), gold);

            // paleto: ombreiras, camisa em "V" afinando ate os botoes, lapelas curtas
            // que se encontram, colarinho de pontas, gravata borboleta com nó e bolso com lenço
            var bowTie = Material("Gravata borboleta", new Color(0.04f, 0.04f, 0.07f));
            bowTie.SetFloat("_Smoothness", 0.6f);
            Block(body, "Paleto", new Vector3(0.56f, 0.66f, 0.3f), new Vector3(0f, 1.27f, 0f), jacket);
            Block(body, "Ombreiras", new Vector3(0.62f, 0.08f, 0.31f), new Vector3(0f, 1.56f, 0f), jacket);
            Block(body, "Camisa alta", new Vector3(0.2f, 0.16f, 0.02f), new Vector3(0f, 1.51f, -0.155f), white);
            Block(body, "Camisa meio", new Vector3(0.13f, 0.14f, 0.02f), new Vector3(0f, 1.37f, -0.155f), white);
            Block(body, "Camisa baixa", new Vector3(0.06f, 0.1f, 0.02f), new Vector3(0f, 1.26f, -0.155f), white);
            foreach (var side in new[] { -1f, 1f })
            {
                // em "V": o alto da lapela vai para fora, a ponta de baixo encontra a outra no meio
                var flap = Block(body, "Lapela", new Vector3(0.075f, 0.3f, 0.02f), new Vector3(side * 0.09f, 1.42f, -0.165f), lapel);
                flap.transform.localRotation = Quaternion.Euler(0f, 0f, -side * 20f);
                var collar = Block(body, "Colarinho", new Vector3(0.07f, 0.045f, 0.02f), new Vector3(side * 0.05f, 1.585f, -0.166f), white);
                collar.transform.localRotation = Quaternion.Euler(0f, 0f, side * -32f);
                var wing = Block(body, "Asa da gravata", new Vector3(0.075f, 0.055f, 0.025f), new Vector3(side * 0.045f, 1.545f, -0.176f), bowTie);
                wing.transform.localRotation = Quaternion.Euler(0f, 0f, -side * 12f);   // pontas para cima
            }
            Block(body, "Nó da gravata", new Vector3(0.032f, 0.036f, 0.03f), new Vector3(0f, 1.545f, -0.182f), bowTie);
            Block(body, "Botao", new Vector3(0.032f, 0.032f, 0.012f), new Vector3(0f, 1.15f, -0.156f), gold);
            Block(body, "Botao", new Vector3(0.032f, 0.032f, 0.012f), new Vector3(0f, 1.05f, -0.156f), gold);
            Block(body, "Bolso", new Vector3(0.1f, 0.012f, 0.012f), new Vector3(0.19f, 1.36f, -0.156f), lapel);
            Block(body, "Lenco", new Vector3(0.07f, 0.026f, 0.01f), new Vector3(0.19f, 1.375f, -0.158f), white);
            var tip = Block(body, "Ponta do lenco", new Vector3(0.03f, 0.03f, 0.01f), new Vector3(0.19f, 1.39f, -0.159f), white);
            tip.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var neckSkin = Material("Pele pescoco", new Color(0.7f, 0.47f, 0.3f), 0.35f);
            var neck = Block(body, "Pescoco", new Vector3(0.14f, 0.1f, 0.14f), new Vector3(0f, 1.64f, 0f), neckSkin);
            neck.GetComponent<Renderer>().receiveShadows = false;   // sem a sombra da cabeca, que deixava o pescoco preto

            // cabeca: orelhas, nariz, sobrancelhas, olhos com pupila (piscam) e boca (fala)
            var head = Pivot(body, "Cabeca", new Vector3(0f, 1.68f, 0f));
            Block(head, "Rosto", new Vector3(0.36f, 0.38f, 0.34f), new Vector3(0f, 0.19f, 0f), skin);
            foreach (var side in new[] { -1f, 1f })
            {
                Block(head, "Orelha", new Vector3(0.05f, 0.1f, 0.08f), new Vector3(side * 0.195f, 0.19f, 0.01f), skinShade);
                Block(head, "Sobrancelha", new Vector3(0.09f, 0.025f, 0.02f), new Vector3(side * 0.085f, 0.3f, -0.176f), hair);
                Block(head, "Costeleta", new Vector3(0.03f, 0.1f, 0.12f), new Vector3(side * 0.18f, 0.29f, -0.06f), hair);
            }
            Block(head, "Nariz", new Vector3(0.06f, 0.08f, 0.05f), new Vector3(0f, 0.17f, -0.19f), skinShade);
            Block(head, "Cabelo", new Vector3(0.38f, 0.1f, 0.36f), new Vector3(0f, 0.4f, 0f), hair);
            Block(head, "Topete", new Vector3(0.3f, 0.06f, 0.08f), new Vector3(0.03f, 0.37f, -0.16f), hair);
            Block(head, "Nuca", new Vector3(0.38f, 0.26f, 0.06f), new Vector3(0f, 0.27f, 0.17f), hair);
            var eyes = Pivot(head, "Olhos", new Vector3(0f, 0.235f, -0.172f));
            foreach (var side in new[] { -1f, 1f })
            {
                Block(eyes, "Olho", new Vector3(0.075f, 0.055f, 0.012f), new Vector3(side * 0.085f, 0f, 0f), white);
                Block(eyes, "Pupila", new Vector3(0.035f, 0.045f, 0.012f), new Vector3(side * 0.085f + 0.01f, 0f, -0.006f), dark);
            }
            presenter.eyes = eyes;
            presenter.mouth = Block(head, "Boca", new Vector3(0.12f, 0.025f, 0.02f), new Vector3(0f, 0.09f, -0.175f), dark).transform;
            presenter.head = head;

            // bracos girando no ombro: o do telao (+X) e o do microfone (-X)
            presenter.armScreen = Arm(body, 1f, jacket, white, skin);
            presenter.armScreen.localRotation = Quaternion.Euler(0f, 0f, 8f);
            presenter.armMic = Arm(body, -1f, jacket, white, skin);
            presenter.armMic.localRotation = Quaternion.Euler(55f, 0f, -6f);
            var mic = Block(presenter.armMic, "Microfone", new Vector3(0.05f, 0.2f, 0.05f), new Vector3(0f, -0.72f, 0f), dark);
            Block(mic.transform, "Globo", new Vector3(1.8f, 0.45f, 1.8f), new Vector3(0f, -0.55f, 0f), metal);
            Block(mic.transform, "Anel", new Vector3(1.3f, 0.12f, 1.3f), new Vector3(0f, -0.3f, 0f), gold);

            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                renderer.shadowCastingMode = ShadowCastingMode.On;
            return presenter;
        }

        static Transform Arm(Transform body, float side, Material sleeve, Material cuff, Material skin)
        {
            var shoulder = Pivot(body, side > 0 ? "Braco do telao" : "Braco do microfone", new Vector3(side * 0.36f, 1.56f, 0f));
            Block(shoulder, "Manga", new Vector3(0.15f, 0.56f, 0.17f), new Vector3(0f, -0.28f, 0f), sleeve);
            Block(shoulder, "Punho", new Vector3(0.16f, 0.05f, 0.18f), new Vector3(0f, -0.57f, 0f), cuff);
            Block(shoulder, "Mao", new Vector3(0.13f, 0.12f, 0.13f), new Vector3(0f, -0.65f, 0f), skin);
            return shoulder;
        }

        // ================================================================== balcao do apresentador

        /// <summary>
        /// Balcao de apresentador no lugar do pulpito gerado: rodape, corpo escuro
        /// brilhante, frente de LED azul, filetes dourados, tampo e a placa do
        /// programa presa na frente. O texto fica sobre a placa, sem flutuar.
        /// </summary>
        static void HostDesk(GameObject studio, Vector3 floor)
        {
            var old = studio.transform.Find("Presentation lectern");
            if (old != null) old.gameObject.SetActive(false);

            var body = Material("Balcao", new Color(0.04f, 0.05f, 0.1f));
            body.SetFloat("_Smoothness", 0.8f);
            body.SetFloat("_Metallic", 0.3f);
            var panel = Material("Balcao LED", new Color(0.03f, 0.12f, 0.36f), 0.45f);
            panel.SetColor("_BaseColor", new Color(0.03f, 0.12f, 0.36f));
            panel.SetColor("_EmissionColor", new Color(0.03f, 0.12f, 0.36f) * 0.45f);
            panel.SetFloat("_Smoothness", 0.85f);
            var trim = Material("Filete dourado", new Color(1f, 0.78f, 0.3f), 0.8f);
            trim.SetFloat("_Metallic", 0.8f);
            var plate = Material("Placa do balcao", new Color(0.01f, 0.01f, 0.03f));
            plate.SetFloat("_Smoothness", 0.9f);

            var root = new GameObject("Balcao do apresentador").transform;
            root.position = floor;
            const float width = 1.36f, depth = 0.72f, height = 1.12f;
            float front = -depth / 2f;

            Block(root, "Rodape", new Vector3(width + 0.08f, 0.08f, depth + 0.08f), new Vector3(0f, 0.04f, 0f), body);
            Block(root, "Filete do rodape", new Vector3(width + 0.09f, 0.015f, depth + 0.09f), new Vector3(0f, 0.085f, 0f), trim);
            Block(root, "Corpo", new Vector3(width, height - 0.14f, depth), new Vector3(0f, 0.09f + (height - 0.14f) / 2f, 0f), body);
            Block(root, "Frente de LED", new Vector3(width - 0.16f, height - 0.36f, 0.02f), new Vector3(0f, 0.12f + (height - 0.36f) / 2f + 0.08f, front - 0.01f), panel);
            foreach (var side in new[] { -1f, 1f })
                Block(root, "Coluna dourada", new Vector3(0.03f, height - 0.14f, 0.03f), new Vector3(side * (width / 2f - 0.02f), 0.09f + (height - 0.14f) / 2f, front - 0.012f), trim);
            Block(root, "Tampo", new Vector3(width + 0.12f, 0.06f, depth + 0.14f), new Vector3(0f, height - 0.02f, -0.02f), body);
            Block(root, "Filete do tampo", new Vector3(width + 0.13f, 0.012f, depth + 0.15f), new Vector3(0f, height - 0.055f, -0.02f), trim);

            // placa encostada na frente de LED, e o texto encostado na placa
            var plateY = 0.12f + (height - 0.36f) / 2f + 0.16f;
            var plateZ = front - 0.03f;
            Block(root, "Placa", new Vector3(width - 0.34f, 0.24f, 0.02f), new Vector3(0f, plateY, plateZ), plate);
            var node = new GameObject("Nome do programa");
            node.transform.SetParent(root, false);
            node.transform.localPosition = new Vector3(0f, plateY, plateZ - 0.0115f);
            var text = node.AddComponent<TextMeshPro>();
            text.text = "SHOW DO PROCON";
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(1f, 0.82f, 0.3f);
            text.rectTransform.sizeDelta = new Vector2(width - 0.42f, 0.18f);
            text.enableWordWrapping = false;
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.3f;
            text.fontSizeMax = 1.4f;
        }

        // ================================================================== estudio

        /// <summary>
        /// Parede de fundo com colunas de luz, faixas de LED correndo nos paineis
        /// laterais, faixa de luz na beira do palco, circulo de luz no chao do
        /// apresentador e dois refletores com feixe nas laterais.
        /// </summary>
        static void DecorateStudio(GameObject studio, Bounds wall, float front, bool menu)
        {
            var root = new GameObject("Cenario do estudio").transform;
            var backWall = Material("Parede do estudio", new Color(0.02f, 0.03f, 0.07f));
            var cyan = Material("Coluna de luz ciano", new Color(0.2f, 0.75f, 1f), 1.5f);
            var blue = Material("Coluna de luz azul", new Color(0.15f, 0.35f, 1f), 1.5f);
            cyan.SetColor("_EmissionColor", new Color(0.2f, 0.75f, 1f) * 1.5f);
            blue.SetColor("_EmissionColor", new Color(0.15f, 0.35f, 1f) * 1.5f);
            var stripLight = Material("Faixa de LED", new Color(0.3f, 0.8f, 1f), 3f);

            // parede de fundo e colunas de luz (aparecem atras do apresentador e nas laterais)
            var backZ = wall.max.z + 1.4f;
            Block(root, "Parede de fundo", new Vector3(26f, 10f, 0.2f), new Vector3(0f, 4.5f, backZ), backWall);
            var xs = new[] { -10.6f, -9.2f, -7.8f, -6.4f, 6.4f, 7.8f, 9.2f, 10.6f };
            for (var i = 0; i < xs.Length; i++)
            {
                Block(root, "Coluna de luz", new Vector3(0.14f, 6.4f, 0.06f), new Vector3(xs[i], 3.6f, backZ - 0.13f), i % 2 == 0 ? cyan : blue);
                Block(root, "Moldura da coluna", new Vector3(0.3f, 6.6f, 0.04f), new Vector3(xs[i], 3.6f, backZ - 0.1f), backWall);
            }
            Block(root, "Faixa alta", new Vector3(26f, 0.08f, 0.06f), new Vector3(0f, 7.2f, backZ - 0.13f), blue);
            Block(root, "Faixa baixa", new Vector3(26f, 0.08f, 0.06f), new Vector3(0f, 0.5f, backZ - 0.13f), blue);

            // faixas de luz correndo nos dois paineis laterais
            foreach (var name in new[] { "Left LED wing", "Right LED wing" })
            {
                var wing = studio.transform.Find(name);
                if (wing == null) continue;
                var bounds = Bounds(wing);
                var holder = new GameObject("Faixas " + name).transform;
                holder.SetParent(wing, true);
                var strips = new List<Renderer>();
                for (var k = 0; k < 9; k++)
                {
                    var y = Mathf.Lerp(bounds.min.y + 0.3f, bounds.max.y - 0.3f, k / 8f);
                    var strip = Block(holder, "Faixa", new Vector3(bounds.size.x * 0.8f, 0.05f, 0.02f), Vector3.zero, stripLight);
                    strip.transform.position = new Vector3(bounds.center.x, y, bounds.min.z - 0.02f);
                    strip.transform.rotation = wing.rotation;
                    strips.Add(strip.GetComponent<Renderer>());
                }
                var chase = holder.gameObject.AddComponent<Marquee>();
                chase.bulbs = strips.ToArray();
                chase.on = new Color(0.3f, 0.8f, 1f);
                chase.off = new Color(0.03f, 0.08f, 0.15f);
                chase.glow = 3f;
                chase.stepSeconds = 0.22f;
            }

            // faixa de luz na beira do palco, embaixo do telao
            Block(root, "Beira do palco", new Vector3(wall.size.x + 1.2f, 0.03f, 0.05f), new Vector3(wall.center.x, 0.235f, front - 0.6f), stripLight);

            if (!menu)
            {
                // refletores nas laterais, longe do telao, varrendo devagar
                MovingSpot(10, new Vector3(-8.2f, 7.4f, -0.4f), new Color(0.3f, 0.6f, 1f));
                MovingSpot(11, new Vector3(6.9f, 7.4f, -0.4f), new Color(1f, 0.82f, 0.4f));
            }
        }

        /// <summary>
        /// Iluminacao de estudio: luz principal quente e suave vinda da frente (da
        /// volume ao palco e ao apresentador), contraluz azul atras do apresentador
        /// para destacar o contorno, e pocas de luz coloridas no chao brilhante.
        /// </summary>
        static void StageLighting(Vector3 podium, Bounds wall, bool menu)
        {
            var key = new GameObject("Luz principal").AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.93f, 0.84f);
            key.intensity = 0.55f;
            key.shadows = LightShadows.Soft;
            key.shadowStrength = 0.6f;
            key.transform.rotation = Quaternion.Euler(38f, 18f, 0f);

            var rim = Spot("Contraluz do apresentador", podium + new Vector3(-0.9f, 3.6f, 2.6f), podium + Vector3.up * 1.6f,
                           new Color(0.35f, 0.7f, 1f), 14f, 32f);
            rim.range = 9f;

            // pocas de luz no chao: nas laterais do telao e na frente do palco
            var pools = menu
                ? new[] { new Vector3(-4.6f, 0.22f, -3f), new Vector3(4.6f, 0.22f, -3f), new Vector3(0f, 0.22f, -4.2f) }
                : new[] { new Vector3(wall.min.x + 1.2f, 0.22f, 0.6f), new Vector3(wall.center.x + 1.5f, 0.22f, 0.9f), new Vector3(wall.max.x - 0.8f, 0.22f, 0.6f) };
            var colors = new[] { new Color(0.3f, 0.6f, 1f), new Color(1f, 0.85f, 0.55f), new Color(0.3f, 0.85f, 1f) };
            for (var i = 0; i < pools.Length; i++)
            {
                var pool = Spot("Poca de luz " + (i + 1), pools[i] + new Vector3(0f, 6.5f, -0.4f), pools[i], colors[i], 22f, 30f);
                pool.range = 10f;
            }
        }

        /// <summary>
        /// Sombras das luzes de palco no renderer 3D. (Oclusao de ambiente foi
        /// testada e tirada: ao ligar e desligar, a imagem estourava em ciano.)
        /// </summary>
        static void EnsureRenderQuality()
        {
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/UniversalRP.asset");
            if (urp != null)
            {
                var so = new SerializedObject(urp);
                var shadows = so.FindProperty("m_AdditionalLightShadowsSupported");
                if (shadows != null && !shadows.boolValue) { shadows.boolValue = true; so.ApplyModifiedProperties(); }
            }
        }

        static Transform Pivot(Transform parent, string name, Vector3 position)
        {
            var node = new GameObject(name).transform;
            node.SetParent(parent, false);
            node.localPosition = position;
            return node;
        }

        static GameObject Block(Transform parent, string name, Vector3 size, Vector3 position, Material material)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(block.GetComponent<Collider>());
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = size;
            block.GetComponent<Renderer>().sharedMaterial = material;
            return block;
        }

        // ================================================================== telao e faixa

        static Transform BuildScreenCanvas(Camera camera, Vector3 center, float width)
        {
            const float pixelsWide = 1280f, pixelsHigh = 720f;
            var node = new GameObject("Telao (interface)", typeof(RectTransform));
            var canvas = node.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            node.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 2f;
            node.AddComponent<GraphicRaycaster>();

            var rect = (RectTransform)node.transform;
            rect.sizeDelta = new Vector2(pixelsWide, pixelsHigh);
            rect.position = center;
            rect.rotation = Quaternion.identity;
            rect.localScale = Vector3.one * (width / pixelsWide);
            return node.transform;
        }

        /// <summary>Canvas da tela, por cima do estudio: botoes, placar e resultado.</summary>
        static Transform BuildOverlay()
        {
            var node = new GameObject("Tela (controles)", typeof(RectTransform));
            var canvas = node.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = node.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            node.AddComponent<GraphicRaycaster>();
            return node.transform;
        }

        // ================================================================== materiais e arte do menu

        internal static Material Material(string name, Color color, float emission = 0f)
        {
            var path = ArtFolder + "/Materiais/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            if (!AssetDatabase.IsValidFolder(ArtFolder + "/Materiais")) AssetDatabase.CreateFolder(ArtFolder, "Materiais");
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.35f);
            if (emission > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// Brilho redondo, lampada e feixe de luz do menu, desenhados por codigo e
        /// salvos como PNG (sem texto, ficam nitidos em qualquer tamanho).
        /// </summary>
        public static void EnsureMenuArt()
        {
            Directory.CreateDirectory(ShowArtFolder);
            WriteIfMissing("brilho", 256, 256, (x, y) =>
            {
                var d = Vector2.Distance(new Vector2(x, y), new Vector2(127.5f, 127.5f)) / 128f;
                var a = Mathf.Clamp01(1f - d);
                return new Color(1f, 1f, 1f, a * a);
            });
            WriteIfMissing("lampada", 64, 64, (x, y) =>
            {
                var d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f;
                if (d > 1f) return Color.clear;
                var shine = Vector2.Distance(new Vector2(x, y), new Vector2(24f, 40f)) < 7f ? 1f : 0.82f + 0.18f * (1f - d);
                return new Color(shine, shine, shine, Mathf.Clamp01((1f - d) * 12f));
            });
            WriteIfMissing("feixe", 128, 512, (x, y) =>
            {
                // estreito em cima (y alto), largo embaixo, sumindo nas bordas e no pe
                var t = y / 511f;
                var half = Mathf.Lerp(64f, 6f, t);
                var edge = Mathf.Clamp01(1f - Mathf.Abs(x - 63.5f) / half);
                return new Color(1f, 1f, 1f, edge * edge * Mathf.Lerp(0.15f, 1f, t));
            });
            AssetDatabase.Refresh();
        }

        public static Sprite MenuSprite(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(ShowArtFolder + "/" + name + ".png");

        static void WriteIfMissing(string name, int width, int height, System.Func<int, int, Color> pixel)
        {
            var path = ShowArtFolder + "/" + name + ".png";
            if (File.Exists(path)) return;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                    texture.SetPixel(x, y, pixel(x, y));
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }
    }
}
