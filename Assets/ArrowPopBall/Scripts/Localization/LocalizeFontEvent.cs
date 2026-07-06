using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;

namespace Game.Localization
{
    /// <summary>Serializable LocalizedAsset reference to a TMP font asset.</summary>
    [Serializable]
    public class LocalizedTmpFont : LocalizedAsset<TMP_FontAsset> { }

    /// <summary>Serializable LocalizedAsset reference to a font material preset.</summary>
    [Serializable]
    public class LocalizedFontMaterial : LocalizedAsset<Material> { }

    /// <summary>UnityEvent that carries a localized TMP font asset.</summary>
    [Serializable]
    public class UnityEventTmpFont : UnityEvent<TMP_FontAsset> { }

    /// <summary>
    /// Locale-driven TextMeshPro font switcher, built on Unity Localization's public
    /// <see cref="LocalizedAssetEvent{TObject,TReference,TEvent}"/>.
    ///
    /// The font reference (<see cref="LocalizedAssetBehaviour{TObject,TReference}.AssetReference"/>)
    /// drives the inherited <c>OnUpdateAsset</c> event, which the Localization Tool wires to a
    /// TextMeshPro <c>font</c> setter. Optionally, <see cref="RefMaterial"/> swaps the font material
    /// preset per locale by writing directly to the TMP component on the same GameObject.
    /// </summary>
    [AddComponentMenu("Localization/Localize Font Event (TMP)")]
    public class LocalizeFontEvent
        : LocalizedAssetEvent<TMP_FontAsset, LocalizedTmpFont, UnityEventTmpFont>
    {
        [SerializeField]
        LocalizedFontMaterial m_RefMaterial = new LocalizedFontMaterial();

        /// <summary>
        /// Optional locale-specific font material. Leave empty to swap the font only.
        /// When set, the resolved material is applied to the TMP component's
        /// <see cref="TMP_Text.fontSharedMaterial"/> on this GameObject.
        /// </summary>
        public LocalizedFontMaterial RefMaterial
        {
            get => m_RefMaterial;
            set => m_RefMaterial = value;
        }

        LocalizedAsset<Material>.ChangeHandler m_MaterialHandler;
        bool m_MaterialRegistered;
        TMP_Text m_Target;

        protected override void OnEnable()
        {
            base.OnEnable();
            RegisterMaterialHandler();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ClearMaterialHandler();
        }

        void RegisterMaterialHandler()
        {
            if (m_MaterialRegistered) return;
            if (m_RefMaterial == null || m_RefMaterial.IsEmpty) return;

            m_MaterialHandler ??= OnMaterialChanged;
            m_RefMaterial.AssetChanged += m_MaterialHandler;
            m_MaterialRegistered = true;
        }

        void ClearMaterialHandler()
        {
            if (!m_MaterialRegistered) return;
            if (m_RefMaterial != null && m_MaterialHandler != null)
                m_RefMaterial.AssetChanged -= m_MaterialHandler;
            m_MaterialRegistered = false;
        }

        void OnMaterialChanged(Material material)
        {
            if (material == null) return;
            if (m_Target == null) m_Target = GetComponent<TMP_Text>();
            if (m_Target != null) m_Target.fontSharedMaterial = material;
        }
    }
}
