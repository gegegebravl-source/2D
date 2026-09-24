"""Minimal dependency-free glTF/GLB reader + math used by the EXFIL asset pipeline.

Pure python3, no numpy. Supports: GLB containers, external/data-uri buffers,
accessors with byteStride, TRS + matrix nodes, skins (identity IBM fallback).
"""
import base64
import json
import math
import os
import struct
import urllib.parse

COMP_SIZE = {5120: 1, 5121: 1, 5122: 2, 5123: 2, 5125: 4, 5126: 4}
COMP_FMT = {5120: 'b', 5121: 'B', 5122: 'h', 5123: 'H', 5125: 'I', 5126: 'f'}
NUM_COMP = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT2': 4, 'MAT3': 9, 'MAT4': 16}


# ---------------------------------------------------------------- 4x4 matrices
def mat_identity():
    return [1.0, 0, 0, 0, 0, 1.0, 0, 0, 0, 0, 1.0, 0, 0, 0, 0, 1.0]


def mat_mul(a, b):
    """Column-major 4x4 multiply (glTF convention): result = a * b."""
    r = [0.0] * 16
    for c in range(4):
        for row in range(4):
            r[c * 4 + row] = (a[0 * 4 + row] * b[c * 4 + 0] + a[1 * 4 + row] * b[c * 4 + 1] +
                              a[2 * 4 + row] * b[c * 4 + 2] + a[3 * 4 + row] * b[c * 4 + 3])
    return r


def mat_translation(m):
    return (m[12], m[13], m[14])


def mat_from_trs(t, r, s):
    """glTF quaternion (x,y,z,w) + scale -> column-major matrix."""
    x, y, z, w = r
    sx, sy, sz = s
    xx, yy, zz = x * x, y * y, z * z
    xy, xz, yz = x * y, x * z, y * z
    wx, wy, wz = w * x, w * y, w * z
    m = mat_identity()
    m[0] = (1 - 2 * (yy + zz)) * sx
    m[1] = (2 * (xy + wz)) * sx
    m[2] = (2 * (xz - wy)) * sx
    m[4] = (2 * (xy - wz)) * sy
    m[5] = (1 - 2 * (xx + zz)) * sy
    m[6] = (2 * (yz + wx)) * sy
    m[8] = (2 * (xz + wy)) * sz
    m[9] = (2 * (yz - wx)) * sz
    m[10] = (1 - 2 * (xx + yy)) * sz
    m[12], m[13], m[14] = t
    return m


def mat_xform_point(m, p):
    x, y, z = p
    return (m[0] * x + m[4] * y + m[8] * z + m[12],
            m[1] * x + m[5] * y + m[9] * z + m[13],
            m[2] * x + m[6] * y + m[10] * z + m[14])


def mat_xform_dir(m, p):
    x, y, z = p
    return (m[0] * x + m[4] * y + m[8] * z,
            m[1] * x + m[5] * y + m[9] * z,
            m[2] * x + m[6] * y + m[10] * z)


def mat_rot_quat(m):
    """Rotation part of a matrix -> quaternion (x, y, z, w)."""
    m00, m11, m22 = m[0], m[5], m[10]
    tr = m00 + m11 + m22
    if tr > 0:
        s = math.sqrt(tr + 1.0) * 2.0
        w = 0.25 * s
        x = (m[6] - m[9]) / s
        y = (m[8] - m[2]) / s
        z = (m[1] - m[4]) / s
    elif m00 > m11 and m00 > m22:
        s = math.sqrt(1.0 + m00 - m11 - m22) * 2.0
        w = (m[6] - m[9]) / s
        x = 0.25 * s
        y = (m[4] + m[1]) / s
        z = (m[8] + m[2]) / s
    elif m11 > m22:
        s = math.sqrt(1.0 + m11 - m00 - m22) * 2.0
        w = (m[8] - m[2]) / s
        x = (m[4] + m[1]) / s
        y = 0.25 * s
        z = (m[9] + m[6]) / s
    else:
        s = math.sqrt(1.0 + m22 - m00 - m11) * 2.0
        w = (m[1] - m[4]) / s
        x = (m[8] + m[2]) / s
        y = (m[9] + m[6]) / s
        z = 0.25 * s
    n = math.sqrt(x * x + y * y + z * z + w * w) or 1.0
    return (x / n, y / n, z / n, w / n)


def quat_inv(q):
    x, y, z, w = q
    return (-x, -y, -z, w)


