"""Bake CC0 prop/weapon meshes (KayKit OBJ, Kenney GLB) into EXFIL packs."""
import math
import os
import shutil
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gltf_lib as gl
import pack_io as pk

DL = '/tmp/dl'
OUT = '/tmp/out'


# ------------------------------------------------------------------ OBJ input
def parse_obj(path):
    verts, uvs, norms = [], [], []
    tris = []
    faces = []          # list of [(vi, ti, ni), ...]
    for line in open(path, 'r', errors='ignore'):
        if line.startswith('v '):
            _, x, y, z = line.split()[:4]
            verts.append((float(x), float(y), float(z)))
        elif line.startswith('vt '):
            parts = line.split()[1:3]
            uvs.append((float(parts[0]), float(parts[1]) if len(parts) > 1 else 0.0))
        elif line.startswith('vn '):
            _, x, y, z = line.split()[:4]
            norms.append((float(x), float(y), float(z)))
        elif line.startswith('f '):
            pts = []
            for tok in line.split()[1:]:
                bits = tok.split('/')
                vi = int(bits[0])
                ti = int(bits[1]) if len(bits) > 1 and bits[1] else 0
                ni = int(bits[2]) if len(bits) > 2 and bits[2] else 0
                pts.append((vi - 1 if vi > 0 else len(verts) + vi,
                            ti - 1 if ti > 0 else -1,
                            ni - 1 if ni > 0 else -1))
            if len(pts) >= 3:
                for k in range(1, len(pts) - 1):
                    faces.append([pts[0], pts[k], pts[k + 1]])
    return verts, uvs, norms, faces


def mesh_from_faces(verts, uvs, norms, faces):
    remap = {}
    out_v, out_n, out_uv, out_t = [], [], [], []
    for face in faces:
        idx = []
        for (vi, ti, ni) in face:
            key = (vi, ti, ni)
            if key not in remap:
                remap[key] = len(out_v)
                out_v.append(verts[vi] if 0 <= vi < len(verts) else (0.0, 0.0, 0.0))
                out_uv.append(uvs[ti] if 0 <= ti < len(uvs) else (0.0, 0.0))
                out_n.append(norms[ni] if 0 <= ni < len(norms) else None)
            idx.append(remap[key])
        out_t.extend(idx)
    # generate missing normals
    for i in range(0, len(out_t), 3):
        tri = out_t[i:i + 3]
        if all(out_n[k] is not None for k in tri):
            continue
        a, b, c = (out_v[tri[0]], out_v[tri[1]], out_v[tri[2]])
        e1 = (b[0] - a[0], b[1] - a[1], b[2] - a[2])
        e2 = (c[0] - a[0], c[1] - a[1], c[2] - a[2])
        n = (e1[1] * e2[2] - e1[2] * e2[1], e1[2] * e2[0] - e1[0] * e2[2], e1[0] * e2[1] - e1[1] * e2[0])
        for k in tri:
            if out_n[k] is None:
                out_n[k] = n
    return out_v, out_n, out_uv, out_t


def obj_to_pack(path, key, tex_name):
    verts, uvs, norms, faces = parse_obj(path)
    ov, on, ouv, ot = mesh_from_faces(verts, uvs, norms, faces)
    uv_verts = [pk.to_unity_point(v) for v in ov]
    uv_norms = [pk.to_unity_normal(n or (0.0, 1.0, 0.0)) for n in on]
    uv_uvs = [(float(u[0]), float(u[1])) for u in ouv]
    tris = list(ot)
    tris = [tris[i + 2] if i % 3 == 0 else (tris[i - 1] if i % 3 == 1 else tris[i - 2])
            for i in range(len(tris))]
    pack = pk.Pack(key)
    pack.parts.append(('mesh', 'other', -1, (0.0, 0.0, 0.0), [(uv_verts, uv_norms, uv_uvs, tris)], ''))
    pack.bones = [('origin', 'root', (0.0, 0.0, 0.0), -1)]
    pack.texture = tex_name
    return pack, len(uv_verts), len(tris) // 3


# ----------------------------------------------------------------- glTF input
def gltf_to_pack(path, key, tex_name, scale=1.0):
    g = gl.Gltf(path)
    world, parents, local = g.node_world_matrices()
    pack = pk.Pack(key)
    verts_all, norms_all, uvs_all, tris_all = [], [], [], []
    for ni, node in enumerate(g.json.get('nodes', [])):
        if 'mesh' not in node:
            continue
        m = world[ni]
        for prim in g.json['meshes'][node['mesh']]['primitives']:
            attrs = prim['attributes']
            pos = g.accessor(attrs['POSITION'])
            nrm = g.accessor(attrs['NORMAL']) if 'NORMAL' in attrs else None
            uvs = g.accessor(attrs['TEXCOORD_0']) if 'TEXCOORD_0' in attrs else None
            idx = g.indices(prim['indices']) if 'indices' in prim else list(range(len(pos)))
            mat = None
            if 'material' in prim:
                mat = g.json['materials'][prim['material']]
            off, scl = gl.uv_transform(mat)
            base = len(verts_all)
            for vi in range(len(pos)):
                p = gl.mat_xform_point(m, pos[vi])
                verts_all.append(pk.to_unity_point((p[0] * scale, p[1] * scale, p[2] * scale)))
                n = gl.mat_xform_dir(m, nrm[vi]) if nrm else (0.0, 1.0, 0.0)
                ln = math.sqrt(sum(c * c for c in n)) or 1.0
                n = (n[0] / ln, n[1] / ln, n[2] / ln)
                norms_all.append(pk.to_unity_normal(n))
                uv = uvs[vi] if uvs else (0.0, 0.0)
                uvs_all.append(gl.apply_uv_transform(uv, off, scl))
            for t in range(0, len(idx), 3):
                tri = idx[t:t + 3]
                if len(tri) < 3:
                    continue
                tris_all.extend([base + tri[2], base + tri[1], base + tri[0]])
    pack.parts.append(('mesh', 'other', -1, (0.0, 0.0, 0.0), [(verts_all, norms_all, uvs_all, tris_all)], ''))
    pack.bones = [('origin', 'root', (0.0, 0.0, 0.0), -1)]
    pack.texture = tex_name
    return pack, len(verts_all), len(tris_all) // 3


def bounds_of(pack):
    lo = [1e9] * 3
    hi = [-1e9] * 3
    verts = 0
    tris = 0
    for (_n, _r, _p, _pv, meshes, _t) in pack.parts:
        for (v, _nn, _uv, t) in meshes:
            verts += len(v)
            tris += len(t) // 3
            for p in v:
                for i in range(3):
                    lo[i] = min(lo[i], p[i])
                    hi[i] = max(hi[i], p[i])
    return lo, hi, verts, tris


# --------------------------------------------------------------- copy helpers
def copy_texture(src, dst_dir, dst_name):
    os.makedirs(dst_dir, exist_ok=True)
    dst = os.path.join(dst_dir, dst_name)
    if not os.path.exists(dst):
        shutil.copyfile(src, dst)
    return dst_name


def find_colormap(glb_path):
    d = os.path.dirname(glb_path)
    for candidate in ('Textures/colormap.png', '../Textures/colormap.png', 'colormap.png',
                      '../models/Textures/colormap.png'):
        p = os.path.normpath(os.path.join(d, candidate))
        if os.path.exists(p):
            return p
    return None
