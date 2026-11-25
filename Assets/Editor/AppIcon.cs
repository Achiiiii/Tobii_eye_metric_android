using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

// Sets the app icon from Assets/Icons: the whole icon for the default, legacy and round slots, and the plate plus the
// raised E as the Android adaptive icon's background and foreground.
// Run: Unity.exe -batchmode -quit -projectPath . -executeMethod AppIcon.Apply
public static class AppIcon
{
    private const string Full = "Assets/Icons/AppIcon.png";
    private const string Background = "Assets/Icons/AppIconBackground.png";
    private const string Foreground = "Assets/Icons/AppIconForeground.png";

    public static void Apply()
    {
        AssetDatabase.Refresh();
        var full = AssetDatabase.LoadAssetAtPath<Texture2D>(Full);
        var background = AssetDatabase.LoadAssetAtPath<Texture2D>(Background);
        var foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(Foreground);
        if (full == null || background == null || foreground == null)
        {
            Debug.LogError("[AppIcon] icon textures not found in Assets/Icons");
            return;
        }

        PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { full });

        var android = NamedBuildTarget.Android;
        Fill(android, AndroidPlatformIconKind.Legacy, full);
        Fill(android, AndroidPlatformIconKind.Round, full);
        var adaptive = PlayerSettings.GetPlatformIcons(android, AndroidPlatformIconKind.Adaptive);
        foreach (var icon in adaptive)
            icon.SetTextures(background, foreground);
        PlayerSettings.SetPlatformIcons(android, AndroidPlatformIconKind.Adaptive, adaptive);

        AssetDatabase.SaveAssets();
        Debug.Log("[AppIcon] icons set: default, legacy " + PlayerSettings.GetPlatformIcons(android, AndroidPlatformIconKind.Legacy).Length
                  + ", round, adaptive " + adaptive.Length);
    }

    private static void Fill(NamedBuildTarget target, PlatformIconKind kind, Texture2D texture)
    {
        var icons = PlayerSettings.GetPlatformIcons(target, kind);
        foreach (var icon in icons)
            icon.SetTexture(texture);
        PlayerSettings.SetPlatformIcons(target, kind, icons);
    }
}
