using System.Collections.Generic;
using UnityEngine;
using EXFIL.Core;
using EXFIL.Items;
using EXFIL.Art;

namespace EXFIL.Characters
{
    /// <summary>
    /// Builds 3D gear that is sized from the actual body it is stacked on: every piece is
    /// measured against the renderer bounds of the body part it attaches to, so helmets,
    /// vests, rigs and backpacks fit all six operators instead of floating around them.
    ///
    /// Real imported models win: assign EquipmentVisual.Prefab3D and this factory is skipped.
    /// </summary>
    public static class GearFactory
    {
        private static readonly Dictionary<string, Material> Fabric = new Dictionary<string, Material>();

        // ------------------------------------------------------------ public entry
        public static GameObject Build(EquipmentSlot slot, ItemDefinition def, EquipmentVisual visual, Transform anchor)
        {
            if (anchor == null) return null;
            Color tint = visual != null ? visual.Tint : Color.white;
            if (tint == Color.clear) tint = Color.white;

            GameObject root = new GameObject("Gear_" + slot);
            root.transform.SetParent(anchor, false);

            Bounds body = PartBounds(anchor);
            float height = body.size.y;
            float width = body.size.x;
            float depth = body.size.z;

            switch (slot)
            {
                case EquipmentSlot.Helmet: BuildHelmet(root.transform, body, tint); break;
                case EquipmentSlot.Headwear: BuildCap(root.transform, body, tint); break;
                case EquipmentSlot.FaceCover: BuildMask(root.transform, body, tint); break;
                case EquipmentSlot.Eyewear: BuildGoggles(root.transform, body, tint); break;
                case EquipmentSlot.EarPiece: BuildHeadset(root.transform, body, tint); break;
                case EquipmentSlot.ArmorVest: BuildVest(root.transform, body, tint, true); break;
                case EquipmentSlot.ChestRig: BuildRig(root.transform, body, tint); break;
                case EquipmentSlot.Backpack: BuildBackpack(root.transform, body, tint); break;
                case EquipmentSlot.Boots: BuildBoots(root.transform, body, tint); break;
                case EquipmentSlot.Gloves: BuildGloves(root.transform, body, tint); break;
                case EquipmentSlot.BodySuit: BuildJacket(root.transform, body, tint); break;
                default: BuildClothing(root.transform, body, tint); break;
            }
            return root;
        }

        private static Bounds PartBounds(Transform anchor)
        {
            Renderer renderer = anchor.GetComponent<Renderer>();
            if (renderer == null) renderer = anchor.GetComponentInParent<Renderer>();
            if (renderer == null) renderer = anchor.GetComponentInChildren<Renderer>();
            if (renderer == null) return new Bounds(Vector3.zero, new Vector3(0.3f, 0.3f, 0.3f));
            return renderer.bounds;
        }

        /// <summary>Centre of the body part expressed in the anchor's local space.</summary>
        private static Vector3 LocalCenter(Transform anchor, Bounds bounds)
        {
            return anchor.InverseTransformPoint(bounds.center);
        }

        // ------------------------------------------------------------------ pieces
        private static void BuildHelmet(Transform parent, Bounds body, Color tint)
        {
            Vector3 centre = LocalCenter(parent, body);
            Color shell = Blend(tint, new Color(0.22f, 0.26f, 0.20f), 0.5f);
            float r = Mathf.Max(body.size.x, body.size.z) * 0.62f;
            float h = body.size.y * 0.55f;
            AddSphere(parent, "shell", centre + new Vector3(0f, h * 0.28f, 0f),
                      new Vector3(r * 1.08f, h * 0.85f, r * 1.12f), shell);
            AddBox(parent, "visor", centre + new Vector3(0f, -h * 0.02f, r * 0.34f),
                   new Vector3(r * 1.1f, h * 0.30f, r * 0.22f), Quaternion.Euler(12f, 0f, 0f), new Color(0.09f, 0.10f, 0.11f));
            AddBox(parent, "ear_l", centre + new Vector3(-r * 0.58f, -h * 0.05f, 0f),
                   new Vector3(r * 0.18f, h * 0.42f, r * 0.5f), Quaternion.identity, shell * 0.85f);
            AddBox(parent, "ear_r", centre + new Vector3(r * 0.58f, -h * 0.05f, 0f),
                   new Vector3(r * 0.18f, h * 0.42f, r * 0.5f), Quaternion.identity, shell * 0.85f);
            AddBox(parent, "rim", centre + new Vector3(0f, -h * 0.42f, 0f),
                   new Vector3(r * 1.25f, h * 0.14f, r * 1.3f), Quaternion.identity, shell * 0.7f);
            // NVG mount / rails keep the silhouette readable from behind
            AddBox(parent, "mount", centre + new Vector3(0f, h * 0.42f, r * 0.30f),
                   new Vector3(r * 0.22f, h * 0.28f, r * 0.34f), Quaternion.identity, new Color(0.14f, 0.14f, 0.15f));
        }

