using System.IO;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Game.Editor.Localization
{
    /// <summary>
    /// One-shot editor setup for the localization system. Creates the active
    /// <see cref="LocalizationSettings"/>, the initial locales (ko, en), a String Table
    /// collection and a Font (Asset) Table collection, so the project is ready to author
    /// entries in <c>Window &gt; Asset Management &gt; Localization Tables</c>.
    ///
    /// Idempotent: re-running only creates what is missing.
    /// </summary>
    public static class LocalizationSetup
    {
        const string RootDir = "Assets/ArrowPopBall/Localization";
        const string LocalesDir = RootDir + "/Locales";
        const string StringTableName = "StringTable";
        const string FontTableName = "FontTable";
        static readonly string[] LocaleCodes = { "ko", "en" };

        [MenuItem("Tools/Arrow Pop/Localization/Setup Tables")]
        public static void Setup()
        {
            EnsureFolder(RootDir);
            EnsureFolder(LocalesDir);

            EnsureSettings();
            foreach (var code in LocaleCodes)
                EnsureLocale(code);

            EnsureStringTable();
            EnsureFontTable();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[LocalizationSetup] Ready: locales (ko, en) + '" + StringTableName +
                      "' + '" + FontTableName + "'. Edit entries in " +
                      "Window > Asset Management > Localization Tables.");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace("\\", "/");
            var leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static void EnsureSettings()
        {
            if (LocalizationEditorSettings.ActiveLocalizationSettings != null) return;

            var settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            settings.name = "Localization Settings";
            AssetDatabase.CreateAsset(settings, RootDir + "/Localization Settings.asset");
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
        }

        static void EnsureLocale(string code)
        {
            if (LocalizationEditorSettings.GetLocale(code) != null) return;

            var locale = Locale.CreateLocale(new LocaleIdentifier(code));
            locale.name = string.IsNullOrEmpty(locale.LocaleName) ? code : locale.LocaleName + " (" + code + ")";
            AssetDatabase.CreateAsset(locale, LocalesDir + "/" + code + ".asset");
            LocalizationEditorSettings.AddLocale(locale);
        }

        static void EnsureStringTable()
        {
            if (LocalizationEditorSettings.GetStringTableCollection(StringTableName) != null) return;
            LocalizationEditorSettings.CreateStringTableCollection(StringTableName, RootDir);
        }

        static void EnsureFontTable()
        {
            if (LocalizationEditorSettings.GetAssetTableCollection(FontTableName) != null) return;
            LocalizationEditorSettings.CreateAssetTableCollection(FontTableName, RootDir);
        }
    }
}
