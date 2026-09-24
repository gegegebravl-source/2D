using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace EXFILEditor
{
    public class PackMeshData
    {
        public Vector3[] Verts;
        public Vector3[] Normals;
        public Vector2[] UVs;
        public int[] Tris;
        public Bounds Bounds;
    }

    public class PackPartData
    {
        public string Name;
        public string Role;
        public int Parent;
        public Vector3 Pivot;
        public List<PackMeshData> Meshes = new List<PackMeshData>();
    }

    public class PackBoneData
    {
        public string Name;
        public string Role;
        public Vector3 Position;
        public int Parent;
    }

    public class PackAnimData
    {
        public string Name;            // "canonical|SourceName"
        public string Canonical;
        public string SourceName;
        public float Duration;
        public int Rate;
        public string[] PartRoles;
        public Vector3[][] Positions;
        public Quaternion[][] Rotations;

        public bool Loop
        {
            get
            {
                switch (Canonical)
                {
                    case "idle":
                    case "walk":
                    case "walk_b":
                    case "run":
                    case "walk_back":
                    case "aim_1h":
                    case "aim_2h":
                    case "crouch":
                    case "sit":
                        return true;
                    default:
                        return false;
                }
            }
        }
    }

    /// <summary>In-memory view of one .expak file (see Tools/pack_io.py for the writer).</summary>
    public class PackData
    {
        public string Name;
        public int Flags;
        public string Texture;
        public List<PackPartData> Parts = new List<PackPartData>();
        public List<PackBoneData> Bones = new List<PackBoneData>();
        public List<PackAnimData> Anims = new List<PackAnimData>();

        public bool IsCharacter { get { return (Flags & 2) != 0; } }

        public Bounds ComputeBounds()
        {
            bool any = false;
            Vector3 min = Vector3.zero;
            Vector3 max = Vector3.zero;
            for (int p = 0; p < Parts.Count; p++)
            {
                PackPartData part = Parts[p];
                for (int m = 0; m < part.Meshes.Count; m++)
                {
                    Vector3[] verts = part.Meshes[m].Verts;
                    for (int i = 0; i < verts.Length; i++)
                    {
                        Vector3 v = verts[i] + part.Pivot;
                        if (!any)
                        {
                            min = v;
                            max = v;
                            any = true;
                        }
                        else
                        {
                            min = Vector3.Min(min, v);
                            max = Vector3.Max(max, v);
                        }
                    }
                }
            }
            Bounds bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }
    }

    public static class ModelPack
    {
        public static PackData Read(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            using (MemoryStream stream = new MemoryStream(bytes))
            using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
            {
                string magic = new string(reader.ReadChars(8));
                if (magic != "EXPAK1\u0000\u0000")
                    throw new InvalidDataException("not an EXFIL pack: " + path + " (" + magic + ")");

                PackData pack = new PackData();
                pack.Name = ReadString(reader);
                pack.Flags = reader.ReadInt32();

                int partCount = reader.ReadInt32();
                for (int p = 0; p < partCount; p++)
                {
                    PackPartData part = new PackPartData();
                    part.Name = ReadString(reader);
                    part.Role = ReadString(reader);
                    part.Parent = reader.ReadInt32();
                    part.Pivot = ReadVector3(reader);
                    int meshCount = reader.ReadInt32();
                    for (int m = 0; m < meshCount; m++)
                    {
                        PackMeshData mesh = new PackMeshData();
                        int vertexCount = reader.ReadInt32();
                        int indexCount = reader.ReadInt32();
                        mesh.Verts = new Vector3[vertexCount];
                        for (int i = 0; i < vertexCount; i++) mesh.Verts[i] = ReadVector3(reader);
                        mesh.Normals = new Vector3[vertexCount];
                        for (int i = 0; i < vertexCount; i++) mesh.Normals[i] = ReadVector3(reader);
                        mesh.UVs = new Vector2[vertexCount];
                        for (int i = 0; i < vertexCount; i++) mesh.UVs[i] = ReadVector2(reader);
                        mesh.Tris = new int[indexCount];
                        for (int i = 0; i < indexCount; i++) mesh.Tris[i] = (int)reader.ReadUInt32();
                        mesh.Bounds = BuildBounds(mesh.Verts);
                        part.Meshes.Add(mesh);
                    }
                    pack.Parts.Add(part);
                }

                pack.Texture = ReadString(reader);

                int boneCount = reader.ReadInt32();
                for (int b = 0; b < boneCount; b++)
                {
                    PackBoneData bone = new PackBoneData();
                    bone.Name = ReadString(reader);
                    bone.Role = ReadString(reader);
                    bone.Position = ReadVector3(reader);
                    bone.Parent = reader.ReadInt32();
                    pack.Bones.Add(bone);
                }

                int animCount = reader.ReadInt32();
                for (int a = 0; a < animCount; a++)
                {
                    PackAnimData anim = new PackAnimData();
                    anim.Name = ReadString(reader);
                    anim.Duration = reader.ReadSingle();
                    anim.Rate = reader.ReadInt32();
                    int frameCount = reader.ReadInt32();
                    string[] split = anim.Name.Split('|');
                    anim.Canonical = split.Length > 0 ? split[0] : anim.Name;
                    anim.SourceName = split.Length > 1 ? split[1] : anim.Name;
                    anim.PartRoles = new string[partCount];
                    for (int p = 0; p < partCount; p++) anim.PartRoles[p] = pack.Parts[p].Role;
                    anim.Positions = new Vector3[frameCount][];
                    anim.Rotations = new Quaternion[frameCount][];
                    for (int f = 0; f < frameCount; f++)
                    {
                        anim.Positions[f] = new Vector3[partCount];
                        anim.Rotations[f] = new Quaternion[partCount];
                        for (int p = 0; p < partCount; p++)
                        {
                            anim.Positions[f][p] = ReadVector3(reader);
                            float x = reader.ReadSingle();
                            float y = reader.ReadSingle();
                            float z = reader.ReadSingle();
                            float w = reader.ReadSingle();
                            anim.Rotations[f][p] = new Quaternion(x, y, z, w);
                        }
                    }
                    pack.Anims.Add(anim);
                }
                return pack;
            }
        }

        private static Bounds BuildBounds(Vector3[] verts)
        {
            Bounds bounds = new Bounds();
            if (verts.Length == 0) { bounds.center = Vector3.zero; bounds.size = Vector3.zero; return bounds; }
            Vector3 min = verts[0];
            Vector3 max = verts[0];
            for (int i = 1; i < verts.Length; i++)
            {
                min = Vector3.Min(min, verts[i]);
                max = Vector3.Max(max, verts[i]);
            }
            bounds.SetMinMax(min, max);
            return bounds;
        }

        private static string ReadString(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length <= 0) return string.Empty;
            byte[] data = reader.ReadBytes(length);
            return Encoding.UTF8.GetString(data);
        }

        private static Vector3 ReadVector3(BinaryReader reader)
        {
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            float z = reader.ReadSingle();
            return new Vector3(x, y, z);
        }

        private static Vector2 ReadVector2(BinaryReader reader)
        {
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            return new Vector2(x, y);
        }
    }

    [Serializable]
    public class ManifestRecord
    {
        public string key;
        public string file;
        public string group;
        public string category;
        public string source;
        public string[] tags;
        public float[] size;
        public float[] min;
        public float[] max;
        public int verts;
        public int tris;
        public int bytes;
    }

    [Serializable]
    public class ManifestData
    {
        public string generated;
        public ManifestRecord[] models;
    }
}
