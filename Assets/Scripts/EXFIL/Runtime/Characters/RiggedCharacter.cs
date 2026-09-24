using System.Collections.Generic;
using UnityEngine;
using EXFIL.Art;

namespace EXFIL.Characters
{
    /// <summary>
    /// Plays the clips baked by Tools/bake_characters.py on a rigid-part operator rig.
    /// The rig is a flat list of parts (hips, spine, chest, head, arms, legs) whose bind
    /// transforms sit in model space - exactly what the offline baker wrote - so playback
    /// is just "set local TRS from the frame data".
    ///
    /// Aim / crouch are additive overlays evaluated in LateUpdate (after the Animation
    /// component wrote the clip pose), which keeps the walk cycle running underneath.
    /// </summary>
    [DefaultExecutionOrder(60)]
    public class RiggedCharacter : MonoBehaviour
    {
        [Header("Rig")]
        public Animation Anim;
        public CharacterAnimationSet Clips;
        public Transform[] Parts = new Transform[0];
        public string[] Roles = new string[0];

        [Header("Locomotion state (driven by controllers)")]
        public float SpeedNormalized;          // 0..1+ of walk speed
        public bool Running;
        public bool Crouched;
        public bool Aiming;
        public bool TwoHandedAim = true;
        public float AimPitch;                 // degrees, -60..60
        public bool Dead;

        [Header("Overlay tuning")]
        public float AimBlendSpeed = 8f;
        public float CrouchDrop = 0.34f;
        public float CrouchThigh = 52f;
        public float CrouchShin = -104f;
        public float CrouchFoot = 48f;
        public float PitchInfluence = 0.45f;

        private readonly Dictionary<string, Transform> _byRole = new Dictionary<string, Transform>();
        private readonly Dictionary<string, Transform> _attach = new Dictionary<string, Transform>();
        private readonly Dictionary<string, Vector3> _posePos = new Dictionary<string, Vector3>();
        private readonly Dictionary<string, Quaternion> _poseRot = new Dictionary<string, Quaternion>();

        private string _locoKey = "";
        private bool _crouchClipActive;
        private string _oneShot;
        private float _oneShotEnd;
        private float _aimBlend;
        private float _crouchBlend;
        private Vector3 _rootOffset;
        private bool _configured;

        public bool IsConfigured { get { return _configured; } }

        private void Awake()
        {
            if (Anim == null) Anim = GetComponent<Animation>();
            CacheParts();
        }

        private void CacheParts()
        {
            _byRole.Clear();
            for (int i = 0; i < Parts.Length && i < Roles.Length; i++)
                if (Parts[i] != null) _byRole[Roles[i]] = Parts[i];
            // auto-collect when the fields were not wired (prefab built by the pipeline)
            if (Parts.Length == 0 || Roles.Length == 0)
            {
                List<Transform> parts = new List<Transform>();
                List<string> roles = new List<string>();
                for (int i = 0; i < transform.childCount; i++)
                {
                    Transform child = transform.GetChild(i);
                    if (child.name.StartsWith("AP_")) continue;
                    parts.Add(child);
                    roles.Add(child.name.ToLowerInvariant());
                }
                Parts = parts.ToArray();
                Roles = roles.ToArray();
                for (int i = 0; i < Parts.Length; i++) _byRole[Roles[i]] = Parts[i];
            }
        }

        /// <summary>Binds the baked clip set and starts idling.</summary>
        public void Configure(CharacterAnimationSet set)
        {
            Clips = set;
            if (Anim == null) Anim = GetComponent<Animation>();
            if (Anim == null) Anim = gameObject.AddComponent<Animation>();
            Anim.playAutomatically = false;
            Anim.cullingType = AnimationCullingType.AlwaysAnimate;
            Anim.animatePhysics = false;
            CacheParts();

            if (Clips != null)
            {
                for (int i = 0; i < Clips.Clips.Count; i++)
                {
                    ClipEntry entry = Clips.Clips[i];
                    if (entry == null || entry.Clip == null) continue;
                    entry.Clip.legacy = true;
                    if (Anim.GetClip(entry.Key) == null) Anim.AddClip(entry.Clip, entry.Key);
                    AnimationState state = Anim[entry.Key];
                    if (state != null)
                    {
                        state.wrapMode = entry.Loop ? WrapMode.Loop : WrapMode.ClampForever;
                        state.speed = 1f;
                        state.weight = 1f;
                    }
                }
            }
            _locoKey = "";
            SetLocomotion(0f, false);
            _configured = true;
        }

