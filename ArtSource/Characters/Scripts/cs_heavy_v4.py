"""Cargo Squad - Heavy v4 (high-detail look-dev model, built from ArtSource/References/Heavy_Approved_Concept.png).

Run inside the live Blender session:
    import sys; sys.path.insert(0, r"<repo>/ArtSource/Characters/Scripts")
    import cs_heavy_v4 as H; H.build(); H.pose('ready')

The model lives in its own scene (CS_Heavy_v4). Parts are authored in a straight rest pose and parented to joint
empties (a review posing aid, NOT the game rig), so the same meshes can be shown in an A-pose or holding the shotgun.
"""
import bpy, bmesh, math, importlib
from math import sin, cos, pi, radians
from mathutils import Vector, Matrix
import hv4_kit as K
from hv4_kit import Part, T, Rx, Ry, Rz, face_to, rbox, plate, wrap, tube, cyl, ball, sweep, prism

SCENE = 'CS_Heavy_v4'
REPO = r"C:\kfupm\T261\SWE402\SWE402-project-CargoSquad"

# joint empties: rest position, parent  (left side; right is mirrored)
JOINTS = dict(ROOT=((0, 0, 0), None), PELVIS=((0, 0, 0.96), 'ROOT'), CHEST=((0, 0, 1.16), 'PELVIS'),
              HEAD=((0, 0, 1.58), 'CHEST'), GUN=((0, -0.5, 1.1), 'ROOT'))
SIDE = dict(UARM=((0.298, 0, 1.49), 'CHEST'), FARM=((0.298, 0, 1.19), 'UARM'), HAND=((0.298, 0, 0.925), 'FARM'),
            THIGH=((0.162, 0, 0.94), 'PELVIS'), SHIN=((0.172, 0, 0.50), 'THIGH'), FOOT=((0.172, 0, 0.10), 'SHIN'))
# authored (slim) joint positions -> bulked-up rest positions; parts are widened about their joint when finished
AUTH = dict(ROOT=(0, 0, 0), PELVIS=(0, 0, 0.96), CHEST=(0, 0, 1.16), HEAD=(0, 0, 1.58), GUN=(0, -0.5, 1.1),
            UARM=(0.255, 0, 1.49), FARM=(0.255, 0, 1.19), HAND=(0.255, 0, 0.925),
            THIGH=(0.14, 0, 0.94), SHIN=(0.15, 0, 0.50), FOOT=(0.15, 0, 0.10))
BULK = dict(ROOT=(1, 1, 1), PELVIS=(1.14, 1.14, 1), CHEST=(1.16, 1.16, 1), HEAD=(1.07, 1.07, 1.05), GUN=(1, 1, 1),
            UARM=(1.18, 1.18, 1), FARM=(1.18, 1.18, 1), HAND=(1.12, 1.12, 1.12),
            THIGH=(1.2, 1.2, 1), SHIN=(1.17, 1.17, 1), FOOT=(1.18, 1.1, 1.03))
WRINKLE = 0.022
PACK_C = Vector((0, 0.355, 1.39))
PACK_W, PACK_D, PACK_H = 0.52, 0.33, 0.74
HOLD = Vector((0.222, -0.008, 0.775))          # axis of an object gripped by the (left) hand, rest pose
GUN_FORE = Vector((0, -0.33, 0.05))            # pump hold point in gun space (gun origin = pistol grip)


def materials():
    S = K.surface
    return dict(
        olive=S('HV4_ArmorOlive', '4c4630', rough=0.6, wear=0.95, grime=0.7, dust=0.25, mott=('2b2c1c', 12.0, 0.85, '6f6644'),
                scratch=0.4, rust=0.2, bump='metal', bump_s=0.45),
        pack=S('HV4_PackShell', '33312b', rough=0.6, wear=0.85, grime=0.6, dust=0.3, rust=0.6, scratch=0.8, mott=('45412a', 5.0, 0.6), bump='metal',
               bump_s=0.3),
        steel=S('HV4_DarkSteel', '2a2a2c', rough=0.45, metal=1.0, wear=0.65, under=('9a9c9f', 1.0, 0.3), rust=0.35, grime=0.4,
                dust=0.2, scratch=0.5, bump='metal', bump_s=0.15),
        bare=S('HV4_BareSteel', '8b8d90', rough=0.4, metal=1.0, rust=0.25, grime=0.5, dust=0.2, scratch=0.6, bump='metal',
               bump_s=0.12),
        cloth=S('HV4_ClothCharcoal', '141416', rough=0.86, grime=0.35, dust=0.14, mott=('0b0b0c', 10.0, 0.7, '27272b'), bump='fabric', bump_s=1.0, sheen=0.06),
        cloth_olive=S('HV4_ClothOlive', '423d28', rough=0.92, wear=0.5, under=('76704e', 0.0, 0.95), grime=0.95, dust=0.22,
                      mott=('262718', 16.0, 0.75, '5d5638'), bump='fabric', bump_s=1.0, sheen=0.05),
        web=S('HV4_WebbingTan', '7d6229', rough=0.88, wear=0.3, under=('a8935a', 0.0, 0.95), grime=0.7, dust=0.15, bump='fabric',
              bump_s=0.6),
        leather=S('HV4_LeatherBrown', '4e3520', rough=0.6, wear=0.55, under=('a37a4c', 0.0, 0.8), grime=0.6, dust=0.3,
                  scratch=0.4, bump='leather', bump_s=0.5),
        boot=S('HV4_BootLeather', '2e2c26', rough=0.7, wear=0.5, under=('6b624e', 0.0, 0.85), grime=0.6, dust=0.45, scratch=0.5,
               bump='leather', bump_s=0.5),
        glove=S('HV4_Glove', '3e3d27', rough=0.75, wear=0.4, under=('8a8160', 0.0, 0.9), grime=0.6, dust=0.35, bump='leather',
                bump_s=0.45),
        rubber=S('HV4_Rubber', '121213', rough=0.65, wear=0.15, under=('3d3d3d', 0.0, 0.8), grime=0.4, dust=0.2, bump='rubber',
                 bump_s=0.3),
        hazard=S('HV4_HazardPaint', 'c9650f', rough=0.55, wear=0.8, under=('3a3835', 1.0, 0.4), grime=0.5, dust=0.3,
                 stripes=('201d1a', 0.075, (1.0, 1.0, 1.0)), scratch=0.6, bump='metal', bump_s=0.2),
        orange=S('HV4_OrangePaint', 'c9650f', rough=0.55, wear=0.8, under=('3a3835', 1.0, 0.4), grime=0.5, dust=0.3, rust=0.3,
                 scratch=0.6, bump='metal', bump_s=0.2),
        brass=S('HV4_Brass', 'b08d3a', rough=0.35, metal=1.0, grime=0.5, dust=0.1, scratch=0.4, bump='metal', bump_s=0.1),
        shell=S('HV4_ShellRed', '8a1a14', rough=0.4, wear=0.2, under=('c9b9a0', 0.0, 0.6), grime=0.5, dust=0.2, bump='rubber',
                bump_s=0.15),
        wood=S('HV4_StockBrown', '4a2c18', rough=0.55, wear=0.5, under=('8e6a44', 0.0, 0.7), grime=0.5, dust=0.2, scratch=0.6,
               bump='leather', bump_s=0.3),
        skin=S('HV4_Skin', '946848', rough=0.55, grime=0.5, dust=0.15, bump='rubber', bump_s=0.15, sss=0.15),
        eye=S('HV4_EyeWhite', 'd9d4cc', rough=0.25, grime=0.2, dust=0.0, bump='rubber', bump_s=0.0),
        iris=S('HV4_Iris', '2a1c12', rough=0.2, grime=0.0, dust=0.0, bump='rubber', bump_s=0.0),
        hair=S('HV4_Brow', '1d1612', rough=0.8, grime=0.0, dust=0.1, bump='fabric', bump_s=0.4),
    )


# ============================================================================== scene + posing aid
def _scene():
    sc = bpy.data.scenes.get(SCENE) or bpy.data.scenes.new(SCENE)
    for ob in list(sc.collection.all_objects):
        data = ob.data
        bpy.data.objects.remove(ob, do_unlink=True)
        if data is not None and data.users == 0:
            for coll in (bpy.data.meshes, bpy.data.cameras, bpy.data.lights):
                if data.name in coll and coll[data.name] == data:
                    coll.remove(data)
                    break
    sc.unit_settings.system = 'METRIC'
    return sc


def _empties(sc):
    E = {}
    def mk(name, pos, parent):
        e = bpy.data.objects.new('HV4_J_' + name, None)
        e.empty_display_type = 'SPHERE'
        e.empty_display_size = 0.025
        e.hide_render = True
        sc.collection.objects.link(e)
        e['rest'] = pos
        if parent:
            e.parent = E[parent]
            e.matrix_parent_inverse = Matrix.Identity(4)
            e.location = Vector(pos) - Vector(E[parent]['rest'])
        else:
            e.location = pos
        E[name] = e
    for n, (p, par) in JOINTS.items():
        mk(n, p, par)
    for n, (p, par) in SIDE.items():
        for tag, s in (('_L', 1), ('_R', -1)):
            mk(n + tag, (p[0] * s, p[1], p[2]), par if par in JOINTS else par + tag)
    return E


