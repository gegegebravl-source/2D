#!/usr/bin/env python3
"""Static sanity checker for the EXFIL C# sources (no compiler in the sandbox).

Checks performed:
  1. every type we declare has a known base type (ours / Unity / BCL)
  2. "OurType.Member" and "variable.Member" accesses resolve to real members
  3. call arity for our own methods (Type.Method(args) / var.Method(args))
  4. "new OurType(...)" arity
"""
import os
import re
import sys
from collections import defaultdict

ROOT = '/home/user/2D/Assets/Scripts'

EXTERNAL_PREFIXES = (
    'UnityEngine.', 'UnityEditor.', 'System.', 'Unity.', 'TMPro.', 'UnityEngine.UI.',
)
EXTERNAL_TYPES = set("""
object string bool byte sbyte char decimal double float int uint long ulong short ushort void var dynamic
Vector2 Vector3 Vector4 Vector2Int Vector3Int Quaternion Matrix4x4 Color Color32 Rect Bounds Ray Ray2D
Transform GameObject Component MonoBehaviour ScriptableObject Object Behaviour Renderer MeshRenderer
MeshFilter Mesh SkinnedMeshRenderer Material Shader Texture Texture2D Sprite AudioClip AudioSource
Light Camera ParticleSystem ParticleSystemRenderer Collider BoxCollider SphereCollider CapsuleCollider
MeshCollider CharacterController Rigidbody Animation AnimationClip AnimationState AnimationCurve
WrapMode Keyframe PlayMode Animator NavMeshAgent NavMeshPath NavMeshHit Random Mathf Time Debug
Application SceneManager Scene Resources JsonUtility PlayerPrefs Screen Cursor LightType PrimitiveType
HideFlags LayerMask Physics RaycastHit Space Gizmos RectTransform Canvas CanvasScaler GraphicRaycaster
Image Button Text Slider Toggle ScrollRect EventSystem StandaloneInputModule LayoutElement
VerticalLayoutGroup HorizontalLayoutGroup ContentSizeFitter TextAnchor FontStyle Coroutine
WaitForSeconds WaitForEndOfFrame WaitForSecondsRealtime IEnumerator IEnumerable List Dictionary Queue
Stack HashSet SortedDictionary KeyValuePair Dictionary Exception StringBuilder Action Func Predicate
Tuple Array Enum Convert Math DateTime TimeSpan Guid Path File Directory Stream MemoryStream
BinaryReader BinaryWriter Encoding Serializable Tooltip Header Range TextArea SerializeField
HideInInspector RequireComponent DefaultExecutionOrder CreateAssetMenu EnumFlags Min Max ExecuteAlways
ExecuteInEditMode AddComponentMenu DisallowMultipleComponent IInteractable IList ICollection IDictionary
IDisposable UnityEvent GraphicsSettings AssetDatabase EditorUtility EditorApplication AssetImporter
TextureImporter PrefabUtility GameObjectUtility StaticEditorFlags MenuItem Selection Undo
EditorSceneManager NavMeshBuilder NavMeshBuildSettings Lightmapping RenderSettings QualitySettings
AmbientMode ShadowCastingMode IndexFormat MovementStance BodyPart BodyZone EquipmentSlot ItemType
ItemRarity WeaponClass AmmoCaliber FireMode ArmorClass ArmorMaterial AttachmentSlot SkillType Faction
RaidStatus RaidResult BotState StationKind PropertyAttribute INetBackend LocalNetBackend
RaycastHit SceneAsset MaterialProperty AudioListener Mask RectOffset NetworkManager NetworkBehaviour NetworkObject InvalidDataException InvalidOperationException EditorBuildSettingsScene AudioReverbZone EditorGUI EditorGUILayout GUILayout GUIStyle AssetPreview
""".split())

FILE_NAMESPACE = {}
CLASS_MEMBERS = {}     # simple name -> set(members)
CLASS_METHODS = {}     # simple name -> {method: param_count}
CLASS_BASES = {}       # simple name -> [bases]
CLASS_KIND = {}
CLASS_FILE = {}
NAMESPACES = set()


def strip_comments(text):
    text = re.sub(r'/\*.*?\*/', '', text, flags=re.S)
    text = re.sub(r'(?<!:)//[^\n]*', '', text)
    text = re.sub(r'"(?:[^"\\\n]|\\.)*"', '""', text)
    text = re.sub(r"'(?:[^'\\\n]|\\.)*'", "''", text)
    return text


