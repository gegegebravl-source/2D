"""Bake KayKit / Kenney skinned GLB characters into dependency-free EXFIL packs.

Splits every skinned mesh by its dominant joint, re-pivots each chunk on that
joint, and bakes the source animation clips (sampled) into per-part transforms.
Unity side (Editor/AssetPipeline.cs) turns the pack into Mesh + AnimationClip
assets with no third-party importer needed.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gltf_lib as gl
import pack_io as pk

# --------------------------------------------------------------- role mapping
ROLE_ALIASES = {
    'root': 'root',
    'hips': 'hips', 'pelvis': 'hips',
    'spine': 'spine', 'spine1': 'spine', 'spine2': 'chest', 'torso': 'spine',
    'chest': 'chest', 'upperchest': 'chest',
    'neck': 'neck',
    'head': 'head',
    'shoulder.l': 'shoulder_l', 'shoulder_l': 'shoulder_l', 'clavicle.l': 'shoulder_l',
    'shoulder.r': 'shoulder_r', 'shoulder_r': 'shoulder_r', 'clavicle.r': 'shoulder_r',
    'upperarm.l': 'upperarm_l', 'upperarm_l': 'upperarm_l', 'arm-left': 'upperarm_l', 'arm.l': 'upperarm_l',
    'upperarm.r': 'upperarm_r', 'upperarm_r': 'upperarm_r', 'arm-right': 'upperarm_r', 'arm.r': 'upperarm_r',
    'lowerarm.l': 'lowerarm_l', 'lowerarm_l': 'lowerarm_l', 'forearm.l': 'lowerarm_l', 'elbow.l': 'lowerarm_l',
    'lowerarm.r': 'lowerarm_r', 'lowerarm_r': 'lowerarm_r', 'forearm.r': 'lowerarm_r', 'elbow.r': 'lowerarm_r',
    'hand.l': 'hand_l', 'hand_l': 'hand_l', 'hand-left': 'hand_l', 'wrist.l': 'hand_l',
    'hand.r': 'hand_r', 'hand_r': 'hand_r', 'hand-right': 'hand_r', 'wrist.r': 'hand_r',
    'upperleg.l': 'upperleg_l', 'upperleg_l': 'upperleg_l', 'thigh.l': 'upperleg_l',
    'leg-left': 'upperleg_l', 'leg.l': 'upperleg_l', 'hip.l': 'upperleg_l',
    'upperleg.r': 'upperleg_r', 'upperleg_r': 'upperleg_r', 'thigh.r': 'upperleg_r',
    'leg-right': 'upperleg_r', 'leg.r': 'upperleg_r', 'hip.r': 'upperleg_r',
    'lowerleg.l': 'lowerleg_l', 'lowerleg_l': 'lowerleg_l', 'shin.l': 'lowerleg_l', 'knee.l': 'lowerleg_l',
    'lowerleg.r': 'lowerleg_r', 'lowerleg_r': 'lowerleg_r', 'shin.r': 'lowerleg_r', 'knee.r': 'lowerleg_r',
    'foot.l': 'foot_l', 'foot_l': 'foot_l', 'ankle.l': 'foot_l', 'foot-left': 'foot_l',
    'foot.r': 'foot_r', 'foot_r': 'foot_r', 'ankle.r': 'foot_r', 'foot-right': 'foot_r',
    'toe.l': 'toe_l', 'toe_l': 'toe_l',
    'toe.r': 'toe_r', 'toe_r': 'toe_r',
}
# roles that get their own rigid chunk (everything else is merged into the
# nearest mapped ancestor)
MESH_ROLES = [
    'hips', 'spine', 'chest', 'head',
    'upperarm_l', 'lowerarm_l', 'hand_l',
    'upperarm_r', 'lowerarm_r', 'hand_r',
    'upperleg_l', 'lowerleg_l', 'foot_l',
    'upperleg_r', 'lowerleg_r', 'foot_r',
]
BONE_ROLES = ['hips', 'spine', 'chest', 'neck', 'head',
              'upperarm_l', 'lowerarm_l', 'hand_l',
              'upperarm_r', 'lowerarm_r', 'hand_r',
              'upperleg_l', 'lowerleg_l', 'foot_l',
              'upperleg_r', 'lowerleg_r', 'foot_r']


def norm_name(n):
    n = n.strip().lower()
    for prefix in ('knight_', 'barbarian_', 'mage_', 'rogue_', 'roguehooded_', 'character-soldier_', 'prototypepete_'):
        if n.startswith(prefix):
            n = n[len(prefix):]
    n = n.replace('-mesh', '').replace('_mesh', '')
    return n


def role_of(name):
    n = norm_name(name)
    if n in ROLE_ALIASES:
        return ROLE_ALIASES[n]
    if n.endswith('.l') or n.endswith('_l') or n.endswith('-left'):
        base = n.rsplit('.', 1)[0].rsplit('_', 1)[0]
        key = base + '.l'
        if key in ROLE_ALIASES:
            return ROLE_ALIASES[key]
    if n.endswith('.r') or n.endswith('_r') or n.endswith('-right'):
        base = n.rsplit('.', 1)[0].rsplit('_', 1)[0]
        key = base + '.r'
        if key in ROLE_ALIASES:
            return ROLE_ALIASES[key]
    return None


# ---------------------------------------------------------------- diagnostics
def diagnose(path):
    g = gl.Gltf(path)
    world, parents, local = g.node_world_matrices()
    print('--- %s' % os.path.basename(path))
    print('    nodes=%d meshes=%d skins=%d anims=%d' % (
        len(g.json.get('nodes', [])), len(g.json.get('meshes', [])),
        len(g.json.get('skins', [])), len(g.json.get('animations', []))))
    joints = []
    for s in g.json.get('skins', []):
        joints = s['joints']
        print('    skin "%s": joints=%d ibm=%s' % (
            s.get('name'), len(joints), 'inverseBindMatrices' in s))
    for j in joints:
        print('      %-24s role=%-12s world=%s' % (
            g.node_name(j), role_of(g.node_name(j)),
            tuple(round(v, 3) for v in gl.mat_translation(world[j]))))
    # geometry: interpret vertices with and without skin matrix
    for ni, node in enumerate(g.json.get('nodes', [])):
        if 'mesh' not in node or 'skin' not in node:
            continue
        skin = g.json['skins'][node['skin']]
        jmats = [world[joints[k]] for k in range(len(skin['joints']))]
        ibms = None
        if 'inverseBindMatrices' in skin:
            raw = g.accessor(skin['inverseBindMatrices'])
            flat = [c for m in raw for c in m]
            ibms = [flat[k * 16:k * 16 + 16] for k in range(len(skin['joints']))]
        mesh = g.json['meshes'][node['mesh']]
        prim = mesh['primitives'][0]
        pos = g.accessor(prim['attributes']['POSITION'])
        joints_a = g.accessor(prim['attributes']['JOINTS_0'])
        weights_a = g.accessor(prim['attributes']['WEIGHTS_0'])
        raw_min = [min(p[i] for p in pos) for i in range(3)]
        raw_max = [max(p[i] for p in pos) for i in range(3)]
        sk_min = [1e9] * 3
        sk_max = [-1e9] * 3
        plain_min = [1e9] * 3
        plain_max = [-1e9] * 3
        for vi, p in enumerate(pos):
            acc = [0.0, 0.0, 0.0]
            jv = joints_a[vi]
            wv = weights_a[vi]
            tot = sum(wv) or 1.0
            for k in range(4):
                w = wv[k] / tot
                if w <= 0.0:
                    continue
                m = jmats[jv[k]] if ibms is None else gl.mat_mul(jmats[jv[k]], ibms[jv[k]])
                q = gl.mat_xform_point(m, p)
                acc[0] += q[0] * w
                acc[1] += q[1] * w
                acc[2] += q[2] * w
            for i in range(3):
                sk_min[i] = min(sk_min[i], acc[i])
                sk_max[i] = max(sk_max[i], acc[i])
                plain_min[i] = min(plain_min[i], p[i])
                plain_max[i] = max(plain_max[i], p[i])
        print('    mesh node "%s": verts=%d' % (g.node_name(ni), len(pos)))
        print('      raw   size=%s min_y=%.3f max_y=%.3f' % (
            tuple(round(raw_max[i] - raw_min[i], 3) for i in range(3)), raw_min[1], raw_max[1]))
        print('      skinned size=%s min_y=%.3f max_y=%.3f' % (
            tuple(round(sk_max[i] - sk_min[i], 3) for i in range(3)), sk_min[1], sk_max[1]))
    names = [a.get('name') for a in g.json.get('animations', [])]
    print('    animations (%d): %s' % (len(names), ', '.join(names)))


if __name__ == '__main__':
    for p in sys.argv[1:]:
        diagnose(p)