        private static void BuildCap(Transform parent, Bounds body, Color tint)
        {
            Vector3 centre = LocalCenter(parent, body);
            Color shell = Blend(tint, new Color(0.32f, 0.34f, 0.28f), 0.6f);
            float r = Mathf.Max(body.size.x, body.size.z) * 0.6f;
            AddSphere(parent, "cap", centre + new Vector3(0f, body.size.y * 0.16f, 0f),
                      new Vector3(r * 1.05f, body.size.y * 0.34f, r * 1.05f), shell);
            AddBox(parent, "brim", centre + new Vector3(0f, body.size.y * 0.04f, r * 0.5f),
                   new Vector3(r * 0.95f, r * 0.09f, r * 0.6f), Quaternion.Euler(6f, 0f, 0f), shell * 0.8f);
        }

        private static void BuildMask(Transform parent, Bounds body, Color tint)
        {
            Vector3 centre = LocalCenter(parent, body);
            float r = Mathf.Max(body.size.x, body.size.z) * 0.5f;
            float h = body.size.y;
            AddBox(parent, "mask", centre + new Vector3(0f, -h * 0.08f, r * 0.52f),
                   new Vector3(r * 1.2f, h * 0.42f, r * 0.34f), Quaternion.identity, Blend(tint, new Color(0.15f, 0.15f, 0.16f), 0.6f));
            AddBox(parent, "strap", centre + new Vector3(0f, -h * 0.08f, 0f),
                   new Vector3(r * 1.5f, h * 0.10f, r * 1.1f), Quaternion.identity, new Color(0.12f, 0.12f, 0.13f));
        }

        private static void BuildGoggles(Transform parent, Bounds body, Color tint)
        {
            Vector3 centre = LocalCenter(parent, body);
            float r = Mathf.Max(body.size.x, body.size.z) * 0.5f;
            AddBox(parent, "lens", centre + new Vector3(0f, body.size.y * 0.08f, r * 0.55f),
                   new Vector3(r * 1.3f, body.size.y * 0.16f, r * 0.3f), Quaternion.identity, new Color(0.08f, 0.11f, 0.13f));
            AddBox(parent, "band", centre + new Vector3(0f, body.size.y * 0.08f, 0f),
                   new Vector3(r * 1.6f, body.size.y * 0.08f, r * 1.15f), Quaternion.identity, new Color(0.13f, 0.13f, 0.14f));
        }

        private static void BuildHeadset(Transform parent, Bounds body, Color tint)
        {
            Vector3 centre = LocalCenter(parent, body);
            float r = Mathf.Max(body.size.x, body.size.z) * 0.5f;
            Color shell = new Color(0.14f, 0.14f, 0.15f);
            AddCylinder(parent, "cup_l", centre + new Vector3(-r * 1.02f, -body.size.y * 0.02f, 0f),
                        new Vector3(r * 0.3f, r * 0.12f, r * 0.3f), Quaternion.Euler(0f, 0f, 90f), shell);
            AddCylinder(parent, "cup_r", centre + new Vector3(r * 1.02f, -body.size.y * 0.02f, 0f),
                        new Vector3(r * 0.3f, r * 0.12f, r * 0.3f), Quaternion.Euler(0f, 0f, 90f), shell);
            AddBox(parent, "band", centre + new Vector3(0f, body.size.y * 0.34f, 0f),
                   new Vector3(r * 2.1f, r * 0.12f, r * 0.5f), Quaternion.identity, shell * 1.2f);
            AddCylinder(parent, "mic", centre + new Vector3(r * 0.5f, -body.size.y * 0.18f, r * 0.5f),
                        new Vector3(0.012f, 0.05f, 0.012f), Quaternion.Euler(60f, 0f, 0f), shell);
        }