def match_brace(text, start):
    depth = 0
    i = start
    while i < len(text):
        if text[i] == '{':
            depth += 1
        elif text[i] == '}':
            depth -= 1
            if depth == 0:
                return i
        i += 1
    return len(text) - 1


DECL_RE = re.compile(
    r'(?:^|[;{}\n])\s*(?:\[[^\]]*\]\s*)*(?:(?:public|private|protected|internal|static|readonly|const|virtual|override|'
    r'abstract|sealed|extern|new|async|partial|unsafe|volatile|event|params|in|out|ref)\s+)*'
    r'([A-Za-z_][\w\.<>,\[\]\? ]*?)\s+([A-Za-z_]\w*)\s*(?:<[^<>()]*>)?\s*'
    r'(\(|\{|=>|;|=|,)', re.M)

CTOR_RE = re.compile(r'(?:^|[;{}\n])\s*(?:(?:public|private|protected|internal|static)\s+)*([A-Za-z_]\w*)\s*\(')


def arity_range(params):
    """(min,max) argument counts, accounting for optional params and params arrays."""
    params = params.strip()
    if not params:
        return (0, 0)
    parts = []
    depth = 0
    current = ''
    for ch in params:
        if ch in '([{<':
            depth += 1
        elif ch in ')]}>':
            depth -= 1
        if ch == ',' and depth == 0:
            parts.append(current)
            current = ''
        else:
            current += ch
    parts.append(current)
    minimum = 0
    maximum = len(parts)
    for part in parts:
        part = part.strip()
        if '=' in part or part.startswith('params '):
            continue
        minimum += 1
    return (minimum, maximum)


def split_args(text):
    """Counts comma-separated arguments inside a balanced argument list text (no outer parens)."""
    text = text.strip()
    if not text:
        return 0
    depth = 0
    count = 1
    i = 0
    while i < len(text):
        ch = text[i]
        if ch in '([{':
            depth += 1
        elif ch in ')]}':
            depth -= 1
        elif ch == ',' and depth == 0:
            count += 1
        i += 1
    return count


def collect():
    files = []
    for root, _dirs, names in os.walk(ROOT):
        for name in names:
            if name.endswith('.cs'):
                files.append(os.path.join(root, name))
    for path in files:
        raw = open(path, encoding='utf-8').read()
        text = strip_comments(raw)
        ns = ''
        m = re.search(r'namespace\s+([\w\.]+)', text)
        if m:
            ns = m.group(1)
            NAMESPACES.add(ns)
        # find type declarations and their bodies
        spans = []
        for tm in re.finditer(r'\b(class|struct|interface|enum)\s+([A-Za-z_]\w*)', text):
            brace = text.find('{', tm.end())
            if brace < 0:
                continue
            end = match_brace(text, brace)
            spans.append((tm.group(2), tm.group(1), tm.start(), brace, end))
        for (name, kind, decl_start, brace, end) in spans:
            # ignore nested types inside another type's body that started earlier
            if any(s[2] < decl_start and s[4] > decl_start for s in spans):
                continue
            CLASS_KIND[name] = kind
            CLASS_FILE[name] = path
            body = text[brace + 1:end]
            # strip nested type bodies
            for (n2, _k2, _s2, b2, e2) in spans:
                if brace < b2 < end:
                    start_in_body = b2 - (brace + 1)
                    end_in_body = e2 - (brace + 1)
                    if 0 <= start_in_body < len(body) and end_in_body < len(body):
                        body = body[:start_in_body] + ' ' * (end_in_body - start_in_body + 1) + body[end_in_body + 1:]
            members = set()
            methods = {}
            depth_map = []
            depth = 0
            in_str = False
            for ch in body:
                if ch == '{':
                    depth += 1
                depth_map.append(depth)
                if ch == '}':
                    depth -= 1
                    depth_map[-1] = depth
            for dm in DECL_RE.finditer(body):
                members.add(dm.group(2))
                if dm.start() < len(depth_map) and depth_map[dm.start()] > 0:
                    continue
                if dm.group(3) == '(':
                    # parameter count
                    open_paren = brace + dm.end()
                    depth = 0
                    i = open_paren
                    while i < len(text):
                        if text[i] == '(':
                            depth += 1
                        elif text[i] == ')':
                            depth -= 1
                            if depth == 0:
                                break
                        i += 1
                    params = text[open_paren + 1:i]
                    low, high = arity_range(params)
                    methods.setdefault(dm.group(2), set()).update(range(low, high + 1))
            for (n2, k2, s2, b2, e2) in spans:
                if brace < s2 < end and k2 in ('class', 'struct', 'enum', 'interface'):
                    members.add(n2)
                    nested_body = text[b2 + 1:e2]
                    nested_members = set()
                    nested_methods = {}
                    for nm in DECL_RE.finditer(nested_body):
                        nested_members.add(nm.group(2))
                        if nm.group(3) == '(':
                            low, high = arity_range(nested_body[nm.end():nm.end() + 400].split(')')[0])
                            nested_methods.setdefault(nm.group(2), set()).update(range(low, high + 1))
                    CLASS_MEMBERS[n2] = nested_members
                    CLASS_METHODS[n2] = nested_methods
                    CLASS_KIND[n2] = k2
                    CLASS_FILE[n2] = path
                    CLASS_BASES.setdefault(n2, [])
            for cm in CTOR_RE.finditer(body):
                if cm.start() < len(depth_map) and depth_map[cm.start()] == 0:
                    members.add(cm.group(1))
            # properties (Allman brace on the next line is covered by DECL_RE "{" case)
            CLASS_MEMBERS[name] = members
            CLASS_METHODS[name] = methods
            tail = text[decl_start:brace]
            bases = re.findall(r'(?::|,)\s*([A-Za-z_][\w\.<>]*)', tail)
            CLASS_BASES[name] = [b for b in bases if b not in ('class', 'struct')][:8]
    return files


