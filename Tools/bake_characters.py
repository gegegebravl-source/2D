"""Bake skinned GLB characters into EXFIL packs: rigid bone chunks + real clips.

Verified assumptions (see diagnose()):
  * bind pose skin transform == raw vertex data, so chunks are cut from raw verts
  * every rig provides inverseBindMatrices, so clip baking uses bone world matrices
  * roles follow the Mixamo/KayKit naming used by both source packs
"""
import math
import os
import shutil
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gltf_lib as gl
import pack_io as pk

MESH_ROLES = ['hips', 'spine', 'chest', 'head',
              'upperarm_l', 'lowerarm_l', 'hand_l',
              'upperarm_r', 'lowerarm_r', 'hand_r',
              'upperleg_l', 'lowerleg_l', 'foot_l',
              'upperleg_r', 'lowerleg_r', 'foot_r']
# joints that exist only to be attached to (no own mesh chunk)
BONE_ROLES = ['root', 'hips', 'spine', 'chest', 'neck', 'head',
              'upperarm_l', 'lowerarm_l', 'hand_l',
              'upperarm_r', 'lowerarm_r', 'hand_r',
              'upperleg_l', 'lowerleg_l', 'foot_l',
              'upperleg_r', 'lowerleg_r', 'foot_r']
MESH_ROLE_FALLBACK = {'neck': 'head', 'toe_l': 'foot_l', 'toe_r': 'foot_r'}
FPS = 24

CLIPS = [
    # canonical, candidate source names (lower-case substrings, in priority order), loop
    ('idle', ['idle', 'unarmed_idle'], True),
    ('walk', ['walking_a', 'walk_a', 'walk'], True),
    ('walk_b', ['walking_b', 'walk_b'], True),
    ('run', ['running_a', 'sprint', 'run'], True),
    ('walk_back', ['walking_backwards', 'walk_back', 'walking_back'], True),
    ('jump', ['jump_start', 'jump'], False),
    ('land', ['jump_land', 'land'], False),
    ('crouch', ['crouch'], True),
    ('hit', ['hit_a', 'hit'], False),
    ('death', ['death_a', 'die'], False),
    ('aim_1h', ['1h_ranged_aiming', 'holding-right'], True),
    ('shoot_1h', ['1h_ranged_shoot', 'holding-right-shoot'], False),
    ('reload_1h', ['1h_ranged_reload', 'reload'], False),
    ('aim_2h', ['2h_ranged_aiming', 'holding-both'], True),
    ('shoot_2h', ['2h_ranged_shoot', 'holding-both-shoot'], False),
    ('reload_2h', ['2h_ranged_reload', 'reload'], False),
    ('melee', ['1h_melee_attack_slice_horizontal', 'attack-melee-right'], False),
    ('interact', ['interact', 'interact-right', 'use_item'], False),
    ('pickup', ['pick_up', 'pick-up'], False),
    ('sit', ['sit_chair_idle', 'sit'], True),
]
from build_characters import role_of, norm_name  # noqa: E402  (shared role tables)


