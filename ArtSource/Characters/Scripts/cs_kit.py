"""Heavy v3 - modelling + look-dev toolkit (Blender 5.3, live session via MCP).

Everything is authored in metres, Z up, character faces -Y, character-left = +X.
Patches and pouches are authored facing -Y and placed with a matrix (see face_to).
Materials are procedural Cycles look-dev materials (Bevel + AO nodes); they must be baked for a game engine.
"""
import bpy, bmesh, math
from math import sin, cos, pi, radians, sqrt
from mathutils import Vector, Matrix, Euler


# ----------------------------------------------------------------------------- small helpers
def _lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def rgb(h):
    h = h.lstrip('#')
    return tuple(_lin(int(h[i:i + 2], 16) / 255) for i in (0, 2, 4)) + (1.0,)


def T(x, y=None, z=None):
    return Matrix.Translation(Vector(x) if y is None else Vector((x, y, z)))


def Rx(d): return Matrix.Rotation(radians(d), 4, 'X')
def Ry(d): return Matrix.Rotation(radians(d), 4, 'Y')
def Rz(d): return Matrix.Rotation(radians(d), 4, 'Z')


def face_to(direction, up=(0, 0, 1)):
    """Rotation taking the authoring frame (front -Y, up +Z) to: front = direction, up ~ up."""
    f = Vector(direction).normalized()
    u = Vector(up)
    u = u - f * u.dot(f)
    if u.length < 1e-5:
        u = Vector((0, 1, 0)) - f * f.y
    u.normalize()
    x = (-f).cross(u)          # local X
    return Matrix((x, -f, u)).transposed().to_4x4()


def smooth_path(pts, n=6):
    """Catmull-Rom resample."""
    pts = [Vector(p) for p in pts]
    if len(pts) < 3:
        return pts
    out = []
    P = [pts[0]] + pts + [pts[-1]]
    for i in range(1, len(P) - 2):
        p0, p1, p2, p3 = P[i - 1], P[i], P[i + 1], P[i + 2]
        for k in range(n):
            t = k / n
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t +
                              (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t))
    out.append(pts[-1])
    return out


# ----------------------------------------------------------------------------- primitives (temp bmesh)
def _bevel_sharp(t, offset, seg=2, angle=40.0):
    if offset <= 0:
        return
    es = [e for e in t.edges if len(e.link_faces) == 2 and e.calc_face_angle(0.0) > radians(angle)]
    if es:
        bmesh.ops.bevel(t, geom=es, offset=offset, offset_type='OFFSET', segments=seg, profile=0.6,
                        affect='EDGES', clamp_overlap=True)


def rbox(size, bevel=0.006, seg=2, puff=0.0, taper=(1.0, 1.0), cuts=0):
    """Box centred on the origin. puff>0 pillows it (soft pouches), taper scales the top (x, y)."""
    t = bmesh.new()
    bmesh.ops.create_cube(t, size=1.0)
    if puff > 0 or cuts:
        bmesh.ops.subdivide_edges(t, edges=t.edges[:], cuts=cuts or 3, use_grid_fill=True)
    for v in t.verts:
        p = v.co.copy()
        if puff > 0:
            p = p.lerp(p.normalized() * 0.60, puff)
        k = p.z + 0.5
        v.co = Vector((p.x * size[0] * (1 + (taper[0] - 1) * k), p.y * size[1] * (1 + (taper[1] - 1) * k), p.z * size[2]))
    bmesh.ops.recalc_face_normals(t, faces=t.faces[:])
    _bevel_sharp(t, min(bevel, min(size) * 0.4), seg)
    return t


