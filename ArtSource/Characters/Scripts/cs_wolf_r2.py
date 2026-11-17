"""Cargo Squad - Mutated Wolf redesign v2.

Base: "Dire Wolf Lite" by Literock Studios (free licence, modification allowed; see Reviews/MutatedWolf/RedesignV1/
ASSET_SOURCES.md). This script customises the imported base IN ITS OWN SCENE (CS_Wolf_R2): reshapes the body, paints
mutation masks, adds growths, matted-fur tufts, fangs, claws and eyes (all skinned to the base rig), builds the hide
material on top of the base textures, poses the existing rig for review and renders. It never opens, saves or resets a
.blend file. Convention: metres, Z up, the wolf faces -Y.
"""
import bpy, bmesh, math, random, importlib
from math import sin, cos, pi, radians
from mathutils import Vector, Matrix, noise
from mathutils.kdtree import KDTree
import cs_kit as K
from cs_kit import T, Rx, Ry, Rz, cyl, ball

SCENE = 'CS_Wolf_R2'
REPO = r"C:\kfupm\T261\SWE402\SWE402-project-CargoSquad"
GLB = REPO + r"\ArtSource\External\Dire_Wolf_Lite_v1.0.0\Dire_Wolf_Lite\Dire_Wolf_Lite.glb"

# mutation layout (rest pose, world metres)
SWELL = [((0, -0.42, 1.10), 0.34, 0.085), ((0, -0.66, 1.12), 0.22, 0.04), ((0.2, 0.12, 0.74), 0.30, -0.04),
         ((-0.2, 0.12, 0.74), 0.30, -0.04), ((0, -1.08, 1.0), 0.17, 0.012), ((0, 0.25, 1.0), 0.3, 0.02)]
FLESH = [((0.21, -0.46, 0.98), 0.31), ((-0.13, -0.72, 1.07), 0.19), ((-0.23, 0.10, 0.86), 0.29), ((0.15, -0.44, 0.42), 0.17),
         ((-0.075, -1.05, 1.04), 0.09), ((0.19, 0.50, 0.74), 0.21), ((0.02, -0.26, 1.14), 0.24), ((-0.15, -0.47, 0.6), 0.14),
         ((0.05, -1.09, 1.045), 0.05), ((0.14, 0.62, 0.45), 0.12), ((-0.14, -0.42, 0.25), 0.1)]
GROWTH = [((0.10, -0.43, 1.25), 7, 0.10), ((-0.07, -0.26, 1.22), 5, 0.078), ((0.03, -0.64, 1.2), 4, 0.06),
          ((-0.2, 0.08, 0.98), 3, 0.05)]


def _fall(p, c, r):
    d = (p - Vector(c)).length / r
    return (1 - d * d) ** 2 if d < 1 else 0.0


def scene_objects():
    sc = bpy.data.scenes[SCENE]
    return sc, bpy.data.objects['MW2_Rig'], bpy.data.objects['MW2_Body']


def reset_pose(arm):
    if arm.animation_data:
        arm.animation_data.action = None
    for pb in arm.pose.bones:
        pb.matrix_basis.identity()
    bpy.context.view_layer.update()


class Skinned:
    """Extra geometry skinned to the base rig: every added piece takes the weights of the nearest base vertex."""

    def __init__(self, name, body):
        self.name, self.body = name, body
        self.bm = bmesh.new()
        self.mats, self.spans = [], []
        me = body.data
        self.kd = KDTree(len(me.vertices))
        for v in me.vertices:
            self.kd.insert(v.co, v.index)
        self.kd.balance()

    def add(self, t, mat, anchor=None, M=None, smooth=True):
        if mat not in self.mats:
            self.mats.append(mat)
        mi = self.mats.index(mat)
        M = Matrix.Identity(4) if M is None else M
        start = len(self.bm.verts)
        mp = {}
        for v in t.verts:
            mp[v] = self.bm.verts.new(M @ v.co)
        for f in t.faces:
            try:
                nf = self.bm.faces.new([mp[v] for v in f.verts])
                nf.material_index, nf.smooth = mi, smooth
            except ValueError:
                pass
        t.free()
        self.spans.append((start, len(self.bm.verts), anchor if isinstance(anchor, str) or anchor is None else Vector(anchor)))

    def finish(self, sc, arm):
        me = bpy.data.meshes.new(self.name)
        self.bm.normal_update()
        self.bm.to_mesh(me)
        self.bm.free()
        for m in self.mats:
            me.materials.append(m)
        ob = bpy.data.objects.new(self.name, me)
        sc.collection.objects.link(ob)
        names = [g.name for g in self.body.vertex_groups]
        for n in names:
            ob.vertex_groups.new(name=n)
        base = self.body.data
        for start, end, anchor in self.spans:
            if end <= start:
                continue
            if isinstance(anchor, str):
                ob.vertex_groups[anchor].add(list(range(start, end)), 1.0, 'REPLACE')
                continue
            a = anchor if anchor is not None else sum((me.vertices[i].co for i in range(start, end)), Vector()) / (end - start)
            _, idx, _ = self.kd.find(a)
            for g in base.vertices[idx].groups:
                if g.weight > 0.01:
                    ob.vertex_groups[names[g.group]].add(list(range(start, end)), g.weight, 'REPLACE')
        ob.parent = arm
        mod = ob.modifiers.new('Armature', 'ARMATURE')
        mod.object = arm
        return ob


def _surface(body, x, y, z0=2.0):
    ok, loc, nor, _ = body.ray_cast(Vector((x, y, z0)), Vector((0, 0, -1)))
    return (loc, nor) if ok else (None, None)