# --------------------------------------------------------------- rig analysis
class Rig(object):
    def __init__(self, path):
        self.gltf = gl.Gltf(path)
        self.world, self.parents, self.local = self.gltf.node_world_matrices()
        skin = self.gltf.json['skins'][0]
        self.joints = list(skin['joints'])
        jset = set(self.joints)
        raw = self.gltf.accessor(skin['inverseBindMatrices'])
        flat = [c for m in raw for c in m]
        self.ibm = [flat[k * 16:k * 16 + 16] for k in range(len(self.joints))]
        # role per joint + resolution of unmapped joints to the nearest mapped ancestor
        self.role = {}
        for j in self.joints:
            self.role[j] = role_of(self.gltf.node_name(j)) or 'other'
        self.primary = {}
        for role in MESH_ROLES:
            candidates = [j for j in self.joints if self.effective_role(j) == role]
            if candidates:
                candidates.sort(key=lambda j: self.depth(j))
                self.primary[role] = candidates[0]
        self.joint_index = {j: k for k, j in enumerate(self.joints)}

    def depth(self, j):
        d = 0
        while j in self.parents and self.parents[j] in self.role:
            j = self.parents[j]
            d += 1
        return d

    def effective_role(self, j):
        """Role of the rigid chunk a joint's vertices belong to."""
        while True:
            r = self.role.get(j, 'other')
            if r in MESH_ROLES:
                return r
            r = MESH_ROLE_FALLBACK.get(r)
            if r:
                return r
            if j in self.parents and self.parents[j] in self.role:
                j = self.parents[j]
            else:
                return None

    def bone_parent(self, j):
        p = self.parents.get(j)
        while p is not None and p not in self.role:
            p = self.parents.get(p)
        return p

    # -- mesh splitting ---------------------------------------------------
    def build_parts(self):
        g = self.gltf
        chunks = {}   # role -> dict(verts, normals, uvs, tris)
        for ni, node in enumerate(g.json.get('nodes', [])):
            if 'mesh' not in node or 'skin' not in node:
                continue
            mesh_index = node['mesh']
            node_world = self.world[ni]
            for prim in g.json['meshes'][mesh_index]['primitives']:
                attrs = prim['attributes']
                pos = g.accessor(attrs['POSITION'])
                nrm = g.accessor(attrs['NORMAL']) if 'NORMAL' in attrs else [(0.0, 1.0, 0.0)] * len(pos)
                uvs = g.accessor(attrs['TEXCOORD_0']) if 'TEXCOORD_0' in attrs else [(0.0, 0.0)] * len(pos)
                joints_a = g.accessor(attrs['JOINTS_0'])
                weights_a = g.accessor(attrs['WEIGHTS_0'])
                idx = g.indices(prim['indices']) if 'indices' in prim else list(range(len(pos)))
                # skinned(bind) == raw, but be safe: transform through the node matrix
                remap = {}
                for vi in range(len(pos)):
                    w = weights_a[vi]
                    jv = joints_a[vi]
                    best = 0
                    if w[1] > w[best]:
                        best = 1
                    if w[2] > w[best]:
                        best = 2
                    if w[3] > w[best]:
                        best = 3
                    joint = self.joints[jv[best]]
                    role = self.effective_role(joint)
                    if role is None:
                        role = 'hips'
                    p = gl.mat_xform_point(node_world, pos[vi])
                    n = gl.mat_xform_dir(node_world, nrm[vi])
                    key = (role, vi)
                    if key not in remap:
                        ch = chunks.setdefault(role, {'verts': [], 'normals': [], 'uvs': [], 'tris': []})
                        remap[key] = len(ch['verts'])
                        ch['verts'].append(p)
                        ch['normals'].append(n)
                        ch['uvs'].append(uvs[vi])
                for t in range(0, len(idx), 3):
                    tri = idx[t:t + 3]
                    if len(tri) < 3:
                        continue
                    roles = set()
                    mapped = []
                    ok = True
                    for k in tri:
                        joint = self.joints[joints_a[k][0]]
                        role = self.effective_role(joint) or 'hips'
                        if (role, k) not in remap:
                            ok = False
                            break
                        roles.add(role)
                        mapped.append(remap[(role, k)])
                    if not ok or len(roles) != 1:
                        continue   # seam triangle: dropped (keeps chunks rigid)
                    chunks[roles.pop()]['tris'].extend(mapped)
        parts = []
        for role in MESH_ROLES:
            if role not in chunks:
                continue
            ch = chunks[role]
            joint = self.primary[role]
            pivot = gl.mat_translation(self.world[joint])
            local_verts = [(v[0] - pivot[0], v[1] - pivot[1], v[2] - pivot[2]) for v in ch['verts']]
            # mirror X so the pack is already in Unity's left-handed space
            u_verts = [pk.to_unity_point(v) for v in local_verts]
            u_normals = [pk.to_unity_normal(n) for n in ch['normals']]
            u_uvs = [(float(u[0]), float(u[1])) for u in ch['uvs']]
            tris = list(ch['tris'])
            tris = [tris[i + 2] if i % 3 == 0 else (tris[i - 1] if i % 3 == 1 else tris[i - 2])
                    for i in range(len(tris))]      # mirrored -> flip winding
            parts.append((role, joint, pk.to_unity_point(pivot), (u_verts, u_normals, u_uvs, tris)))
        return parts

    def build_bones(self):
        bones = []
        for j in self.joints:
            role = self.role.get(j, 'other')
            if role not in BONE_ROLES:
                continue
            pos = pk.to_unity_point(gl.mat_translation(self.world[j]))
            bones.append((j, role, self.gltf.node_name(j), pos, self.bone_parent(j)))
        # parent indices inside the emitted bone list (else -1)
        lookup = {b[0]: i for i, b in enumerate(bones)}
        out = []
        for (j, role, name, pos, parent) in bones:
            out.append((name, role, pos, lookup.get(parent, -1) if parent is not None else -1))
        return out