def shell(fn, nu, nv, thick, bevel=0.004, seg=2):
    """Thick curved plate. fn(u, v) -> position for u, v in [-1, 1]; outward normal = dP/du x dP/dv."""
    t = bmesh.new()
    O, I = {}, {}
    e = 1e-3
    for i in range(nu + 1):
        for j in range(nv + 1):
            u, v = -1 + 2 * i / nu, -1 + 2 * j / nv
            p = Vector(fn(u, v))
            n = (Vector(fn(u + e, v)) - Vector(fn(u - e, v))).cross(Vector(fn(u, v + e)) - Vector(fn(u, v - e)))
            n = n.normalized() if n.length > 1e-12 else Vector((0, -1, 0))
            O[i, j] = t.verts.new(p)
            I[i, j] = t.verts.new(p - n * thick)
    for i in range(nu):
        for j in range(nv):
            t.faces.new((O[i, j], O[i + 1, j], O[i + 1, j + 1], O[i, j + 1]))
            t.faces.new((I[i, j + 1], I[i + 1, j + 1], I[i + 1, j], I[i, j]))
    for i in range(nu):
        t.faces.new((O[i, 0], I[i, 0], I[i + 1, 0], O[i + 1, 0]))
        t.faces.new((O[i + 1, nv], I[i + 1, nv], I[i, nv], O[i, nv]))
    for j in range(nv):
        t.faces.new((O[0, j + 1], I[0, j + 1], I[0, j], O[0, j]))
        t.faces.new((O[nu, j], I[nu, j], I[nu, j + 1], O[nu, j + 1]))
    bmesh.ops.recalc_face_normals(t, faces=t.faces[:])
    _bevel_sharp(t, min(bevel, thick * 0.45), seg, 50.0)
    return t


def _sq(u, v, p):
    """Square -> superellipse outline (p=2 circle, large p = square)."""
    m = max(abs(u), abs(v))
    if m < 1e-9:
        return 0.0, 0.0
    k = m / (abs(u) ** p + abs(v) ** p) ** (1.0 / p)
    return u * k, v * k


def plate(w, h, thick=0.012, dome=0.0, p=6.0, bend=0.0, nu=8, nv=8, bevel=0.004, cut=0.0):
    """Armour plate facing -Y. dome = bulge depth, bend = horizontal wrap radius (0 = flat), p = outline roundness,
    cut = chamfer of the four corners (0..0.5) for the angular plates in the concept."""
    def fn(u, v):
        a, b = _sq(u, v, p * 1.8)
        if cut > 0:
            s = abs(a) + abs(b)
            lim = 2.0 - cut * 2
            if s > lim:
                a, b = a * lim / s, b * lim / s
        x, z = a * w / 2, b * h / 2
        y = -dome * 0.6 * max(0.0, 1 - max(abs(a), abs(b)) ** 3)
        if bend > 0:
            ang = x / bend
            return (sin(ang) * (bend - y), bend - cos(ang) * (bend - y) , z)
        return (x, y, z)
    return shell(fn, nu, nv, thick, bevel)


def wrap(r, a0, a1, h, thick=0.012, r2=None, nu=12, nv=3, bevel=0.004):
    """Cylinder-wall plate around +Z: angles in degrees, 0 = front (-Y), positive toward +X."""
    r2 = r if r2 is None else r2
    def fn(u, v):
        a = radians(a0 + (a1 - a0) * (u + 1) / 2)
        rr = r + (r2 - r) * (v + 1) / 2
        return (rr * sin(a), -rr * cos(a), v * h / 2)
    return shell(fn, nu, nv, thick, bevel)


def tube(rings, seg=20, steps=None, fold=None, caps=True):
    """Vertical-ish tube. ring = (centre, rx, ry) or (centre, rx, ry_front, ry_back, power).
    steps: resample the ring list; fold = (amplitude, frequency per metre, phase) for cloth wrinkles."""
    R = []
    for r in rings:
        c = Vector(r[0])
        if len(r) == 3:
            R.append((c, r[1], r[2], r[2], 2.0))
        else:
            R.append((c, r[1], r[2], r[3], r[4]))
    if steps:
        out = []
        n = len(R) - 1
        for k in range(steps + 1):
            x = k / steps * n
            i = min(int(x), n - 1)
            f = x - i
            f = f * f * (3 - 2 * f) * 0.5 + f * 0.5
            a, b = R[i], R[i + 1]
            out.append((a[0].lerp(b[0], f),) + tuple(a[q] + (b[q] - a[q]) * f for q in range(1, 5)))
        R = out
    t = bmesh.new()
    loops = []
    for c, rx, rf, rb, p in R:
        e = 2.0 / p
        vs = []
        for i in range(seg):
            a = 2 * pi * i / seg
            ca, sa = cos(a), sin(a)
            x = math.copysign(abs(ca) ** e, ca) * rx
            y = math.copysign(abs(sa) ** e, sa) * (rb if sa >= 0 else rf)
            k = 1.0
            if fold:
                amp, freq, ph = fold
                k += 1.9 * amp * sin(freq * c.z * 2 * pi + 2.6 * sin(2 * a + ph) + ph) * (0.6 + 0.4 * sin(3 * a + ph * 2))
            vs.append(t.verts.new(c + Vector((x * k, y * k, 0))))
        loops.append(vs)
    for j in range(len(loops) - 1):
        a, b = loops[j], loops[j + 1]
        for i in range(seg):
            t.faces.new((a[i], a[(i + 1) % seg], b[(i + 1) % seg], b[i]))
    if caps:
        t.faces.new(loops[0])
        t.faces.new(loops[-1])
    bmesh.ops.recalc_face_normals(t, faces=t.faces[:])
    return t