class Rig:
    def __init__(self, sc, existing=False):
        self.sc = sc
        self.E = ({o.name[6:]: o for o in sc.objects if o.name.startswith('HV4_J_')} if existing else _empties(sc))
        self.only = None
        self.objs = []

    def fin(self, part, joint, subsurf=0, mirror=False, solidify=0.0):
        if self.only and not part.name.startswith(self.only):
            part.bm.free()
            return None
        # bulk up: widen the part about its joint and move it to the joint's bulked-up rest position
        base = joint[:-2] if joint[-2:] in ('_L', '_R') else joint
        sx = -1.0 if joint.endswith('_R') else 1.0
        a = Vector(AUTH[base])
        a.x *= sx
        n = Vector(self.E[joint]['rest'])
        k = BULK[base]
        for v in part.bm.verts:
            d = v.co - a
            v.co = n + Vector((d.x * k[0], d.y * k[1], d.z * k[2]))
        ob = part.to_object(self.sc.collection, subsurf=subsurf)
        if subsurf:
            # cloth: real wrinkle relief (banded noise in mesh space, so it stays put when the part is posed)
            ob.modifiers['Subdivision'].levels = 2
            ob.modifiers['Subdivision'].render_levels = 2
            tex = bpy.data.textures.get('HV4_Folds')
            if tex is None:
                tex = bpy.data.textures.new('HV4_Folds', 'WOOD')
                tex.wood_type = 'BANDNOISE'
                tex.noise_scale = 0.11
                tex.turbulence = 9.0
            fine = bpy.data.textures.get('HV4_FoldsFine')
            if fine is None:
                fine = bpy.data.textures.new('HV4_FoldsFine', 'CLOUDS')
                fine.noise_scale = 0.035
                fine.noise_depth = 2
            for nm, tx, st in (('Folds', tex, WRINKLE), ('FoldsFine', fine, WRINKLE * 0.45)):
                d = ob.modifiers.new(nm, 'DISPLACE')
                d.texture = tx
                d.texture_coords = 'LOCAL'
                d.strength = st
                d.mid_level = 0.5
        if solidify:
            m = ob.modifiers.new('Solidify', 'SOLIDIFY')
            m.thickness = solidify
            m.offset = -1.0
        self._bind(ob, joint)
        if mirror:
            o2 = K.mirrored(ob, ob.name[:-2] + '_R', self.sc.collection)
            self._bind(o2, joint[:-2] + '_R')
        return ob

    def _bind(self, ob, joint):
        e = self.E[joint]
        ob.parent = e
        ob.matrix_parent_inverse = Matrix.Translation(-Vector(e['rest']))
        ob['joint'] = joint
        self.objs.append(ob)


# ============================================================================== body parts (rest pose, world coords)
def _legs(R, M):
    # ---- thigh
    c = Part('HV4_Trouser_L')
    c.add(tube([((0.135, 0, 1.01), 0.128, 0.138), ((0.145, 0, 0.82), 0.130, 0.135), ((0.150, 0, 0.62), 0.112, 0.118),
                ((0.150, 0, 0.46), 0.100, 0.106)], 22, steps=12, fold=(0.06, 6.5, 0.9)), M['cloth'])
    R.fin(c, 'THIGH_L', subsurf=1, mirror=True)
    h = Part('HV4_ThighGear_L')
    h.add(tube([((0.147, 0, 0.715), 0.131, 0.136), ((0.148, 0, 0.755), 0.134, 0.139)], 22), M['hazard'])
    h.add(tube([((0.142, 0, 0.865), 0.137, 0.142), ((0.141, 0, 0.895), 0.138, 0.143)], 22), M['leather'])
    h.buckle((0.275, -0.03, 0.88), (1, -0.2, 0), M['bare'])
    h.pouch((0.283, 0.0, 0.775), (0.135, 0.07, 0.18), (1, -0.1, 0), M['cloth_olive'], M['cloth_olive'], M['web'], M['bare'],
            decal=(M['orange'], M['steel']))
    h.add(rbox((0.012, 0.04, 0.17), 0.002, 1), M['web'], T(0.268, 0.0, 0.95))
    R.fin(h, 'THIGH_L', mirror=True)
    # ---- shin
    c = Part('HV4_ShinCloth_L')
    c.add(tube([((0.15, 0, 0.56), 0.102, 0.108), ((0.15, 0.012, 0.41), 0.104, 0.114), ((0.15, 0.006, 0.25), 0.098, 0.104),
                ((0.15, 0, 0.15), 0.086, 0.092)], 22, steps=10, fold=(0.065, 7.5, 2.0)), M['cloth'])
    R.fin(c, 'SHIN_L', subsurf=1, mirror=True)
    g = Part('HV4_LegArmor_L')
    # knee: padded backing + angular plate with bolts
    g.add(plate(0.21, 0.24, 0.034, dome=0.085, p=2.6, cut=0.2, nu=10, nv=10), M['cloth'], T(0.15, -0.092, 0.52))
    g.add(plate(0.162, 0.188, 0.018, dome=0.095, p=2.6, cut=0.22, nu=10, nv=10), M['olive'], T(0.15, -0.118, 0.52))
    for bx, bz in ((-0.06, 0.07), (0.06, 0.07), (-0.06, -0.07), (0.06, -0.07)):
        g.bolt((0.15 + bx * 0.9, -0.126, 0.52 + bz * 0.9), (0, -1, 0), M['bare'], 0.006)
    g.add(tube([((0.15, 0.0, 0.495), 0.113, 0.118), ((0.15, 0.0, 0.535), 0.113, 0.118)], 20), M['web'])
    # shin guard: stacked boxy plates, hazard strip, side wings
    g.add(plate(0.205, 0.255, 0.032, dome=0.022, p=9, bend=0.17, cut=0.08), M['olive'], T(0.15, -0.108, 0.325))
    g.add(plate(0.16, 0.075, 0.022, dome=0.012, p=7, bend=0.15), M['olive'], T(0.15, -0.126, 0.435) @ Rx(-14))
    g.add(rbox((0.16, 0.012, 0.052), 0.003, 1), M['hazard'], T(0.15, -0.139, 0.268))
    g.add(wrap(0.108, 38, 125, 0.21, 0.02, r2=0.118, nu=10), M['olive'], T(0.15, 0.0, 0.33))
    g.add(wrap(0.108, -125, -38, 0.21, 0.02, r2=0.118, nu=10), M['olive'], T(0.15, 0.0, 0.33))
    for bz in (0.24, 0.41):
        for bx in (-0.075, 0.075):
            g.bolt((0.15 + bx, -0.132, bz), (0, -1, 0), M['bare'], 0.0065)
    for z in (0.265, 0.385):
        g.add(tube([((0.15, 0.012, z), 0.121, 0.127), ((0.15, 0.012, z + 0.03), 0.121, 0.127)], 20), M['leather'])
        g.buckle((0.272, 0.01, z + 0.015), (1, 0, 0), M['bare'])
    R.fin(g, 'SHIN_L', mirror=True)
    # ---- boot
    b = Part('HV4_Boot_L')
    X = 0.15
    # sole: shaped outsole with toe spring, heel block, welt and cross lugs that follow the outline
    outline = [(0.122, 0.040), (0.095, 0.056), (0.03, 0.060), (-0.05, 0.066), (-0.12, 0.070), (-0.175, 0.064), (-0.21, 0.046),
               (-0.228, 0.022)]
    b.add(K.loft([(y, X, hw + 0.006, 0.052 + (0.012 if y < -0.17 else 0.0), 0.022 + (0.012 if y < -0.17 else 0.0))
                  for y, hw in outline], 18, p=5.0), M['rubber'])
    b.add(K.loft([(0.124, X, 0.040, 0.03, 0.0), (0.095, X, 0.058, 0.03, 0.0), (0.03, X, 0.060, 0.03, 0.0)], 14, p=6.0),
          M['rubber'])
    for i in range(6):
        y = -0.045 - 0.031 * i
        hw = 0.064 if i < 4 else 0.056 - 0.006 * (i - 4)
        b.add(rbox((hw * 2, 0.02, 0.02), 0.003, 1), M['rubber'], T(X, y, 0.012 + (0.006 if i > 4 else 0)))
    b.add(K.loft([(y, X, hw + 0.009, 0.062 + (0.012 if y < -0.17 else 0.0), 0.05 + (0.012 if y < -0.17 else 0.0))
                  for y, hw in outline], 18, p=5.0), M['leather'])
    # upper: one lofted leather form from heel counter over the instep to a rounded toe box
    b.add(K.loft([(0.118, X, 0.036, 0.15, 0.052), (0.092, X, 0.052, 0.20, 0.052), (0.045, X, 0.058, 0.215, 0.052),
                  (-0.01, X, 0.058, 0.185, 0.052), (-0.07, X, 0.062, 0.145, 0.052), (-0.13, X, 0.066, 0.118, 0.056),
                  (-0.18, X, 0.062, 0.108, 0.062), (-0.212, X, 0.046, 0.098, 0.064), (-0.226, X, 0.022, 0.086, 0.066)],
                 20, p=2.8, flat_bottom=0.6), M['boot'])
    # reinforced toe cap and heel counter as separate overlapping leather panels
    b.add(K.loft([(-0.135, X, 0.068, 0.121, 0.058), (-0.18, X, 0.064, 0.112, 0.062), (-0.214, X, 0.048, 0.101, 0.064),
                  (-0.229, X, 0.023, 0.088, 0.066)], 20, p=2.8, flat_bottom=0.6), M['rubber'])
    b.add(K.loft([(0.121, X, 0.038, 0.135, 0.054), (0.095, X, 0.054, 0.15, 0.054), (0.055, X, 0.060, 0.14, 0.054)], 20, p=2.8,
                 flat_bottom=0.6), M['leather'])
    # shaft with padded collar, tongue, eyelets and laces
    b.add(tube([((X, 0.045, 0.17), 0.058, 0.066), ((X, 0.03, 0.235), 0.062, 0.070), ((X, 0.018, 0.30), 0.068, 0.074),
                ((X, 0.012, 0.335), 0.072, 0.077)], 20, steps=7, fold=(0.012, 14, 0.5)), M['boot'])
    b.add(tube([((X, 0.012, 0.325), 0.076, 0.081), ((X, 0.012, 0.352), 0.077, 0.082)], 20), M['leather'])
    b.add(sweep([(X, -0.105, 0.132), (X, -0.06, 0.168), (X, -0.035, 0.225), (X, -0.045, 0.30), (X, -0.052, 0.345)],
                rect=(0.05, 0.012), normal=lambda q: Vector((0, -1, 0.5)), smooth=5), M['leather'])
    for i in range(6):
        z = 0.168 + 0.03 * i
        yy = -0.062 + 0.012 * min(i, 2) - 0.004 * max(0, i - 2)
        b.add(cyl((X - 0.03, yy - 0.004, z), (X + 0.03, yy - 0.004, z + 0.012), 0.0035, seg=6), M['web'])
        b.add(cyl((X - 0.03, yy - 0.004, z + 0.012), (X + 0.03, yy - 0.004, z), 0.0035, seg=6), M['web'])
        for sx in (-1, 1):
            b.add(cyl((X + sx * 0.032, yy, z + 0.006), (X + sx * 0.032, yy - 0.006, z + 0.006), 0.006, seg=8), M['bare'])
    # one ankle strap with buckle, armoured instep plate with bolts (kept from v3, slimmer)
    b.add(tube([((X, 0.03, 0.238), 0.066, 0.075), ((X, 0.03, 0.266), 0.067, 0.076)], 20), M['web'])
    b.buckle((X + 0.068, 0.02, 0.252), (1, 0, 0), M['bare'], 0.03, 0.022)
    b.add(plate(0.10, 0.11, 0.012, dome=0.03, p=5, cut=0.14), M['olive'], T(X, -0.118, 0.142) @ Rx(66))
    for bx in (-0.036, 0.036):
        b.bolt((X + bx, -0.145, 0.136), (0, -0.4, 0.9), M['bare'], 0.005)
    R.fin(b, 'FOOT_L', mirror=True)
    # right shin only: tool sheath on the outer side (visible on the near leg in the concept)
    s = Part('HV4_Sheath')
    s.add(rbox((0.036, 0.06, 0.25), 0.006, 2, taper=(0.9, 0.8)), M['steel'], T(-0.292, -0.01, 0.335))
    s.add(cyl((-0.292, -0.01, 0.46), (-0.292, -0.01, 0.56), 0.016, seg=10, bevel=0.003), M['rubber'])
    s.add(rbox((0.04, 0.07, 0.02), 0.003, 1), M['leather'], T(-0.29, -0.01, 0.40))
    s.add(rbox((0.04, 0.07, 0.02), 0.003, 1), M['leather'], T(-0.29, -0.01, 0.27))
    R.fin(s, 'SHIN_R')


