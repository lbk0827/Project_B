using UnityEngine;

namespace Game.Settings
{
    public enum BuildProfileType
    {
        Dev,
        Live
    }

    [CreateAssetMenu(fileName = "BuildProfile", menuName = "ArrowPopBall/Build/Build Profile")]
    public class BuildProfileSO : ScriptableObject
    {
        [Header("Profile")]
        [SerializeField] private BuildProfileType _profileType = BuildProfileType.Dev;

        [Header("Player Settings")]
        [SerializeField] private string _productName;
        [SerializeField] private string _androidApplicationIdentifier;
        [SerializeField] private string _iosApplicationIdentifier;
        [SerializeField] private string _appleDeveloperTeamID;

        [Header("Notes")]
        [SerializeField] private string _routingUrl;
        [SerializeField] private string _memo;

        public BuildProfileType ProfileType => _profileType;
        public string ProductName => _productName;
        public string AndroidApplicationIdentifier => _androidApplicationIdentifier;
        public string IosApplicationIdentifier => _iosApplicationIdentifier;
        public string AppleDeveloperTeamID => _appleDeveloperTeamID;
        public string RoutingUrl => _routingUrl;
        public string Memo => _memo;
    }
}
