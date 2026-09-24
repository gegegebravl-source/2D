using System;
using System.Collections.Generic;
using UnityEngine;

namespace EXFIL.Art
{
    /// <summary>Attach anchor roles resolved from a rigged character's bones.</summary>
    public static class AttachRoles
    {
        public const string Head = "head";
        public const string Face = "face";
        public const string Neck = "neck";
        public const string Chest = "chest";
        public const string Spine = "spine";
        public const string Hips = "hips";
        public const string Back = "back";
        public const string LeftHand = "hand_l";
        public const string RightHand = "hand_r";
        public const string LeftFoot = "foot_l";
        public const string RightFoot = "foot_r";
    }

    [Serializable]
    public class ClipEntry
    {
        public string Key;              // canonical name, e.g. "walk"
        public string SourceName;       // original clip name inside the source pack
        public AnimationClip Clip;
        public bool Loop = true;
    }

    [Serializable]
    public class PoseEntry
    {
        public string Key;
        public Vector3[] Positions = new Vector3[0];
        public Quaternion[] Rotations = new Quaternion[0];
        public bool Valid;
    }

    /// <summary>
    /// Baked animation set for one operator: real AnimationClips plus static poses
    /// (aim / hold / crouch) used for additive-style blending in RiggedCharacter.
    /// </summary>
    public class CharacterAnimationSet : ScriptableObject
    {
        public string OperatorKey;
        public string[] PartRoles = new string[0];
        public List<ClipEntry> Clips = new List<ClipEntry>();
        public List<PoseEntry> Poses = new List<PoseEntry>();

        private Dictionary<string, ClipEntry> _clipIndex;
        private Dictionary<string, PoseEntry> _poseIndex;

        private void BuildIndex()
        {
            if (_clipIndex != null) return;
            _clipIndex = new Dictionary<string, ClipEntry>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Clips.Count; i++)
                if (Clips[i].Clip != null && !string.IsNullOrEmpty(Clips[i].Key)) _clipIndex[Clips[i].Key] = Clips[i];
            _poseIndex = new Dictionary<string, PoseEntry>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Poses.Count; i++)
                if (!string.IsNullOrEmpty(Poses[i].Key)) _poseIndex[Poses[i].Key] = Poses[i];
        }

        public ClipEntry Get(string key)
        {
            BuildIndex();
            ClipEntry entry;
            return _clipIndex.TryGetValue(key, out entry) ? entry : null;
        }

        public AnimationClip GetClip(string key)
        {
            ClipEntry entry = Get(key);
            return entry != null ? entry.Clip : null;
        }

        /// <summary>Returns the first available clip out of the given synonyms.</summary>
        public ClipEntry GetFirst(params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                ClipEntry entry = Get(keys[i]);
                if (entry != null) return entry;
            }
            return null;
        }

        public PoseEntry GetPose(string key)
        {
            BuildIndex();
            PoseEntry entry;
            return _poseIndex.TryGetValue(key, out entry) ? entry : null;
        }

        public bool HasPose(string key)
        {
            PoseEntry pose = GetPose(key);
            return pose != null && pose.Valid && pose.Positions.Length > 0;
        }

        public int IndexOfRole(string role)
        {
            for (int i = 0; i < PartRoles.Length; i++)
                if (string.Equals(PartRoles[i], role, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }
    }
}