def _hips(R, M):
    c = Part('HV4_HipCloth')
    c.add(tube([((0, 0, 0.83), 0.205, 0.15, 0.165, 2.6), ((0, 0, 0.93), 0.228, 0.168, 0.182, 2.6),
                ((0, 0, 1.05), 0.222, 0.168, 0.172, 2.6)], 26, steps=6, fold=(0.03, 8, 0.3)), M['cloth'])
    R.fin(c, 'PELVIS', subsurf=1)
    b = Part('HV4_Belt')
    b.add(tube([((0, 0, 0.985), 0.236, 0.182, 0.192, 2.7), ((0, 0, 1.05), 0.236, 0.182, 0.192, 2.7)], 28), M['leather'])
    b.add(rbox((0.075, 0.014, 0.055), 0.003, 1), M['bare'], T(0, -0.188, 1.018))
    b.add(rbox((0.04, 0.012, 0.03), 0.002, 1), M['steel'], T(0, -0.194, 1.018))
    for s in (1, -1):
        b.pouch((s * 0.112, -0.218, 0.955), (0.155, 0.08, 0.175), (s * 0.22, -1, 0), M['cloth_olive'], M['cloth_olive'], M['web'],
                M['bare'], decal=(M['orange'], M['steel']))
    b.pouch((0.243, -0.035, 0.95), (0.115, 0.07, 0.15), (1, -0.25, 0), M['cloth_olive'], M['cloth_olive'], M['web'], M['bare'])
    b.pouch((-0.215, 0.105, 0.95), (0.12, 0.07, 0.15), (-0.8, 0.6, 0), M['cloth_olive'], M['cloth_olive'], M['web'], M['bare'])
    b.pouch((0.0, 0.215, 0.905), (0.27, 0.09, 0.15), (0, 1, 0), M['cloth_olive'], M['cloth_olive'], M['web'], M['bare'])
    # groin flap with strap
    b.add(rbox((0.15, 0.035, 0.21), 0.004, 2, puff=0.12), M['cloth_olive'], T(0, -0.168, 0.845))
    b.add(rbox((0.03, 0.008, 0.2), 0.002, 1), M['web'], T(0, -0.188, 0.85))
    # carabiner hanging from the belt
    loop = [(0.0, 0.0), (0.016, 0.004), (0.02, -0.03), (0.016, -0.066), (0.0, -0.072), (-0.016, -0.066), (-0.02, -0.03),
            (-0.016, 0.004)]
    b.add(sweep([(0.03 + x, -0.262, 0.99 + z) for x, z in loop] + [(0.03, -0.262, 0.99)], r=0.0045, seg=8, closed=True,
                smooth=3), M['bare'])
    # right hip: orange gas canister in a strap cradle
    b.add(cyl((-0.268, -0.02, 0.78), (-0.268, -0.02, 0.985), 0.037, seg=18, bevel=0.006), M['orange'])
    b.add(cyl((-0.268, -0.02, 0.985), (-0.268, -0.02, 1.02), 0.02, seg=12, bevel=0.003), M['bare'])
    b.add(cyl((-0.268, -0.02, 1.02), (-0.268, -0.02, 1.032), 0.026, seg=12, bevel=0.002), M['steel'])
    for z in (0.83, 0.93):
        b.add(cyl((-0.268, -0.02, z), (-0.268, -0.02, z + 0.022), 0.041, seg=18), M['leather'])
    R.fin(b, 'PELVIS')


def _torso(R, M):
    c = Part('HV4_TorsoSuit')
    c.add(tube([((0, 0, 1.01), 0.216, 0.166, 0.170, 2.6), ((0, 0, 1.15), 0.222, 0.176, 0.176, 2.6),
                ((0, 0, 1.33), 0.236, 0.186, 0.182, 2.6), ((0, 0, 1.47), 0.218, 0.162, 0.168, 2.6),
                ((0, 0, 1.55), 0.125, 0.112, 0.118, 2.3), ((0, 0.005, 1.62), 0.082, 0.084, 0.086, 2.0)], 28, steps=10,
               fold=(0.012, 8, 0.4)), M['cloth'])
    R.fin(c, 'CHEST', subsurf=1)
    v = Part('HV4_Vest')
    v.add(tube([((0, 0, 1.065), 0.250, 0.203, 0.202, 2.9), ((0, 0, 1.19), 0.254, 0.208, 0.205, 2.9),
                ((0, 0, 1.305), 0.258, 0.210, 0.205, 2.9)], 30, steps=4), M['cloth_olive'])
    v.add(plate(0.35, 0.37, 0.042, dome=0.016, p=7, bend=0.42, nu=12, nv=10, cut=0.07), M['olive'], T(0, -0.172, 1.34))
    v.add(plate(0.27, 0.095, 0.03, dome=0.01, p=7, bend=0.4, cut=0.1), M['olive'], T(0, -0.176, 1.51) @ Rx(-24))
    v.add(plate(0.36, 0.40, 0.04, dome=0.016, p=7, bend=0.44, nu=12, nv=10, cut=0.07), M['olive'], T(0, 0.17, 1.35) @ Rz(180))
    # molle rows on the front plate
    for z in (1.25, 1.29, 1.33):
        v.add(rbox((0.30, 0.006, 0.022), 0.002, 1), M['web'], T(0, -0.207, z))
    # belly pouches
    for s in (1, -1):
        v.pouch((s * 0.082, -0.238, 1.165), (0.13, 0.062, 0.155), (s * 0.12, -1, 0), M['cloth_olive'], M['cloth_olive'], M['web'],
                M['bare'], decal=(M['orange'], M['steel']) if s < 0 else None)
    v.pouch((-0.205, -0.135, 1.17), (0.10, 0.06, 0.15), (-0.75, -0.65, 0), M['cloth_olive'], M['cloth_olive'], M['web'], M['bare'])
    v.pouch((0.205, -0.135, 1.17), (0.10, 0.06, 0.15), (0.75, -0.65, 0), M['cloth_olive'], M['cloth_olive'], M['web'], M['bare'])
    # right chest admin pouch, left chest shell holder (3 red shells with brass heads)
    v.pouch((-0.088, -0.228, 1.385), (0.135, 0.05, 0.125), (-0.08, -1, 0), M['cloth_olive'], M['cloth_olive'], M['web'], M['bare'])
    v.add(rbox((0.15, 0.03, 0.10), 0.004, 2, puff=0.1), M['cloth_olive'], T(0.088, -0.222, 1.395))
    for i in range(3):
        x = 0.048 + 0.04 * i
        v.add(cyl((x, -0.25, 1.352), (x, -0.25, 1.432), 0.0155, seg=14, bevel=0.002), M['shell'])
        v.add(cyl((x, -0.25, 1.432), (x, -0.25, 1.452), 0.0162, seg=14, bevel=0.002), M['brass'])
    v.add(rbox((0.14, 0.014, 0.022), 0.002, 1), M['web'], T(0.088, -0.262, 1.385))
    v.add(rbox((0.05, 0.035, 0.065), 0.005, 2), M['steel'], T(-0.158, -0.198, 1.475))
    v.add(cyl((-0.158, -0.198, 1.505), (-0.158, -0.198, 1.56), 0.006, seg=8), M['rubber'])
    # padded shoulder straps with buckles
    for s in (1, -1):
        path = [(s * 0.128, -0.196, 1.43), (s * 0.136, -0.14, 1.552), (s * 0.14, -0.02, 1.598), (s * 0.138, 0.10, 1.565),
                (s * 0.13, 0.172, 1.44)]
        v.add(sweep(path, rect=(0.082, 0.026), normal=lambda p: p - Vector((p.x * 0.85, 0.0, 1.40)), smooth=6), M['web'])
        v.buckle((s * 0.13, -0.19, 1.485), (0, -1, 0.35), M['bare'], 0.05, 0.03)
        v.add(rbox((0.05, 0.014, 0.05), 0.003, 1), M['leather'], T(s * 0.132, -0.185, 1.455) @ Rx(-20))
    R.fin(v, 'CHEST')
    k = Part('HV4_Cowl')
    ring = [(0.142 * cos(a), 0.015 + 0.132 * sin(a), 1.592 - 0.03 * cos(a * 1.0 + pi / 2) * -1 + 0.012 * sin(3 * a))
            for a in [2 * pi * i / 14 for i in range(14)]]
    k.add(sweep(ring + [ring[0]], r=0.05, seg=10, closed=True, smooth=3), M['cloth_olive'])
    k.add(ball((0.135, 0.085, 0.115), 16, 10), M['cloth_olive'], T(0, 0.125, 1.60))
    R.fin(k, 'CHEST', subsurf=1)


