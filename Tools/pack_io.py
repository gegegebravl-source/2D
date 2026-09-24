"""EXFIL model pack (.expak) writer + glTF->Unity axis conversion.

Pack layout (little endian):
  char[8]  "EXPAK1\0\0"
  string   name
  int32    flags            (1 = has animations, 2 = character rig)
  int32    partCount
  repeated per part:
    string name, string role, int32 parentIndex, vec3 bindPivot
    int32 meshCount, then per mesh: int32 vertexCount, int32 indexCount,
        vec3[] positions, vec3[] normals, vec2[] uvs, uint32[] indices
  string   textureName
  int32    boneCount, then per bone: string name, string role, vec3 bindPos, int32 parentIndex
  int32    animCount, then per anim:
    string name, float duration, int32 sampleRate, int32 frameCount
    frames: frameCount x partCount x (vec3 pos, quat rot)
"""
import os
import struct

MAGIC = b'EXPAK1\x00\x00'

ROLE_ORDER = [
    'root', 'hips', 'spine', 'chest', 'neck', 'head',
    'shoulder_l', 'upperarm_l', 'lowerarm_l', 'hand_l', 'hand_l_index',
    'shoulder_r', 'upperarm_r', 'lowerarm_r', 'hand_r', 'hand_r_index',
    'upperleg_l', 'lowerleg_l', 'foot_l', 'toe_l',
    'upperleg_r', 'lowerleg_r', 'foot_r', 'toe_r',
    'other',
]


# ------------------------------------------------------------------ axis flip
def to_unity_point(p):
    """glTF/Blender right-handed Y-up  ->  Unity left-handed Y-up (mirror X)."""
    return (-p[0], p[1], p[2])


def to_unity_normal(n):
    return (-n[0], n[1], n[2])


def _mat_from_quat(q):
    x, y, z, w = q
    return [
        1 - 2 * (y * y + z * z), 2 * (x * y + z * w), 2 * (x * z - y * w), 0.0,
        2 * (x * y - z * w), 1 - 2 * (x * x + z * z), 2 * (y * z + x * w), 0.0,
        2 * (x * z + y * w), 2 * (y * z - x * w), 1 - 2 * (x * x + y * y), 0.0,
        0.0, 0.0, 0.0, 1.0,
    ]


def _quat_from_mat(m):
    m00, m01, m02 = m[0], m[4], m[8]
    m10, m11, m12 = m[1], m[5], m[9]
    m20, m21, m22 = m[2], m[6], m[10]
    tr = m00 + m11 + m22
    import math
    if tr > 0.0:
        s = math.sqrt(tr + 1.0) * 2.0
        w = 0.25 * s
        x = (m21 - m12) / s
        y = (m02 - m20) / s
        z = (m10 - m01) / s
    elif m00 > m11 and m00 > m22:
        s = math.sqrt(1.0 + m00 - m11 - m22) * 2.0
        w = (m21 - m12) / s
        x = 0.25 * s
        y = (m01 + m10) / s
        z = (m02 + m20) / s
    elif m11 > m22:
        s = math.sqrt(1.0 + m11 - m00 - m22) * 2.0
        w = (m02 - m20) / s
        x = (m01 + m10) / s
        y = 0.25 * s
        z = (m12 + m21) / s
    else:
        s = math.sqrt(1.0 + m22 - m00 - m11) * 2.0
        w = (m10 - m01) / s
        x = (m02 + m20) / s
        y = (m12 + m21) / s
        z = 0.25 * s
    n = math.sqrt(x * x + y * y + z * z + w * w) or 1.0
    return (x / n, y / n, z / n, w / n)


def to_unity_quat(q):
    """Change basis by mirroring X: R' = M R M  (keeps front faces consistent)."""
    m = _mat_from_quat(q)
    # M R M with M = diag(-1, 1, 1): negate row0/col0 off-diagonal terms
    mm = list(m)
    mm[4] = -mm[4]
    mm[8] = -mm[8]
    mm[1] = -mm[1]
    mm[2] = -mm[2]
    return _quat_from_mat(mm)


# ------------------------------------------------------------------ writing
def _w_str(fh, s):
    data = s.encode('utf-8')
    fh.write(struct.pack('<i', len(data)))
    fh.write(data)


def _w_vec3(fh, v):
    fh.write(struct.pack('<3f', v[0], v[1], v[2]))


def _w_quat(fh, q):
    fh.write(struct.pack('<4f', q[0], q[1], q[2], q[3]))


