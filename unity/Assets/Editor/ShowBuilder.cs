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
            public Transform screen;        // moldura da interface dentro do telao
            public Presenter presenter;
            public TextMeshProUGUI presenterLine;
        }

        // ================================================================== estudio

        public static StudioRig BuildStudioScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var rig = new StudioRig();

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.13f, 0.24f);
            RenderSettings.skybox = null;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StudioBuilder.PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("Missao PROCON: falta " + StudioBuilder.PrefabPath + ". Rode 'Gerar estudio de TV' com o llm-tools instalado.");
                return rig;
            }
            var studio = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

            // o telao principal: a interface vai na frente dele, do mesmo tamanho
            var wall = studio.transform.Find("Main LED backdrop");
            var wallBounds = Bounds(wall);
            var front = wallBounds.min.z - 0.015f;
            var screenCenter = new Vector3(wallBounds.center.x, wallBounds.center.y, front);

            // apresentador atras do pulpito, a esquerda e fora do telao
            var lectern = studio.transform.Find("Presentation lectern");
            var podium = new Vector3(wallBounds.min.x - 0.56f, 0.22f, 1.3f);
            if (lectern != null) lectern.position = new Vector3(podium.x, lectern.position.y, podium.z);
            rig.presenter = BuildPresenter(new Vector3(podium.x, 0.22f, podium.z + 0.48f));
            if (lectern != null) LecternSign(lectern);

            var camera = BuildCamera(screenCenter);
            BuildLights(wallBounds, podium);
            BuildBulbs(wallBounds, front);
            BuildVolume();

            rig.screen = BuildScreenCanvas(camera, screenCenter, wallBounds.size.x * 0.965f);
            rig.presenterLine = BuildOverlay();

            var events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();
            events.AddComponent<InputSystemUIInputModule>();
            return rig;
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
            RenderSettings.ambientLight = new Color(0.12f, 0.09f, 0.2f);
            RenderSettings.skybox = null;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StudioBuilder.PrefabPath);
            if (prefab == null) return;
            var studio = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var wall = Bounds(studio.transform.Find("Main LED backdrop"));
            var front = wall.min.z - 0.015f;

            var lectern = studio.transform.Find("Presentation lectern");
            var podium = new Vector3(wall.min.x - 0.56f, 0.22f, 1.3f);
            if (lectern != null) { lectern.position = new Vector3(podium.x, lectern.position.y, podium.z); LecternSign(lectern); }
            BuildPresenter(new Vector3(podium.x, 0.22f, podium.z + 0.48f));

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
                title.transform.position = new Vector3(0f, wall.center.y + 0.15f, front - 1.1f);
                title.transform.localScale = Vector3.one * 1.05f;
                title.AddComponent<Hover>();
                Spot("Luz do titulo", new Vector3(0f, 6.8f, front - 6f), title.transform.position + Vector3.up * 0.6f,
                     new Color(1f, 0.92f, 0.8f), 28f, 34f);
            }

            // holofotes de show: luz real com cone de luz visivel, varrendo o palco
            var colors = new[] { new Color(1f, 0.3f, 0.75f), new Color(0.3f, 0.85f, 1f), new Color(1f, 0.8f, 0.3f), new Color(0.6f, 0.35f, 1f) };
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
            if (material != null) return material;
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

        static Camera BuildCamera(Vector3 screenCenter)
        {
            var node = new GameObject("Main Camera");
            node.tag = "MainCamera";
            var camera = node.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.02f, 0.07f);
            camera.fieldOfView = 42f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 60f;
            node.AddComponent<AudioListener>();

            var data = node.AddComponent<UniversalAdditionalCameraData>();
            data.SetRenderer(StudioRenderer);
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            // um pouco para a esquerda do centro do telao: o apresentador cabe no quadro
            var target = screenCenter + new Vector3(-1.2f, -0.15f, 0f);
            node.transform.position = target + new Vector3(0f, 0.05f, -8.6f);
            node.transform.LookAt(target);
            node.AddComponent<CameraDrift>().target = target;
            return camera;
        }

        static void BuildLights(Bounds wall, Vector3 podium)
        {
            Spot("Luz roxa", new Vector3(-6.5f, 6.5f, -1.5f), new Vector3(wall.min.x, wall.center.y, wall.center.z),
                 new Color(0.62f, 0.35f, 1f), 6f, 40f);
            Spot("Luz ciano", new Vector3(6.5f, 6.5f, -1.5f), new Vector3(wall.max.x, wall.center.y, wall.center.z),
                 new Color(0.3f, 0.85f, 1f), 6f, 40f);
            Spot("Luz do apresentador", new Vector3(podium.x + 1.2f, 4.8f, podium.z - 3.5f), podium + Vector3.up * 1.5f,
                 new Color(1f, 0.9f, 0.75f), 9f, 26f);
        }

        static void Spot(string name, Vector3 position, Vector3 target, Color color, float intensity, float angle)
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
            var pants = Material("Calca", new Color(0.08f, 0.09f, 0.18f));
            var skin = Material("Pele", new Color(0.76f, 0.52f, 0.34f));
            var hair = Material("Cabelo", new Color(0.17f, 0.1f, 0.07f));
            var white = Material("Camisa", new Color(0.95f, 0.95f, 0.92f));
            var gold = Material("Dourado", new Color(1f, 0.78f, 0.24f), 0.6f);
            var dark = Material("Detalhe", new Color(0.05f, 0.05f, 0.08f));
            var metal = Material("Microfone", new Color(0.7f, 0.72f, 0.76f));

            var root = new GameObject("Apresentador");
            root.transform.position = feet;
            var presenter = root.AddComponent<Presenter>();

            var body = new GameObject("Corpo").transform;
            body.SetParent(root.transform, false);
            presenter.body = body;

            foreach (var side in new[] { -1f, 1f })
                Block(body, "Perna", new Vector3(0.17f, 0.9f, 0.2f), new Vector3(side * 0.11f, 0.45f, 0f), pants);
            Block(body, "Paleto", new Vector3(0.56f, 0.66f, 0.3f), new Vector3(0f, 1.23f, 0f), jacket);
            Block(body, "Camisa", new Vector3(0.14f, 0.5f, 0.02f), new Vector3(0f, 1.3f, -0.155f), white);
            Block(body, "Gravata", new Vector3(0.17f, 0.07f, 0.03f), new Vector3(0f, 1.5f, -0.17f), gold);
            Block(body, "Pescoco", new Vector3(0.14f, 0.08f, 0.14f), new Vector3(0f, 1.6f, 0f), skin);

            var head = Pivot(body, "Cabeca", new Vector3(0f, 1.64f, 0f));
            Block(head, "Rosto", new Vector3(0.36f, 0.38f, 0.34f), new Vector3(0f, 0.19f, 0f), skin);
            Block(head, "Cabelo", new Vector3(0.38f, 0.1f, 0.36f), new Vector3(0f, 0.4f, 0f), hair);
            Block(head, "Nuca", new Vector3(0.38f, 0.26f, 0.06f), new Vector3(0f, 0.27f, 0.17f), hair);
            foreach (var side in new[] { -1f, 1f })
                Block(head, "Olho", new Vector3(0.05f, 0.05f, 0.02f), new Vector3(side * 0.08f, 0.22f, -0.175f), dark);
            Block(head, "Sorriso", new Vector3(0.13f, 0.03f, 0.02f), new Vector3(0f, 0.1f, -0.175f), dark);
            presenter.head = head;

            // braco do telao (+X) e braco do microfone (-X), girando no ombro
            presenter.armScreen = Arm(body, 1f, jacket, skin);
            presenter.armScreen.localRotation = Quaternion.Euler(0f, 0f, 8f);
            presenter.armMic = Arm(body, -1f, jacket, skin);
            presenter.armMic.localRotation = Quaternion.Euler(55f, 0f, -6f);
            var mic = Block(presenter.armMic, "Microfone", new Vector3(0.05f, 0.2f, 0.05f), new Vector3(0f, -0.72f, 0f), dark);
            Block(mic.transform, "Globo", new Vector3(1.8f, 0.45f, 1.8f), new Vector3(0f, -0.55f, 0f), metal);
            return presenter;
        }

        static Transform Arm(Transform body, float side, Material sleeve, Material skin)
        {
            var shoulder = Pivot(body, side > 0 ? "Braco do telao" : "Braco do microfone", new Vector3(side * 0.36f, 1.52f, 0f));
            Block(shoulder, "Manga", new Vector3(0.15f, 0.6f, 0.17f), new Vector3(0f, -0.28f, 0f), sleeve);
            Block(shoulder, "Mao", new Vector3(0.13f, 0.12f, 0.13f), new Vector3(0f, -0.62f, 0f), skin);
            return shoulder;
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

        static void LecternSign(Transform lectern)
        {
            var bounds = Bounds(lectern);
            var node = new GameObject("Placa do pulpito");
            node.transform.position = new Vector3(bounds.center.x, bounds.max.y - 0.2f, bounds.min.z - 0.01f);
            var text = node.AddComponent<TextMeshPro>();
            text.text = "PROCON";
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(1f, 0.82f, 0.3f);
            text.rectTransform.sizeDelta = new Vector2(bounds.size.x * 0.85f, 0.3f);
            text.enableAutoSizing = true;
            text.fontSizeMin = 0.5f;
            text.fontSizeMax = 2.2f;
        }

        // ================================================================== telao e faixa

        static Transform BuildScreenCanvas(Camera camera, Vector3 center, float width)
        {
            const float pixelsWide = 1500f, pixelsHigh = 844f;
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

            var background = UiKit.Panel("Background", node.transform, Palette.Ink);
            UiKit.Full(UiKit.Rect(background.gameObject));
            var cabinet = UiKit.Panel("Cabinet", node.transform, Palette.Ink2);
            UiKit.Stretch(UiKit.Rect(cabinet.gameObject), 12, 12, 12, 12);
            return cabinet.transform;
        }

        /// <summary>Faixa de baixo com a fala do apresentador e o selo AO VIVO.</summary>
        static TextMeshProUGUI BuildOverlay()
        {
            var node = new GameObject("Tela (faixas de TV)", typeof(RectTransform));
            var canvas = node.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = node.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var band = UiKit.Panel("Faixa do apresentador", node.transform, new Color(0.07f, 0.04f, 0.16f, 0.92f));
            band.raycastTarget = false;
            UiKit.BottomBand(UiKit.Rect(band.gameObject), 18, 96, 60, 60);
            var stripe = UiKit.Panel("Filete", band.transform, Palette.Yellow);
            stripe.raycastTarget = false;
            UiKit.TopBand(UiKit.Rect(stripe.gameObject), 0, 5);

            var tag = UiKit.Panel("Selo", band.transform, Palette.Yellow);
            tag.raycastTarget = false;
            var tagRect = UiKit.Rect(tag.gameObject);
            tagRect.anchorMin = new Vector2(0f, 0f);
            tagRect.anchorMax = new Vector2(0f, 1f);
            tagRect.pivot = new Vector2(0f, 0.5f);
            tagRect.offsetMin = new Vector2(0f, 0f);
            tagRect.offsetMax = new Vector2(260f, -5f);
            var tagText = UiKit.Label("Texto", tag.transform, "APRESENTADOR", 26, Palette.Ink, TextAlignmentOptions.Center);
            tagText.fontStyle = FontStyles.Bold;
            UiKit.Full(tagText.rectTransform);

            var line = UiKit.Label("Fala", band.transform, "Bem-vindos ao Show do PROCON!", 30, Palette.White, TextAlignmentOptions.Left);
            UiKit.Stretch(line.rectTransform, 290, 10, 24, 5);
            line.enableAutoSizing = true;
            line.fontSizeMin = 20f;
            line.fontSizeMax = 30f;

            var live = UiKit.Panel("Ao vivo", node.transform, Palette.Red);
            live.raycastTarget = false;
            var liveRect = UiKit.Rect(live.gameObject);
            liveRect.anchorMin = liveRect.anchorMax = new Vector2(0f, 1f);
            liveRect.pivot = new Vector2(0f, 1f);
            liveRect.sizeDelta = new Vector2(150f, 44f);
            liveRect.anchoredPosition = new Vector2(28f, -24f);
            var liveText = UiKit.Label("Texto", live.transform, "● AO VIVO", 24, Palette.White, TextAlignmentOptions.Center);
            liveText.fontStyle = FontStyles.Bold;
            UiKit.Full(liveText.rectTransform);
            return line;
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