def _backpack(R, M):
    p = Part('HV4_Backpack')
    C = PACK_C
    W, D, H = PACK_W, PACK_D, PACK_H
    p.add(rbox((W, D, H), 0.02, 3), M['pack'], T(C))
    hw, hd, hh = W / 2, D / 2, H / 2
    rail = 0.036
    for sx in (1, -1):
        for sy in (1, -1):
            p.add(rbox((rail, rail, H + 0.02), 0.006, 2), M['steel'], T(C + Vector((sx * hw, sy * hd, 0))))
        for sz in (1, -1):
            p.add(rbox((rail, D + 0.02, rail), 0.006, 2), M['steel'], T(C + Vector((sx * hw, 0, sz * hh))))
    for sy in (1, -1):
        for sz in (1, -1):
            p.add(rbox((W + 0.02, rail, rail), 0.006, 2), M['steel'], T(C + Vector((0, sy * hd, sz * hh))))
    for sx in (1, -1):
        for sy in (1, -1):
            for sz in (1, -1):
                cc = C + Vector((sx * hw, sy * hd, sz * hh))
                p.add(rbox((0.075, 0.075, 0.075), 0.012, 2), M['steel'], T(cc))
                p.bolt(cc + Vector((sx * 0.039, 0, 0)), (sx, 0, 0), M['bare'], 0.008)
                p.bolt(cc + Vector((0, sy * 0.039, 0)), (0, sy, 0), M['bare'], 0.008)
    # lid + latches + top handle
    p.add(rbox((W + 0.03, D + 0.03, 0.05), 0.01, 2), M['pack'], T(C + Vector((0, 0, hh + 0.012))))
    for x in (-0.16, 0.16):
        p.add(rbox((0.05, 0.02, 0.085), 0.004, 1), M['bare'], T(C + Vector((x, hd + 0.016, hh - 0.03))))
        p.add(rbox((0.03, 0.016, 0.03), 0.003, 1), M['steel'], T(C + Vector((x, hd + 0.03, hh - 0.05))))
    p.add(sweep([(-0.13, C.y, C.z + hh + 0.03), (-0.115, C.y, C.z + hh + 0.085), (0.115, C.y, C.z + hh + 0.085),
                 (0.13, C.y, C.z + hh + 0.03)], r=0.012, seg=10, smooth=4), M['steel'])
    # straps: two horizontal belts all round, two vertical on the back face, with buckles
    for dz in (-0.19, 0.17):
        z = C.z + dz
        p.add(rbox((W + 0.018, 0.012, 0.045), 0.003, 1), M['leather'], T(0, C.y + hd + 0.007, z))
        for sx in (1, -1):
            p.add(rbox((0.012, D + 0.016, 0.045), 0.003, 1), M['leather'], T(sx * (hw + 0.007), C.y, z))
            p.buckle((sx * (hw + 0.014), C.y - 0.04, z), (sx, 0, 0), M['bare'], 0.05, 0.036)
        p.buckle((0.0, C.y + hd + 0.014, z), (0, 1, 0), M['bare'], 0.055, 0.038)
    for x in (-0.15, 0.15):
        p.add(rbox((0.05, 0.012, H + 0.01), 0.003, 1), M['leather'], T(x, C.y + hd + 0.008, C.z))
        p.buckle((x, C.y + hd + 0.016, C.z + 0.30), (0, 1, 0), M['bare'], 0.05, 0.036)
    # hazard panels (back face and both sides) + plate rivets
    for dz in (0.30, -0.06):
        p.add(rbox((0.19, 0.01, 0.075), 0.003, 1), M['hazard'] if dz > 0 else M['olive'], T(0.0, C.y + hd + 0.006, C.z + dz))
        for sx in (1, -1):
            p.add(rbox((0.01, 0.17, 0.075), 0.003, 1), M['hazard'] if sx < 0 and dz > 0 else M['olive'], T(sx * (hw + 0.006), C.y + 0.02, C.z + dz))
    for sx in (1, -1):
        for i in range(7):
            z = C.z - 0.36 + 0.12 * i
            p.bolt((sx * (hw + 0.019), C.y + hd, z), (sx, 0, 0), M['bare'], 0.006)
            p.bolt((sx * hw, C.y + hd + 0.019, z), (0, 1, 0), M['bare'], 0.006)
    # side pouch + hanging strap with snap hook (his right side, as in the concept), bedroll-style tube on the left
    p.pouch((-(hw + 0.04), C.y + 0.01, C.z - 0.26), (0.17, 0.07, 0.2), (-1, 0, 0), M['cloth_olive'], M['cloth_olive'], M['web'],
            M['bare'])
    p.pouch(((hw + 0.04), C.y + 0.01, C.z - 0.26), (0.17, 0.07, 0.2), (1, 0, 0), M['cloth_olive'], M['cloth_olive'], M['web'],
            M['bare'])
    x = -(hw + 0.03)
    p.add(sweep([(x, C.y - 0.11, C.z + 0.12), (x - 0.03, C.y - 0.13, C.z - 0.10), (x - 0.035, C.y - 0.12, C.z - 0.36),
                 (x - 0.02, C.y - 0.10, C.z - 0.47)], rect=(0.03, 0.008), normal=lambda q: Vector((-1, -0.2, 0)), smooth=6),
          M['leather'])
    hook = [(0, 0), (0.02, -0.01), (0.026, -0.05), (0.014, -0.085), (-0.01, -0.09), (-0.024, -0.06), (-0.02, -0.02)]
    p.add(sweep([(x - 0.02, C.y - 0.10 + a * 0.3, C.z - 0.47 + b) for a, b in hook] + [(x - 0.02, C.y - 0.10, C.z - 0.47)],
                r=0.006, seg=8, closed=True, smooth=3), M['bare'])
    # air hose: pack -> over the right shoulder -> chest unit
    p.add(sweep([(-0.20, 0.235, 1.76), (-0.215, 0.12, 1.74), (-0.225, 0.0, 1.69), (-0.215, -0.11, 1.61), (-0.185, -0.175, 1.53),
                 (-0.16, -0.20, 1.50)], r=0.019, seg=10, ribs=0.28, smooth=9), M['rubber'])
    p.add(cyl((-0.20, 0.215, 1.76), (-0.20, 0.25, 1.76), 0.026, seg=12, bevel=0.003), M['bare'])
    # --- refinement pass: layered panels, bedroll, extra straps and clutter
    for sx in (1, -1):
        for dz in (0.12, -0.25):
            p.add(rbox((0.012, 0.24, 0.20), 0.004, 2), M['olive'], T(sx * (hw + 0.004), C.y, C.z + dz + 0.04))
            for by in (-0.09, 0.09):
                for bz in (-0.075, 0.075):
                    p.bolt((sx * (hw + 0.011), C.y + by, C.z + dz + 0.04 + bz), (sx, 0, 0), M['bare'], 0.005)
    for x in (-0.075, 0.075):
        for dz in (0.12, -0.22):
            p.add(rbox((0.085, 0.012, 0.15), 0.004, 2), M['olive'], T(x, C.y + hd + 0.004, C.z + dz))
    # dangling web straps and a second hook on the right side, loops under the pack
    for i, (yy, ln) in enumerate(((-0.02, 0.26), (0.08, 0.34))):
        x0 = -(hw + 0.022)
        p.add(sweep([(x0, C.y + yy, C.z - 0.02), (x0 - 0.018, C.y + yy - 0.01, C.z - 0.02 - ln * 0.5),
                     (x0 - 0.01, C.y + yy + 0.01, C.z - 0.02 - ln)], rect=(0.026, 0.006), normal=lambda q: Vector((-1, 0, 0)),
                    smooth=5), M['web'])
    for x in (-0.18, 0.18):
        p.add(sweep([(x, C.y - 0.08, C.z - hh), (x, C.y - 0.03, C.z - hh - 0.075), (x, C.y + 0.05, C.z - hh - 0.08),
                     (x, C.y + 0.10, C.z - hh)], rect=(0.03, 0.007), normal=lambda q: Vector((0, 0, -1)), smooth=5), M['web'])
    p.add(rbox((0.30, 0.13, 0.11), 0.01, 2, puff=0.18), M['cloth_olive'], T(0, C.y + 0.02, C.z - hh - 0.055))
    # --- v4: external load frame (tubes + shelf + lumbar pad), waist belt, and straps that hold the scanned props
    for sx in (1, -1):
        p.add(sweep([(sx * 0.17, C.y - hd - 0.03, C.z + hh + 0.06), (sx * 0.17, C.y - hd - 0.035, C.z), (sx * 0.17, C.y - hd - 0.03, C.z - hh - 0.03),
                     (sx * 0.17, C.y - hd + 0.06, C.z - hh - 0.05), (sx * 0.17, C.y + hd, C.z - hh - 0.05)], r=0.013, seg=10, smooth=4),
              M['steel'])
    for dz in (hh - 0.02, 0.0, -hh + 0.04):
        p.add(cyl((-0.17, C.y - hd - 0.032, C.z + dz), (0.17, C.y - hd - 0.032, C.z + dz), 0.011, seg=10), M['steel'])
    p.add(rbox((0.30, 0.035, 0.16), 0.01, 2, puff=0.1), M['cloth'], T(0, C.y - hd - 0.045, C.z - hh + 0.13))
    p.add(rbox((0.36, D + 0.02, 0.014), 0.004, 1), M['steel'], T(0, C.y, C.z - hh - 0.05))
    for x in (-0.19, 0.19):
        p.add(rbox((0.03, 0.007, 0.25), 0.002, 1), M['web'], T(x, C.y - 0.137, C.z + hh + 0.13))
        p.add(rbox((0.03, 0.007, 0.25), 0.002, 1), M['web'], T(x, C.y + 0.137, C.z + hh + 0.13))
        p.add(rbox((0.03, 0.28, 0.007), 0.002, 1), M['web'], T(x, C.y, C.z + hh + 0.252))
        p.buckle((x, C.y + 0.143, C.z + hh + 0.12), (0, 1, 0), M['bare'], 0.036, 0.026)
    for sx in (1, -1):
        off = 0.113 if sx < 0 else 0.082
        p.add(rbox((0.008, 0.03, 0.34 if sx < 0 else 0.2), 0.002, 1), M['web'], T(sx * (hw + off), C.y + 0.03, C.z + (0.06 if sx < 0 else 0.10)))
        p.add(rbox((off, 0.03, 0.008), 0.002, 1), M['web'], T(sx * (hw + off / 2), C.y + 0.03, C.z + (0.225 if sx < 0 else 0.198)))
        p.add(rbox((off, 0.03, 0.008), 0.002, 1), M['web'], T(sx * (hw + off / 2), C.y + 0.03, C.z + (-0.105 if sx < 0 else 0.002)))
    R.fin(p, 'CHEST')