        private static void BuildVest(Transform parent, Bounds body, Color tint, bool plates)
        {
            Vector3 centre = LocalCenter(parent, body);
            float w = body.size.x;
            float h = body.size.y;
            float d = body.size.z;
            Color shell = Blend(tint, new Color(0.26f, 0.30f, 0.24f), 0.55f);

            AddBox(parent, "carrier", centre + new Vector3(0f, -h * 0.02f, 0f),
                   new Vector3(w * 0.82f, h * 0.66f, d * 0.86f), Quaternion.identity, shell);
            if (plates)
            {
                AddBox(parent, "plate_front", centre + new Vector3(0f, 0f, d * 0.40f),
                       new Vector3(w * 0.52f, h * 0.46f, d * 0.18f), Quaternion.Euler(4f, 0f, 0f), shell * 0.72f);
                AddBox(parent, "plate_back", centre + new Vector3(0f, 0f, -d * 0.40f),
                       new Vector3(w * 0.52f, h * 0.46f, d * 0.16f), Quaternion.Euler(-3f, 0f, 0f), shell * 0.72f);
            }
            AddBox(parent, "shoulder_l", centre + new Vector3(-w * 0.42f, h * 0.28f, 0f),
                   new Vector3(w * 0.20f, h * 0.18f, d * 0.7f), Quaternion.identity, shell * 0.9f);
            AddBox(parent, "shoulder_r", centre + new Vector3(w * 0.42f, h * 0.28f, 0f),
                   new Vector3(w * 0.20f, h * 0.18f, d * 0.7f), Quaternion.identity, shell * 0.9f);
        }

        private static void BuildRig(Transform parent, Bounds body, Color tint)
        {
            Vector3 centre = LocalCenter(parent, body);
            float w = body.size.x;
            float h = body.size.y;
            float d = body.size.z;
            Color webbing = Blend(tint, new Color(0.34f, 0.31f, 0.24f), 0.55f);
            Color pouch = webbing * 0.78f;

            AddBox(parent, "chestrig", centre + new Vector3(0f, -h * 0.06f, 0f),
                   new Vector3(w * 0.74f, h * 0.36f, d * 0.66f), Quaternion.identity, webbing);
            for (int i = -1; i <= 1; i++)
            {
                AddBox(parent, "mag" + i, centre + new Vector3(i * w * 0.22f, -h * 0.20f, d * 0.42f),
                       new Vector3(w * 0.17f, h * 0.20f, d * 0.20f), Quaternion.identity, pouch);
            }
            AddBox(parent, "admin", centre + new Vector3(-w * 0.34f, h * 0.06f, d * 0.40f),
                   new Vector3(w * 0.20f, h * 0.16f, d * 0.14f), Quaternion.identity, pouch);
            AddBox(parent, "dump", centre + new Vector3(w * 0.36f, -h * 0.16f, d * 0.12f),
                   new Vector3(w * 0.18f, h * 0.24f, d * 0.42f), Quaternion.identity, pouch);
        }