# --------------------------------------------------------------- anim baking
class ClipSampler(object):
    def __init__(self, gltf, anim_index, joints):
        self.gltf = gltf
        self.joints = joints
        anim = gltf.json['animations'][anim_index]
        self.tracks = {}
        self.duration = 0.0
        for ch in anim['channels']:
            target = ch['target']
            node = target['node']
            path = target['path']
            samp = anim['samplers'][ch['sampler']]
            times = gltf.accessor(samp['input'])
            values = gltf.accessor(samp['output'])
            interp = samp.get('interpolation', 'LINEAR')
            if interp == 'CUBICSPLINE':
                values = [values[i * 3 + 1] for i in range(len(times))]
                interp = 'LINEAR'
            self.duration = max(self.duration, times[-1])
            self.tracks.setdefault(node, {})[path] = (times, values, interp)

    def sample(self, t):
        """node -> (translation, rotation, scale) at time t."""
        out = {}
        for node, paths in self.tracks.items():
            base_t, base_r, base_s = (0.0, 0.0, 0.0), (0.0, 0.0, 0.0, 1.0), (1.0, 1.0, 1.0)
            if 'translation' in paths:
                base_t = self._interp(paths['translation'], t, 3)
            if 'rotation' in paths:
                base_r = self._interp_quat(paths['rotation'], t)
            if 'scale' in paths:
                base_s = self._interp(paths['scale'], t, 3)
            out[node] = (base_t, base_r, base_s)
        return out

    def _interp(self, track, t, n):
        times, values, interp = track
        if t <= times[0]:
            return values[0]
        if t >= times[-1]:
            return values[-1]
        i = 0
        lo, hi = 0, len(times) - 1
        while lo <= hi:
            mid = (lo + hi) // 2
            if times[mid] <= t:
                i = mid
                lo = mid + 1
            else:
                hi = mid - 1
        if interp == 'STEP':
            return values[i]
        t0, t1 = times[i], times[i + 1]
        a = 0.0 if t1 <= t0 else (t - t0) / (t1 - t0)
        v0, v1 = values[i], values[i + 1]
        return tuple(v0[k] + (v1[k] - v0[k]) * a for k in range(n))

    def _interp_quat(self, track, t):
        times, values, interp = track
        if t <= times[0]:
            return values[0]
        if t >= times[-1]:
            return values[-1]
        i = 0
        lo, hi = 0, len(times) - 1
        while lo <= hi:
            mid = (lo + hi) // 2
            if times[mid] <= t:
                i = mid
                lo = mid + 1
            else:
                hi = mid - 1
        t0, t1 = times[i], times[i + 1]
        a = 0.0 if t1 <= t0 else (t - t0) / (t1 - t0)
        if interp == 'STEP':
            return values[i]
        return gl.quat_slerp(values[i], values[i + 1], a)


def bake_animation(rig, sampler, parts, sample_rate=FPS):
    """Sample a clip into per-part (position, rotation) in Unity space."""
    g = rig.gltf
    frames = max(2, int(math.ceil(sampler.duration * sample_rate)) + 1)
    bind_world = rig.world
    # world matrices animated: nodes listed in the clip use sampled TRS
    all_nodes = list(range(len(g.json.get('nodes', []))))
    anim_nodes = set(sampler.tracks.keys())
    for j in rig.joints:
        anim_nodes.add(j)
    anim_nodes = sorted(anim_nodes)
    base_local = {n: (g.json['nodes'][n].get('translation', [0.0, 0.0, 0.0]),
                      g.json['nodes'][n].get('rotation', [0.0, 0.0, 0.0, 1.0]),
                      g.json['nodes'][n].get('scale', [1.0, 1.0, 1.0])) for n in all_nodes}
    data = []
    for f in range(frames):
        t = min(sampler.duration, f / float(sample_rate))
        sampled = sampler.sample(t)
        local = dict(base_local)
        for n, trs in sampled.items():
            local[n] = trs
        world_cache = {}

        def world_of(n):
            if n in world_cache:
                return world_cache[n]
            m = gl.mat_from_trs(local[n][0], local[n][1], local[n][2])
            p = rig.parents.get(n)
            res = m if p is None else gl.mat_mul(world_of(p), m)
            world_cache[n] = res
            return res
        for n in all_nodes:
            world_of(n)
        frame = []
        for (role, joint, pivot_u, _mesh) in parts:
            m = world_cache[joint]
            b = bind_world[joint]
            delta = gl.mat_mul(m, rigid_inverse(b))
            rot = gl.mat_rot_quat(delta)
            pos = gl.mat_translation(m)
            frame.append((pk.to_unity_point(pos), pk.to_unity_quat(rot)))
        data.append(frame)
    return data