def _arms(R, M):
    c = Part('HV4_Sleeve_L')
    c.add(tube([((0.250, 0, 1.56), 0.100, 0.108), ((0.264, 0, 1.47), 0.103, 0.108), ((0.260, -0.004, 1.37), 0.087, 0.099), ((0.258, 0, 1.26), 0.078, 0.084),
                ((0.257, 0, 1.165), 0.073, 0.077)], 20, steps=12, fold=(0.05, 7.5, 1.1)), M['cloth'])
    R.fin(c, 'UARM_L', subsurf=1, mirror=True)
    a = Part('HV4_Pauldron_L')
    Mp = T(0.352, 0.0, 1.505) @ face_to((1, 0, 0.42))
    a.add(plate(0.235, 0.215, 0.022, dome=0.065, p=4.2, cut=0.17, nu=10, nv=10), M['olive'], Mp)
    a.triangle(Mp @ Vector((0, -0.069, 0.01)), Mp.to_3x3() @ Vector((0, -1, 0)), 0.05, M['orange'], M['steel'],
               up=Mp.to_3x3() @ Vector((0, 0, 1)))
    for bx, bz in ((-0.085, -0.07), (0.085, -0.07), (-0.085, 0.07), (0.085, 0.07)):
        a.bolt(Mp @ Vector((bx, -0.03, bz)), Mp.to_3x3() @ Vector((0, -1, 0)), M['bare'], 0.0075)
    a.add(plate(0.21, 0.105, 0.018, dome=0.035, p=5, cut=0.12), M['olive'], T(0.338, 0.0, 1.392) @ face_to((1, 0, -0.08)))
    # --- v3: articulated shoulder - third lame, connecting straps with buckles, padded collar strap to the vest
    a.add(plate(0.19, 0.092, 0.016, dome=0.014, p=9, cut=0.2), M['olive'], T(0.326, 0.0, 1.312) @ face_to((1, 0, -0.22)))
    for y in (-0.062, 0.062):
        a.add(rbox((0.008, 0.022, 0.16), 0.002, 1), M['leather'], T(0.343, y, 1.40) @ Ry(-6))
        a.buckle((0.352, y, 1.445), (1, 0, 0.1), M['bare'], 0.026, 0.018)
        a.bolt((0.345, y, 1.33), (1, 0, -0.2), M['bare'], 0.0055)
    a.add(sweep([(0.33, 0.0, 1.585), (0.27, 0.0, 1.612), (0.19, 0.0, 1.608)], rect=(0.06, 0.012),
                normal=lambda q: Vector((0.2, 0, 1)), smooth=4), M['web'])
    a.buckle((0.25, 0.0, 1.622), (0.1, 0, 1), M['bare'], 0.03, 0.045, up=(1, 0, 0))
    a.add(rbox((0.11, 0.20, 0.03), 0.006, 2, puff=0.12), M['cloth'], T(0.285, 0.0, 1.585))
    a.add(tube([((0.258, 0, 1.30), 0.086, 0.091), ((0.258, 0, 1.345), 0.089, 0.097)], 20), M['hazard'])
    a.add(tube([((0.258, 0, 1.272), 0.083, 0.088), ((0.258, 0, 1.294), 0.084, 0.089)], 20), M['leather'])
    a.buckle((0.165, -0.02, 1.283), (-1, -0.3, 0), M['bare'], 0.03, 0.02)
    R.fin(a, 'UARM_L', mirror=True)
    c = Part('HV4_ForearmCloth_L')
    c.add(tube([((0.257, 0, 1.215), 0.075, 0.079), ((0.259, 0, 1.13), 0.083, 0.085), ((0.256, 0, 1.02), 0.066, 0.07),
                ((0.255, 0, 0.93), 0.054, 0.058)], 20, steps=9, fold=(0.05, 8.5, 2.2)), M['cloth'])
    R.fin(c, 'FARM_L', subsurf=1, mirror=True)
    f = Part('HV4_Bracer_L')
    f.add(rbox((0.13, 0.05, 0.14), 0.004, 2, puff=0.14), M['cloth'], T(0.257, 0.07, 1.19))
    f.add(plate(0.125, 0.135, 0.018, dome=0.038, p=3.6, cut=0.18), M['olive'], T(0.257, 0.098, 1.19) @ Rz(180))
    f.add(wrap(0.076, 15, 205, 0.18, 0.017, r2=0.091, nu=14), M['olive'], T(0.256, 0.0, 1.065))
    f.add(plate(0.115, 0.135, 0.015, dome=0.014, p=7, cut=0.12), M['olive'], T(0.353, 0.0, 1.07) @ face_to((1, 0, 0)))
    for bz in (1.02, 1.12):
        f.bolt((0.368, 0.035, bz), (1, 0, 0), M['bare'], 0.006)
        f.bolt((0.368, -0.035, bz), (1, 0, 0), M['bare'], 0.006)
    for z in (1.005, 1.105):
        f.add(tube([((0.256, 0, z), 0.078 if z < 1.05 else 0.084, 0.081 if z < 1.05 else 0.087), ((0.256, 0, z + 0.026), 0.079 if z < 1.05 else 0.085, 0.082 if z < 1.05 else 0.088)], 20), M['leather'])
        f.buckle((0.17, 0.0, z + 0.013), (-1, 0, 0), M['bare'], 0.03, 0.02)
    R.fin(f, 'FARM_L', mirror=True)
    g = Part('HV4_Glove_L')
    g.add(tube([((0.255, 0, 0.955), 0.062, 0.066), ((0.255, 0, 0.90), 0.058, 0.062)], 18), M['glove'])
    g.add(rbox((0.046, 0.104, 0.11), 0.004, 2, puff=0.14), M['glove'], T(0.255, -0.005, 0.855))
    g.add(plate(0.095, 0.075, 0.012, dome=0.012, p=5, cut=0.12), M['olive'], T(0.284, -0.005, 0.862) @ face_to((1, 0, 0)))
    for i in range(4):
        y = -0.044 + 0.026 * i
        ln = 1.0 - 0.08 * abs(i - 1.2)
        g.add(sweep([(0.262, y, 0.808), (0.259, y, 0.768), (0.238, y, 0.736), (0.208 + (1 - ln) * 0.02, y, 0.738),
                     (0.192 + (1 - ln) * 0.03, y, 0.762)], r=0.0128, seg=8, smooth=5), M['glove'])
        g.add(rbox((0.012, 0.022, 0.03), 0.003, 1), M['olive'], T(0.274, y, 0.787))
        for jx, jz in ((0.2595, 0.768), (0.2385, 0.7365)):
            g.add(ball(0.0148, 8, 6), M['glove'], T(jx, y, jz))
    g.add(sweep([(0.247, -0.052, 0.875), (0.228, -0.078, 0.845), (0.205, -0.084, 0.808), (0.196, -0.07, 0.785)], r=0.0145, seg=8,
                smooth=5), M['glove'])
    R.fin(g, 'HAND_L', mirror=True)