def cyl(p0, p1, r, r2=None, seg=16, bevel=0.0):
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    t = bmesh.new()
    bmesh.ops.create_cone(t, cap_ends=True, cap_tris=False, segments=seg, radius1=r,
                          radius2=r if r2 is None else r2, depth=d.length)
    if bevel > 0:
        _bevel_sharp(t, bevel, 2, 60.0)
    M = T((p0 + p1) * 0.5) @ Vector((0, 0, 1)).rotation_difference(d.normalized()).to_matrix().to_4x4()
    bmesh.ops.transform(t, matrix=M, verts=t.verts[:])
    return t


def ball(radii, useg=20, vseg=12, cut=None):
    t = bmesh.new()
    bmesh.ops.create_uvsphere(t, u_segments=useg, v_segments=vseg, radius=1.0)
    if cut:
        bmesh.ops.delete(t, geom=[v for v in t.verts if cut(v.co)], context='VERTS')
    if isinstance(radii, (int, float)):
        radii = (radii,) * 3
    bmesh.ops.scale(t, vec=Vector(radii), verts=t.verts[:])
    return t


def sweep(pts, r=0.01, seg=10, rect=None, normal=None, ribs=0.0, closed=False, smooth=5):
    """Sweep a circle (r) or a rectangle rect=(width, thickness) along a path.
    normal: callable(point) -> outward direction (keeps straps flat on a body). ribs: radius ripple (hoses)."""
    P = smooth_path(pts, smooth) if smooth else [Vector(p) for p in pts]
    if closed:
        P = P[:-1] if (P[0] - P[-1]).length < 1e-6 else P
    n = len(P)
    t = bmesh.new()
    loops = []
    prev_n = None
    for i, p in enumerate(P):
        a = P[(i - 1) % n] if (closed or i > 0) else P[i]
        b = P[(i + 1) % n] if (closed or i < n - 1) else P[i]
        tan = (b - a).normalized()
        if normal is not None:
            nn = Vector(normal(p))
        elif prev_n is not None:
            nn = prev_n
        else:
            nn = Vector((0, 0, 1)) if abs(tan.z) < 0.9 else Vector((1, 0, 0))
        nn = (nn - tan * nn.dot(tan))
        nn = nn.normalized() if nn.length > 1e-6 else (prev_n or Vector((0, 0, 1)))
        prev_n = nn
        side = tan.cross(nn).normalized()
        vs = []
        if rect:
            w, th = rect[0] / 2, rect[1] / 2
            for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                vs.append(t.verts.new(p + side * (sx * w) + nn * (sy * th)))
        else:
            rr = r * (1 + ribs * (1 if i % 2 else -1) * 0.5) if ribs else r
            for k in range(seg):
                a2 = 2 * pi * k / seg
                vs.append(t.verts.new(p + side * (cos(a2) * rr) + nn * (sin(a2) * rr)))
        loops.append(vs)
    m = len(loops[0])
    last = len(loops) if closed else len(loops) - 1
    for j in range(last):
        a, b = loops[j], loops[(j + 1) % len(loops)]
        for i in range(m):
            t.faces.new((a[i], a[(i + 1) % m], b[(i + 1) % m], b[i]))
    if not closed:
        t.faces.new(loops[0])
        t.faces.new(loops[-1])
    bmesh.ops.recalc_face_normals(t, faces=t.faces[:])
    return t


def prism(pts2d, depth):
    """Extrude a 2D polygon (x, z) facing -Y by depth."""
    t = bmesh.new()
    f0 = [t.verts.new((x, 0, z)) for x, z in pts2d]
    f1 = [t.verts.new((x, -depth, z)) for x, z in pts2d]
    n = len(f0)
    t.faces.new(f0)
    t.faces.new(f1)
    for i in range(n):
        t.faces.new((f0[i], f0[(i + 1) % n], f1[(i + 1) % n], f1[i]))
    bmesh.ops.recalc_face_normals(t, faces=t.faces[:])
    return t


