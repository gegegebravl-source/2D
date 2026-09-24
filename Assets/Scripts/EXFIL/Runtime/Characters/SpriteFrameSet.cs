using System;
using UnityEngine;

namespace EXFIL.Characters
{
    /// <summary>
    /// A directional sprite set: frames are stored direction-major.
    /// Layout: [dir0_f0, dir0_f1 ... dir0_fn, dir1_f0, ... dir7_fn]
    /// Direction order is Facing8 (S, SE, E, NE, N, NW, W, SW) so the
    /// generated AI sprite sheets (8 cells per strip) map 1:1 onto it.
    /// </summary>
    [CreateAssetMenu(menuName = "EXFIL/Character/Sprite frame set", fileName = "Frames_")]
    public class SpriteFrameSet : ScriptableObject
    {
        public Sprite[] Frames = new Sprite[0];
        public int FramesPerDirection = 1;
        public float FramesPerSecond = 8f;
        public bool Loop = true;
        public Vector2 PivotNormalized = new Vector2(0.5f, 0f);

        public int DirectionCount
        {
            get { return FramesPerDirection > 0 ? Frames.Length / FramesPerDirection : Frames.Length; }
        }

        public bool IsValid
        {
            get { return Frames != null && Frames.Length > 0 && FramesPerDirection > 0; }
        }

        public Sprite Get(Core.Facing8 facing, int frame)
        {
            if (!IsValid) return null;
            int fpd = Mathf.Max(1, FramesPerDirection);
            int dir = (int)facing % Mathf.Max(1, DirectionCount);
            int idx = dir * fpd + (frame % fpd);
            if (idx < 0 || idx >= Frames.Length) return Frames[0];
            return Frames[idx];
        }

        public float Duration
        {
            get { return IsValid ? Mathf.Max(1, FramesPerDirection) / Mathf.Max(0.01f, FramesPerSecond) : 0f; }
        }
    }

    /// <summary>
    /// How an equippable item is drawn on the character (paper-doll layer).
    /// Full suits replace the underwear base body; vests/helmets/backpacks are overlays.
    /// </summary>
    [Serializable]
    public class EquipmentVisual
    {
        [Tooltip("Full body suit: replaces the base (underwear) body sprite instead of layering over it.")]
        public bool ReplacesBody = false;

        public SpriteFrameSet FrameSet;

        [Tooltip("Applied to the rendered sprite (camo colours, worn look).")]
        public Color Tint = Color.white;

        [Tooltip("Extra sorting order, e.g. backpacks render behind the body when facing away.")]
        public int SortingOrder = 0;

        [Tooltip("When true the layer is hidden for facings that turn the back to camera.")]
        public bool HideWhenFacingAway = false;

        [Tooltip("When true the layer hides for facings that look at the camera (backpacks).")]
        public bool HideWhenFacingCamera = false;

        public Vector2 Offset = Vector2.zero;

        public bool HasSprite
        {
            get { return FrameSet != null && FrameSet.IsValid; }
        }

        // ------------------------------------------------------------- 3D model
        [Header("3D")]
        [Tooltip("Real 3D mesh attached to the character body. Empty = primitive stub is generated.")]
        public UnityEngine.GameObject Prefab3D;
        public AttachPoint AttachPoint3D = AttachPoint.Chest;
        public UnityEngine.Vector3 Offset3D = UnityEngine.Vector3.zero;
        public UnityEngine.Vector3 Rotation3D = UnityEngine.Vector3.zero;
        public UnityEngine.Vector3 Scale3D = UnityEngine.Vector3.one;
    }

    /// <summary>Named render layer of the 2D paper doll, ordered back to front.</summary>
    public enum BodyLayer
    {
        Shadow = 0,
        BodySuit,       // replaces base body
        BaseBody,       // underwear base layer
        Boots,
        Legs,
        Torso,
        Gloves,
        Vest,
        Rig,
        BackpackBehind,
        Headwear,
        Helmet,
        FaceCover,
        EarPiece,
        BackpackFront,
        Weapon
    }
}
