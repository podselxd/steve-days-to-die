"""Reads Minecraft's entity model geometry (createBodyLayer) out of the jar's bytecode via javap -c, by running the
builder calls (CubeListBuilder / PartDefinition / PartPose) in a tiny stack machine. Output: JSON per model."""
import subprocess, re, json, sys, os, math

JP = r"C:/Program Files/Eclipse Adoptium/jdk-8.0.492.9-hotspot/bin/javap.exe"
BASE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "mcm")
_cache = {}


def disasm(cls):
    if cls in _cache: return _cache[cls]
    path = os.path.join(BASE, cls + ".class")
    if not os.path.exists(path): _cache[cls] = None; return None
    out = subprocess.run([JP, "-c", "-p", path], capture_output=True, text=True).stdout
    methods = {}
    cur = None
    for line in out.splitlines():
        m = re.match(r"\s+(?:[\w.$<>\[\], ]+ )?([\w$<>]+)\(([^)]*)\)[^;]*;$", line)
        if m and not line.strip().startswith(("0:", "1:")) and "Code:" not in line and ": " not in line.strip()[:8]:
            cur = []
            methods.setdefault(m.group(1), []).append((m.group(2), cur))
            continue
        m = re.match(r"\s+(\d+): (\w+)\s*(.*)$", line)
        if m and cur is not None:
            cur.append((int(m.group(1)), m.group(2), m.group(3)))
    _cache[cls] = methods
    return methods


class Part:
    def __init__(s, name): s.name = name; s.cubes = []; s.pose = [0, 0, 0, 0, 0, 0]; s.kids = []
    def child(s, n):
        for k in s.kids:
            if k.name == n: return k
        return None
    def js(s): return {"name": s.name, "pose": s.pose, "cubes": s.cubes, "kids": [k.js() for k in s.kids]}


class Builder:
    def __init__(s): s.u = 0; s.v = 0; s.mirror = False; s.cubes = []


class Mesh:
    def __init__(s): s.root = Part("root")


def desc_args(d):
    a = re.match(r"\((.*)\)(.*)", d)
    args, i = [], 0
    s = a.group(1)
    while i < len(s):
        c = s[i]
        if c == "L": j = s.index(";", i); args.append(s[i + 1:j]); i = j + 1
        elif c == "[":
            j = i
            while s[j] == "[": j += 1
            if s[j] == "L": j = s.index(";", j)
            args.append("[" ); i = j + 1
        else: args.append(c); i += 1
    return args, a.group(2)