def _head(R, M):
    h = Part('HV4_Head')
    h.add(ball((0.092, 0.105, 0.118), 24, 14), M['skin'], T(0, -0.005, 1.705))
    h.add(rbox((0.032, 0.03, 0.04), 0.004, 2, puff=0.4), M['skin'], T(0, -0.104, 1.708))
    for s in (1, -1):
        h.add(ball(0.0115, 12, 8), M['eye'], T(s * 0.032, -0.0935, 1.727))
        h.add(ball(0.0062, 10, 6), M['iris'], T(s * 0.032, -0.1005, 1.727))
        h.add(rbox((0.04, 0.01, 0.006), 0.002, 1), M['hair'], T(s * 0.034, -0.1, 1.7445) @ Ry(s * 9))
        h.add(rbox((0.05, 0.02, 0.016), 0.004, 2, puff=0.14), M['skin'], T(s * 0.036, -0.094, 1.742) @ Ry(s * 12))
    R.fin(h, 'HEAD')
    b = Part('HV4_Balaclava')
    b.add(ball((0.099, 0.113, 0.125), 28, 16, cut=lambda c: c.y < -0.45 and 0.07 < c.z < 0.34 and abs(c.x) < 0.7), M['cloth'],
          T(0, -0.005, 1.705))
    R.fin(b, 'HEAD', subsurf=1)
    e = Part('HV4_Helmet')
    e.add(ball((0.130, 0.150, 0.126), 32, 18, cut=lambda c: c.z < -0.24 or (c.y < -0.30 and c.z < 0.03) or (abs(c.x) > 0.8 and c.z < -0.04 and -0.3 < c.y < 0.4)), M['olive'],
          T(0, 0.012, 1.768))
    R.fin(e, 'HEAD', solidify=0.014)
    g = Part('HV4_HelmetGear')
    g.add(rbox((0.055, 0.022, 0.05), 0.004, 2), M['steel'], T(0, -0.148, 1.80) @ Rx(-18))
    g.add(rbox((0.03, 0.012, 0.022), 0.003, 1), M['bare'], T(0, -0.16, 1.80) @ Rx(-18))
    g.add(rbox((0.034, 0.17, 0.012), 0.004, 2), M['steel'], T(0, 0.015, 1.894))
    g.add(rbox((0.095, 0.035, 0.055), 0.005, 2, puff=0.1), M['cloth_olive'], T(0, 0.158, 1.79) @ Rx(12))
    g.add(tube([((0, 0.012, 1.80), 0.1335, 0.151, 0.153, 2.0), ((0, 0.012, 1.822), 0.131, 0.148, 0.150, 2.0)], 30), M['web'])
    for s in (1, -1):
        g.add(rbox((0.013, 0.115, 0.024), 0.003, 1), M['steel'], T(s * 0.131, 0.005, 1.775))
        for y in (-0.035, 0.045):
            g.bolt((s * 0.138, y, 1.775), (s, 0, 0), M['bare'], 0.006)
        g.add(cyl((s * 0.10, 0.012, 1.712), (s * 0.142, 0.012, 1.712), 0.045, seg=18, bevel=0.006), M['steel'])
        g.add(cyl((s * 0.142, 0.012, 1.712), (s * 0.15, 0.012, 1.712), 0.03, seg=14, bevel=0.003), M['rubber'])
        g.add(sweep([(s * 0.108, -0.035, 1.70), (s * 0.095, -0.07, 1.655), (s * 0.06, -0.10, 1.612)], rect=(0.02, 0.006),
                    normal=lambda q, s=s: Vector((s, -0.5, 0)), smooth=5), M['web'])
    # --- v3: high-cut helmet furniture (velcro panels, strap anchors, mount arm, rear light, cover seams)
    for s in (1, -1):
        g.add(rbox((0.006, 0.07, 0.045), 0.002, 1), M['cloth_olive'], T(s * 0.118, 0.05, 1.835) @ Ry(-s * 24))
        for y in (-0.055, 0.085):
            g.bolt((s * 0.132, y, 1.752), (s, 0, 0.1), M['bare'], 0.0055)
        g.add(rbox((0.004, 0.012, 0.19), 0.001, 1), M['web'], T(s * 0.062, 0.012, 1.868) @ Ry(-s * 52))
    g.add(rbox((0.07, 0.006, 0.04), 0.002, 1), M['cloth_olive'], T(0, -0.118, 1.852) @ Rx(-38))
    g.add(rbox((0.016, 0.05, 0.016), 0.003, 1), M['steel'], T(0, -0.172, 1.812) @ Rx(-18))
    g.add(cyl((0, -0.195, 1.80), (0, -0.215, 1.795), 0.014, seg=12, bevel=0.002), M['steel'])
    g.add(rbox((0.03, 0.02, 0.022), 0.003, 1), M['orange'], T(0.05, 0.142, 1.842) @ Rx(30))
    g.add(rbox((0.004, 0.26, 0.004), 0.001, 1), M['web'], T(0, 0.012, 1.892))
    g.triangle((-0.121, -0.06, 1.815), (-0.86, -0.36, 0.36), 0.04, M['orange'], M['steel'], up=(0, 0, 1))
    R.fin(g, 'HEAD')
    r = Part('HV4_Respirator')
    r.add(ball((0.074, 0.064, 0.072), 20, 12), M['rubber'], T(0, -0.092, 1.662))
    r.add(rbox((0.045, 0.03, 0.04), 0.004, 2, puff=0.4), M['rubber'], T(0, -0.125, 1.705))
    r.add(cyl((0, -0.13, 1.655), (0, -0.192, 1.652), 0.046, seg=24, bevel=0.005), M['steel'])
    r.add(cyl((0, -0.192, 1.652), (0, -0.199, 1.652), 0.037, seg=24, bevel=0.002), M['bare'])
    for i in range(8):
        a = 2 * pi * i / 8
        r.add(cyl((0.022 * cos(a), -0.199, 1.652 + 0.022 * sin(a)), (0.022 * cos(a), -0.2025, 1.652 + 0.022 * sin(a)), 0.0065,
                  seg=8), M['rubber'])
    r.add(cyl((0, -0.199, 1.652), (0, -0.205, 1.652), 0.012, seg=12, bevel=0.002), M['steel'])
    for s in (1, -1):
        r.add(cyl((s * 0.048, -0.112, 1.652), (s * 0.122, -0.150, 1.630), 0.036, seg=20, bevel=0.005), M['steel'])
        r.add(cyl((s * 0.122, -0.150, 1.630), (s * 0.129, -0.1535, 1.628), 0.029, seg=20, bevel=0.002), M['bare'])
        r.add(sweep([(s * 0.066, -0.07, 1.682), (s * 0.106, 0.0, 1.69), (s * 0.095, 0.085, 1.695)], rect=(0.022, 0.006),
                    normal=lambda q, s=s: Vector((s, -0.2, 0)), smooth=5), M['rubber'])
    r.add(cyl((0, -0.118, 1.612), (0, -0.136, 1.592), 0.021, seg=14, bevel=0.003), M['steel'])
    R.fin(r, 'HEAD')


