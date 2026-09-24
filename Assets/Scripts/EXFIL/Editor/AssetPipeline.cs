using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using EXFIL.Art;
using EXFIL.Characters;
using EXFIL.Combat;
using EXFIL.Core;

namespace EXFILEditor
{
    /// <summary>
    /// Turns the CC0 model packs in Assets/Art/Models into Unity-native assets:
    /// meshes, materials, AnimationClips, prop/weapon/operator prefabs and the
    /// ModelLibrary the runtime reads. No third-party importer required.
    /// </summary>
    public static class AssetPipeline
    {
        public const string ManifestPath = "Assets/Art/Models/manifest.json";
        public const string GenRoot = "Assets/Generated";
        public const string MeshRoot = "Assets/Generated/Meshes";
        public const string MatRoot = "Assets/Generated/Materials";
        public const string AnimRoot = "Assets/Generated/Anims";
        public const string PrefabRoot = "Assets/Generated/Prefabs";
        public const string SetRoot = "Assets/Generated/AnimSets";
        public const string LibraryPath = "Assets/Resources/Data/ModelLibrary.asset";

        // weapon model -> real-world length (metres)
        private static readonly Dictionary<string, float> WeaponLengths = new Dictionary<string, float>
        {
            { "pistol", 0.24f }, { "smg", 0.52f }, { "rifle", 0.92f }, { "dmr", 1.10f },
            { "sniper", 1.24f }, { "shotgun", 0.95f }, { "melee", 0.36f }, { "launcher", 1.02f },
        };

        private static readonly Dictionary<string, BodyPart> RoleToBodyPart = new Dictionary<string, BodyPart>
        {
            { "head", BodyPart.Head }, { "chest", BodyPart.Thorax }, { "spine", BodyPart.Thorax },
            { "hips", BodyPart.Stomach },
            { "upperarm_l", BodyPart.LeftArm }, { "lowerarm_l", BodyPart.LeftArm }, { "hand_l", BodyPart.LeftArm },
            { "upperarm_r", BodyPart.RightArm }, { "lowerarm_r", BodyPart.RightArm }, { "hand_r", BodyPart.RightArm },
            { "upperleg_l", BodyPart.LeftLeg }, { "lowerleg_l", BodyPart.LeftLeg }, { "foot_l", BodyPart.LeftLeg },
            { "upperleg_r", BodyPart.RightLeg }, { "lowerleg_r", BodyPart.RightLeg }, { "foot_r", BodyPart.RightLeg },
        };

        [MenuItem("EXFIL/Art/1 - Import all model packs (CC0 art)", false, 60)]
        public static void ImportAll()
        {
            Build(true);
        }

        [MenuItem("EXFIL/Art/2 - Only rebuild the model library asset", false, 61)]
        public static void RebuildLibraryOnly()
        {
            Build(false);
        }

