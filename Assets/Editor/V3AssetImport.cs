using UnityEditor;

// Import settings for the V3 images in Resources/V3 (mosaic mark, icons): no compression (thin lines smear
// under ASTC/ETC), no power-of-two rescale, clamped edges, and mipmaps - they are drawn at about half their
// size and thin lines break up without them.
public class V3AssetImport : AssetPostprocessor
{
    public override uint GetVersion() => 3;

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/V3/"))
            return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        importer.filterMode = UnityEngine.FilterMode.Trilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        var android = importer.GetPlatformTextureSettings("Android");
        android.overridden = true;
        android.format = TextureImporterFormat.RGBA32;
        importer.SetPlatformTextureSettings(android);
    }
}