# ----------------------------------------------------------------------------- part = one mesh object
class Part:
    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.mats = []

    def mi(self, mat):
        if mat not in self.mats:
            self.mats.append(mat)
        return self.mats.index(mat)

    def add(self, t, mat, M=None, smooth=True):
        mi = self.mi(mat)
        M = Matrix.Identity(4) if M is None else M
        flip = M.determinant() < 0
        mp = {}
        for v in t.verts:
            mp[v] = self.bm.verts.new(M @ v.co)
        for f in t.faces:
            vs = [mp[v] for v in f.verts]
            if flip:
                vs.reverse()
            try:
                nf = self.bm.faces.new(vs)
            except ValueError:
                continue
            nf.material_index = mi
            nf.smooth = smooth
        t.free()
        return self

    # ---- composite details
    def bolt(self, pos, normal, mat, r=0.006, h=0.004):
        pos, n = Vector(pos), Vector(normal).normalized()
        self.add(cyl(pos - n * 0.03, pos + n * h, r, r * 0.8, seg=8), mat)

    def buckle(self, pos, normal, mat, w=0.034, h=0.022, up=(0, 0, 1)):
        M = T(pos) @ face_to(normal, up)
        self.add(rbox((w, 0.007, h), 0.002, 1), mat, M)
        self.add(rbox((w * 0.55, 0.006, h * 0.45), 0.0015, 1), mat, M @ T(0, -0.004, 0))

    def pouch(self, c, size, direction, body, flap, strap, metal, up=(0, 0, 1), flap_h=0.42, puff=0.12, decal=None):
        """Soft pouch with an overlapping flap, a closing strap and a buckle. Authored facing -Y."""
        w, d, h = size
        M = T(c) @ face_to(direction, up)
        self.add(rbox((w, d, h), 0.004, 2, puff=puff), body, M)
        fh = h * flap_h
        self.add(rbox((w * 1.05, d * 0.62, fh), 0.003, 2, puff=0.07), flap, M @ T(0, -d * 0.26, h / 2 - fh / 2 + 0.006))
        self.add(rbox((w * 1.06, d * 0.64, 0.006), 0.001, 1), strap, M @ T(0, -d * 0.262, h / 2 - fh + 0.012))
        self.add(rbox((w * 1.02, d * 1.0, 0.012), 0.003, 1, puff=0.15), flap, M @ T(0, -0.002, h / 2 + 0.002))
        for kz in (-0.14, -0.31):
            self.add(rbox((w * 0.9, 0.006, 0.017), 0.002, 1), strap, M @ T(0, -d * 0.50, h * kz))
        self.add(rbox((w * 0.22, 0.006, h * 0.62), 0.002, 1), strap, M @ T(0, -d * 0.60, -h * 0.02))
        self.buckle(M @ Vector((0, -d * 0.62, h * 0.10)), M.to_3x3() @ Vector((0, -1, 0)), metal, w * 0.30, 0.02,
                    up=M.to_3x3() @ Vector((0, 0, 1)))
        if decal:
            self.triangle(M @ Vector((0, -d * 0.60, -h * 0.20)), M.to_3x3() @ Vector((0, -1, 0)), 0.035, decal[0], decal[1],
                          up=M.to_3x3() @ Vector((0, 0, 1)))

    def triangle(self, pos, normal, size, m_out, m_in, up=(0, 0, 1)):
        """Raised hazard-triangle badge."""
        M = T(pos) @ face_to(normal, up)
        s = size
        self.add(prism([(-s * 0.58, -s * 0.33), (s * 0.58, -s * 0.33), (0, s * 0.67)], 0.0025), m_out, M, smooth=False)
        k = 0.55
        self.add(prism([(-s * 0.58 * k, -s * 0.33 * k), (s * 0.58 * k, -s * 0.33 * k), (0, s * 0.67 * k)], 0.0035), m_in, M,
                 smooth=False)

    def to_object(self, coll, sharp=42.0, subsurf=0):
        me = bpy.data.meshes.new(self.name)
        self.bm.normal_update()
        self.bm.to_mesh(me)
        self.bm.free()
        for m in self.mats:
            me.materials.append(m)
        try:
            me.set_sharp_from_angle(angle=radians(sharp))
        except Exception as e:
            print('sharp', e)
        ob = bpy.data.objects.new(self.name, me)
        coll.objects.link(ob)
        if subsurf:
            m = ob.modifiers.new('Subdivision', 'SUBSURF')
            m.levels = subsurf
            m.render_levels = subsurf
        return ob