        /// <summary>Main entry point; also called by the one-click setup.</summary>
        public static ModelLibrary Build(bool fullRebuild)
        {
            if (!File.Exists(ManifestPath))
            {
                Debug.LogError("[EXFIL/Art] " + ManifestPath + " not found - run Tools/build_all.py first.");
                return null;
            }

            ManifestData manifest = JsonUtility.FromJson<ManifestData>(File.ReadAllText(ManifestPath));
            if (manifest == null || manifest.models == null || manifest.models.Length == 0)
            {
                Debug.LogError("[EXFIL/Art] manifest is empty.");
                return null;
            }

            EnsureFolder("Assets/Generated");
            EnsureFolder(MeshRoot);
            EnsureFolder(MatRoot);
            EnsureFolder(AnimRoot);
            EnsureFolder(PrefabRoot);
            EnsureFolder(PrefabRoot + "/Props");
            EnsureFolder(PrefabRoot + "/Weapons");
            EnsureFolder(PrefabRoot + "/Characters");
            EnsureFolder(SetRoot);
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/Data");

            ModelLibrary library = AssetDatabase.LoadAssetAtPath<ModelLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<ModelLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            library.Entries.Clear();

            Dictionary<string, Material> materials = new Dictionary<string, Material>();
            Material fallback = CreateFallbackMaterial();

            int index = 0;
            int created = 0;
            try
            {
                for (int i = 0; i < manifest.models.Length; i++)
                {
                    ManifestRecord record = manifest.models[i];
                    index++;
                    if (fullRebuild)
                        EditorUtility.DisplayProgressBar("EXFIL - importing art",
                            string.Format("{0} ({1}/{2})", record.key, index, manifest.models.Length),
                            index / (float)manifest.models.Length);

                    string packPath = Path.Combine("Assets/Art/Models", record.file).Replace('\\', '/');
                    if (!File.Exists(packPath)) continue;
                    PackData pack;
                    try
                    {
                        pack = ModelPack.Read(packPath);
                    }
                    catch (Exception error)
                    {
                        Debug.LogError("[EXFIL/Art] failed to read " + packPath + ": " + error.Message);
                        continue;
                    }

                    Material material = ResolveMaterial(pack, packPath, materials, fallback);
                    GameObject prefab = null;
                    if (record.category == "character")
                        prefab = BuildCharacterPrefab(pack, record, material);
                    else if (record.category == "weapon")
                        prefab = BuildWeaponPrefab(pack, record, material);
                    else
                        prefab = BuildPropPrefab(pack, record, material);

                    if (prefab == null) continue;

                    ModelEntry entry = new ModelEntry();
                    entry.Key = record.key;
                    entry.Category = record.category;
                    entry.Tags = record.tags ?? new string[0];
                    entry.Prefab = prefab;
                    Bounds bounds = pack.ComputeBounds();
                    entry.Size = record.size != null && record.size.Length == 3
                        ? new Vector3(record.size[0], record.size[1], record.size[2])
                        : bounds.size;
                    entry.BoundsMin = bounds.min;
                    entry.Source = record.source;
                    library.Entries.Add(entry);
                    created++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (fullRebuild)
            {
                int wired = WireOperators(library);
                Debug.Log(string.Format("[EXFIL/Art] imported {0}/{1} models ({2} operator definitions wired). Library: {3}",
                    created, manifest.models.Length, wired, LibraryPath));
            }
            return library;
        }

        // ------------------------------------------------------------------ materials
        private static Material CreateFallbackMaterial()
        {
            Shader shader = PickShader();
            Material material = new Material(shader);
            material.name = "EXFIL_Fallback";
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(0.6f, 0.6f, 0.62f));
            if (material.HasProperty("_Color")) material.color = new Color(0.6f, 0.6f, 0.62f);
            return material;
        }

        public static Shader PickShader()
        {
            bool urp = GraphicsSettings.currentRenderPipeline != null;
            Shader shader = urp ? Shader.Find("Universal Render Pipeline/Lit") : null;
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Diffuse");
            return shader;
        }

        private static Material ResolveMaterial(PackData pack, string packPath, Dictionary<string, Material> cache, Material fallback)
        {
            if (string.IsNullOrEmpty(pack.Texture)) return fallback;
            string folder = Path.GetDirectoryName(packPath).Replace('\\', '/');
            string texturePath = (folder + "/" + pack.Texture).Replace('\\', '/');
            Material cached;
            if (cache.TryGetValue(texturePath, out cached)) return cached;
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                cache[texturePath] = fallback;
                return fallback;
            }
            ConfigureTexture(texturePath);

            string materialPath = MatRoot + "/" + Path.GetFileNameWithoutExtension(texturePath) + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(PickShader());
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.name = Path.GetFileNameWithoutExtension(texturePath);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color")) material.color = Color.white;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.08f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.08f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            cache[texturePath] = material;
            return material;
        }

