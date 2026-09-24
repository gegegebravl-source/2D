using UnityEngine;

namespace EXFIL.Core
{
    /// <summary>Player facing settings (video / audio / controls). Persisted by Meta.SaveSystem.</summary>
    [CreateAssetMenu(menuName = "EXFIL/Settings/Game settings", fileName = "GameSettings")]
    public class GameSettings : ScriptableObject
    {
        [Header("Camera")]
        public float MouseSensitivity = 1.1f;
        public float FieldOfView = 75f;
        public float AimFieldOfView = 52f;
        public bool InvertY;
        public float HeadBobAmount = 0.035f;

        [Header("Audio")]
        [Range(0f, 1f)] public float MasterVolume = 0.9f;
        [Range(0f, 1f)] public float SfxVolume = 1f;
        [Range(0f, 1f)] public float MusicVolume = 0.5f;
        [Range(0f, 1f)] public float VoiceVolume = 1f;

        [Header("Gameplay")]
        public bool ToggleAim = false;
        public bool ToggleSprint = true;
        public bool AutoReloadEmpty = true;
        public bool ShowDamageNumbers = true;

        [Header("Graphics")]
        public int TargetFrameRate = 144;
        public float RenderScale = 1f;
        public bool Shadows = true;

        public static GameSettings Load()
        {
            GameSettings settings = Resources.Load<GameSettings>("Data/GameSettings");
            return settings != null ? settings : CreateInstance<GameSettings>();
        }
    }
}