def mirrored(ob, name, coll):
    """Copy of a mesh object mirrored across X (character left <-> right)."""
    me = ob.data.copy()
    me.name = name
    bm = bmesh.new()
    bm.from_mesh(me)
    for v in bm.verts:
        v.co.x = -v.co.x
    bmesh.ops.reverse_faces(bm, faces=bm.faces[:])
    bm.normal_update()
    bm.to_mesh(me)
    bm.free()
    try:
        me.set_sharp_from_angle(angle=radians(42.0))
    except Exception:
        pass
    o2 = bpy.data.objects.new(name, me)
    coll.objects.link(o2)
    for m in ob.modifiers:
        if m.type == 'SUBSURF':
            n = o2.modifiers.new(m.name, 'SUBSURF')
            n.levels, n.render_levels = m.levels, m.render_levels
        elif m.type == 'DISPLACE':
            n = o2.modifiers.new(m.name, 'DISPLACE')
            n.texture, n.texture_coords, n.strength, n.mid_level = m.texture, m.texture_coords, m.strength, m.mid_level
    return o2


# ----------------------------------------------------------------------------- materials
class NT:
    def __init__(self, mat):
        self.nt = mat.node_tree
        self.nodes = self.nt.nodes
        self.links = self.nt.links
        for n in list(self.nodes):
            self.nodes.remove(n)
        self.out = self.nodes.new('ShaderNodeOutputMaterial')
        self.bsdf = self.nodes.new('ShaderNodeBsdfPrincipled')
        self.links.new(self.bsdf.outputs[0], self.out.inputs[0])
        self.obj = self.nodes.new('ShaderNodeTexCoord').outputs['Object']

    def node(self, kind, **props):
        n = self.nodes.new(kind)
        for k, v in props.items():
            setattr(n, k, v)
        return n

    def put(self, inp, v):
        if isinstance(v, bpy.types.NodeSocket):
            self.links.new(v, inp)
        else:
            inp.default_value = v

    def math(self, op, a, b=None, clamp=False):
        n = self.node('ShaderNodeMath', operation=op, use_clamp=clamp)
        self.put(n.inputs[0], a)
        if b is not None:
            self.put(n.inputs[1], b)
        return n.outputs[0]

    def mixc(self, f, a, b):
        n = self.node('ShaderNodeMix', data_type='RGBA')
        self.put(n.inputs[0], f)
        self.put(n.inputs[6], a)
        self.put(n.inputs[7], b)
        return n.outputs[2]

    def mixf(self, f, a, b):
        n = self.node('ShaderNodeMix', data_type='FLOAT')
        self.put(n.inputs[0], f)
        self.put(n.inputs[2], a)
        self.put(n.inputs[3], b)
        return n.outputs[0]

    def vec(self, scale=(1, 1, 1), rot=(0, 0, 0)):
        n = self.node('ShaderNodeMapping')
        self.links.new(self.obj, n.inputs['Vector'])
        n.inputs['Scale'].default_value = scale
        n.inputs['Rotation'].default_value = [radians(a) for a in rot]
        return n.outputs[0]

    def noise(self, scale, detail=4.0, rough=0.55, vec=None):
        n = self.node('ShaderNodeTexNoise')
        self.links.new(vec or self.obj, n.inputs['Vector'])
        n.inputs['Scale'].default_value = scale
        n.inputs['Detail'].default_value = detail
        n.inputs['Roughness'].default_value = rough
        return n.outputs[0]

    def ramp(self, x, lo, hi):
        n = self.node('ShaderNodeMapRange', clamp=True)
        self.put(n.inputs[0], x)
        n.inputs[1].default_value, n.inputs[2].default_value = lo, hi
        return n.outputs[0]