        private static void ConfigureTexture(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            if (importer.filterMode == FilterMode.Point && !importer.mipmapEnabled) return;
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;      // crisp low-poly atlas look
            importer.mipmapEnabled = false;              // avoid atlas bleeding
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        // -------------------------------------------------------------------- meshes
        private static Mesh BuildMesh(PackMeshData data, string name, string assetPath)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (mesh == null)
            {
                mesh = new Mesh();
                mesh.name = name;
                AssetDatabase.CreateAsset(mesh, assetPath);
            }
            mesh.Clear();
            mesh.name = name;
            mesh.indexFormat = data.Verts.Length > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(data.Verts);
            mesh.SetNormals(data.Normals);
            mesh.SetUVs(0, data.UVs);
            mesh.SetTriangles(data.Tris, 0, true);
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static void AttachMesh(GameObject target, Mesh mesh, Material material)
        {
            MeshFilter filter = target.GetComponent<MeshFilter>();
            if (filter == null) filter = target.AddComponent<MeshFilter>();
            MeshRenderer renderer = target.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = target.AddComponent<MeshRenderer>();
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        // ---------------------------------------------------------------- prop prefab
        private static GameObject BuildPropPrefab(PackData pack, ManifestRecord record, Material material)
        {
            string prefabPath = PrefabRoot + "/Props/" + record.key + ".prefab";
            GameObject root = new GameObject(record.key);
            PropInfo info = root.AddComponent<PropInfo>();
            info.Key = record.key;
            info.Category = "prop";
            info.Tags = record.tags ?? new string[0];

            for (int p = 0; p < pack.Parts.Count; p++)
            {
                PackPartData part = pack.Parts[p];
                GameObject go = new GameObject(pack.Parts.Count > 1 ? part.Role : "Model");
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = part.Pivot;
                if (part.Meshes.Count > 0)
                {
                    string path = string.Format("{0}/{1}_{2}.asset", MeshRoot, record.key, part.Role);
                    Mesh mesh = BuildMesh(part.Meshes[0], record.key + "_" + part.Role, path);
                    AttachMesh(go, mesh, material);
                }
            }

            Bounds bounds = pack.ComputeBounds();
            info.Size = bounds.size;
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = bounds.size;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        // -------------------------------------------------------------- weapon prefab
        private static GameObject BuildWeaponPrefab(PackData pack, ManifestRecord record, Material material)
        {
            string prefabPath = PrefabRoot + "/Weapons/" + record.key + ".prefab";
            GameObject root = new GameObject(record.key);
            PropInfo info = root.AddComponent<PropInfo>();
            info.Key = record.key;
            info.Category = "weapon";
            info.Tags = record.tags ?? new string[0];

            Bounds bounds = pack.ComputeBounds();
            Vector3 size = bounds.size;
            Quaternion orientation = Quaternion.identity;
            float lengthAxis = size.z;
            if (size.y >= size.x && size.y >= size.z)
            {
                orientation = Quaternion.Euler(90f, 0f, 0f);      // Y-long model -> +Z forward
                lengthAxis = size.y;
            }
            else if (size.x > size.z)
            {
                orientation = Quaternion.Euler(0f, -90f, 0f);     // X-long model -> +Z forward
                lengthAxis = size.x;
            }

            string kind = "rifle";
            if (record.tags != null)
                for (int i = 0; i < record.tags.Length; i++)
                    if (WeaponLengths.ContainsKey(record.tags[i])) kind = record.tags[i];
            float targetLength = WeaponLengths[kind];
            float scale = lengthAxis > 0.0001f ? targetLength / lengthAxis : 1f;

            GameObject model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            model.transform.localRotation = orientation;
            model.transform.localScale = Vector3.one * scale;
            for (int p = 0; p < pack.Parts.Count; p++)
            {
                PackPartData part = pack.Parts[p];
                if (part.Meshes.Count == 0) continue;
                string path = string.Format("{0}/{1}_{2}.asset", MeshRoot, record.key, part.Role);
                Mesh mesh = BuildMesh(part.Meshes[0], record.key + "_" + part.Role, path);
                AttachMesh(model, mesh, material);
            }

            // barrel tip = vertex with the largest local z after normalisation
            Vector3 muzzle = Vector3.zero;
            Vector3 eject = new Vector3(0f, bounds.center.y - 0.5f * size.y, 0f);
            float best = float.MinValue;
            float lowest = float.MaxValue;
            for (int p = 0; p < pack.Parts.Count; p++)
            {
                PackPartData part = pack.Parts[p];
                for (int m = 0; m < part.Meshes.Count; m++)
                {
                    Vector3[] verts = part.Meshes[m].Verts;
                    for (int v = 0; v < verts.Length; v++)
                    {
                        Vector3 world = orientation * ((verts[v] + part.Pivot) * scale);
                        if (world.z > best) { best = world.z; muzzle = world; }
                        if (world.y < lowest) { lowest = world.y; eject = world; }
                    }
                }
            }
            GameObject muzzleGo = new GameObject("Muzzle");
            muzzleGo.transform.SetParent(root.transform, false);
            muzzleGo.transform.localPosition = muzzle + Vector3.forward * 0.02f;
            GameObject ejectGo = new GameObject("ShellEject");
            ejectGo.transform.SetParent(root.transform, false);
            ejectGo.transform.localPosition = eject + new Vector3(0.05f, 0.02f, 0f);

            info.Size = new Vector3(size.x, size.y, size.z) * scale;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        // ------------------------------------------------------------ character prefab
        private static GameObject BuildCharacterPrefab(PackData pack, ManifestRecord record, Material material)
        {
            string prefabPath = PrefabRoot + "/Characters/" + record.key + ".prefab";
            GameObject root = new GameObject(record.key);

            CharacterBody3D body = root.AddComponent<CharacterBody3D>();
            RiggedCharacter rig = root.AddComponent<RiggedCharacter>();
            Animation animation = root.AddComponent<Animation>();
            animation.playAutomatically = false;
            animation.cullingType = AnimationCullingType.AlwaysAnimate;

            List<Transform> parts = new List<Transform>();
            List<string> roles = new List<string>();
            Dictionary<string, Transform> byRole = new Dictionary<string, Transform>();

            for (int p = 0; p < pack.Parts.Count; p++)
            {
                PackPartData part = pack.Parts[p];
                GameObject go = new GameObject(part.Role);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = part.Pivot;
                if (part.Meshes.Count > 0)
                {
                    string path = string.Format("{0}/{1}_{2}.asset", MeshRoot, record.key, part.Role);
                    Mesh mesh = BuildMesh(part.Meshes[0], record.key + "_" + part.Role, path);
                    AttachMesh(go, mesh, material);

                    Bounds bounds = part.Meshes[0].Bounds;
                    BoxCollider hit = go.AddComponent<BoxCollider>();
                    hit.center = bounds.center;
                    hit.size = bounds.size;
                    Hitbox hitbox = go.AddComponent<Hitbox>();
                    BodyPart zone;
                    hitbox.Part = RoleToBodyPart.TryGetValue(part.Role, out zone) ? zone : BodyPart.Thorax;
                }
                parts.Add(go.transform);
                roles.Add(part.Role);
                byRole[part.Role] = go.transform;
            }

            // attach anchors (equipment, weapon hands, optics)
            Transform head = GetOrNull(byRole, "head");
            Transform chest = GetOrNull(byRole, "chest");
            Transform spine = GetOrNull(byRole, "spine");
            Transform hips = GetOrNull(byRole, "hips");
            Transform handR = HandAnchor(byRole, pack, true);
            Transform handL = HandAnchor(byRole, pack, false);

            body.HeadAnchor = head;
            body.SpineAnchor = spine;
            body.ChestAnchor = chest;
            body.HipsAnchor = hips;
            body.RightHandAnchor = handR;
            body.FaceAnchor = MakeAnchor(head, "AP_face", new Vector3(0f, 0.08f, 0.09f));
            body.BackAnchor = MakeAnchor(chest, "AP_back", new Vector3(0f, 0.02f, -0.13f));
            MakeAnchor(head, "AP_head", Vector3.zero);
            MakeAnchor(chest, "AP_chest", Vector3.zero);
            MakeAnchor(spine, "AP_spine", Vector3.zero);
            MakeAnchor(hips, "AP_hips", Vector3.zero);
            MakeAnchor(hips, "AP_hip_right", new Vector3(0.16f, -0.05f, 0.05f));
            MakeAnchor(hips, "AP_hip_left", new Vector3(-0.16f, -0.05f, 0.05f));
            if (handR != null) MakeAnchor(handR, "AP_hand_r", Vector3.zero);
            if (handL != null) MakeAnchor(handL, "AP_hand_l", Vector3.zero);
            Transform footR = GetOrNull(byRole, "foot_r");
            Transform footL = GetOrNull(byRole, "foot_l");
            if (footR != null) MakeAnchor(footR, "AP_foot_r", Vector3.zero);
            if (footL != null) MakeAnchor(footL, "AP_foot_l", Vector3.zero);

            CharacterAnimationSet set = BuildAnimationSet(pack, record, roles);
            rig.Parts = parts.ToArray();
            rig.Roles = roles.ToArray();
            rig.Anim = animation;
            rig.Clips = set;
            if (set == null || set.Clips.Count == 0)
            {
                // no baked clips (simple rig): drive the legs procedurally
                rig.enabled = true;
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static Transform GetOrNull(Dictionary<string, Transform> map, string role)
        {
            Transform value;
            return map.TryGetValue(role, out value) ? value : null;
        }

        private static Transform MakeAnchor(Transform parent, string name, Vector3 offset)
        {
            if (parent == null) return null;
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = offset;
            return go.transform;
        }

        /// <summary>Hand anchor: the hand bone when the rig has one, otherwise the far end of the arm mesh.</summary>
        private static Transform HandAnchor(Dictionary<string, Transform> byRole, PackData pack, bool right)
        {
            Transform hand = GetOrNull(byRole, right ? "hand_r" : "hand_l");
            if (hand != null) return hand;
            Transform arm = GetOrNull(byRole, right ? "lowerarm_r" : "lowerarm_l");
            if (arm == null) arm = GetOrNull(byRole, right ? "upperarm_r" : "upperarm_l");
            if (arm == null) return null;

            float best = float.MinValue;
            Vector3 bestLocal = Vector3.zero;
            for (int p = 0; p < pack.Parts.Count; p++)
            {
                PackPartData part = pack.Parts[p];
                if (part.Role != (right ? "upperarm_r" : "upperarm_l") && part.Role != (right ? "lowerarm_r" : "lowerarm_l"))
                    continue;
                for (int m = 0; m < part.Meshes.Count; m++)
                {
                    Vector3[] verts = part.Meshes[m].Verts;
                    for (int v = 0; v < verts.Length; v++)
                    {
                        float axis = right ? verts[v].x : -verts[v].x;
                        if (axis > best) { best = axis; bestLocal = verts[v]; }
                    }
                }
            }
            return MakeAnchor(arm, right ? "AP_hand_r" : "AP_hand_l", bestLocal);
        }

        // ----------------------------------------------------------------- anim sets
        private static CharacterAnimationSet BuildAnimationSet(PackData pack, ManifestRecord record, List<string> roles)
        {
            if (pack.Anims.Count == 0) return null;
            string setPath = SetRoot + "/" + record.key + "_Anims.asset";
            CharacterAnimationSet set = AssetDatabase.LoadAssetAtPath<CharacterAnimationSet>(setPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<CharacterAnimationSet>();
                AssetDatabase.CreateAsset(set, setPath);
            }
            set.OperatorKey = record.key;
            set.PartRoles = roles.ToArray();
            set.Clips.Clear();
            set.Poses.Clear();

            for (int a = 0; a < pack.Anims.Count; a++)
            {
                PackAnimData data = pack.Anims[a];
                AnimationClip clip = BuildClip(data, record.key);
                if (clip == null) continue;
                ClipEntry entry = new ClipEntry();
                entry.Key = data.Canonical;
                entry.SourceName = data.SourceName;
                entry.Clip = clip;
                entry.Loop = data.Loop;
                set.Clips.Add(entry);

                // aiming / ready poses come from the matching baked clip
                if (data.Canonical == "aim_1h" || data.Canonical == "aim_2h" || data.Canonical == "crouch")
                    set.Poses.Add(BuildPose(data, data.Canonical));
            }
            if (set.GetPose("aim_2h") == null && set.GetPose("aim_1h") != null)
                set.Poses.Add(ClonePose(set.GetPose("aim_1h"), "aim_2h"));

            EditorUtility.SetDirty(set);
            return set;
        }

        private static PoseEntry BuildPose(PackAnimData data, string key)
        {
            PoseEntry pose = new PoseEntry();
            pose.Key = key;
            int frame = Mathf.Clamp(data.Positions.Length / 2, 0, Mathf.Max(0, data.Positions.Length - 1));
            if (data.Positions.Length == 0) return pose;
            pose.Positions = (Vector3[])data.Positions[frame].Clone();
            pose.Rotations = (Quaternion[])data.Rotations[frame].Clone();
            pose.Valid = true;
            return pose;
        }

        private static PoseEntry ClonePose(PoseEntry source, string key)
        {
            PoseEntry pose = new PoseEntry();
            pose.Key = key;
            pose.Positions = (Vector3[])source.Positions.Clone();
            pose.Rotations = (Quaternion[])source.Rotations.Clone();
            pose.Valid = source.Valid;
            return pose;
        }

        private static AnimationClip BuildClip(PackAnimData data, string operatorKey)
        {
            if (data.Positions.Length < 2) return null;
            string clipPath = AnimRoot + "/" + operatorKey + "_" + data.Canonical + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            bool existing = clip != null;
            if (!existing)
            {
                clip = new AnimationClip();
                clip.name = operatorKey + "_" + data.Canonical;
                AssetDatabase.CreateAsset(clip, clipPath);
            }
            clip.frameRate = data.Rate > 0 ? data.Rate : 24;
            clip.legacy = true;
            clip.wrapMode = data.Loop ? WrapMode.Loop : WrapMode.ClampForever;

            int frameCount = data.Positions.Length;
            int partCount = data.Positions[0].Length;

            for (int p = 0; p < partCount; p++)
            {
                string path = data.PartRoles != null && p < data.PartRoles.Length ? data.PartRoles[p] : null;
                if (string.IsNullOrEmpty(path)) continue;

                AnimationCurve px = new AnimationCurve();
                AnimationCurve py = new AnimationCurve();
                AnimationCurve pz = new AnimationCurve();
                AnimationCurve rx = new AnimationCurve();
                AnimationCurve ry = new AnimationCurve();
                AnimationCurve rz = new AnimationCurve();
                AnimationCurve rw = new AnimationCurve();
                Quaternion previous = Quaternion.identity;
                for (int f = 0; f < frameCount; f++)
                {
                    float time = f / clip.frameRate;
                    Vector3 position = data.Positions[f][p];
                    Quaternion rotation = data.Rotations[f][p];
                    if (f > 0 && Quaternion.Dot(previous, rotation) < 0f)
                        rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
                    previous = rotation;
                    px.AddKey(time, position.x);
                    py.AddKey(time, position.y);
                    pz.AddKey(time, position.z);
                    rx.AddKey(time, rotation.x);
                    ry.AddKey(time, rotation.y);
                    rz.AddKey(time, rotation.z);
                    rw.AddKey(time, rotation.w);
                }
                SmoothTangents(px); SmoothTangents(py); SmoothTangents(pz);
                SmoothTangents(rx); SmoothTangents(ry); SmoothTangents(rz); SmoothTangents(rw);
                clip.SetCurve(path, typeof(Transform), "localPosition.x", px);
                clip.SetCurve(path, typeof(Transform), "localPosition.y", py);
                clip.SetCurve(path, typeof(Transform), "localPosition.z", pz);
                clip.SetCurve(path, typeof(Transform), "localRotation.x", rx);
                clip.SetCurve(path, typeof(Transform), "localRotation.y", ry);
                clip.SetCurve(path, typeof(Transform), "localRotation.z", rz);
                clip.SetCurve(path, typeof(Transform), "localRotation.w", rw);
            }
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static void SmoothTangents(AnimationCurve curve)
        {
            for (int i = 0; i < curve.length; i++)
                curve.SmoothTangents(i, 0f);
        }

        // --------------------------------------------------------------- operators
        /// <summary>Assigns imported operator prefabs to the matching CharacterDefinition assets.</summary>
        public static int WireOperators(ModelLibrary library)
        {
            string[] guids = AssetDatabase.FindAssets("t:CharacterDefinition");
            int wired = 0;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                CharacterDefinition definition = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path);
                if (definition == null) continue;
                string haystack = ((definition.Id ?? "") + " " + (definition.DisplayName ?? "") + " " +
                                   (definition.Callsign ?? "")).ToLowerInvariant();
                for (int e = 0; e < library.Entries.Count; e++)
                {
                    ModelEntry entry = library.Entries[e];
                    if (entry.Category != "character" || entry.Prefab == null) continue;
                    bool match = false;
                    if (entry.Tags != null)
                    {
                        for (int t = 0; t < entry.Tags.Length; t++)
                        {
                            string tag = entry.Tags[t];
                            if (tag == "character" || tag.Length < 4) continue;
                            if (haystack.Contains(tag)) { match = true; break; }
                        }
                    }
                    if (!match && haystack.Contains(entry.Key.ToLowerInvariant().Replace("operator_", "")))
                        match = true;
                    if (!match) continue;
                    definition.BodyPrefab = entry.Prefab;
                    definition.FactionColor = Color.white;
                    EditorUtility.SetDirty(definition);
                    wired++;
                    break;
                }
            }
            AssetDatabase.SaveAssets();
            return wired;
        }
    }
}