class Pack(object):
    def __init__(self, name):
        self.name = name
        self.parts = []      # (name, role, parentIndex, pivot, meshes, texture)
        self.bones = []      # (name, role, bindPos, parentIndex)
        self.anims = []      # (name, duration, rate, frames[list of list of (pos, quat)])
        self.texture = ''
        self.flags = 0

    def write(self, path):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, 'wb') as fh:
            fh.write(MAGIC)
            _w_str(fh, self.name)
            fh.write(struct.pack('<i', self.flags))
            fh.write(struct.pack('<i', len(self.parts)))
            for (pname, role, parent, pivot, meshes, _tex) in self.parts:
                _w_str(fh, pname)
                _w_str(fh, role)
                fh.write(struct.pack('<i', parent))
                _w_vec3(fh, pivot)
                fh.write(struct.pack('<i', len(meshes)))
                for (verts, normals, uvs, tris) in meshes:
                    fh.write(struct.pack('<ii', len(verts), len(tris)))
                    for v in verts:
                        _w_vec3(fh, v)
                    for n in normals:
                        _w_vec3(fh, n)
                    for uv in uvs:
                        fh.write(struct.pack('<2f', uv[0], uv[1]))
                    fh.write(struct.pack('<%dI' % len(tris), *tris))
            _w_str(fh, self.texture)
            fh.write(struct.pack('<i', len(self.bones)))
            for (bname, role, pos, parent) in self.bones:
                _w_str(fh, bname)
                _w_str(fh, role)
                _w_vec3(fh, pos)
                fh.write(struct.pack('<i', parent))
            fh.write(struct.pack('<i', len(self.anims)))
            for (aname, dur, rate, frames) in self.anims:
                _w_str(fh, aname)
                fh.write(struct.pack('<f', dur))
                fh.write(struct.pack('<i', rate))
                fh.write(struct.pack('<i', len(frames)))
                for frame in frames:
                    for (pos, rot) in frame:
                        _w_vec3(fh, pos)
                        _w_quat(fh, rot)


class PackReader(object):
    """Read back a pack (used by the validation harness)."""

    def __init__(self, path):
        self.fh = open(path, 'rb')

    def _r_str(self):
        n = struct.unpack('<i', self.fh.read(4))[0]
        return self.fh.read(n).decode('utf-8')

    def read(self):
        magic = self.fh.read(8)
        assert magic == MAGIC, 'bad magic %r' % magic
        name = self._r_str()
        flags = struct.unpack('<i', self.fh.read(4))[0]
        parts = []
        for _ in range(struct.unpack('<i', self.fh.read(4))[0]):
            pname = self._r_str()
            role = self._r_str()
            parent = struct.unpack('<i', self.fh.read(4))[0]
            pivot = struct.unpack('<3f', self.fh.read(12))
            meshes = []
            for _m in range(struct.unpack('<i', self.fh.read(4))[0]):
                vc, ic = struct.unpack('<ii', self.fh.read(8))
                verts = [struct.unpack('<3f', self.fh.read(12)) for _ in range(vc)]
                normals = [struct.unpack('<3f', self.fh.read(12)) for _ in range(vc)]
                uvs = [struct.unpack('<2f', self.fh.read(8)) for _ in range(vc)]
                tris = struct.unpack('<%dI' % ic, self.fh.read(4 * ic))
                meshes.append((verts, normals, uvs, tris))
            parts.append((pname, role, parent, pivot, meshes))
        tex = self._r_str()
        bones = []
        for _ in range(struct.unpack('<i', self.fh.read(4))[0]):
            bname = self._r_str()
            role = self._r_str()
            pos = struct.unpack('<3f', self.fh.read(12))
            parent = struct.unpack('<i', self.fh.read(4))[0]
            bones.append((bname, role, pos, parent))
        anims = []
        for _ in range(struct.unpack('<i', self.fh.read(4))[0]):
            aname = self._r_str()
            dur = struct.unpack('<f', self.fh.read(4))[0]
            rate = struct.unpack('<i', self.fh.read(4))[0]
            frames = struct.unpack('<i', self.fh.read(4))[0]
            data = []
            for _f in range(frames):
                fr = []
                for _p in range(len(parts)):
                    pos = struct.unpack('<3f', self.fh.read(12))
                    rot = struct.unpack('<4f', self.fh.read(16))
                    fr.append((pos, rot))
                data.append(fr)
            anims.append((aname, dur, rate, data))
        return dict(name=name, flags=flags, parts=parts, texture=tex, bones=bones, anims=anims)