def quat_mul(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (aw * bx + ax * bw + ay * bz - az * by,
            aw * by - ax * bz + ay * bw + az * bx,
            aw * bz + ax * by - ay * bx + az * bw,
            aw * bw - ax * bx - ay * by - az * bz)


def quat_slerp(a, b, t):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    dot = ax * bx + ay * by + az * bz + aw * bw
    if dot < 0.0:
        bx, by, bz, bw, dot = -bx, -by, -bz, -bw, -dot
    if dot > 0.9995:
        r = (ax + (bx - ax) * t, ay + (by - ay) * t, az + (bz - az) * t, aw + (bw - aw) * t)
        n = math.sqrt(sum(c * c for c in r)) or 1.0
        return tuple(c / n for c in r)
    th0 = math.acos(max(-1.0, min(1.0, dot)))
    th = th0 * t
    s0 = math.sin(th0 - th) / math.sin(th0)
    s1 = math.sin(th) / math.sin(th0)
    return (ax * s0 + bx * s1, ay * s0 + by * s1, az * s0 + bz * s1, aw * s0 + bw * s1)


def quat_axis_angle(axis, angle):
    n = math.sqrt(sum(c * c for c in axis)) or 1.0
    x, y, z = axis[0] / n, axis[1] / n, axis[2] / n
    s = math.sin(angle * 0.5)
    return (x * s, y * s, z * s, math.cos(angle * 0.5))


def quat_to_euler_deg(q):
    """Quaternion -> (pitch, yaw, roll) degrees, Unity ZXY order."""
    x, y, z, w = q
    # roll (z)
    sy = 2 * (w * y + x * z)
    sy = max(-1.0, min(1.0, sy))
    pitch = math.asin(sy)
    # yaw (y)
    yaw = math.atan2(2 * (w * x - y * z), 1 - 2 * (x * x + y * y))
    roll = math.atan2(2 * (w * z - x * y), 1 - 2 * (y * y + z * z))
    return (math.degrees(pitch), math.degrees(yaw), math.degrees(roll))


# --------------------------------------------------------------------- document
class Gltf(object):
    def __init__(self, path):
        self.path = path
        self.dir = os.path.dirname(os.path.abspath(path))
        data = open(path, 'rb').read()
        self.bin_chunk = None
        if data[:4] == b'glTF':
            off = 12
            while off < len(data):
                clen, ctype = struct.unpack('<II', data[off:off + 8])
                off += 8
                chunk = data[off:off + clen]
                off += clen
                if ctype == 0x4E4F534A:
                    self.json = json.loads(chunk.decode('utf-8'))
                elif ctype == 0x004E4942:
                    self.bin_chunk = chunk
        else:
            with open(path, 'r', encoding='utf-8') as fh:
                self.json = json.load(fh)
        self._buffers = None

    # -- buffers ----------------------------------------------------------
    def buffer(self, index):
        if self._buffers is None:
            self._buffers = []
            for b in self.json.get('buffers', []):
                uri = b.get('uri')
                if uri is None:
                    self._buffers.append(self.bin_chunk)
                elif uri.startswith('data:'):
                    self._buffers.append(base64.b64decode(uri.split(',', 1)[1]))
                else:
                    with open(os.path.join(self.dir, urllib.parse.unquote(uri)), 'rb') as fh:
                        self._buffers.append(fh.read())
        return self._buffers[index]

    # -- accessors --------------------------------------------------------
    def accessor(self, index):
        """Return list of tuples (or floats for SCALAR)."""
        acc = self.json['accessors'][index]
        ncomp = NUM_COMP[acc['type']]
        ctype = acc['componentType']
        fmt = COMP_FMT[ctype]
        csize = COMP_SIZE[ctype]
        count = acc['count']
        out = []
        if 'bufferView' not in acc:
            zero = (0.0,) * ncomp if ncomp > 1 else 0.0
            return [zero] * count
        bv = self.json['bufferViews'][acc['bufferView']]
        raw = self.buffer(bv['buffer'])
        base = bv.get('byteOffset', 0) + acc.get('byteOffset', 0)
        stride = bv.get('byteStride') or csize * ncomp
        unpack = struct.Struct('<' + fmt * ncomp).unpack_from
        for i in range(count):
            vals = unpack(raw, base + i * stride)
            out.append(tuple(vals) if ncomp > 1 else vals[0])
        return out

    def indices(self, index):
        return [int(v) for v in self.accessor(index)]

    # -- nodes ------------------------------------------------------------
    def node_world_matrices(self):
        nodes = self.json.get('nodes', [])
        parents = {}
        for i, n in enumerate(nodes):
            for c in n.get('children', []):
                parents[c] = i
        local = []
        for n in nodes:
            if 'matrix' in n:
                local.append(list(n['matrix']))
            else:
                t = n.get('translation', [0.0, 0.0, 0.0])
                r = n.get('rotation', [0.0, 0.0, 0.0, 1.0])
                s = n.get('scale', [1.0, 1.0, 1.0])
                local.append(mat_from_trs(t, r, s))
        world = [None] * len(nodes)

        def resolve(i):
            if world[i] is None:
                if i in parents:
                    world[i] = mat_mul(resolve(parents[i]), local[i])
                else:
                    world[i] = local[i]
            return world[i]
        for i in range(len(nodes)):
            resolve(i)
        return world, parents, local

    def node_name(self, i):
        return self.json['nodes'][i].get('name', 'node_%d' % i)

    # -- images -----------------------------------------------------------
    def image_bytes(self, index):
        img = self.json['images'][index]
        if 'bufferView' in img:
            bv = self.json['bufferViews'][img['bufferView']]
            raw = self.buffer(bv['buffer'])
            off = bv.get('byteOffset', 0)
            return raw[off:off + bv['byteLength']]
        uri = img.get('uri', '')
        if uri.startswith('data:'):
            return base64.b64decode(uri.split(',', 1)[1])
        with open(os.path.join(self.dir, urllib.parse.unquote(uri)), 'rb') as fh:
            return fh.read()

    def image_name(self, index):
        img = self.json['images'][index]
        if img.get('name'):
            return img['name']
        uri = img.get('uri', 'texture')
        return os.path.splitext(os.path.basename(uri))[0]


def uv_transform(material):
    """KHR_texture_transform -> (offset, scale, rotation) applied to UV."""
    if not material:
        return ((0.0, 0.0), (1.0, 1.0))
    pbr = material.get('pbrMetallicRoughness', {})
    for src in (pbr.get('baseColorTexture'), material.get('normalTexture'), material.get('emissiveTexture')):
        if src and 'extensions' in src:
            tr = src['extensions'].get('KHR_texture_transform')
            if tr:
                return (tuple(tr.get('offset', (0.0, 0.0))), tuple(tr.get('scale', (1.0, 1.0))))
    return ((0.0, 0.0), (1.0, 1.0))


def apply_uv_transform(uv, offset, scale):
    return (uv[0] * scale[0] + offset[0], uv[1] * scale[1] + offset[1])