def surface(name, col, rough=0.7, metal=0.0, wear=0.0, under=('8f9194', 1.0, 0.32), grime=0.35, dust=0.25,
            mott=None, rust=0.0, scratch=0.0, bump='fabric', bump_s=0.35, stripes=None, edge=0.006, sss=0.0, sheen=0.0):
    """Layered look-dev material: base (+mottling/stripes) -> scratches -> edge wear -> rust -> grime -> dust.
    mott = (hex, scale, amount); stripes = (hex2, period_m, (ax, ay, az)); under = (hex, metallic, roughness)."""
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
    N = NT(m)
    c = rgb(col)
    color = c
    r_val, m_val = rough, metal
    if mott:
        k = N.ramp(N.noise(mott[1], 3.0, 0.5), 0.5 - 0.06, 0.5 + 0.06)
        color = N.mixc(N.math('MULTIPLY', k, mott[2]), color, rgb(mott[0]))
        if len(mott) > 3:
            k2 = N.ramp(N.noise(mott[1] * 1.7, 2.0, 0.5, N.vec((1, 1, 1), (20, 40, 60))), 0.62, 0.70)
            color = N.mixc(N.math('MULTIPLY', k2, mott[2]), color, rgb(mott[3]))
    if stripes:
        dot = N.node('ShaderNodeVectorMath', operation='DOT_PRODUCT')
        N.links.new(N.obj, dot.inputs[0])
        dot.inputs[1].default_value = stripes[2]
        s = N.math('GREATER_THAN', N.math('FRACT', N.math('MULTIPLY', dot.outputs['Value'], 1.0 / stripes[1])), 0.5)
        color = N.mixc(s, color, rgb(stripes[0]))
    # tonal variation
    var = N.ramp(N.noise(7.0, 5.0, 0.6), 0.25, 0.8)
    color = N.mixc(var, N.mixc(1.0, color, (0.62, 0.62, 0.62, 1)) if False else color, color)
    dark = N.node('ShaderNodeMix', data_type='RGBA', blend_type='MULTIPLY')
    N.put(dark.inputs[0], 1.0)
    N.put(dark.inputs[6], color)
    tone = N.node('ShaderNodeCombineColor')
    tv = N.mixf(var, 0.72, 1.12)
    for i in range(3):
        N.links.new(tv, tone.inputs[i])
    N.links.new(tone.outputs[0], dark.inputs[7])
    color = dark.outputs[2]
    rough_s = N.mixf(N.noise(38.0, 4.0, 0.6), r_val - 0.13, min(1.0, r_val + 0.13))
    metal_s = m_val
    if scratch > 0:
        sc = N.ramp(N.noise(46.0, 2.0, 0.4, N.vec((1.0, 14.0, 1.0), (15, 25, 40))), 0.70, 0.73)
        sc2 = N.ramp(N.noise(60.0, 2.0, 0.4, N.vec((12.0, 1.0, 1.5), (-30, 10, 70))), 0.72, 0.75)
        s = N.math('MULTIPLY', N.math('MAXIMUM', sc, sc2), scratch, clamp=True)
        color = N.mixc(s, color, N.mixc(0.55, color, rgb(under[0])))
        rough_s = N.mixf(s, rough_s, 0.35)
    # geometry-driven masks (Cycles)
    geo = N.node('ShaderNodeNewGeometry')
    bev = N.node('ShaderNodeBevel', samples=4)
    bev.inputs['Radius'].default_value = edge
    dp = N.node('ShaderNodeVectorMath', operation='DOT_PRODUCT')
    N.links.new(bev.outputs[0], dp.inputs[0])
    N.links.new(geo.outputs['True Normal'], dp.inputs[1])
    edge_m = N.math('MULTIPLY', N.math('SUBTRACT', 1.0, dp.outputs['Value']), 9.0, clamp=True)
    if wear > 0:
        chips = N.noise(55.0, 6.0, 0.65)
        big = N.noise(9.0, 3.0, 0.5)
        w = N.math('ADD', N.math('MULTIPLY', edge_m, N.math('ADD', chips, 0.35)), N.math('MULTIPLY', N.ramp(big, 0.62, 0.74), 0.5 * wear))
        w = N.math('MULTIPLY', N.ramp(w, 0.42, 0.62), min(1.0, wear * 1.4), clamp=True)
        color = N.mixc(w, color, rgb(under[0]))
        metal_s = N.mixf(w, metal_s, under[1])
        rough_s = N.mixf(w, rough_s, under[2])
    ao = N.node('ShaderNodeAmbientOcclusion', samples=4)
    ao.inputs['Distance'].default_value = 0.07
    cav = N.math('SUBTRACT', 1.0, ao.outputs['AO'])
    if rust > 0:
        rn = N.ramp(N.noise(16.0, 6.0, 0.7), 0.50, 0.66)
        rm = N.math('MULTIPLY', N.math('ADD', N.math('MULTIPLY', cav, 1.6), N.math('MULTIPLY', rn, 0.75)), rust, clamp=True)
        rm = N.ramp(rm, 0.25, 0.60)
        rcol = N.mixc(N.noise(70.0, 3.0, 0.6), rgb('5a2c12'), rgb('a4561f'))
        color = N.mixc(rm, color, rcol)
        metal_s = N.mixf(rm, metal_s, 0.0)
        rough_s = N.mixf(rm, rough_s, 0.88)
    if grime > 0:
        g = N.math('MULTIPLY', N.math('MULTIPLY', cav, N.math('ADD', N.noise(11.0, 5.0, 0.6), 0.45)), 2.4 * grime, clamp=True)
        color = N.mixc(g, color, rgb('1c1712'))
        rough_s = N.mixf(g, rough_s, 0.92)
        metal_s = N.mixf(g, metal_s, 0.0) if not isinstance(metal_s, (int, float)) or metal_s > 0 else metal_s
    if dust > 0:
        sep = N.node('ShaderNodeSeparateXYZ')
        N.links.new(geo.outputs['Normal'], sep.inputs[0])
        up = N.ramp(sep.outputs['Z'], 0.15, 0.95)
        low = N.ramp(N.noise(4.0, 4.0, 0.6), 0.35, 0.75)
        d = N.math('MULTIPLY', N.math('ADD', N.math('MULTIPLY', up, 0.8), N.math('MULTIPLY', low, 0.45)),
                   N.math('MULTIPLY', N.ramp(N.noise(19.0, 6.0, 0.7), 0.3, 0.75), dust), clamp=True)
        color = N.mixc(d, color, rgb('8d7b62'))
        rough_s = N.mixf(d, rough_s, 0.95)
    b = N.bsdf
    N.put(b.inputs['Base Color'], color)
    N.put(b.inputs['Roughness'], rough_s)
    N.put(b.inputs['Metallic'], metal_s)
    if sss > 0:
        b.inputs['Subsurface Weight'].default_value = sss
        b.inputs['Subsurface Scale'].default_value = 0.008
    if sheen > 0:
        b.inputs['Sheen Weight'].default_value = sheen
        b.inputs['Sheen Roughness'].default_value = 0.6
    # bump
    if bump == 'fabric':
        wv = N.node('ShaderNodeTexWave', wave_type='BANDS', bands_direction='X')
        N.links.new(N.obj, wv.inputs['Vector'])
        wv.inputs['Scale'].default_value = 520.0
        wz = N.node('ShaderNodeTexWave', wave_type='BANDS', bands_direction='Z')
        N.links.new(N.obj, wz.inputs['Vector'])
        wz.inputs['Scale'].default_value = 520.0
        hgt = N.math('ADD', N.math('MULTIPLY', N.math('MULTIPLY', wv.outputs[0], wz.outputs[0]), 0.35),
                     N.math('MULTIPLY', N.noise(22.0, 5.0, 0.6), 2.2))
        dist = 0.009
    elif bump == 'leather':
        vo = N.node('ShaderNodeTexVoronoi', feature='DISTANCE_TO_EDGE')
        N.links.new(N.obj, vo.inputs['Vector'])
        vo.inputs['Scale'].default_value = 260.0
        hgt = N.math('ADD', N.math('MULTIPLY', N.ramp(vo.outputs['Distance'], 0.0, 0.12), 0.5), N.noise(40.0, 4.0, 0.6))
        dist = 0.0025
    elif bump == 'rubber':
        hgt = N.noise(180.0, 3.0, 0.6)
        dist = 0.0012
    else:  # metal / painted plate: dents + fine pitting
        hgt = N.math('ADD', N.math('MULTIPLY', N.noise(14.0, 3.0, 0.5), 1.0), N.math('MULTIPLY', N.noise(240.0, 3.0, 0.6), 0.25))
        dist = 0.003
    bp = N.node('ShaderNodeBump')
    bp.inputs['Strength'].default_value = bump_s
    bp.inputs['Distance'].default_value = dist
    N.links.new(hgt, bp.inputs['Height'])
    N.links.new(bev.outputs[0], bp.inputs['Normal'])
    N.links.new(bp.outputs[0], b.inputs['Normal'])
    m.diffuse_color = c
    return m