UNIVERSAL_MEMBERS = set("""
transform gameObject name tag enabled hideFlags isActiveAndEnabled GetComponent GetComponents
GetComponentInChildren GetComponentsInChildren GetComponentInParent GetComponentsInParent
StartCoroutine StopCoroutine StopAllCoroutines Invoke InvokeRepeating CancelInvoke CompareTag
SetActive activeSelf activeInHierarchy SendMessage BroadcastMessage AddComponent enabled
Instantiate Destroy DestroyImmediate FindObjectOfType FindObjectsOfType Equals GetHashCode GetType
ToString CompareTo GetInstanceID DontDestroyOnLoad SetParent position rotation localPosition
localRotation localScale Image Icon Sprite Text Clip ClipName Spawn SpawnData
""".split())


def members_of(name, depth=0, seen=None):
    if seen is None:
        seen = set()
    if depth > 8 or name in seen or name not in CLASS_MEMBERS:
        return set()
    seen.add(name)
    result = set(CLASS_MEMBERS[name])
    for base in CLASS_BASES.get(name, []):
        simple = base.split('<')[0].split('.')[-1]
        result |= members_of(simple, depth + 1, seen)
    return result


def methods_of(name, depth=0, seen=None):
    if seen is None:
        seen = set()
    if depth > 8 or name in seen or name not in CLASS_METHODS:
        return {}
    seen.add(name)
    result = dict(CLASS_METHODS[name])
    for base in CLASS_BASES.get(name, []):
        simple = base.split('<')[0].split('.')[-1]
        for key, value in methods_of(simple, depth + 1, seen).items():
            result.setdefault(key, value)
    return result


def call_arity(text, open_paren):
    depth = 0
    i = open_paren
    while i < len(text):
        if text[i] in '([{':
            depth += 1
        elif text[i] in ')]}':
            depth -= 1
            if depth == 0:
                break
        i += 1
    inner = text[open_paren + 1:i]
    if not inner.strip():
        return 0, i
    return split_args(inner), i


