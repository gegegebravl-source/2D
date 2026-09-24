using System;
using System.Collections.Generic;
using UnityEngine;

namespace EXFIL.Characters
{
    /// <summary>Anchor points on a character body where equipment prefabs are attached.</summary>
    public enum AttachPoint { Head = 0, Face, Spine, Chest, Hips, Back, LeftHand, RightHand, LeftFoot, RightFoot }

    /// <summary>
    /// Builds and dresses a 3D body. Works with a real imported character prefab when one is
    /// assigned on CharacterDefinition, otherwise assembles a placeholder body from primitives
    /// so the whole game is playable before art exists (see Editor/PlaceholderFactory).
    /// </summary>
    public class CharacterBody3D : MonoBehaviour
    {
        [Header("Anchors")]
        public Transform HeadAnchor;
        public Transform FaceAnchor;
        public Transform SpineAnchor;
        public Transform ChestAnchor;
        public Transform HipsAnchor;
        public Transform BackAnchor;
        public Transform RightHandAnchor;

        [Header("Renderers")]
        public SkinnedMeshRenderer BodyRenderer;
        public List<Renderer> GearRenderers = new List<Renderer>();

        private readonly Dictionary<AttachPoint, List<GameObject>> _attached =
            new Dictionary<AttachPoint, List<GameObject>>();

        public CharacterDefinition Definition { get; private set; }
        public bool IsPlaceholder { get; private set; }

        public static CharacterBody3D Spawn(CharacterDefinition definition, Transform parent, Vector3 position, Quaternion rotation)
        {
            GameObject go;
            CharacterBody3D body;

            if (definition != null && definition.BodyPrefab != null)
            {
                go = Instantiate(definition.BodyPrefab, position, rotation, parent);
                body = go.GetComponent<CharacterBody3D>();
                if (body == null) body = go.AddComponent<CharacterBody3D>();
                body.IsPlaceholder = false;
            }
            else
            {
                go = new GameObject(definition != null ? definition.DisplayName : "Operator");
                go.transform.SetPositionAndRotation(position, rotation);
                go.transform.SetParent(parent, true);
                body = go.AddComponent<CharacterBody3D>();
                body.BuildPlaceholder(definition != null ? definition.FactionColor : Color.grey);
                body.IsPlaceholder = true;
            }

            body.Definition = definition;
            body.CollectAnchors();
            return body;
        }

        /// <summary>Placeholder humanoid assembled from primitives with named anchors.</summary>
        public void BuildPlaceholder(Color factionColor)
        {
            Material skin = CreateMaterial(new Color(0.78f, 0.63f, 0.52f));
            Material suit = CreateMaterial(new Color(0.28f, 0.30f, 0.26f));
            Material accent = CreateMaterial(factionColor);

            Transform hips = CreatePart("Hips", transform, Vector3.up * 0.95f, new Vector3(0.36f, 0.22f, 0.24f), suit, PrimitiveType.Cube);
            Transform torso = CreatePart("Torso", transform, Vector3.up * 1.25f, new Vector3(0.44f, 0.42f, 0.26f), suit, PrimitiveType.Cube);
            Transform head = CreatePart("Head", transform, Vector3.up * 1.65f, new Vector3(0.22f, 0.26f, 0.22f), skin, PrimitiveType.Sphere);
            Transform face = CreatePart("Face", head, new Vector3(0f, 0f, 0.11f), new Vector3(0.16f, 0.10f, 0.02f), accent, PrimitiveType.Cube);

            Transform armL = CreatePart("ArmL", torso, new Vector3(-0.28f, -0.02f, 0f), new Vector3(0.13f, 0.42f, 0.13f), skin, PrimitiveType.Capsule);
            Transform armR = CreatePart("ArmR", torso, new Vector3(0.28f, -0.02f, 0f), new Vector3(0.13f, 0.42f, 0.13f), skin, PrimitiveType.Capsule);
            Transform legL = CreatePart("LegL", transform, new Vector3(-0.11f, 0.42f, 0f), new Vector3(0.15f, 0.85f, 0.15f), suit, PrimitiveType.Capsule);
            Transform legR = CreatePart("LegR", transform, new Vector3(0.11f, 0.42f, 0f), new Vector3(0.15f, 0.85f, 0.15f), suit, PrimitiveType.Capsule);

            Transform handR = CreatePart("HandR", armR, new Vector3(0f, -0.26f, 0f), Vector3.one * 0.12f, skin, PrimitiveType.Sphere);
            Transform footL = CreatePart("FootL", legL, new Vector3(0f, -0.45f, 0.03f), new Vector3(0.14f, 0.09f, 0.24f), suit, PrimitiveType.Cube);
            Transform footR = CreatePart("FootR", legR, new Vector3(0f, -0.45f, 0.03f), new Vector3(0.14f, 0.09f, 0.24f), suit, PrimitiveType.Cube);

            HeadAnchor = head;
            FaceAnchor = face;
            SpineAnchor = torso;
            ChestAnchor = torso;
            HipsAnchor = hips;
            BackAnchor = CreatePart("Back", torso, new Vector3(0f, 0f, -0.16f), Vector3.one * 0.01f, suit, PrimitiveType.Cube);
            RightHandAnchor = handR;

            // keep references so the compiler does not warn about unused locals
            if (armL == null || legL == null || legR == null || footL == null || footR == null)
                Debug.LogWarning("[EXFIL] Placeholder body part missing.");
        }