def materials(body):
    S = K.surface
    M = dict(
        fur=S('MW2_FurMatted', '241d19', rough=0.9, grime=0.6, dust=0.15, mott=('100d0b', 26.0, 0.75, '45322a'), bump='fabric',
              bump_s=0.6),
        growth=S('MW2_Growth', '8c8514', rough=0.42, grime=1.0, dust=0.0, mott=('4a5210', 45.0, 0.8, 'b9a92a'), bump='leather',
                 bump_s=1.0, sss=0.35),
        tooth=S('MW2_Tooth', 'c9b98f', rough=0.35, grime=1.0, dust=0.0, mott=('7a6238', 60.0, 0.6), bump='rubber', bump_s=0.3),
        claw=S('MW2_Claw', '2b241d', rough=0.4, wear=0.5, under=('8a7a62', 0.0, 0.5), grime=0.7, dust=0.3, scratch=0.6,
               bump='rubber', bump_s=0.4),
        eye=S('MW2_Eye', 'f2c21a', rough=0.15, grime=0.0, dust=0.0, bump='rubber', bump_s=0.0),
        pupil=S('MW2_Pupil', '050505', rough=0.1, grime=0.0, dust=0.0, bump='rubber', bump_s=0.0),
        gum=S('MW2_Gum', '4a1414', rough=0.3, grime=0.6, dust=0.0, mott=('2a0a0a', 50.0, 0.6), bump='rubber', bump_s=0.5, sss=0.2),
    )
    b = next(n for n in M['eye'].node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    b.inputs['Emission Color'].default_value = K.rgb('ffc21a')
    b.inputs['Emission Strength'].default_value = 7.0
    # hide: the base PBR textures, darkened and dirtied, with flesh / bare skin / chemical staining driven by painted masks
    src = bpy.data.materials['DireWolf']
    old = bpy.data.materials.get('MW2_Hide')
    if old:
        bpy.data.materials.remove(old)
    m = src.copy()
    m.name = 'MW2_Hide'
    nt = m.node_tree
    L = nt.links
    bsdf = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    def new(kind, **kw):
        n = nt.nodes.new(kind)
        for k, v in kw.items():
            setattr(n, k, v)
        return n
    def mixc(f, a, b2, blend='MIX'):
        n = new('ShaderNodeMix', data_type='RGBA', blend_type=blend)
        for inp, v in ((n.inputs[0], f), (n.inputs[6], a), (n.inputs[7], b2)):
            L.new(v, inp) if isinstance(v, bpy.types.NodeSocket) else setattr(inp, 'default_value', v)
        return n.outputs[2]
    def mixf(f, a, b2):
        n = new('ShaderNodeMix', data_type='FLOAT')
        for inp, v in ((n.inputs[0], f), (n.inputs[2], a), (n.inputs[3], b2)):
            L.new(v, inp) if isinstance(v, bpy.types.NodeSocket) else setattr(inp, 'default_value', v)
        return n.outputs[0]
    def math_(op, a, b2=None, clamp=False):
        n = new('ShaderNodeMath', operation=op, use_clamp=clamp)
        for inp, v in ((n.inputs[0], a), (n.inputs[1], b2)):
            if v is not None:
                L.new(v, inp) if isinstance(v, bpy.types.NodeSocket) else setattr(inp, 'default_value', v)
        return n.outputs[0]
    obj = new('ShaderNodeTexCoord').outputs['Object']
    def noise_(scale, detail=4.0):
        n = new('ShaderNodeTexNoise')
        L.new(obj, n.inputs['Vector'])
        n.inputs['Scale'].default_value, n.inputs['Detail'].default_value = scale, detail
        return n.outputs[0]
    def ramp(x, lo, hi):
        n = new('ShaderNodeMapRange', clamp=True)
        L.new(x, n.inputs[0]) if isinstance(x, bpy.types.NodeSocket) else setattr(n.inputs[0], 'default_value', x)
        n.inputs[1].default_value, n.inputs[2].default_value = lo, hi
        return n.outputs[0]
    base = bsdf.inputs['Base Color'].links[0].from_socket
    rough = bsdf.inputs['Roughness'].links[0].from_socket if bsdf.inputs['Roughness'].is_linked else 0.8
    col = mixc(1.0, base, K.rgb('453a33'), 'MULTIPLY')                       # dark grey-brown, dirty coat
    col = mixc(ramp(noise_(5.0, 5.0), 0.35, 0.75), col, mixc(1.0, col, (0.35, 0.3, 0.26, 1), 'MULTIPLY'))
    fl = new('ShaderNodeAttribute', attribute_name='mw_flesh').outputs['Fac']
    tx = new('ShaderNodeAttribute', attribute_name='mw_toxic').outputs['Fac']
    edge = math_('ADD', fl, math_('MULTIPLY', math_('SUBTRACT', noise_(38.0, 6.0), 0.5), 0.55))
    skin = ramp(edge, 0.06, 0.30)            # bare, inflamed skin ring
    flesh = ramp(edge, 0.36, 0.48)           # open tissue
    wave = new('ShaderNodeTexWave', wave_type='BANDS', bands_direction='Z')
    L.new(obj, wave.inputs['Vector'])
    wave.inputs['Scale'].default_value, wave.inputs['Distortion'].default_value = 26.0, 9.0
    wave.inputs['Detail'].default_value = 3.0
    skin_col = mixc(noise_(45.0, 4.0), K.rgb('2a1310'), K.rgb('5c261e'))
    flesh_col = mixc(wave.outputs[0], K.rgb('220606'), K.rgb('6a1612'))
    flesh_col = mixc(ramp(noise_(90.0, 2.0), 0.62, 0.7), flesh_col, K.rgb('b0988a'))     # pale sinew flecks
    col = mixc(skin, col, skin_col)
    col = mixc(flesh, col, flesh_col)
    stain = math_('MULTIPLY', tx, math_('ADD', noise_(22.0, 5.0), 0.3), clamp=True)
    col = mixc(math_('MULTIPLY', stain, 0.5), col, K.rgb('4f5016'))
    L.new(col, bsdf.inputs['Base Color'])
    r = mixf(skin, rough, 0.55)
    r = mixf(flesh, r, 0.28)
    L.new(r, bsdf.inputs['Roughness'])
    bsdf.inputs['Metallic'].default_value = 0.0
    for l in list(bsdf.inputs['Metallic'].links):
        L.remove(l)
    bump = new('ShaderNodeBump')
    bump.inputs['Strength'].default_value, bump.inputs['Distance'].default_value = 1.0, 0.012
    hgt = math_('MULTIPLY', math_('ADD', math_('MULTIPLY', wave.outputs[0], 0.7), noise_(120.0, 3.0)), skin)
    L.new(hgt, bump.inputs['Height'])
    if bsdf.inputs['Normal'].is_linked:
        L.new(bsdf.inputs['Normal'].links[0].from_socket, bump.inputs['Normal'])
    L.new(bump.outputs[0], bsdf.inputs['Normal'])
    body.data.materials[0] = m
    M['hide'] = m
    return M


def reshape(body):
    """Hunched, starved silhouette + painted mutation masks. Works from a pristine copy of the base mesh each time."""
    me = body.data
    if 'mw_rest' not in me.attributes:
        a = me.attributes.new('mw_rest', 'FLOAT_VECTOR', 'POINT')
        for v in me.vertices:
            a.data[v.index].vector = v.co
    rest = me.attributes['mw_rest']
    for v in me.vertices:
        v.co = rest.data[v.index].vector
    me.update()
    for name in ('mw_flesh', 'mw_toxic'):
        if name not in me.attributes:
            me.attributes.new(name, 'FLOAT', 'POINT')
    fa, ta = me.attributes['mw_flesh'], me.attributes['mw_toxic']
    normals = [v.normal.copy() for v in me.vertices]
    for v in me.vertices:
        p = v.co.copy()
        d = sum(amt * _fall(p, c, r) for c, r, amt in SWELL)
        # ribs: shallow grooves on the starved flank
        if 0.0 < p.y < 0.36 and 0.62 < p.z < 0.98 and abs(p.x) > 0.1:
            d -= 0.012 * (0.5 + 0.5 * sin(p.y * 62.0 - p.z * 9.0)) * _fall(p, (p.x, 0.17, 0.8), 0.24)
        fl = max(_fall(p, c, r) for c, r in FLESH)
        if fl > 0.3:
            d -= 0.012 * fl                     # open tissue sits below the coat
        v.co = p + normals[v.index] * d
        fa.data[v.index].value = fl
        ta.data[v.index].value = max(_fall(p, c, s * 4.5) for c, n, s in GROWTH)
    me.update()


def growths(sc, arm, body, M):
    random.seed(7)
    S = Skinned('MW2_Growths', body)
    for c, count, size in GROWTH:
        for i in range(count):
            a = random.uniform(0, 2 * pi)
            rr = 0.0 if i == 0 else random.uniform(0.6, 1.5) * size
            x, y = c[0] + cos(a) * rr, c[1] + sin(a) * rr
            ok, loc, nor, _ = body.ray_cast(Vector((x * 1.6, y, c[2] + 0.5)), (Vector((x, y, c[2] - 0.25)) - Vector((x * 1.6, y, c[2] + 0.5))).normalized())
            if not ok:
                continue
            s = size * (1.0 if i == 0 else random.uniform(0.35, 0.75))
            t = ball((s, s * random.uniform(0.85, 1.1), s * random.uniform(0.75, 0.95)), 14, 9)
            for v in t.verts:
                n = noise.noise(v.co * (9.0 / max(s, 0.01)) * 0.02 + Vector((i, 0, 0)))
                v.co *= 1.0 + 0.16 * n
            S.add(t, M['growth'], anchor=loc, M=T(loc + nor * s * 0.35))
            for k in range(random.randint(2, 4)):                    # satellite nodules
                b2 = random.uniform(0, 2 * pi)
                q = loc + Vector((cos(b2), sin(b2), 0)) * s * random.uniform(0.9, 1.3)
                ok2, l2, n2, _ = body.ray_cast(q + nor * 0.3, -nor)
                if ok2:
                    s2 = s * random.uniform(0.18, 0.32)
                    S.add(ball(s2, 8, 6), M['growth'], anchor=l2, M=T(l2 + n2 * s2 * 0.3))
    return S.finish(sc, arm)


def tufts(sc, arm, body, M, count=2800):
    """Matted fur clumps: flat tapered spikes that break up the silhouette along the spine, neck, haunches and tail."""
    random.seed(11)
    S = Skinned('MW2_FurTufts', body)
    me = body.data
    fl = me.attributes['mw_flesh'].data
    tx = me.attributes['mw_toxic'].data
    cands, wts = [], []
    for p in me.polygons:
        c, n = p.center, p.normal
        if max(fl[i].value for i in p.vertices) > 0.3 or max(tx[i].value for i in p.vertices) > 0.72 or c.z < 0.5 or c.y < -0.95:
            continue
        w = max(0.0, n.z) * 1.4 + 0.25
        if -0.85 < c.y < -0.3:
            w *= 2.2                      # mane and shoulders
        if c.y > 0.62:
            w *= 2.6                      # tail
        if n.z < -0.3:
            w *= 0.15
        cands.append(p.index)
        wts.append(w * p.area)
    for pi_ in random.choices(cands, weights=wts, k=count):
        p = me.polygons[pi_]
        c, n = p.center.copy(), p.normal.copy()
        ln = random.uniform(0.045, 0.095) * (1.5 if (-0.85 < c.y < -0.3 and n.z > 0.2) or c.y > 0.62 else 1.0)
        back = Vector((random.uniform(-0.3, 0.3), 1.0, random.uniform(-0.35, 0.05)))
        back = (back - n * back.dot(n)).normalized()
        d = (n * random.uniform(0.25, 0.75) + back).normalized()
        side = d.cross(n)
        side = side.normalized() if side.length > 1e-4 else Vector((1, 0, 0))
        w = random.uniform(0.0035, 0.008)
        t = bmesh.new()
        a, b = t.verts.new(c - side * w - n * 0.01), t.verts.new(c + side * w - n * 0.01)
        mid = c + d * ln * 0.55 + n * 0.012
        e, f = t.verts.new(mid - side * w * 0.55), t.verts.new(mid + side * w * 0.55)
        tip = t.verts.new(c + d * ln)
        ridge0, ridge1 = t.verts.new(c + n * 0.012), t.verts.new(mid + n * 0.01)
        for quad in ((a, ridge0, ridge1, e), (ridge0, b, f, ridge1)):
            t.faces.new(quad)
        t.faces.new((e, ridge1, tip))
        t.faces.new((ridge1, f, tip))
        S.add(t, M['fur'], anchor=c, smooth=False)
    return S.finish(sc, arm)


def _cone_chain(pts, r0, seg=6):
    """Curved tapering spike through pts."""
    t = bmesh.new()
    loops = []
    n = len(pts)
    for i, p in enumerate(pts[:-1]):
        d = (pts[i + 1] - p).normalized()
        u = d.orthogonal().normalized()
        w = d.cross(u)
        r = r0 * (1 - i / (n - 1)) ** 0.8
        loops.append([t.verts.new(p + (u * cos(2 * pi * k / seg) + w * sin(2 * pi * k / seg)) * r) for k in range(seg)])
    tip = t.verts.new(pts[-1])
    for j in range(len(loops) - 1):
        for k in range(seg):
            t.faces.new((loops[j][k], loops[j][(k + 1) % seg], loops[j + 1][(k + 1) % seg], loops[j + 1][k]))
    for k in range(seg):
        t.faces.new((loops[-1][k], loops[-1][(k + 1) % seg], tip))
    t.faces.new(loops[0])
    bmesh.ops.recalc_face_normals(t, faces=t.faces[:])
    return t


def claws(sc, arm, body, M):
    S = Skinned('MW2_Claws', body)
    for s in (1, -1):
        for y0, z0, ln in ((-0.655, 0.03, 0.062), (0.30, 0.03, 0.055)):       # front paw, hind paw
            for i, dx in enumerate((-0.042, -0.015, 0.015, 0.042)):
                x = s * 0.142 + dx
                spread = dx * 0.5
                yy = y0 + abs(dx) * 0.55
                ok, loc, nor, _ = body.ray_cast(Vector((x, yy - 0.2, z0 + 0.03)), Vector((0, 1, 0)))
                base = loc if ok else Vector((x, yy, z0 + 0.03))
                p0 = base + Vector((0, 0.012, 0.004))
                pts = [p0, p0 + Vector((spread * 0.3, -ln * 0.45, 0.004)), p0 + Vector((spread * 0.6, -ln * 0.85, -0.012)),
                       p0 + Vector((spread * 0.7, -ln, -0.03))]
                S.add(_cone_chain(pts, 0.0105), M['claw'], anchor=Vector((s * 0.142, y0 + 0.03, 0.03)))
    return S.finish(sc, arm)


def face(sc, arm, body, M, eyes, upper, lower):
    """eyes: [(x, y, z)] centres; upper / lower: lists of (x, y, z, length) tooth roots on the head and the jaw."""
    S = Skinned('MW2_Face', body)
    head = 'head'
    jaw = 'jaw'
    A = arm.data.bones
    for (x, y, z) in eyes:
        sg = 1.0 if x > 0 else -1.0
        ok, loc, nor, _ = body.ray_cast(Vector((sg * 0.3, y, z)), Vector((-sg, 0, 0)))
        c = (loc if ok else Vector((x, y, z))) - Vector((sg * 0.005, 0, 0))
        S.add(ball(0.0135, 12, 8), M['eye'], anchor=head, M=T(c))
        S.add(ball((0.004, 0.005, 0.009), 8, 6), M['pupil'], anchor=head, M=T(c + Vector((sg * 0.0105, -0.006, 0))))
    for (x, y, z, ln) in upper:
        p = Vector((x, y, z))
        S.add(_cone_chain([p + Vector((0, 0, 0.006)), p + Vector((0, -ln * 0.12, -ln * 0.55)), p + Vector((0, -ln * 0.05, -ln))],
                          0.0045 + ln * 0.11), M['tooth'], anchor=p + Vector((0, 0, 0.012)))
    for (x, y, z, ln) in lower:
        p = Vector((x, y, z))
        S.add(_cone_chain([p - Vector((0, 0, 0.006)), p + Vector((0, -ln * 0.1, ln * 0.55)), p + Vector((0, 0.0, ln))],
                          0.004 + ln * 0.11), M['tooth'], anchor=p - Vector((0, 0, 0.012)))
    ob = S.finish(sc, arm)
    # teeth on the lower jaw must follow the jaw bone only; everything else the head bone
    names = {g.name: g for g in ob.vertex_groups}
    me = ob.data
    lower_start = None
    return ob


def pose(arm, kind='snarl'):
    reset_pose(arm)
    if kind == 'rest':
        return
    def rot(name, deg, axis='X'):
        pb = arm.pose.bones[name]
        bpy.context.view_layer.update()
        piv = pb.matrix.translation.copy()
        pb.matrix = Matrix.Translation(piv) @ Matrix.Rotation(radians(deg), 4, axis) @ Matrix.Translation(-piv) @ pb.matrix
        bpy.context.view_layer.update()
    # lowered, predatory stance: front crouched, head low and forward, jaw open, ears back, tail low
    rot('spine_02', 3)
    rot('spine_03', 6)
    rot('neck_01', 18)
    rot('neck_02', 4)
    rot('head', -2)
    rot('jaw', 24)
    for s in ('_L', '_R'):
        rot('ear' + s, -32)
    rot('tail_01', -34)
    rot('tail_02', -12)
    rot('tail_03', 8)
    rot('tail_04', 10)
    # put the paws back on the ground
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    body = bpy.data.objects['MW2_Body'].evaluated_get(dg)
    me = body.to_mesh()
    zmin = min(v.co.z for v in me.vertices)
    body.to_mesh_clear()
    root = arm.pose.bones['root']
    root.location = (0, 0, 0)
    if True:
        bpy.context.view_layer.update()
        b2 = bpy.data.objects['MW2_Body'].evaluated_get(bpy.context.evaluated_depsgraph_get())
        m2 = b2.to_mesh()
        wz = min((b2.matrix_world @ v.co).z for v in m2.vertices)
        b2.to_mesh_clear()
        arm.location.z -= wz
    bpy.context.view_layer.update()
    return round(zmin, 4)


# ----------------------------------------------------------------------------- review renders
VIEWS = dict(front=(0, 6, 85, 6.4), back=(180, 6, 85, 6.4), left=(90, 4, 85, 8.2), right=(-90, 4, 85, 8.2),
             hero=(-38, 9, 60, 4.9), gameplay=(-30, 45, 85, 8.8))


def studio(samples=64, res=(1200, 900)):
    sc = bpy.data.scenes[SCENE]
    for o in list(sc.objects):
        if o.name.startswith('MW2_Studio'):
            bpy.data.objects.remove(o, do_unlink=True)
    sc.render.engine = 'CYCLES'
    sc.cycles.samples, sc.cycles.use_denoising = samples, True
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = False
    sc.render.image_settings.file_format = 'PNG'
    try:
        sc.view_settings.view_transform = 'Standard'
        sc.view_settings.look = 'None'
    except Exception as e:
        print('view', e)
    w = bpy.data.worlds.get('MW2_World') or bpy.data.worlds.new('MW2_World')
    if w.node_tree is None:
        w.use_nodes = True
    nt = w.node_tree
    bg = next(n for n in nt.nodes if n.type == 'BACKGROUND')
    for n in [n for n in nt.nodes if n.type in ('LIGHT_PATH', 'MIX')]:
        nt.nodes.remove(n)
    lp, mx = nt.nodes.new('ShaderNodeLightPath'), nt.nodes.new('ShaderNodeMix')
    mx.data_type = 'RGBA'
    mx.inputs[6].default_value = (0.16, 0.155, 0.15, 1.0)
    mx.inputs[7].default_value = K.rgb('8f8b86')
    nt.links.new(lp.outputs['Is Camera Ray'], mx.inputs[0])
    nt.links.new(mx.outputs[2], bg.inputs[0])
    bg.inputs[1].default_value = 1.0
    sc.world = w
    piv = bpy.data.objects.new('MW2_StudioPivot', None)
    sc.collection.objects.link(piv)
    def light(name, loc, energy, size, col=(1, 1, 1)):
        l = bpy.data.lights.new(name, 'AREA')
        l.energy, l.size, l.color = energy, size, col
        o = bpy.data.objects.new(name, l)
        sc.collection.objects.link(o)
        o.location = loc
        o.rotation_euler = (Vector((0, 0, 0.8)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        o.parent = piv
    light('MW2_StudioKey', (-2.8, -3.4, 3.4), 700, 2.4, (1.0, 0.95, 0.88))
    light('MW2_StudioFill', (3.4, -2.4, 1.4), 60, 4.0, (0.9, 0.95, 1.0))
    light('MW2_StudioRim', (1.6, 3.6, 3.0), 520, 2.5)
    me = bpy.data.meshes.new('MW2_StudioFloor')
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=30.0)
    bm.to_mesh(me)
    bm.free()
    fl = bpy.data.objects.new('MW2_StudioFloor', me)
    sc.collection.objects.link(fl)
    fl.is_shadow_catcher = True
    cam = bpy.data.objects.new('MW2_StudioCam', bpy.data.cameras.new('MW2_StudioCam'))
    sc.collection.objects.link(cam)
    sc.camera = cam
    return sc


def render(view, path, target=(0, 0.0, 0.62), res=None, samples=None):
    sc = bpy.data.scenes[SCENE]
    if res:
        sc.render.resolution_x, sc.render.resolution_y = res
    if samples:
        sc.cycles.samples = samples
    az, el, lens, dist = VIEWS[view] if isinstance(view, str) else view
    cam = sc.camera
    cam.data.lens = lens
    cam.data.sensor_fit = 'VERTICAL'
    a, e = radians(az), radians(el)
    d = Vector((sin(a) * cos(e), -cos(a) * cos(e), sin(e)))
    cam.location = Vector(target) + d * dist
    cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    bpy.data.objects['MW2_StudioPivot'].rotation_euler = (0, 0, radians(az + 38))
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True, scene=SCENE)
    return path


def stats():
    sc = bpy.data.scenes[SCENE]
    out = []
    for o in sc.objects:
        if o.type == 'MESH' and not o.name.startswith('MW2_Studio'):
            out.append((o.name, sum(len(p.vertices) - 2 for p in o.data.polygons)))
    return sum(t for _, t in out), out


def build(eyes=(), upper=(), lower=()):
    importlib.reload(K)
    sc, arm, body = scene_objects()
    bpy.context.window_manager.windows[0].scene = sc
    arm.location = (0, 0, 0)
    reset_pose(arm)
    for o in list(sc.objects):
        if o.name in ('MW2_Growths', 'MW2_FurTufts', 'MW2_Claws', 'MW2_Face'):
            me = o.data
            bpy.data.objects.remove(o, do_unlink=True)
            bpy.data.meshes.remove(me)
    reshape(body)
    bpy.context.view_layer.update()
    M = materials(body)
    growths(sc, arm, body, M)
    tufts(sc, arm, body, M)
    claws(sc, arm, body, M)
    if eyes or upper or lower:
        face(sc, arm, body, M, eyes, upper, lower)
    bpy.context.view_layer.update()
    return stats()


# ============================================================================== V2 refinement (overrides the V1 versions above)
# wounds are torn streaks: (centre, radius, long-axis direction, elongation)
TEARS = [((0.21, -0.46, 0.98), 0.20, (0.2, 0.5, -1), 2.3), ((-0.14, -0.70, 1.07), 0.12, (0, 1, -0.6), 2.2),
         ((-0.23, 0.10, 0.86), 0.17, (0, 1, -0.5), 2.6), ((0.15, -0.44, 0.42), 0.09, (0, 0, 1), 2.6),
         ((0.19, 0.50, 0.74), 0.13, (0, -0.4, 1), 2.2), ((0.02, -0.24, 1.14), 0.15, (0.3, 1, 0), 2.4),
         ((-0.15, -0.47, 0.6), 0.08, (0, 0.2, 1), 2.4), ((0.14, 0.62, 0.45), 0.07, (0, 0, 1), 2.6),
         ((-0.045, -1.10, 1.045), 0.05, (0, 1, 0.5), 3.0), ((0.06, -1.06, 1.12), 0.045, (0.4, 1, 0.9), 3.2),
         ((-0.07, -0.97, 1.19), 0.05, (0, 1, 0.3), 2.6), ((0.10, -0.60, 0.80), 0.11, (0, 1, 0.4), 2.4)]
GROWTH = [((0.11, -0.44, 1.24), 9, 0.066), ((-0.08, -0.27, 1.21), 7, 0.054), ((0.03, -0.63, 1.19), 6, 0.046),
          ((-0.16, -0.50, 1.10), 5, 0.044), ((0.0, -0.05, 1.12), 4, 0.038)]


def _tear(p, c, r, axis, k):
    d = p - Vector(c)
    a = Vector(axis).normalized()
    along = d.dot(a)
    across = (d - a * along).length
    q = math.sqrt((along / (r * k)) ** 2 + (across / r) ** 2)
    return (1 - q * q) ** 2 if q < 1 else 0.0


def reshape(body):
    me = body.data
    rest = me.attributes['mw_rest']
    for v in me.vertices:
        v.co = rest.data[v.index].vector
    me.update()
    for name in ('mw_flesh', 'mw_toxic'):
        if name not in me.attributes:
            me.attributes.new(name, 'FLOAT', 'POINT')
    fa, ta = me.attributes['mw_flesh'], me.attributes['mw_toxic']
    normals = [v.normal.copy() for v in me.vertices]
    for v in me.vertices:
        p = v.co.copy()
        d = sum(amt * _fall(p, c, r) for c, r, amt in SWELL)
        if 0.0 < p.y < 0.36 and 0.62 < p.z < 0.98 and abs(p.x) > 0.1:
            d -= 0.012 * (0.5 + 0.5 * sin(p.y * 62.0 - p.z * 9.0)) * _fall(p, (p.x, 0.17, 0.8), 0.24)
        # brow ridge and muzzle bulk for a heavier, angrier head
        d += 0.012 * _fall(p, (0.06, -1.03, 1.17), 0.06) + 0.012 * _fall(p, (-0.06, -1.03, 1.17), 0.06)
        d += 0.008 * _fall(p, (0, -1.12, 1.04), 0.09)
        fl = max(_tear(p, c, r, ax, k) for c, r, ax, k in TEARS)
        gr = max(_fall(p, c, s * 3.2) for c, n, s in GROWTH)          # raw tissue around every growth cluster
        fl = max(fl, gr * 0.9)
        rag = noise.noise(p * 13.0) * 0.5 + noise.noise(p * 37.0) * 0.28   # torn, irregular borders
        fl = max(0.0, min(1.0, fl + rag * (0.55 if fl > 0.02 else 0.0)))
        if fl > 0.3:
            d -= 0.014 * fl
        v.co = p + normals[v.index] * d
        fa.data[v.index].value = fl
        ta.data[v.index].value = max(_fall(p, c, s * 6.0) for c, n, s in GROWTH)
    me.update()


def growths(sc, arm, body, M):
    random.seed(7)
    S = Skinned('MW2_Growths', body)
    for c, count, size in GROWTH:
        for i in range(count):
            a = random.uniform(0, 2 * pi)
            rr = 0.0 if i == 0 else random.uniform(0.5, 2.2) * size
            x, y = c[0] + cos(a) * rr, c[1] + sin(a) * rr
            o = Vector((x * 1.6, y, c[2] + 0.5))
            ok, loc, nor, _ = body.ray_cast(o, (Vector((x, y, c[2] - 0.25)) - o).normalized())
            if not ok:
                continue
            s = size * (1.0 if i == 0 else random.uniform(0.3, 0.8))
            t = ball((s * random.uniform(0.9, 1.3), s * random.uniform(0.8, 1.2), s * random.uniform(0.5, 0.75)), 12, 8)
            seed = Vector((i * 3.1, c[0] * 50, c[1] * 50))
            for v in t.verts:
                u = v.co / max(s, 1e-4)
                n = noise.noise(u * 1.7 + seed) * 0.34 + noise.noise(u * 4.5 + seed) * 0.14
                v.co *= 1.0 + n
            # tilt to the surface and sink it in so it reads as erupting from the tissue
            S.add(t, M['growth'], anchor=loc, M=T(loc + nor * s * 0.3) @ Vector((0, 0, 1)).rotation_difference(nor).to_matrix().to_4x4())
            for k in range(random.randint(1, 3)):
                b2 = random.uniform(0, 2 * pi)
                q = loc + Vector((cos(b2), sin(b2), 0)) * s * random.uniform(0.9, 1.5)
                ok2, l2, n2, _ = body.ray_cast(q + nor * 0.3, -nor)
                if ok2:
                    s2 = s * random.uniform(0.16, 0.3)
                    S.add(ball((s2, s2, s2 * 0.7), 7, 5), M['growth'], anchor=l2, M=T(l2 + n2 * s2 * 0.1))
    return S.finish(sc, arm)


def tufts(sc, arm, body, M, count=2500):
    """Matted fur: flat, drooping, overlapping locks that follow the coat direction (long on the mane and shoulders,
    medium along the back and tail, none on legs, face, wounds or growth sites)."""
    random.seed(11)
    S = Skinned('MW2_FurTufts', body)
    me = body.data
    fl = me.attributes['mw_flesh'].data
    tx = me.attributes['mw_toxic'].data
    cands, wts, kind = [], [], {}
    for p in me.polygons:
        c, n = p.center, p.normal
        if max(fl[i].value for i in p.vertices) > 0.14 or c.z < 0.55 or c.y < -0.93 or n.z < -0.45:
            continue
        if max(tx[i].value for i in p.vertices) > 0.42:
            continue
        mane = -0.88 < c.y < -0.32 and c.z > 0.8
        tail = c.y > 0.62
        w = (max(0.0, n.z) * 1.2 + 0.35) * (2.4 if mane else 2.2 if tail else 1.0)
        cands.append(p.index)
        wts.append(w * p.area)
        kind[p.index] = 2 if mane else 1 if tail else 0
    for pi_ in random.choices(cands, weights=wts, k=count):
        p = me.polygons[pi_]
        c, n = p.center.copy(), p.normal.copy()
        kd = kind[pi_]
        ln = random.uniform(0.07, 0.13) if kd == 2 else random.uniform(0.06, 0.11) if kd == 1 else random.uniform(0.035, 0.065)
        flow = Vector((random.uniform(-0.25, 0.25), 1.0, random.uniform(-0.75, -0.2)))       # backwards and down the body
        flow = (flow - n * flow.dot(n))
        flow = flow.normalized() if flow.length > 1e-4 else Vector((0, 1, 0))
        lift = random.uniform(0.12, 0.5)
        side = flow.cross(n).normalized()
        w = ln * random.uniform(0.055, 0.105)
        t = bmesh.new()
        p0 = c - n * 0.008
        p1 = c + flow * ln * 0.5 + n * ln * lift
        p2 = c + flow * ln + n * ln * (lift * 0.35) + side * random.uniform(-0.3, 0.3) * w
        a, b = t.verts.new(p0 - side * w), t.verts.new(p0 + side * w)
        r0 = t.verts.new(p0 + n * w * 0.55)
        e, f = t.verts.new(p1 - side * w * 0.7), t.verts.new(p1 + side * w * 0.7)
        r1 = t.verts.new(p1 + n * w * 0.5)
        tip = t.verts.new(p2)
        for quad in ((a, r0, r1, e), (r0, b, f, r1)):
            t.faces.new(quad)
        t.faces.new((e, r1, tip))
        t.faces.new((r1, f, tip))
        S.add(t, M['fur'], anchor=c, smooth=True)
    return S.finish(sc, arm)


def face(sc, arm, body, M, eyes, upper, lower):
    S = Skinned('MW2_Face', body)
    for (x, y, z) in eyes:
        sg = 1.0 if x > 0 else -1.0
        ok, loc, nor, _ = body.ray_cast(Vector((sg * 0.3, y, z)), Vector((-sg, 0, 0)))
        c = (loc if ok else Vector((x, y, z))) - Vector((sg * 0.006, 0, 0))
        S.add(ball(0.0135, 12, 8), M['eye'], anchor='head', M=T(c))
        S.add(ball((0.004, 0.005, 0.0095), 8, 6), M['pupil'], anchor='head', M=T(c + Vector((sg * 0.0108, -0.0062, 0))))
        # heavy dark brow fold over the eye
        S.add(K.rbox((0.012, 0.046, 0.012), 0.004, 1), M['fur'], anchor='head',
              M=T(c + Vector((sg * 0.006, -0.004, 0.017))) @ Rz(sg * 20) @ Rx(-22))
    for (x, y, z, ln) in upper:
        p = Vector((x, y, z))
        S.add(_cone_chain([p + Vector((0, 0, 0.008)), p + Vector((0, -ln * 0.14, -ln * 0.55)), p + Vector((0, -ln * 0.04, -ln))],
                          0.005 + ln * 0.12), M['tooth'], anchor=p + Vector((0, 0, 0.012)))
        S.add(ball((0.0075 + ln * 0.1, 0.007, 0.0065), 8, 5), M['gum'], anchor=p + Vector((0, 0, 0.012)), M=T(p + Vector((0, 0, 0.006))))
    for (x, y, z, ln) in lower:
        p = Vector((x, y, z))
        S.add(_cone_chain([p - Vector((0, 0, 0.008)), p + Vector((0, -ln * 0.1, ln * 0.55)), p + Vector((0, 0.0, ln))],
                          0.0045 + ln * 0.12), M['tooth'], anchor=p - Vector((0, 0, 0.012)))
        S.add(ball((0.007 + ln * 0.1, 0.0065, 0.006), 8, 5), M['gum'], anchor=p - Vector((0, 0, 0.012)), M=T(p - Vector((0, 0, 0.005))))
    return S.finish(sc, arm)


def _paw_sets(body):
    names = {g.index: g.name for g in body.vertex_groups}
    out = dict(front=[], hind=[])
    for v in body.data.vertices:
        best = max(v.groups, key=lambda g: g.weight, default=None)
        if best is None:
            continue
        n = names[best.group]
        if n.startswith(('finger', 'hand')):
            out['front'].append(v.index)
        elif n.startswith(('toe', 'foot')):
            out['hind'].append(v.index)
    return out


def _paw_heights(body, sets):
    bpy.context.view_layer.update()
    be = body.evaluated_get(bpy.context.evaluated_depsgraph_get())
    me = be.to_mesh()
    Mw = be.matrix_world
    res = {k: min((Mw @ me.vertices[i].co).z for i in idx) for k, idx in sets.items()}
    cy = {k: sum((Mw @ me.vertices[i].co).y for i in idx) / len(idx) for k, idx in sets.items()}
    be.to_mesh_clear()
    return res, cy


def pose(arm, kind='snarl', crouch=13.0):
    """Lowered predatory stance. Front and hind legs are folded by amounts solved so that all four paws end at the
    same height; then the whole rig is dropped onto the ground."""
    reset_pose(arm)
    arm.location = (0, 0, 0)
    if kind == 'rest':
        return
    body = bpy.data.objects['MW2_Body']
    sets = _paw_sets(body)
    base = {}
    def rot(name, deg, axis='X'):
        pb = arm.pose.bones[name]
        bpy.context.view_layer.update()
        piv = pb.matrix.translation.copy()
        pb.matrix = Matrix.Translation(piv) @ Matrix.Rotation(radians(deg), 4, axis) @ Matrix.Translation(-piv) @ pb.matrix
        bpy.context.view_layer.update()
    def upper():
        rot('spine_02', 3)
        rot('spine_03', 6)
        rot('neck_01', 16)
        rot('neck_02', 3)
        rot('head', -6)
        rot('jaw', 26)
        for s in ('_L', '_R'):
            rot('ear' + s, -30)
        rot('tail_01', -34)
        rot('tail_02', -12)
        rot('tail_03', 8)
        rot('tail_04', 10)
    def legs(a, b):
        for s in ('_L', '_R'):
            rot('upperarm' + s, -a)
            rot('forearm' + s, 2 * a)
            rot('hand' + s, -a)
            rot('thigh' + s, b)
            rot('calf' + s, -2 * b)
            rot('foot' + s, b)
    def attempt(a, b):
        reset_pose(arm)
        upper()
        legs(a, b)
        h, _ = _paw_heights(body, sets)
        return h
    lo, hi = -30.0, 30.0
    f_lo = attempt(crouch, lo)
    f_lo = f_lo['hind'] - f_lo['front']
    b = 0.0
    for _ in range(12):
        mid = (lo + hi) / 2
        h = attempt(crouch, mid)
        d = h['hind'] - h['front']
        if (d > 0) == (f_lo > 0):
            lo, f_lo = mid, d
        else:
            hi = mid
        b = mid
    h = attempt(crouch, b)
    arm.location.z -= min(h.values())
    bpy.context.view_layer.update()
    h2, _ = _paw_heights(body, sets)
    return dict(hind_fold=round(b, 2), front=round(h2['front'], 4), hind=round(h2['hind'], 4))


_materials_v1 = materials


def materials(body):
    # rebuild the plain base material from the packed Dire Wolf textures (the V1 file only kept the derived one)
    if 'DireWolf' not in bpy.data.materials:
        m = bpy.data.materials.new('DireWolf')
        nt = m.node_tree
        b = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
        def tex(name, data):
            n = nt.nodes.new('ShaderNodeTexImage')
            n.image = bpy.data.images[name]
            if data:
                n.image.colorspace_settings.name = 'Non-Color'
            return n
        nt.links.new(tex('DireWolf_BaseColor', False).outputs['Color'], b.inputs['Base Color'])
        sep = nt.nodes.new('ShaderNodeSeparateColor')
        nt.links.new(tex('DireWolf_MetalRough', True).outputs['Color'], sep.inputs[0])
        nt.links.new(sep.outputs[1], b.inputs['Roughness'])
        nm = nt.nodes.new('ShaderNodeNormalMap')
        nt.links.new(tex('DireWolf_Normal', True).outputs['Color'], nm.inputs['Color'])
        nt.links.new(nm.outputs[0], b.inputs['Normal'])
    M = _materials_v1(body)
    S = K.surface
    M['growth'] = S('MW2_Growth', '6f6d17', rough=0.6, grime=1.0, dust=0.0, mott=('3b4110', 55.0, 0.85, '9a8e2a'), bump='leather',
                    bump_s=1.0, sss=0.25)
    g = M['growth'].node_tree
    b = next(n for n in g.nodes if n.type == 'BSDF_PRINCIPLED')
    vo = g.nodes.new('ShaderNodeTexVoronoi')            # pits
    vo.inputs['Scale'].default_value = 95.0
    tc = g.nodes.new('ShaderNodeTexCoord')
    g.links.new(tc.outputs['Object'], vo.inputs['Vector'])
    rm = g.nodes.new('ShaderNodeMapRange')
    rm.inputs[1].default_value, rm.inputs[2].default_value = 0.05, 0.3
    g.links.new(vo.outputs['Distance'], rm.inputs[0])
    mx = g.nodes.new('ShaderNodeMix')
    mx.data_type, mx.blend_type = 'RGBA', 'MULTIPLY'
    mx.inputs[0].default_value = 1.0
    g.links.new(b.inputs['Base Color'].links[0].from_socket, mx.inputs[6])
    cc = g.nodes.new('ShaderNodeCombineColor')
    for i in range(3):
        g.links.new(rm.outputs[0], cc.inputs[i])
    g.links.new(cc.outputs[0], mx.inputs[7])
    g.links.new(mx.outputs[2], b.inputs['Base Color'])
    bump = next(n for n in g.nodes if n.type == 'BUMP')
    bump.inputs['Distance'].default_value = 0.01
    g.links.new(rm.outputs[0], bump.inputs['Height'])
    eb = next(n for n in M['eye'].node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    eb.inputs['Emission Strength'].default_value = 3.2
    return M


VIEWS = dict(hero=(-36, 7, 60, 4.7), side=(-90, 4, 85, 8.2), gameplay=(-30, 45, 85, 8.8), head=(-52, 6, 120, 4.6))