def main():
    files = collect()
    problems = []

    # 1. base types
    for name, bases in CLASS_BASES.items():
        for base in bases:
            simple = base.split('<')[0].split('.')[-1]
            if simple in CLASS_MEMBERS or simple in EXTERNAL_TYPES:
                continue
            if base.startswith(EXTERNAL_PREFIXES):
                continue
            if simple.startswith('I') and simple not in CLASS_MEMBERS:
                continue    # likely an interface we do not parse (Unity/etc.)
            problems.append('%s: unknown base "%s" (%s)' % (name, base, os.path.basename(CLASS_FILE.get(name, '?'))))

    # 2/3/4. member + arity checks
    for path in files:
        text = strip_comments(open(path, encoding='utf-8').read())
        base_name = os.path.basename(path)
        scoped = {}
        # fields and locals: "Type name =", "Type name;", parameters "(Type name," / ", Type name)"
        for m in re.finditer(r'\b([A-Z][A-Za-z0-9_]*)\s+([a-z_]\w*)\s*(?:[=;,)])', text):
            scoped.setdefault(m.group(2), set()).add(m.group(1))
        for m in re.finditer(r'\(\s*([A-Z][A-Za-z0-9_]*)\s+([a-z_]\w*)\s*[,)]', text):
            scoped.setdefault(m.group(2), set()).add(m.group(1))

        # static access Type.Member
        for m in re.finditer(r'\b([A-Z][A-Za-z0-9_]*)\.([A-Za-z_]\w*)(\s*\()?', text):
            tname, member, paren = m.group(1), m.group(2), m.group(3)
            if tname not in CLASS_MEMBERS or CLASS_KIND.get(tname) == 'enum':
                continue
            if member not in members_of(tname) and member not in UNIVERSAL_MEMBERS:
                problems.append('%s: %s.%s not found' % (base_name, tname, member))
                continue
            if paren:
                arity, _end = call_arity(text, m.end() - 1)
                expected = methods_of(tname).get(member)
                if expected and arity not in expected:
                    problems.append('%s: %s.%s arity mismatch, got %d, expected one of %s' %
                                    (base_name, tname, member, arity, sorted(expected)))

        # variable access
        for m in re.finditer(r'\b([a-z_]\w*)\.([A-Za-z_]\w*)(\s*\()?', text):
            var, member, paren = m.group(1), m.group(2), m.group(3)
            candidates = scoped.get(var)
            if not candidates or len(candidates) != 1:
                continue
            tname = next(iter(candidates))
            if tname not in CLASS_MEMBERS or CLASS_KIND.get(tname) == 'enum':
                continue
            if member not in members_of(tname) and member not in UNIVERSAL_MEMBERS:
                # any of the candidate types may be the right one
                ok = any(member in members_of(c) for c in scoped[var])
                if not ok:
                    problems.append('%s: %s (%s).%s not found' % (base_name, var, tname, member))
                continue
            if paren:
                arity, _end = call_arity(text, m.end() - 1)
                expected = methods_of(tname).get(member)
                if expected and arity not in expected:
                    ok = False
                    for c in scoped[var]:
                        candidate = methods_of(c).get(member)
                        if candidate and arity in candidate:
                            ok = True
                    if not ok:
                        problems.append('%s: %s (%s).%s arity mismatch, got %d, expected %s' %
                                        (base_name, var, tname, member, arity, sorted(expected)))

        # 4. constructor arity
        for m in re.finditer(r'\bnew\s+([A-Z][A-Za-z0-9_]*)\s*\(', text):
            tname = m.group(1)
            if tname not in CLASS_MEMBERS:
                continue
            arity, _end = call_arity(text, m.end() - 1)
            expected = CLASS_METHODS.get(tname, {}).get(tname)
            if expected and arity not in expected:
                problems.append('%s: new %s arity mismatch, got %d, expected %s' %
                                (base_name, tname, arity, sorted(expected)))

    # 5. type names used in generics / AddComponent / CreateInstance must exist
    GENERIC_CALLS = re.compile(r'\b(?:AddComponent|GetComponent|GetComponentInChildren|GetComponentInParent|'
                               r'FindObjectOfType|FindObjectsOfType|CreateInstance|LoadAssetAtPath|Load|'
                               r'Resources\.Load)<\s*([A-Z][A-Za-z0-9_\.]*)')
    for path in files:
        text = strip_comments(open(path, encoding='utf-8').read())
        for m in GENERIC_CALLS.finditer(text):
            tname = m.group(1)
            simple = tname.split('.')[-1]
            if simple in CLASS_MEMBERS or simple in EXTERNAL_TYPES or len(simple) < 3:
                continue
            problems.append('%s: unknown type in generic call: %s' % (os.path.basename(path), tname))

    # 6. "new OurType(...)" for names that look like our types but do not exist
    for path in files:
        text = strip_comments(open(path, encoding='utf-8').read())
        for m in re.finditer(r'\bnew\s+([A-Z][A-Za-z0-9_]{4,})\s*\(', text):
            tname = m.group(1)
            if tname in CLASS_MEMBERS or tname in EXTERNAL_TYPES:
                continue
            problems.append('%s: new unknown type %s' % (os.path.basename(path), tname))

    # 7. sibling namespace references need "using EXFIL;" when the file lives outside EXFIL.*
    SUBNAMESPACES = ['Core', 'Meta', 'UI', 'Characters', 'Items', 'Combat', 'Hideout', 'Raid',
                     'Progression', 'Economy', 'Art', 'AI', 'Player', 'Audio', 'Utils']
    for path in files:
        text = strip_comments(open(path, encoding='utf-8').read())
        ns_match = re.search(r'namespace\s+([\w\.]+)', text)
        ns = ns_match.group(1) if ns_match else ''
        if ns.startswith('EXFIL.'):
            continue
        has_using = re.search(r'^using\s+EXFIL\s*;', text, re.M) is not None
        if has_using:
            continue
        for sub in SUBNAMESPACES:
            if re.search(r'\b' + sub + r'\.[A-Z]', text):
                problems.append('%s: uses "%s." but has no "using EXFIL;"' % (os.path.basename(path), sub))

    # 8. bare *type-position* references to our types must be reachable
    TYPE_NS = {}
    for path in files:
        text = strip_comments(open(path, encoding='utf-8').read())
        ns_match = re.search(r'namespace\s+([\w\.]+)', text)
        ns = ns_match.group(1) if ns_match else ''
        for tm in re.finditer(r'\b(class|struct|interface|enum)\s+([A-Za-z_]\w*)', text):
            TYPE_NS.setdefault(tm.group(2), ns)

    TYPE_POSITION = [
        r'(?<![\w\.])new\s+%s\s*\(',
        r'\b(?:AddComponent|GetComponent|GetComponentInChildren|GetComponentInParent|FindObjectOfType|'
        r'FindObjectsOfType|CreateInstance|LoadAssetAtPath|Resources\.Load)<\s*%s\s*>',
        r'(?<![\w\.])%s\s+[A-Za-z_]\w*\s*(?:[=;,)\[])',    # Type name = ...
        r':\s*%s\b',                                          # base type / interface
        r'(?<![\w\.])(?:is|as|case)\s+%s\b',
        r'(?<![\w\.])%s\[\]',
        r'\(\s*%s\s*\)',
    ]
    NESTED_OWNER = {}
    for path in files:
        text = strip_comments(open(path, encoding='utf-8').read())
        for tm in re.finditer(r'\b(class|struct)\s+([A-Za-z_]\w*)', text):
            outer = tm.group(2)
            brace = text.find('{', tm.end())
            if brace < 0:
                continue
            end_b = match_brace(text, brace)
            for nm in re.finditer(r'\b(class|struct|interface|enum)\s+([A-Za-z_]\w*)', text[brace:end_b]):
                NESTED_OWNER.setdefault(nm.group(2), outer)

    for path in files:
        text = strip_comments(open(path, encoding='utf-8').read())
        ns_match = re.search(r'namespace\s+([\w\.]+)', text)
        ns = ns_match.group(1) if ns_match else ''
        usings = set(re.findall(r'^using\s+([\w\.]+)\s*;', text, re.M))
        for word, owner_ns in TYPE_NS.items():
            if owner_ns == ns:
                continue
            if word in NESTED_OWNER and owner_ns == ns:
                continue
            if owner_ns in usings:
                continue
            # enclosing namespace resolution (file in EXFIL.AI can use EXFIL types)
            if ns and owner_ns and (ns == owner_ns or ns.startswith(owner_ns + '.') or owner_ns.startswith(ns + '.')):
                continue
            if 'EXFIL' in usings and owner_ns.startswith('EXFIL.'):
                continue
            hit = False
            for pattern in TYPE_POSITION:
                if re.search(pattern % re.escape(word), text):
                    hit = True
                    break
            if hit:
                problems.append('%s: uses type %s (%s) without "using %s;"' %
                                (os.path.basename(path), word, owner_ns, owner_ns))

    seen = set()
    unique = []
    for p in problems:
        if p in seen:
            continue
        seen.add(p)
        unique.append(p)
    print('files: %d  types: %d  findings: %d' % (len(files), len(CLASS_MEMBERS), len(unique)))
    for p in unique:
        print('  -', p)
    return 0


if __name__ == '__main__':
    sys.exit(main())