def rigid_inverse(m):
    """Inverse of a rigid transform (rotation+translation, unit scale)."""
    # basis columns
    c0 = (m[0], m[1], m[2])
    c1 = (m[4], m[5], m[6])
    c2 = (m[8], m[9], m[10])
    t = (m[12], m[13], m[14])

    def n(v):
        l = math.sqrt(v[0] ** 2 + v[1] ** 2 + v[2] ** 2) or 1.0
        return (v[0] / l, v[1] / l, v[2] / l)
    c0, c1, c2 = n(c0), n(c1), n(c2)
    # inverse rotation = transpose of the orthonormal basis
    inv = [c0[0], c1[0], c2[0], 0.0,
           c0[1], c1[1], c2[1], 0.0,
           c0[2], c1[2], c2[2], 0.0,
           0.0, 0.0, 0.0, 1.0]
    tx = -(inv[0] * t[0] + inv[4] * t[1] + inv[8] * t[2])
    ty = -(inv[1] * t[0] + inv[5] * t[1] + inv[9] * t[2])
    tz = -(inv[2] * t[0] + inv[6] * t[1] + inv[10] * t[2])
    inv[12], inv[13], inv[14] = tx, ty, tz
    return inv


# ------------------------------------------------------------------- driver
def bake_character(src, out_dir, name, texture_src=None):
    rig = Rig(src)
    parts = rig.build_parts()
    bones = rig.build_bones()
    pack = pk.Pack(name)
    pack.flags = 1 | 2
    parent_of = {}
    for (role, joint, pivot_u, mesh) in parts:
        parent_role = None
        p = rig.bone_parent(joint)
        seen = 0
        while p is not None and seen < 32:
            r = rig.effective_role(p)
            if r in [x[0] for x in parts] and r != role:
                parent_role = r
                break
            p = rig.bone_parent(p)
            seen += 1
        parent_of[role] = parent_role
    role_index = {p[0]: i for i, p in enumerate(parts)}
    for (role, joint, pivot_u, mesh) in parts:
        pr = parent_of.get(role)
        pack.parts.append((role, role, role_index.get(pr, -1) if pr else -1, pivot_u, [mesh], ''))
    pack.bones = bones
    anims = rig.gltf.json.get('animations', [])
    for (canon, matches, loop) in CLIPS:
        idx = None
        for match in matches:
            for ai, a in enumerate(anims):
                if (a.get('name') or '').lower() == match:
                    idx = ai
                    break
            if idx is not None:
                break
        if idx is None:
            for match in matches:
                for ai, a in enumerate(anims):
                    if match in (a.get('name') or '').lower():
                        idx = ai
                        break
                if idx is not None:
                    break
        if idx is None:
            continue
        sampler = ClipSampler(rig.gltf, idx, rig.joints)
        data = bake_animation(rig, sampler, parts)
        orig = anims[idx].get('name')
        pack.anims.append(('%s|%s' % (canon, orig), sampler.duration, FPS, data))
    tex_name = ''
    if texture_src and os.path.exists(texture_src):
        tex_name = name + '_tex.png'
        os.makedirs(out_dir, exist_ok=True)
        shutil.copyfile(texture_src, os.path.join(out_dir, tex_name))
    pack.texture = tex_name
    out_path = os.path.join(out_dir, name + '.expak')
    pack.write(out_path)
    # statistics
    total_v = sum(len(m[0]) for (_n, _r, _p, _pv, ms, _t) in pack.parts for m in ms)
    total_t = sum(len(m[3]) // 3 for (_n, _r, _p, _pv, ms, _t) in pack.parts for m in ms)
    print('%-16s parts=%2d bones=%2d verts=%5d tris=%5d clips=%2d size=%6.1f KB  %s' % (
        name, len(pack.parts), len(pack.bones), total_v, total_t, len(pack.anims),
        os.path.getsize(out_path) / 1024.0, os.path.basename(src)))
    return pack
