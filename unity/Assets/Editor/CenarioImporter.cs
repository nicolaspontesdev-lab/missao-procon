using UnityEditor;

namespace Procon.EditorTools
{
    /// <summary>
    /// Importa a arte extraida do jogo em HTML: sprite de UI, sem compressao.
    /// A arte sai em 2x e aparece menor na tela, entao a Unity precisa REDUZIR.
    /// Com filtro de ponto, reduzir descarta pixel alternado e come o traco das
    /// pecas finas; por isso filtro bilinear com mipmap, que tira a media.
    /// Vale para toda a arte do jogo em Assets/Art (cenario e menu).
    /// </summary>
    public class CenarioImporter : AssetPostprocessor
    {
        const string Pasta = "Assets/Art/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Pasta)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = UnityEngine.FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 4096;
        }
    }
}