def _shotgun(R, M):
    g = Part('HV4_Shotgun')
    O = Vector((0, -0.5, 1.1))            # GUN joint rest position = pistol-grip hold point
    def P(x, y, z):
        return O + Vector((x, y, z))
    g.add(rbox((0.048, 0.275, 0.078), 0.006, 2), M['steel'], T(P(0, -0.06, 0.066)))
    g.add(rbox((0.004, 0.075, 0.026), 0.001, 1), M['bare'], T(P(-0.0245, -0.07, 0.078)))
    g.add(rbox((0.014, 0.21, 0.01), 0.002, 1), M['steel'], T(P(0, -0.07, 0.109)))
    g.add(rbox((0.038, 0.052, 0.115), 0.008, 2, taper=(1.0, 1.25)), M['wood'], T(P(0, 0.012, -0.012)) @ Rx(-16))
    g.add(rbox((0.042, 0.21, 0.085), 0.012, 2, taper=(0.9, 1.0)), M['wood'], T(P(0, 0.175, 0.04)) @ Rx(-7))
    g.add(rbox((0.046, 0.02, 0.095), 0.006, 2), M['rubber'], T(P(0, 0.287, 0.027)) @ Rx(-7))
    g.add(cyl(P(0, -0.19, 0.086), P(0, -0.74, 0.086), 0.0145, seg=16), M['steel'])
    g.add(cyl(P(0, -0.19, 0.05), P(0, -0.69, 0.05), 0.0165, seg=16), M['steel'])
    g.add(cyl(P(0, -0.71, 0.086), P(0, -0.745, 0.086), 0.0185, seg=16, bevel=0.002), M['steel'])
    g.add(cyl(P(0, -0.685, 0.05), P(0, -0.70, 0.05), 0.019, seg=16, bevel=0.002), M['bare'])
    g.add(rbox((0.052, 0.022, 0.078), 0.004, 1), M['steel'], T(P(0, -0.62, 0.068)))
    g.add(cyl(P(0, -0.285, 0.05), P(0, -0.475, 0.05), 0.031, seg=20, bevel=0.004), M['rubber'])
    for i in range(6):
        y = -0.30 - 0.03 * i
        g.add(cyl(P(0, y, 0.05), P(0, y - 0.013, 0.05), 0.0345, seg=20, bevel=0.002), M['orange'] if i % 2 == 0 else M['rubber'])
    g.add(sweep([P(0, -0.03, 0.03), P(0, -0.045, -0.006), P(0, -0.09, -0.008), P(0, -0.108, 0.03)], r=0.0042, seg=8, smooth=5),
          M['steel'])
    g.add(rbox((0.006, 0.01, 0.026), 0.001, 1), M['bare'], T(P(0, -0.065, 0.012)) @ Rx(18))
    g.add(cyl(P(0, -0.73, 0.1), P(0, -0.73, 0.112), 0.0035, seg=8), M['brass'])
    # --- v3: functional details
    g.add(wrap(0.0195, 95, 265, 0.30, 0.003, nu=10, nv=6), M['steel'], T(P(0, -0.46, 0.086)) @ Rx(90))     # heat shield
    for i in range(5):
        g.add(rbox((0.012, 0.03, 0.004), 0.001, 1), M['rubber'], T(P(0, -0.34 - 0.055 * i, 0.1055)))        # vent slots
    g.add(rbox((0.03, 0.075, 0.006), 0.001, 1), M['bare'], T(P(0, -0.105, 0.026)))                        # loading port
    g.add(rbox((0.006, 0.006, 0.016), 0.001, 1), M['bare'], T(P(0, -0.715, 0.108)))                       # front sight
    g.add(rbox((0.02, 0.014, 0.012), 0.002, 1), M['steel'], T(P(0, 0.05, 0.112)))                         # rear sight
    for y in (0.03, -0.06, -0.16):
        g.bolt(P(-0.0245, y, 0.045), (-1, 0, 0), M['bare'], 0.0045, 0.002)                                # receiver pins
    g.add(rbox((0.01, 0.15, 0.05), 0.002, 1), M['steel'], T(P(0.029, -0.06, 0.07)))                       # side saddle
    for i in range(4):
        y = -0.005 - 0.036 * i
        g.add(cyl(P(0.041, y, 0.04), P(0.041, y, 0.085), 0.0105, seg=10), M['shell'])
        g.add(cyl(P(0.041, y, 0.085), P(0.041, y, 0.097), 0.011, seg=10), M['brass'])
    g.add(rbox((0.012, 0.012, 0.02), 0.002, 1), M['bare'], T(P(0, -0.64, 0.028)))                         # sling swivels
    g.add(rbox((0.012, 0.012, 0.02), 0.002, 1), M['bare'], T(P(0, 0.22, -0.012)))
    g.add(rbox((0.05, 0.19, 0.012), 0.003, 1), M['rubber'], T(P(0, 0.175, 0.085)) @ Rx(-7))               # cheek pad
    R.fin(g, 'GUN')


def build():
    importlib.reload(K)
    sc = _scene()
    win = bpy.context.window_manager.windows[0]
    win.scene = sc
    M = materials()
    R = Rig(sc)
    _legs(R, M)
    _hips(R, M)
    _torso(R, M)
    _backpack(R, M)
    _arms(R, M)
    _head(R, M)
    _shotgun(R, M)
    # orange stays on the near (right) arm and thigh only, as in the concept
    for name in ('HV4_ThighGear_L', 'HV4_Pauldron_L', 'HV4_LegArmor_L'):
        me = bpy.data.objects[name].data
        for i, m in enumerate(me.materials):
            if m == M['hazard']:
                me.materials[i] = M['leather']
    sc['hv4_objects'] = len(R.objs)
    bpy.context.view_layer.update()
    return R


# ============================================================================== posing (review aid)
def _E(name):
    return bpy.data.objects['HV4_J_' + name]


def _frame(direction, hint=(1, 0, 0.3)):
    z = -Vector(direction).normalized()
    h = Vector(hint)
    x = (h - z * h.dot(z)).normalized()
    y = z.cross(x)
    return Matrix((x, y, z)).transposed().to_4x4()


def _two_bone(root, target, l1, l2, pole):
    d = target - root
    L = max(abs(l1 - l2) + 1e-4, min(d.length, l1 + l2 - 1e-4))
    dn = d.normalized()
    a = (l1 * l1 - l2 * l2 + L * L) / (2 * L)
    h = math.sqrt(max(l1 * l1 - a * a, 0.0))
    pp = Vector(pole) - dn * Vector(pole).dot(dn)
    pp.normalize()
    return root + dn * a + pp * h, root + dn * L


def _set(name, M):
    e = _E(name)
    e.matrix_world = M
    bpy.context.view_layer.update()


def _hand_matrix(side, hold_world, back_dir, wrist_dir):
    X = Vector(back_dir).normalized() * side
    Z = Vector(wrist_dir)
    Z = (Z - X * Z.dot(X)).normalized()
    Y = Z.cross(X)
    Rm = Matrix((X, Y, Z)).transposed()
    off = (Vector((HOLD.x * side, HOLD.y, HOLD.z)) - Vector((0.255 * side, 0, 0.925))) * BULK['HAND'][0]
    wrist = Vector(hold_world) - Rm @ off
    return Matrix.Translation(wrist) @ Rm.to_4x4(), wrist


def pose(kind='ready'):
    sc = bpy.data.scenes[SCENE]
    win = bpy.context.window_manager.windows[0]
    win.scene = sc
    vl = bpy.context.view_layer
    for o in sc.objects:
        if o.name.startswith('HV4_J_'):
            o.matrix_basis = Matrix.Translation(Vector(o['rest']) - (Vector(o.parent['rest']) if o.parent else Vector()))
    vl.update()
    if kind == 'rest':
        return
    # legs: wide planted stance
    for tag, s, yaw, pitch in (('_L', 1, 16, -15), ('_R', -1, -10, -9)):
        hip = Vector(_E('THIGH' + tag)['rest'])
        if kind == 'apose':
            _set('THIGH' + tag, T(hip) @ Ry(-s * 5))
            continue
        _set('THIGH' + tag, T(hip) @ Rz(yaw) @ Ry(-s * 10) @ Rx(pitch))
        _E('SHIN' + tag).rotation_euler = (radians(-pitch * 1.9), 0, 0)
        vl.update()
        ank = _E('FOOT' + tag).matrix_world.translation.copy()
        _set('FOOT' + tag, T(ank) @ Rz(yaw))
    if kind == 'apose':
        for tag, s in (('_L', 1), ('_R', -1)):
            sh = _E('UARM' + tag).matrix_world.translation.copy()
            _set('UARM' + tag, T(sh) @ Ry(-s * 38) @ Rx(-6))
            _E('FARM' + tag).rotation_euler = (radians(-14), 0, 0)
        _set('GUN', T(0.75, -0.1, 0.02) @ Rx(-90) @ Ry(90))
        vl.update()
    else:
        _E('CHEST').rotation_euler = (radians(5), 0, radians(-18))
        _E('HEAD').rotation_euler = (radians(-2), 0, radians(8))
        vl.update()
        Mg = T(-0.25, -0.30, 1.13) @ Rz(30) @ Rx(9)
        _set('GUN', Mg @ T(-Vector(JOINTS['GUN'][0])) @ T(Vector(JOINTS['GUN'][0])))
        f = (Mg.to_3x3() @ Vector((0, -1, 0))).normalized()
        gup = (Mg.to_3x3() @ Vector((0, 0, 1))).normalized()
        gside = (Mg.to_3x3() @ Vector((1, 0, 0))).normalized()
        # right hand on the pistol grip, left hand under the pump
        Hr, wr = _hand_matrix(-1, Mg @ Vector((0.0, 0.012, -0.012)), -gside, -f * 1.0 + gup * 0.25)
        Hl, wl = _hand_matrix(1, Mg @ GUN_FORE, -gup, gside * 1.0 - f * 0.25)
        for tag, s, H, w, pole in (('_R', -1, Hr, wr, (-1, 0.9, -0.2)), ('_L', 1, Hl, wl, (1.0, 0.1, -0.9))):
            sh = _E('UARM' + tag).matrix_world.translation.copy()
            el, wr2 = _two_bone(sh, w, 0.30, 0.265, pole)
            _set('UARM' + tag, T(sh) @ _frame(el - sh))
            _set('FARM' + tag, T(el) @ _frame(wr2 - el))
            _set('HAND' + tag, T(wr2) @ H.to_3x3().to_4x4())
    # plant on the ground
    vl.update()
    dg = bpy.context.evaluated_depsgraph_get()
    zmin = 1e9
    for o in sc.objects:
        if o.type == 'MESH' and 'Boot' in o.name:
            oe = o.evaluated_get(dg)
            zmin = min(zmin, min((oe.matrix_world @ Vector(c)).z for c in oe.bound_box))
    root = _E('ROOT')
    root.location.z -= zmin
    vl.update()
    return dict(ground_shift=round(-zmin, 4))


# ============================================================================== review renders
VIEWS = dict(front=(0, 3, 85, 9.6), back=(180, 3, 85, 9.6), left=(90, 3, 85, 9.6), right=(-90, 3, 85, 9.6),
             hero=(-52, 6, 70, 7.6), gameplay=(-30, 45, 85, 10.0))