        private static void BuildBackpack(Transform parent, Bounds body, Color tint)
        {
            Vector3 centre = LocalCenter(parent, body);
            float w = body.size.x;
            float h = body.size.y;
            float d = body.size.z;
            Color bag = Blend(tint, new Color(0.24f, 0.26f, 0.22f), 0.5f);

            AddBox(parent, "body", centre + new Vector3(0f, -h * 0.04f, -d * 0.72f),
                   new Vector3(w * 0.72f, h * 0.72f, d * 0.55f), Quaternion.identity, bag);
            AddBox(parent, "lid", centre + new Vector3(0f, h * 0.30f, -d * 0.72f),
                   new Vector3(w * 0.76f, h * 0.16f, d * 0.60f), Quaternion.Euler(-12f, 0f, 0f), bag * 0.85f);
            AddBox(parent, "pocket_l", centre + new Vector3(-w * 0.42f, -h * 0.18f, -d * 0.72f),
                   new Vector3(w * 0.20f, h * 0.34f, d * 0.44f), Quaternion.identity, bag * 0.92f);
            AddBox(parent, "pocket_r", centre + new Vector3(w * 0.42f, -h * 0.18f, -d * 0.72f),
                   new Vector3(w * 0.20f, h * 0.34f, d * 0.44f), Quaternion.identity, bag * 0.92f);
            AddBox(parent, "strap_l", centre + new Vector3(-w * 0.30f, h * 0.10f, d * 0.42f),
                   new Vector3(w * 0.12f, h * 0.40f, d * 0.16f), Quaternion.Euler(18f, 0f, 0f), bag * 1.1f);
            AddBox(parent, "strap_r", centre + new Vector3(w * 0.30f, h * 0.10f, d * 0.42f),
                   new Vector3(w * 0.12f, h * 0.40f, d * 0.16f), Quaternion.Euler(18f, 0f, 0f), bag * 1.1f);
            AddBox(parent, "roll", centre + new Vector3(0f, h * 0.44f, -d * 0.72f),
                   new Vector3(w * 0.62f, h * 0.14f, d * 0.30f), Quaternion.identity, bag * 1.2f);
        }

        private static void BuildBoots(Transform parent, Bounds body, Color tint)
        {
            Vector3 centre = LocalCenter(parent, body);
            float l = body.size.z;
            Color boot = Blend(tint, new Color(0.16f, 0.15f, 0.13f), 0.6f);
            AddBox(parent, "boot", centre + new Vector3(0f, body.size.y * 0.05f, body.size.z * 0.12f),
                   new Vector3(body.size.x * 1.15f, body.size.y * 0.95f, l * 1.55f), Quaternion.identity, boot);
            AddBox(parent, "sole", centre + new Vector3(0f, -body.size.y * 0.42f, body.size.z * 0.12f),
                   new Vector3(body.size.x * 1.2f, body.size.y * 0.18f, l * 1.6f), Quaternion.identity, new Color(0.08f, 0.08f, 0.09f));
        }

        private static void BuildGloves(Transform parent, Bounds body, Color tint)
        {
            Vector3 centre = LocalCenter(parent, body);
            AddBox(parent, "glove", centre, new Vector3(body.size.x * 1.2f, body.size.y * 1.15f, body.size.z * 1.3f),
                   Quaternion.identity, Blend(tint, new Color(0.18f, 0.17f, 0.16f), 0.6f));
        }

        private static void BuildJacket(Transform parent, Bounds body, Color tint)
        {
            Vector3 centre = LocalCenter(parent, body);
            AddBox(parent, "jacket", centre, new Vector3(body.size.x * 0.95f, body.size.y * 0.9f, body.size.z * 0.95f),
                   Quaternion.identity, Blend(tint, new Color(0.24f, 0.26f, 0.22f), 0.6f));
        }

        private static void BuildClothing(Transform parent, Bounds body, Color tint)
        {
            Vector3 centre = LocalCenter(parent, body);
            Color cloth = Blend(tint, new Color(0.26f, 0.27f, 0.24f), 0.6f);
            AddBox(parent, "cloth", centre,
                   new Vector3(body.size.x * 1.02f, body.size.y * 1.02f, body.size.z * 1.02f),
                   Quaternion.identity, cloth);
        }

        // ---------------------------------------------------------------- primitives
        private static void AddBox(Transform parent, string name, Vector3 position, Vector3 size, Quaternion rotation, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = size;
            Style(go, color, 0.1f);
        }

        private static void AddSphere(Transform parent, string name, Vector3 position, Vector3 size, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            Style(go, color, 0.18f);
        }

        private static void AddCylinder(Transform parent, string name, Vector3 position, Vector3 size, Quaternion rotation, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = size;
            Style(go, color, 0.14f);
        }

        private static void Style(GameObject go, Color color, float smoothness)
        {
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            renderer.sharedMaterial = VisualMaterials.Solid("gear", color, smoothness);
            VisualMaterials.Style(renderer);
        }

        private static Color Blend(Color a, Color b, float t)
        {
            return new Color(Mathf.Lerp(a.r, b.r, t), Mathf.Lerp(a.g, b.g, t), Mathf.Lerp(a.b, b.b, t), 1f);
        }
    }
}