# ----------------------------------------------------------------------------- v4 additions
def loft(sections, seg=18, p=2.6, flat_bottom=0.0):
    """Horizontal loft along Y. section = (y, x_centre, half_width, z_top, z_bottom). Cross-sections are
    superellipses in XZ; flat_bottom (0..1) squashes the lower half toward a flat sole."""
    t = bmesh.new()
    loops = []
    e = 2.0 / p
    for y, xc, hw, zt, zb in sections:
        zm, hh = (zt + zb) / 2, (zt - zb) / 2
        vs = []
        for i in range(seg):
            a = 2 * pi * i / seg
            ca, sa = cos(a), sin(a)
            x = math.copysign(abs(ca) ** e, ca) * hw
            z = math.copysign(abs(sa) ** e, sa) * hh
            if sa < 0 and flat_bottom > 0:
                z = z * (1 - flat_bottom) + (-hh) * flat_bottom * min(1.0, abs(sa) * 3)
            vs.append(t.verts.new((xc + x, y, zm + z)))
        loops.append(vs)
    for j in range(len(loops) - 1):
        a, b = loops[j], loops[j + 1]
        for i in range(seg):
            t.faces.new((a[i], a[(i + 1) % seg], b[(i + 1) % seg], b[i]))
    t.faces.new(loops[0])
    t.faces.new(loops[-1])
    bmesh.ops.recalc_face_normals(t, faces=t.faces[:])
    return t


