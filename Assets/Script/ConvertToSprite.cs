using UnityEditor;

#if UNITY_EDITOR
public class ConvertToSprite : AssetPostprocessor {
    void  OnPreprocessTexture(){
        TextureImporter textureImporter = assetImporter as TextureImporter;

        if(textureImporter != null){
            string assetPath = textureImporter.assetPath;
            int isFolderUI = assetPath.IndexOf("Assets/UI");
            bool check = isFolderUI >= 0 ? true : false;
            
            if(check){
                textureImporter.textureType = TextureImporterType.Sprite;
            }
        }
    }
}
#endif