        private Transform CreatePart(string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material, PrimitiveType type)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            return go.transform;
        }

        private static Material CreateMaterial(Color color)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.color = color;
            return material;
        }

        private void CollectAnchors()
        {
            if (HeadAnchor == null) HeadAnchor = FindRecursive(transform, "Head");
            if (FaceAnchor == null) FaceAnchor = FindRecursive(transform, "Face");
            if (SpineAnchor == null) SpineAnchor = FindRecursive(transform, "Spine");
            if (ChestAnchor == null) ChestAnchor = SpineAnchor != null ? SpineAnchor : transform;
            if (HipsAnchor == null) HipsAnchor = FindRecursive(transform, "Hips");
            if (BackAnchor == null) BackAnchor = SpineAnchor != null ? SpineAnchor : transform;
            if (RightHandAnchor == null) RightHandAnchor = FindRecursive(transform, "HandR");
        }

        private static Transform FindRecursive(Transform root, string name)
        {
            if (root.name.Contains(name)) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindRecursive(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        public Transform AnchorFor(AttachPoint point)
        {
            switch (point)
            {
                case AttachPoint.Head: return HeadAnchor != null ? HeadAnchor : transform;
                case AttachPoint.Face: return FaceAnchor != null ? FaceAnchor : HeadAnchor;
                case AttachPoint.Spine: return SpineAnchor != null ? SpineAnchor : transform;
                case AttachPoint.Chest: return ChestAnchor != null ? ChestAnchor : SpineAnchor;
                case AttachPoint.Hips: return HipsAnchor != null ? HipsAnchor : transform;
                case AttachPoint.Back: return BackAnchor != null ? BackAnchor : SpineAnchor;
                case AttachPoint.RightHand: return RightHandAnchor != null ? RightHandAnchor : transform;
                default: return transform;
            }
        }

        /// <summary>Attaches an equipment mesh (real model or primitive stub).</summary>
        public GameObject AttachGear(AttachPoint point, GameObject prefab, Vector3 offset, Vector3 euler, Vector3 scale)
        {
            if (prefab == null) return null;
            GameObject instance = Instantiate(prefab, AnchorFor(point));
            instance.transform.localPosition = offset;
            instance.transform.localEulerAngles = euler;
            instance.transform.localScale = scale;
            instance.name = prefab.name;

            List<GameObject> list;
            if (!_attached.TryGetValue(point, out list))
            {
                list = new List<GameObject>();
                _attached[point] = list;
            }
            list.Add(instance);

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++) GearRenderers.Add(renderers[i]);
            return instance;
        }

        public void ClearGear()
        {
            foreach (KeyValuePair<AttachPoint, List<GameObject>> pair in _attached)
                for (int i = 0; i < pair.Value.Count; i++)
                    if (pair.Value[i] != null) Destroy(pair.Value[i]);
            _attached.Clear();
            GearRenderers.Clear();
        }

        public void SetVisible(bool visible)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = visible;
        }
    }
}