def studio(samples=64, res=(900, 1200)):
    sc = bpy.data.scenes[SCENE]
    for o in list(sc.objects):
        if o.name.startswith('HV4_Studio'):
            d = o.data
            bpy.data.objects.remove(o, do_unlink=True)
    sc.render.engine = 'CYCLES'
    sc.cycles.samples = samples
    sc.cycles.use_denoising = True
    sc.cycles.use_adaptive_sampling = True
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = False
    sc.render.image_settings.file_format = 'PNG'
    try:
        sc.view_settings.view_transform = 'Standard'
        sc.view_settings.look = 'None'
    except Exception as e:
        print('view', e)
    w = bpy.data.worlds.get('HV4_World') or bpy.data.worlds.new('HV4_World')
    bg = next(n for n in w.node_tree.nodes if n.type == 'BACKGROUND') if w.node_tree else None
    if bg is None:
        w.use_nodes = True
        bg = next(n for n in w.node_tree.nodes if n.type == 'BACKGROUND')
    bg.inputs[0].default_value = K.rgb('cdc9c5')
    # the camera sees the pale concept-sheet background; the model is lit by a dimmer, slightly warm environment
    nt = w.node_tree
    for n in [n for n in nt.nodes if n.type in ('LIGHT_PATH', 'MIX')]:
        nt.nodes.remove(n)
    lp = nt.nodes.new('ShaderNodeLightPath')
    mx = nt.nodes.new('ShaderNodeMix')
    mx.data_type = 'RGBA'
    mx.inputs[6].default_value = (0.20, 0.19, 0.18, 1.0)
    mx.inputs[7].default_value = K.rgb('cdc9c5')
    nt.links.new(lp.outputs['Is Camera Ray'], mx.inputs[0])
    nt.links.new(mx.outputs[2], bg.inputs[0])
    bg.inputs[1].default_value = 1.0
    sc.world = w
    def light(name, loc, energy, size, col=(1, 1, 1)):
        l = bpy.data.lights.new(name, 'AREA')
        l.energy, l.size, l.color = energy, size, col
        o = bpy.data.objects.new(name, l)
        sc.collection.objects.link(o)
        o.location = loc
        o.rotation_euler = (Vector((0, 0, 1.1)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        o.parent = bpy.data.objects.get('HV4_StudioPivot')
        return o
    piv = bpy.data.objects.new('HV4_StudioPivot', None)
    sc.collection.objects.link(piv)
    light('HV4_StudioKey', (-2.6, -3.2, 3.6), 560, 2.2, (1.0, 0.95, 0.88))
    light('HV4_StudioFill', (3.2, -2.2, 1.6), 40, 4.0, (0.9, 0.95, 1.0))
    light('HV4_StudioRim', (1.5, 3.4, 3.0), 380, 2.5)
    me = bpy.data.meshes.new('HV4_StudioFloor')
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=30.0)
    bm.to_mesh(me)
    bm.free()
    fl = bpy.data.objects.new('HV4_StudioFloor', me)
    sc.collection.objects.link(fl)
    fl.is_shadow_catcher = True
    cam = bpy.data.objects.new('HV4_StudioCam', bpy.data.cameras.new('HV4_StudioCam'))
    sc.collection.objects.link(cam)
    sc.camera = cam
    return sc


def aim(view, target_z=0.98, dist_scale=1.0):
    az, el, lens, dist = VIEWS[view] if isinstance(view, str) else view
    sc = bpy.data.scenes[SCENE]
    cam = sc.camera
    cam.data.lens = lens
    cam.data.sensor_fit = 'VERTICAL'
    a, e = radians(az), radians(el)
    d = Vector((sin(a) * cos(e), -cos(a) * cos(e), sin(e)))
    tgt = Vector((0, 0.08, target_z))
    cam.location = tgt + d * dist * dist_scale
    cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    # lights follow the camera so every view is lit like the hero view
    piv = bpy.data.objects['HV4_StudioPivot']
    piv.rotation_euler = (0, 0, radians(az + 52))
    return cam


def render(view, path, samples=None, res=None):
    sc = bpy.data.scenes[SCENE]
    if samples:
        sc.cycles.samples = samples
    if res:
        sc.render.resolution_x, sc.render.resolution_y = res
    aim(view)
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True, scene=SCENE)
    return path


def stats():
    sc = bpy.data.scenes[SCENE]
    dg = bpy.context.evaluated_depsgraph_get()
    out = []
    tot = 0
    for o in sc.objects:
        if o.type != 'MESH' or o.name.startswith('HV4_Studio'):
            continue
        me = o.evaluated_get(dg).to_mesh()
        tris = sum(len(p.vertices) - 2 for p in me.polygons)
        o.evaluated_get(dg).to_mesh_clear()
        tot += tris
        out.append((o.name, tris, len(o.data.materials)))
    return tot, out


# ============================================================================== v4: refine the saved v3 scene in place
PH = REPO + r"\ArtSource\External\PolyHaven"
REBUILD = ('HV4_Boot', 'HV4_LegArmor', 'HV4_Glove', 'HV4_Head', 'HV4_Balaclava', 'HV4_Backpack')
MAPS = dict(cloth=('denim_fabric', 7.0, 0.45, 1.2), cloth_olive=('rough_linen', 6.0, 0.5, 1.0), web=('rough_linen', 9.0, 0.5, 1.0),
            leather=('fabric_leather_01', 7.0, 0.5, 0.9), boot=('fabric_leather_01', 6.0, 0.5, 1.0),
            glove=('fabric_leather_01', 8.0, 0.5, 1.0), olive=('metal_plate_02', 2.2, 0.4, 0.5),
            pack=('rusty_metal_03', 1.6, 0.5, 0.6), steel=('rusty_metal_03', 3.0, 0.3, 0.4))


def _prop(sc, names, joint, M4, scale):
    """Move scanned Poly Haven objects from the staging scene onto a joint (rest pose), as one parented group."""
    st = bpy.data.scenes['CS_HV4_Staging']
    objs = [bpy.data.objects[n] for n in names]
    mn = Vector((1e9,) * 3)
    mx = Vector((-1e9,) * 3)
    for o in objs:
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            mn = Vector(map(min, mn, w))
            mx = Vector(map(max, mx, w))
    centre = (mn + mx) / 2
    e = _E(joint)
    out = []
    for o in objs:
        c = o.copy()
        c.name = 'HV4_PH_' + o.name
        sc.collection.objects.link(c)
        c.parent = None
        c.matrix_world = M4 @ Matrix.Scale(scale, 4) @ Matrix.Translation(-centre) @ o.matrix_world
        bpy.context.view_layer.update()
        mw = c.matrix_world.copy()
        c.parent = e
        c.matrix_parent_inverse = e.matrix_world.inverted()
        c.matrix_world = mw
        c['ph'] = o.get('ph', '')
        out.append(c)
    return out, (mx - mn) * scale


def refine():
    importlib.reload(K)
    sc = bpy.data.scenes[SCENE]
    bpy.context.window_manager.windows[0].scene = sc
    pose('rest')
    _E('ROOT').location = (0, 0, 0)
    bpy.context.view_layer.update()
    for o in list(sc.objects):
        if o.name.startswith(REBUILD) or o.name.startswith('HV4_PH_'):
            me = o.data
            bpy.data.objects.remove(o, do_unlink=True)
            if me and me.users == 0 and o is not None:
                try:
                    bpy.data.meshes.remove(me)
                except Exception:
                    pass
    M = materials()                       # same material names: trees are rebuilt in place, kept objects update
    used = {}
    for key, (tex, scale, col, nor) in MAPS.items():
        used[key] = (tex, K.add_maps(M[key], PH + '\\textures\\' + tex, scale, col, nor))
    R = Rig(sc, existing=True)
    R.only = REBUILD
    _legs(R, M)
    _arms(R, M)
    _head(R, M)
    _backpack(R, M)
    me = bpy.data.objects['HV4_LegArmor_L'].data
    for i, m in enumerate(me.materials):
        if m == M['hazard']:
            me.materials[i] = M['leather']
    # scanned CC0 props on the pack (rest pose): crate strapped on top, jerrycan on his right, ammo box on his left
    bpy.context.view_layer.update()
    C, W, D, H = PACK_C, PACK_W, PACK_D, PACK_H
    k = BULK['CHEST'][0]
    top = C.z + H / 2 + 0.04
    props = {}
    props['old_military_crate'], s1 = _prop(sc, ['old_military_crate_a', 'old_military_crate_lid_a', 'old_military_crate_loop_a',
                                                 'old_military_crate_latch_a'], 'CHEST',
                                            T(0, C.y * k, top + 0.105) @ Rz(180), 0.66)
    props['metal_jerrycan_green'], s2 = _prop(sc, ['metal_jerrycan_green_body', 'metal_jerrycan_green_cap',
                                                   'metal_jerrycan_green_latch', 'metal_jerrycan_green_pin',
                                                   'metal_jerrycan_green_fastener'], 'CHEST',
                                              T(-(W / 2 * k + 0.075), C.y * k + 0.03, C.z + 0.06) @ Rz(90), 0.62)
    props['ammo_box'], s3 = _prop(sc, ['ammo_box', 'ammo_box_handle', 'ammo_box_latch', 'ammo_box_handle_ring_a',
                                       'ammo_box_handle_ring_b'], 'CHEST',
                                  T((W / 2 * k + 0.05), C.y * k + 0.02, C.z + 0.10), 0.9)
    bpy.context.view_layer.update()
    return dict(maps=used, props={k2: len(v) for k2, v in props.items()}, crate=tuple(round(x, 2) for x in s1))