        public Transform GetPart(string role)
        {
            Transform t;
            if (_byRole.TryGetValue(role, out t) && t != null) return t;
            return null;
        }

        /// <summary>Equipment / weapon anchors (AP_*) generated by the art pipeline.</summary>
        public Transform GetAttach(string role)
        {
            Transform t;
            if (_attach.TryGetValue(role, out t) && t != null) return t;
            t = FindDeep(transform, "AP_" + role);
            if (t == null && role == AttachRoles.RightHand) t = GetPart("hand_r") ?? GetPart("upperarm_r");
            if (t == null && role == AttachRoles.LeftHand) t = GetPart("hand_l") ?? GetPart("upperarm_l");
            if (t == null && role == AttachRoles.Chest) t = GetPart("chest") ?? GetPart("spine");
            if (t == null && role == AttachRoles.Spine) t = GetPart("spine") ?? GetPart("chest");
            if (t == null && role == AttachRoles.Head) t = GetPart("head");
            if (t == null && role == AttachRoles.Hips) t = GetPart("hips");
            _attach[role] = t;
            return t;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        // ------------------------------------------------------------------ clips
        public void SetLocomotion(float speedNormalized, bool running)
        {
            SpeedNormalized = speedNormalized;
            Running = running;
            if (!_configured || Clips == null) return;
            string key;
            if (speedNormalized < 0.08f) key = "idle";
            else if (running && Clips.GetClip("run") != null) key = "run";
            else key = "walk";
            if (key == _locoKey) return;
            PlayLoco(key);
        }

        private void PlayLoco(string key)
        {
            ClipEntry entry = Clips.GetFirst(key, key == "run" ? "walk" : key);
            if (entry == null) return;
            _locoKey = entry.Key;
            _oneShot = null;
            AnimationState state = Anim[entry.Key];
            if (state == null) return;
            state.wrapMode = WrapMode.Loop;
            state.speed = 1f;
            Anim.CrossFade(entry.Key, 0.22f, PlayMode.StopSameLayer);
        }

        public void SetAim(bool aiming, bool twoHanded)
        {
            Aiming = aiming;
            TwoHandedAim = twoHanded;
        }

        public void SetCrouch(bool crouched)
        {
            Crouched = crouched;
            bool hasClip = Clips != null && Clips.GetClip("crouch") != null;
            if (crouched && hasClip && !_crouchClipActive)
            {
                _crouchClipActive = true;
                _locoKey = "crouch";
                if (Anim["crouch"] != null) Anim.CrossFade("crouch", 0.2f, PlayMode.StopSameLayer);
            }
            else if (!crouched && _crouchClipActive)
            {
                _crouchClipActive = false;
                _locoKey = "";
                SetLocomotion(SpeedNormalized, Running);
            }
        }

        /// <summary>One-shot clip (shoot / reload / hit / interact / melee).</summary>
        public void PlayOneShot(string key, float fade = 0.08f)
        {
            if (!_configured || Clips == null || Dead) return;
            ClipEntry entry = Clips.GetFirst(key);
            if (entry == null) return;
            AnimationState state = Anim[entry.Key];
            if (state == null) return;
            state.wrapMode = WrapMode.ClampForever;
            state.speed = 1f;
            state.time = 0f;
            Anim.CrossFade(entry.Key, fade, PlayMode.StopSameLayer);
            _oneShot = entry.Key;
            _oneShotEnd = Time.time + Mathf.Max(0.15f, entry.Clip != null ? entry.Clip.length : 0.4f) - fade;
        }

        public void PlayShoot(bool twoHanded)
        {
            PlayOneShot(twoHanded ? "shoot_2h" : "shoot_1h", 0.04f);
        }

        public void PlayReload(bool twoHanded)
        {
            PlayOneShot(twoHanded ? "reload_2h" : "reload_1h", 0.12f);
        }

        public void PlayHit()
        {
            PlayOneShot("hit", 0.05f);
        }

        public void PlayInteract()
        {
            PlayOneShot("interact", 0.1f);
        }

        public void PlayMelee()
        {
            PlayOneShot("melee", 0.08f);
        }

        public void PlayDeath()
        {
            Dead = true;
            if (Clips == null) return;
            ClipEntry entry = Clips.GetFirst("death");
            if (entry == null) return;
            AnimationState state = Anim[entry.Key];
            if (state == null) return;
            state.wrapMode = WrapMode.ClampForever;
            state.speed = 1f;
            state.time = 0f;
            Anim.CrossFade(entry.Key, 0.1f, PlayMode.StopSameLayer);
            _oneShot = entry.Key;
            _oneShotEnd = float.MaxValue;
        }

        public void Revive()
        {
            Dead = false;
            _oneShot = null;
            _locoKey = "";
            SetLocomotion(0f, false);
        }

        // ---------------------------------------------------------------- overlay
        private void LateUpdate()
        {
            if (!_configured) return;

            float dt = Time.deltaTime;
            _aimBlend = Mathf.MoveTowards(_aimBlend, Aiming && !Dead ? 1f : 0f, dt * AimBlendSpeed);
            _crouchBlend = Mathf.MoveTowards(_crouchBlend, Crouched && !Dead ? 1f : 0f, dt * 6f);

            if (_oneShot != null && Time.time >= _oneShotEnd)
            {
                _oneShot = null;
                if (!Dead)
                {
                    string previous = _locoKey;
                    _locoKey = "";
                    SetLocomotion(SpeedNormalized, Running);
                    if (_locoKey == previous) _locoKey = "";
                }
            }

            if (!Dead && _aimBlend > 0.01f && Clips != null)
                ApplyAimPose(_aimBlend);

            ApplyPitch();

            if (_crouchBlend > 0.01f || _rootOffset != Vector3.zero)
                ApplyCrouch(_crouchBlend);
        }

        private void ApplyAimPose(float weight)
        {
            string poseKey = TwoHandedAim ? "aim_2h" : "aim_1h";
            if (!Clips.HasPose(poseKey)) poseKey = TwoHandedAim ? "aim_1h" : "aim_2h";
            PoseEntry pose = Clips.GetPose(poseKey);
            if (pose == null || !pose.Valid) return;
            for (int i = 0; i < Parts.Length && i < pose.Positions.Length; i++)
            {
                Transform part = Parts[i];
                if (part == null) continue;
                part.localPosition = Vector3.Lerp(part.localPosition, pose.Positions[i], weight);
                part.localRotation = Quaternion.Slerp(part.localRotation, pose.Rotations[i], weight);
            }
        }

        private void ApplyPitch()
        {
            if (Mathf.Abs(AimPitch) < 0.5f) return;
            float pitch = Mathf.Clamp(AimPitch, -60f, 60f) * PitchInfluence;
            Transform spine = GetPart("spine");
            Transform chest = GetPart("chest");
            Transform head = GetPart("head");
            if (spine != null) spine.localRotation = spine.localRotation * Quaternion.Euler(-pitch * 0.35f, 0f, 0f);
            if (chest != null) chest.localRotation = chest.localRotation * Quaternion.Euler(-pitch * 0.4f, 0f, 0f);
            if (head != null) head.localRotation = head.localRotation * Quaternion.Euler(pitch * 0.5f, 0f, 0f);
        }

        private static readonly string[][] LegChains =
        {
            new[] { "upperleg_l", "lowerleg_l", "foot_l" },
            new[] { "upperleg_r", "lowerleg_r", "foot_r" },
        };

        private void ApplyCrouch(float weight)
        {
            Vector3 offset = new Vector3(0f, -CrouchDrop * weight, 0f);
            if (offset != _rootOffset)
            {
                // parts are absolute in model space, so crouching is a whole-body drop
                Vector3 delta = offset - _rootOffset;
                for (int i = 0; i < Parts.Length; i++)
                    if (Parts[i] != null) Parts[i].localPosition += delta;
                _rootOffset = offset;
            }
            if (_crouchClipActive) return;   // the baked crouch clip already bends the legs
            for (int i = 0; i < LegChains.Length; i++)
            {
                RotateChainJoint(LegChains[i], 0, CrouchThigh * weight);
                RotateChainJoint(LegChains[i], 1, CrouchShin * weight);
                RotateChainJoint(LegChains[i], 2, CrouchFoot * weight);
            }
        }

        /// <summary>Rotates one joint of a leg chain and drags every descendant along.</summary>
        private void RotateChainJoint(string[] chain, int index, float degrees)
        {
            if (Mathf.Abs(degrees) < 0.01f) return;
            Transform joint = GetPart(chain[index]);
            if (joint == null) return;
            Quaternion delta = Quaternion.Euler(degrees, 0f, 0f);
            joint.localRotation = joint.localRotation * delta;
            Quaternion worldDelta = transform.rotation * delta * Quaternion.Inverse(transform.rotation);
            Vector3 pivot = joint.position;
            for (int i = index + 1; i < chain.Length; i++)
            {
                Transform child = GetPart(chain[i]);
                if (child == null) continue;
                child.position = pivot + worldDelta * (child.position - pivot);
                child.rotation = worldDelta * child.rotation;
            }
        }
    }
}