def add_maps(mat, folder, scale=5.0, colour=0.55, normal=1.0, rough=0.5):
    """Layer a real PBR texture set (box-projected in object space) into an existing look-dev material:
    the diffuse map modulates the base colour, the normal map replaces the procedural bump base, the roughness
    map is blended into roughness. folder holds *_diff_*, *_nor_gl_*, *_rough_* files."""
    import glob, os
    def find(key):
        g = glob.glob(os.path.join(folder, '*_%s_*' % key))
        return g[0] if g else None
    nt = mat.node_tree
    b = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    tc = nt.nodes.new('ShaderNodeTexCoord')
    mp = nt.nodes.new('ShaderNodeMapping')
    mp.inputs['Scale'].default_value = (scale, scale, scale)
    nt.links.new(tc.outputs['Object'], mp.inputs['Vector'])
    def image(path, data):
        n = nt.nodes.new('ShaderNodeTexImage')
        n.image = bpy.data.images.load(path, check_existing=True)
        if data:
            n.image.colorspace_settings.name = 'Non-Color'
        n.projection = 'BOX'
        n.projection_blend = 0.25
        nt.links.new(mp.outputs[0], n.inputs['Vector'])
        return n
    used = []
    d = find('diff')
    if d and colour > 0:
        im = image(d, False)
        bw = nt.nodes.new('ShaderNodeRGBToBW')
        nt.links.new(im.outputs['Color'], bw.inputs[0])
        mx = nt.nodes.new('ShaderNodeMix')
        mx.data_type = 'RGBA'
        mx.blend_type = 'OVERLAY'
        mx.inputs[0].default_value = colour
        inp = b.inputs['Base Color']
        if inp.is_linked:
            nt.links.new(inp.links[0].from_socket, mx.inputs[6])
        else:
            mx.inputs[6].default_value = inp.default_value
        nt.links.new(bw.outputs[0], mx.inputs[7])
        nt.links.new(mx.outputs[2], inp)
        used.append('diff')
    n = find('nor_gl')
    if n and normal > 0:
        im = image(n, True)
        nm = nt.nodes.new('ShaderNodeNormalMap')
        nm.space = 'OBJECT' if False else 'TANGENT'
        nm.inputs['Strength'].default_value = normal
        nt.links.new(im.outputs['Color'], nm.inputs['Color'])
        bump = next((x for x in nt.nodes if x.type == 'BUMP'), None)
        nt.links.new(nm.outputs[0], bump.inputs['Normal'] if bump else b.inputs['Normal'])
        used.append('normal')
    r = find('rough')
    if r and rough > 0:
        im = image(r, True)
        mx = nt.nodes.new('ShaderNodeMix')
        mx.data_type = 'FLOAT'
        mx.inputs[0].default_value = rough
        inp = b.inputs['Roughness']
        if inp.is_linked:
            nt.links.new(inp.links[0].from_socket, mx.inputs[2])
        else:
            mx.inputs[2].default_value = inp.default_value
        nt.links.new(im.outputs['Color'], mx.inputs[3])
        nt.links.new(mx.outputs[0], inp)
        used.append('rough')
    return used
