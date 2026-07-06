using System;
using TMPro;
using UnityEngine.Localization;

namespace Game.Localization
{
    /// <summary>
    /// Thin, framework-free helpers for looking up and binding localized strings from code.
    /// Wraps Unity Localization's <see cref="LocalizedString"/> so callers do not need to know
    /// the default table name.
    /// </summary>
    public static class LocalizeStringHelper
    {
        /// <summary>Default String Table collection name (created by LocalizationSetup).</summary>
        public const string DefaultTable = "StringTable";

        /// <summary>
        /// Returns the localized string for <paramref name="key"/> in the current locale.
        /// This forces a synchronous load if the table is not already loaded.
        /// </summary>
        public static string Get(string key, string table = DefaultTable)
        {
            return GetLocalizedString(key, table).GetLocalizedString();
        }

        /// <summary>Creates a <see cref="LocalizedString"/> for the given key and table.</summary>
        public static LocalizedString GetLocalizedString(string key, string table = DefaultTable)
        {
            return new LocalizedString(table, key);
        }

        /// <summary>
        /// Binds a localized string to a <see cref="TextMeshProUGUI"/>: the text updates whenever the
        /// selected locale (or the entry) changes. Dispose the returned handle to stop updating.
        /// </summary>
        public static IDisposable BindToTMP(TextMeshProUGUI text, string key, string table = DefaultTable)
        {
            var localizedString = GetLocalizedString(key, table);
            LocalizedString.ChangeHandler handler = value =>
            {
                if (text != null)
                    text.text = value;
            };
            localizedString.StringChanged += handler;
            return new Binding(localizedString, handler);
        }

        sealed class Binding : IDisposable
        {
            LocalizedString _localizedString;
            LocalizedString.ChangeHandler _handler;

            public Binding(LocalizedString localizedString, LocalizedString.ChangeHandler handler)
            {
                _localizedString = localizedString;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_localizedString == null) return;
                _localizedString.StringChanged -= _handler;
                _localizedString = null;
                _handler = null;
            }
        }
    }
}
