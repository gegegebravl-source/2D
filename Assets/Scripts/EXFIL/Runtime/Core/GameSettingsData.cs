using System;
using UnityEngine;

namespace EXFIL.Core
{
    /// <summary>Flat, JSON serializable mirror of GameSettings (ScriptableObjects cannot be stored in saves).</summary>
    [Serializable]
    public class GameSettingsData
    {
        public float MouseSensitivity = 1.1f;
        public float FieldOfView = 75f;
        public float MasterVolume = 0.9f;
        public float SfxVolume = 1f;
        public float MusicVolume = 0.5f;
        public bool ToggleAim;
        public int TargetFrameRate = 144;
    }
}