def run(cls, mname, args=(), depth=0, tex=None, warn=None):
    ms = disasm(cls)
    if not ms or mname not in ms: return None
    sig, code = ms[mname][0]
    loc = {}
    for i, a in enumerate(args): loc[i] = a
    st = []
    idx = {o: i for i, (o, _, _) in enumerate(code)}
    pc = 0; steps = 0
    while pc < len(code):
        off, op, rest = code[pc]; pc += 1; steps += 1
        if steps > 200000: warn.append("too many steps"); break
        if op.startswith("goto") or op.startswith("if"):
            tgt = idx.get(int(rest.split()[0]), len(code))
            if op.startswith("goto"): pc = tgt; continue
            def num(x): return x if isinstance(x, (int, float)) else (0 if x is None else 1)
            if "_icmp" in op or "_acmp" in op:
                b = st.pop(); a = st.pop()
                if "acmp" in op: a, b = id(a), id(b)
                a, b = num(a), num(b)
                cond = op.split("cmp")[1]
            elif op in ("ifnull", "ifnonnull"):
                a = st.pop(); b = None
                if (a is None) == (op == "ifnull"): pc = tgt
                continue
            else:
                a = num(st.pop()); b = 0; cond = op[2:]
            r = {"eq": a == b, "ne": a != b, "lt": a < b, "ge": a >= b, "gt": a > b, "le": a <= b}[cond]
            if r: pc = tgt
            continue
        if op == "iinc":
            v, k = [int(x) for x in rest.replace(",", " ").split()]
            loc[v] = (loc.get(v) or 0) + k; continue
        if op in ("fcmpl", "fcmpg", "dcmpl", "dcmpg", "lcmp"):
            b = st.pop() or 0; a = st.pop() or 0; st.append((a > b) - (a < b)); continue
        if op in ("irem", "frem"): b = st.pop(); a = st.pop(); st.append(a % b if b else 0); continue
        if op in ("aload", "fload", "iload", "dload"): st.append(loc.get(int(rest.split()[0]))); continue
        m = re.match(r"([afild])load_(\d)", op)
        if m: st.append(loc.get(int(m.group(2)))); continue
        if op in ("astore", "fstore", "istore", "dstore"): loc[int(rest.split()[0])] = st.pop(); continue
        m = re.match(r"([afild])store_(\d)", op)
        if m: loc[int(m.group(2))] = st.pop(); continue
        m = re.match(r"([fid])const_(m1|\d)", op)
        if m: st.append(-1 if m.group(2) == "m1" else int(m.group(2))); continue
        if op == "aconst_null": st.append(None); continue
        if op in ("bipush", "sipush"): st.append(int(rest)); continue
        if op in ("ldc", "ldc_w", "ldc2_w"):
            c = rest.split("//", 1)[1].strip()
            m = re.match(r"(float|double|int|long) (.*)", c)
            if m:
                v = m.group(2).rstrip("fdlL")
                st.append(float(v) if m.group(1) in ("float", "double") else int(v))
            elif c.startswith("String "): st.append(c[7:])
            else: st.append(None)
            continue
        if op in ("fadd", "dadd", "iadd"): b = st.pop(); a = st.pop(); st.append((a or 0) + (b or 0)); continue
        if op in ("fsub", "dsub", "isub"): b = st.pop(); a = st.pop(); st.append((a or 0) - (b or 0)); continue
        if op in ("fmul", "dmul", "imul"): b = st.pop(); a = st.pop(); st.append((a or 0) * (b or 0)); continue
        if op in ("fdiv", "ddiv", "idiv"): b = st.pop(); a = st.pop(); st.append((a or 0) / b if b else 0); continue
        if op in ("fneg", "dneg", "ineg"): st.append(-(st.pop() or 0)); continue
        if op in ("i2f", "f2d", "d2f", "i2d", "f2i", "d2i", "checkcast"):
            if op in ("f2i", "d2i"): st.append(int(st.pop() or 0))
            continue
        if op == "dup": st.append(st[-1]); continue
        if op == "pop": st.pop(); continue
        if op == "pop2": st.pop(); continue
        if op == "swap": st[-1], st[-2] = st[-2], st[-1]; continue
        if op == "new":
            c = rest.split("class ")[-1].strip()
            st.append({"new": c}); continue
        if op == "getstatic":
            f = rest.split("Field ")[-1]
            if "PartPose.ZERO" in f: st.append([0, 0, 0, 0, 0, 0])
            elif "CubeDeformation.NONE" in f: st.append(("def", 0.0))
            elif re.search(r":F$", f): st.append(0.0)
            else: st.append(None)
            continue
        if op == "putstatic" or op == "putfield": st.pop(); continue
        if op == "getfield": st.pop(); st.append(None); continue
        if op in ("areturn", "freturn", "ireturn"): return st.pop()
        if op == "return": return None
        if op.startswith("invoke"):
            m = re.search(r"Method ([\w/$]+)\.([\w$<>\"]+):(\([^ ]*)", rest)
            if not m:
                m0 = re.search(r"Method ([\w$<>\"]+):(\([^ ]*)", rest)
                if m0:
                    class _M:
                        def __init__(s, g): s.g = g
                        def group(s, i): return s.g[i - 1]
                    m = _M((cls, m0.group(1), m0.group(2)))
            if not m:
                m2 = re.search(r"InterfaceMethod ([\w/$]+)\.([\w$<>\"]+):(\([^ ]*)", rest)
                m = m2
            if not m:
                if "invokedynamic" in op:
                    d = re.search(r":(\([^ ]*)", rest)
                    if d:
                        a, r = desc_args(d.group(1))
                        vals = [st.pop() for _ in a][::-1]
                        st.append("part" + "_".join(str(int(x)) if isinstance(x, (int, float)) else str(x) for x in vals) if "makeConcat" in rest else None)
                continue
            owner, name, d = m.group(1), m.group(2).strip('"'), m.group(3)
            a, r = desc_args(d)
            av = [st.pop() for _ in a][::-1]
            recv = st.pop() if op != "invokestatic" else None
            res = None
            sname = owner.split("/")[-1]
            if name == "<init>":
                if isinstance(recv, dict):
                    if recv["new"].endswith("MeshDefinition"): recv.update({"mesh": Mesh()})
                    elif recv["new"].endswith("CubeDeformation"): recv.update({"def": av[0] if av else 0})
                continue
            if sname == "MeshDefinition" and name == "getRoot":
                res = recv["mesh"].root if isinstance(recv, dict) and "mesh" in recv else Part("root")
            elif sname == "CubeListBuilder" and name == "create": res = Builder()
            elif sname == "CubeListBuilder" and name == "texOffs": recv.u, recv.v = av[0], av[1]; res = recv
            elif sname == "CubeListBuilder" and name == "mirror":
                recv.mirror = bool(av[0]) if av else True; res = recv
            elif sname == "CubeListBuilder" and name == "addBox":
                nums = [x for x in av if isinstance(x, (int, float)) and not isinstance(x, bool)]
                infl = 0.0
                for x in av:
                    if isinstance(x, dict) and "def" in x: infl = x["def"] or 0
                    if isinstance(x, tuple) and x[0] == "def": infl = x[1]
                mir = recv.mirror
                if "Z" in a and isinstance(av[a.index("Z")], int): mir = bool(av[a.index("Z")])
                if a[0] == "java/lang/String": nums = nums  # named boxes: same numbers
                if len(nums) >= 6:
                    # addBox(String, x,y,z, int w,h,d, def, u, v) variant -> last two ints are texture offsets
                    u, v = recv.u, recv.v
                    if a[0] == "java/lang/String" and len(nums) >= 8: u, v = nums[-2], nums[-1]
                    recv.cubes.append({"u": u, "v": v, "box": [float(x) for x in nums[:6]], "inflate": float(infl), "mirror": mir})
                res = recv
            elif sname == "PartPose":
                if name == "offset": res = [av[0], av[1], av[2], 0, 0, 0]
                elif name == "offsetAndRotation": res = list(av[:6])
                elif name == "rotation": res = [0, 0, 0, av[0], av[1], av[2]]
                else: res = recv if recv is not None else (av[0] if av else [0] * 6)
            elif sname == "PartDefinition" and name == "addOrReplaceChild":
                p = Part(av[0])
                b = av[1]
                if isinstance(b, Builder): p.cubes = b.cubes
                pose = av[2] if isinstance(av[2], list) else [0] * 6
                p.pose = [float(x or 0) for x in pose]
                if isinstance(recv, Part):
                    old = recv.child(av[0])
                    if old: recv.kids.remove(old); p.kids = old.kids if not p.kids else p.kids
                    recv.kids.append(p)
                res = p
            elif sname == "PartDefinition" and name in ("getChild",):
                res = recv.child(av[0]) if isinstance(recv, Part) else None
                if res is None and isinstance(recv, Part): res = Part(av[0]); recv.kids.append(res)
            elif sname == "PartDefinition" and name == "clearChild":
                if isinstance(recv, Part):
                    c = recv.child(av[0])
                    if c: recv.kids.remove(c)
                res = None
            elif sname == "LayerDefinition" and name == "create":
                tex[0] = (av[1], av[2]); res = av[0]
            elif sname in ("Mth", "Math"):
                f = {"sin": math.sin, "cos": math.cos, "sqrt": math.sqrt, "toRadians": math.radians, "abs": abs}.get(name)
                res = f(av[0]) if f and av and isinstance(av[0], (int, float)) else (av[0] if av else 0)
            elif sname == "LayerDefinition" and name in ("apply",):
                res = recv
            elif owner.startswith("net/minecraft/client/model") and depth < 6:
                argv = ([recv] if recv is not None else []) + av
                res = run(owner, name, argv, depth + 1, tex, warn)
                if res is None and r != "V":
                    res = argv[0] if argv and isinstance(argv[0], dict) else None
            else:
                res = recv if (r.startswith("L") and recv is not None and isinstance(recv, (Part, Builder, dict))) else None
                if r.startswith("L") and res is None and av and isinstance(av[0], dict) and "mesh" in av[0]: res = av[0]
            if r != "V": st.append(res)
            continue
        if op in ("arraylength", "aaload", "iaload", "faload"):
            if op != "arraylength": st.pop()
            st.pop(); st.append(None); continue
        if op in ("aastore", "iastore", "fastore"): st.pop(); st.pop(); st.pop(); continue
        if op in ("anewarray", "newarray"): st.pop(); st.append(None); continue
        if op in ("athrow", "monitorenter", "monitorexit"): continue
        # unknown op: ignore
    return None


def model(cls, method="createBodyLayer", args=()):
    tex = [None]; warn = []
    res = run(cls, method, list(args), 0, tex, warn)
    mesh = res if isinstance(res, dict) and "mesh" in res else None
    if mesh is None: return None
    return {"tex": tex[0], "warn": sorted(set(warn)), "root": mesh["mesh"].root.js()}


if __name__ == "__main__":
    out = {}
    for spec in sys.argv[1:]:
        key, cls, *meth = spec.split("=")[0], spec.split("=")[1],
        parts = spec.split("=")
        key, cls = parts[0], parts[1]
        method = parts[2] if len(parts) > 2 else "createBodyLayer"
        out[key] = model(cls, method, [("def", 0.0)])
        print(key, "ok" if out[key] else "NONE", (out[key] or {}).get("tex"), (out[key] or {}).get("warn"), file=sys.stderr)
    json.dump(out, open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "models.json"), "w"), indent=1)
