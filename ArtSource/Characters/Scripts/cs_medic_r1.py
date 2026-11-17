"""Cargo Squad - Medic redesign v1 (new look-dev model built from ArtSource/References/Medic_Approved_Concept.png).

Run inside the live Blender session:
    import sys; sys.path.insert(0, r"<repo>/ArtSource/Characters/Scripts")
    import cs_medic_r1 as H; H.build(); H.pose('ready')

The model lives in its own scene (CS_Medic_R1). Parts are authored in a straight rest pose and parented to joint
empties (a review posing aid, NOT the game rig), so the same meshes can be shown in an A-pose or holding the shotgun.
"""
import bpy, bmesh, math, importlib
from math import sin, cos, pi, radians
from mathutils import Vector, Matrix
import cs_kit as K
from cs_kit import Part, T, Rx, Ry, Rz, face_to, rbox, plate, wrap, tube, cyl, ball, sweep, prism

SCENE = 'CS_Medic_R1'
REPO = r"C:\kfupm\T261\SWE402\SWE402-project-CargoSquad"

# joint empties: rest position, parent  (left side; right is mirrored)
JOINTS = dict(ROOT=((0, 0, 0), None), PELVIS=((0, 0, 0.97), 'ROOT'), CHEST=((0, 0, 1.17), 'PELVIS'),
              HEAD=((0, 0, 1.56), 'CHEST'), GUN=((0, -0.5, 1.1), 'ROOT'))
SIDE = dict(UARM=((0.202, 0, 1.47), 'CHEST'), FARM=((0.203, 0, 1.18), 'UARM'), HAND=((0.202, 0, 0.915), 'FARM'),
            THIGH=((0.10, 0, 0.94), 'PELVIS'), SHIN=((0.106, 0, 0.50), 'THIGH'), FOOT=((0.105, 0, 0.09), 'SHIN'))
# the Medic is authored directly at final proportions: no bulk scaling
AUTH = dict({k: v[0] for k, v in JOINTS.items()}, **{k: v[0] for k, v in SIDE.items()})
BULK = {k: (1, 1, 1) for k in AUTH}
WRINKLE = 0.016
WRIST = Vector((0.202, 0, 0.915))
HOLD = Vector((0.176, -0.006, 0.772))          # axis of an object gripped by the (left) hand, rest pose
GUN_FORE = Vector((0, -0.125, 0.012))          # support-hand point in device space (origin = grip)


def materials():
    S = K.surface
    M = dict(
        uniform=S('MR1_UniformOlive', '3d4a24', rough=0.9, grime=0.45, dust=0.18, mott=('252c18', 15.0, 0.8, '566034'),
                  bump='fabric', bump_s=1.0, sheen=0.05),
        charcoal=S('MR1_ClothCharcoal', '1a1b1d', rough=0.88, grime=0.3, dust=0.18, mott=('0e0e10', 11.0, 0.6, '2a2b2e'),
                   bump='fabric', bump_s=1.0, sheen=0.05),
        vest=S('MR1_VestOlive', '25271d', rough=0.9, wear=0.35, under=('6a6a50', 0.0, 0.95), grime=0.7, dust=0.25,
               mott=('1c1f15', 13.0, 0.6), bump='fabric', bump_s=1.0),
        sage=S('MR1_CanvasSage', '585c42', rough=0.92, wear=0.45, under=('8a8a6c', 0.0, 0.95), grime=0.9, dust=0.25,
               mott=('3d412d', 12.0, 0.6, '6e7054'), bump='fabric', bump_s=1.0),
        white=S('MR1_ArmorOffWhite', 'b7b2a3', rough=0.6, wear=0.75, under=('3b3a37', 1.0, 0.4), grime=1.0, dust=0.3, rust=0.18,
                scratch=0.45, mott=('8f8a7a', 6.0, 0.55), bump='metal', bump_s=0.4),
        canvas_white=S('MR1_CanvasOffWhite', 'bcb6a6', rough=0.93, wear=0.3, under=('8a8372', 0.0, 0.95), grime=1.0, dust=0.3,
                       mott=('8d8676', 7.0, 0.6), bump='fabric', bump_s=1.0),
        teal=S('MR1_WebbingTeal', '1d5654', rough=0.85, wear=0.35, under=('3f7f7b', 0.0, 0.9), grime=0.7, dust=0.2, bump='fabric',
               bump_s=0.8),
        teal_paint=S('MR1_PaintTeal', '1b6663', rough=0.5, wear=0.7, under=('3a3835', 1.0, 0.4), grime=0.6, dust=0.2, scratch=0.5,
                     bump='metal', bump_s=0.25),
        tan=S('MR1_WebbingTan', '7d6f4c', rough=0.88, wear=0.3, under=('a89a72', 0.0, 0.95), grime=0.8, dust=0.2, bump='fabric',
              bump_s=0.8),
        boot=S('MR1_BootLeather', '1e1b18', rough=0.66, wear=0.45, under=('5a5144', 0.0, 0.85), grime=0.7, dust=0.22, scratch=0.5,
               bump='leather', bump_s=0.5),
        glove=S('MR1_Glove', '1d1d1f', rough=0.75, wear=0.4, under=('4a4a48', 0.0, 0.9), grime=0.5, dust=0.3, bump='leather',
                bump_s=0.45),
        rubber=S('MR1_Rubber', '121213', rough=0.62, wear=0.2, under=('3d3d3d', 0.0, 0.8), grime=0.4, dust=0.3, bump='rubber',
                 bump_s=0.3),
        steel=S('MR1_DarkSteel', '28292b', rough=0.45, metal=1.0, wear=0.6, under=('9a9c9f', 1.0, 0.3), rust=0.25, grime=0.4,
                dust=0.2, scratch=0.5, bump='metal', bump_s=0.15),
        bare=S('MR1_BareSteel', '8b8d90', rough=0.38, metal=1.0, rust=0.2, grime=0.5, dust=0.15, scratch=0.6, bump='metal',
               bump_s=0.12),
        red=S('MR1_RedPolymer', '8e2a22', rough=0.45, wear=0.4, under=('2a2a2a', 0.0, 0.6), grime=0.6, dust=0.15, bump='rubber',
              bump_s=0.15),
        yellow=S('MR1_ContainerYellow', '9c7f24', rough=0.5, wear=0.6, under=('3a3835', 1.0, 0.4), grime=0.8, dust=0.2,
                 scratch=0.5, bump='metal', bump_s=0.2),
        skin=S('MR1_Skin', '94694a', rough=0.55, grime=0.5, dust=0.12, bump='rubber', bump_s=0.15, sss=0.15),
        eye=S('MR1_EyeWhite', 'd9d4cc', rough=0.25, grime=0.2, dust=0.0, bump='rubber', bump_s=0.0),
        iris=S('MR1_Iris', '3a2a18', rough=0.2, grime=0.0, dust=0.0, bump='rubber', bump_s=0.0),
        hair=S('MR1_Brow', '1d1612', rough=0.8, grime=0.0, dust=0.1, bump='fabric', bump_s=0.4),
        cyan=S('MR1_EnergyCyan', '2fd8e6', rough=0.2, grime=0.0, dust=0.0, bump='rubber', bump_s=0.0),
        lamp=S('MR1_LampWarm', 'ffe2b0', rough=0.2, grime=0.0, dust=0.0, bump='rubber', bump_s=0.0),
    )
    for key, col, strength in (('cyan', '2fd8e6', 5.0), ('lamp', 'ffd9a0', 6.0)):
        b = next(n for n in M[key].node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
        b.inputs['Emission Color'].default_value = K.rgb(col)
        b.inputs['Emission Strength'].default_value = strength
    PH = REPO + r"\ArtSource\External\PolyHaven\textures" + "\\"
    for key, (tex, scale, col, nor) in dict(uniform=('denim_fabric', 8.0, 0.5, 1.3), charcoal=('denim_fabric', 8.0, 0.45, 1.2),
                                           vest=('rough_linen', 7.0, 0.5, 1.0), sage=('rough_linen', 7.0, 0.5, 1.0),
                                           canvas_white=('rough_linen', 7.0, 0.55, 1.0), teal=('rough_linen', 11.0, 0.5, 1.0),
                                           tan=('rough_linen', 11.0, 0.5, 1.0), boot=('fabric_leather_01', 7.0, 0.5, 1.0),
                                           glove=('fabric_leather_01', 9.0, 0.5, 1.0), white=('metal_plate_02', 3.0, 0.35, 0.4),
                                           steel=('rusty_metal_03', 3.0, 0.3, 0.4)).items():
        K.add_maps(M[key], PH + tex, scale, col, nor)
    return M


def _badge(part, pos, normal, size, M, up=(0, 0, 1)):
    """Original medic marking: teal square, white diamond, teal core (no protected emblem)."""
    Mb = T(pos) @ face_to(normal, up)
    s = size
    part.add(rbox((s, 0.004, s), 0.0015, 1), M['teal_paint'], Mb)
    part.add(prism([(0, s * 0.36), (s * 0.36, 0), (0, -s * 0.36), (-s * 0.36, 0)], 0.0042), M['canvas_white'], Mb, smooth=False)
    part.add(prism([(0, s * 0.13), (s * 0.13, 0), (0, -s * 0.13), (-s * 0.13, 0)], 0.0056), M['teal_paint'], Mb, smooth=False)


def _boot(b, X, M):
    outline = [(0.112, 0.034), (0.088, 0.047), (0.03, 0.050), (-0.045, 0.054), (-0.105, 0.056), (-0.155, 0.051),
               (-0.185, 0.038), (-0.2, 0.018)]
    lift = lambda y: 0.010 if y < -0.15 else 0.0
    b.add(K.loft([(y, X, hw + 0.005, 0.046 + lift(y), 0.020 + lift(y)) for y, hw in outline], 18, p=5.0), M['rubber'])
    b.add(K.loft([(0.114, X, 0.034, 0.028, 0.0), (0.088, X, 0.049, 0.028, 0.0), (0.035, X, 0.050, 0.028, 0.0)], 14, p=6.0),
          M['rubber'])
    for i in range(6):
        y = -0.035 - 0.027 * i
        hw = 0.052 if i < 4 else 0.046 - 0.006 * (i - 4)
        b.add(rbox((hw * 2, 0.017, 0.018), 0.003, 1), M['rubber'], T(X, y, 0.011 + (0.005 if i > 4 else 0)))
    b.add(K.loft([(0.108, X, 0.031, 0.135, 0.046), (0.084, X, 0.044, 0.18, 0.046), (0.04, X, 0.048, 0.195, 0.046),
                  (-0.01, X, 0.048, 0.165, 0.046), (-0.065, X, 0.051, 0.128, 0.046), (-0.115, X, 0.054, 0.104, 0.05),
                  (-0.158, X, 0.050, 0.094, 0.055), (-0.186, X, 0.037, 0.085, 0.057), (-0.199, X, 0.018, 0.074, 0.059)],
                 20, p=2.8, flat_bottom=0.6), M['boot'])
    b.add(K.loft([(-0.118, X, 0.056, 0.107, 0.052), (-0.158, X, 0.052, 0.098, 0.055), (-0.188, X, 0.039, 0.088, 0.057),
                  (-0.202, X, 0.019, 0.076, 0.059)], 20, p=2.8, flat_bottom=0.6), M['rubber'])
    b.add(K.loft([(0.111, X, 0.033, 0.12, 0.048), (0.088, X, 0.046, 0.135, 0.048), (0.05, X, 0.050, 0.125, 0.048)], 20, p=2.8,
                 flat_bottom=0.6), M['rubber'])
    b.add(tube([((X, 0.04, 0.155), 0.048, 0.056), ((X, 0.03, 0.20), 0.051, 0.059), ((X, 0.02, 0.245), 0.055, 0.061),
                ((X, 0.014, 0.272), 0.058, 0.063)], 20, steps=6, fold=(0.014, 14, 0.5)), M['boot'])
    b.add(tube([((X, 0.014, 0.262), 0.061, 0.066), ((X, 0.014, 0.284), 0.062, 0.067)], 20), M['boot'])
    b.add(sweep([(X, -0.09, 0.118), (X, -0.052, 0.152), (X, -0.032, 0.20), (X, -0.036, 0.25), (X, -0.042, 0.285)],
                rect=(0.04, 0.01), normal=lambda q: Vector((0, -1, 0.5)), smooth=5), M['boot'])
    for i in range(7):
        z = 0.15 + 0.019 * i
        yy = -0.054 + 0.01 * min(i, 2) - 0.003 * max(0, i - 2)
        b.add(cyl((X - 0.025, yy - 0.004, z), (X + 0.025, yy - 0.004, z + 0.011), 0.003, seg=6), M['tan'])
        b.add(cyl((X - 0.025, yy - 0.004, z + 0.011), (X + 0.025, yy - 0.004, z), 0.003, seg=6), M['tan'])
        for sx in (-1, 1):
            b.add(cyl((X + sx * 0.027, yy, z + 0.005), (X + sx * 0.027, yy - 0.005, z + 0.005), 0.005, seg=8), M['bare'])


def _legs(R, M):
    c = Part('MR1_Trouser_L')
    c.add(tube([((0.094, 0, 1.0), 0.098, 0.108), ((0.104, 0, 0.82), 0.094, 0.100), ((0.107, 0, 0.63), 0.080, 0.086),
                ((0.106, 0, 0.47), 0.068, 0.073)], 22, steps=12, fold=(0.05, 6.5, 0.9)), M['uniform'])
    R.fin(c, 'THIGH_L', subsurf=1, mirror=True)
    c = Part('MR1_ShinCloth_L')
    c.add(tube([((0.106, 0, 0.55), 0.070, 0.075), ((0.106, 0.012, 0.41), 0.072, 0.080), ((0.105, 0.006, 0.31), 0.066, 0.071),
                ((0.105, 0.004, 0.275), 0.074, 0.078), ((0.105, 0.006, 0.245), 0.06, 0.064)], 22, steps=10, fold=(0.055, 7.5, 2.0)), M['uniform'])
    R.fin(c, 'SHIN_L', subsurf=1, mirror=True)
    g = Part('MR1_ThighGear_L')
    # off-white reinforced thigh panel and a retention strap (both legs)
    g.add(wrap(0.111, -55, 40, 0.15, 0.01, nu=10), M['canvas_white'], T(0.105, 0, 0.74))
    g.add(tube([((0.104, 0, 0.83), 0.097, 0.103), ((0.104, 0, 0.855), 0.097, 0.103)], 22), M['tan'])
    g.buckle((0.202, -0.02, 0.842), (1, -0.2, 0), M['bare'], 0.028, 0.02)
    R.fin(g, 'THIGH_L', mirror=True)
    k = Part('MR1_KneeGuard_L')
    k.add(plate(0.15, 0.18, 0.026, dome=0.07, p=2.6, cut=0.2, nu=10, nv=10), M['charcoal'], T(0.106, -0.066, 0.505))
    k.add(plate(0.118, 0.145, 0.014, dome=0.08, p=2.6, cut=0.22, nu=10, nv=10), M['white'], T(0.106, -0.097, 0.505))
    for bx, bz in ((-0.04, 0.05), (0.04, 0.05), (-0.04, -0.05), (0.04, -0.05)):
        k.bolt((0.106 + bx, -0.105, 0.505 + bz), (0, -1, 0), M['bare'], 0.0045)
    for z in (0.455, 0.545):
        k.add(tube([((0.106, 0.004, z), 0.075, 0.08), ((0.106, 0.004, z + 0.022), 0.075, 0.08)], 20), M['tan'])
    # lower-leg off-white gaiter panel with strap
    k.add(wrap(0.084, -60, 60, 0.11, 0.01, nu=10), M['canvas_white'], T(0.105, 0.004, 0.36))
    k.add(tube([((0.105, 0.006, 0.30), 0.071, 0.076), ((0.105, 0.006, 0.322), 0.071, 0.076)], 20), M['tan'])
    R.fin(k, 'SHIN_L', mirror=True)
    b = Part('MR1_Boot_L')
    _boot(b, 0.105, M)
    R.fin(b, 'FOOT_L', mirror=True)
    # asymmetric: drop-leg medical pouch on his right thigh, slim utility pouch on his left
    d = Part('MR1_DropPouch_R')
    d.pouch((-0.215, -0.01, 0.76), (0.12, 0.06, 0.17), (-1, -0.15, 0), M['sage'], M['sage'], M['tan'], M['bare'], puff=0.1)
    _badge(d, (-0.249, -0.016, 0.735), (-1, -0.15, 0), 0.045, M)
    d.add(rbox((0.01, 0.035, 0.2), 0.002, 1), M['tan'], T(-0.2, 0.0, 0.93))
    for z in (0.72, 0.80):
        d.add(tube([((-0.104, 0, z), 0.101, 0.106), ((-0.104, 0, z + 0.022), 0.101, 0.106)], 22), M['tan'])
    R.fin(d, 'THIGH_R')
    d = Part('MR1_ThighPouch_L')
    d.pouch((0.205, 0.0, 0.79), (0.085, 0.045, 0.15), (1, -0.1, 0), M['vest'], M['vest'], M['tan'], M['bare'], puff=0.1)
    R.fin(d, 'THIGH_L')


def _hips(R, M):
    c = Part('MR1_HipCloth')
    c.add(tube([((0, 0, 0.84), 0.155, 0.112, 0.125, 2.5), ((0, 0, 0.93), 0.172, 0.122, 0.135, 2.5),
                ((0, 0, 1.04), 0.166, 0.118, 0.122, 2.5)], 26, steps=6, fold=(0.03, 8, 0.3)), M['uniform'])
    R.fin(c, 'PELVIS', subsurf=1)
    b = Part('MR1_Belt')
    b.add(tube([((0, 0, 0.99), 0.178, 0.128, 0.136, 2.6), ((0, 0, 1.04), 0.178, 0.128, 0.136, 2.6)], 28), M['tan'])
    b.add(rbox((0.06, 0.012, 0.045), 0.003, 1), M['steel'], T(0, -0.132, 1.015))
    b.pouch((0.07, -0.152, 0.955), (0.10, 0.05, 0.12), (0.2, -1, 0), M['sage'], M['sage'], M['tan'], M['bare'], puff=0.1)
    b.pouch((0.155, -0.075, 0.955), (0.085, 0.05, 0.12), (0.8, -0.6, 0), M['vest'], M['vest'], M['tan'], M['bare'], puff=0.1)
    b.pouch((0.0, 0.158, 0.94), (0.20, 0.06, 0.12), (0, 1, 0), M['sage'], M['sage'], M['tan'], M['bare'], puff=0.1)
    b.pouch((-0.165, 0.06, 0.95), (0.09, 0.05, 0.12), (-0.85, 0.5, 0), M['vest'], M['vest'], M['tan'], M['bare'], puff=0.1)
    # tool holder on his right-front hip: open teal-lined rack with vials, a bandage roll and trauma shears
    Mh = T(-0.085, -0.15, 0.955) @ face_to((-0.25, -1, 0))
    b.add(rbox((0.105, 0.05, 0.10), 0.004, 2, puff=0.08), M['sage'], Mh)
    b.add(rbox((0.09, 0.036, 0.02), 0.002, 1), M['teal'], Mh @ T(0, 0, 0.052))
    b.add(rbox((0.11, 0.006, 0.02), 0.002, 1), M['tan'], Mh @ T(0, -0.027, 0.01))
    for i, (mat, cap) in enumerate(((M['canvas_white'], M['red']), (M['canvas_white'], M['teal_paint']), (M['red'], M['bare']))):
        x = -0.03 + 0.03 * i
        b.add(cyl(Mh @ Vector((x, 0.004, 0.03)), Mh @ Vector((x, 0.004, 0.095)), 0.011, seg=12, bevel=0.002), mat)
        b.add(cyl(Mh @ Vector((x, 0.004, 0.095)), Mh @ Vector((x, 0.004, 0.108)), 0.0095, seg=12, bevel=0.0015), cap)
    Ms = T(-0.15, -0.105, 0.97) @ face_to((-0.75, -0.65, 0))
    b.add(rbox((0.05, 0.02, 0.10), 0.003, 1, puff=0.06), M['vest'], Ms @ T(0, 0.004, -0.02))
    for sx in (-1, 1):
        ring = [(sx * (0.014 + 0.012 * cos(a)), -0.012, 0.06 + 0.017 * sin(a)) for a in [2 * pi * i / 10 for i in range(10)]]
        b.add(sweep([Ms @ Vector(q) for q in ring] + [Ms @ Vector(ring[0])], r=0.0038, seg=6, closed=True, smooth=2), M['red'])
        b.add(rbox((0.007, 0.004, 0.075), 0.001, 1), M['bare'], Ms @ T(sx * 0.005, -0.012, 0.0) @ Ry(sx * 4))
    b.add(cyl(Ms @ Vector((0, -0.017, 0.038)), Ms @ Vector((0, -0.008, 0.038)), 0.004, seg=8), M['bare'])
    R.fin(b, 'PELVIS')


def _torso(R, M):
    c = Part('MR1_Jacket')
    c.add(tube([((0, 0, 1.0), 0.162, 0.114, 0.12, 2.5), ((0, 0, 1.14), 0.164, 0.122, 0.124, 2.5),
                ((0, 0, 1.32), 0.186, 0.134, 0.132, 2.5), ((0, 0, 1.44), 0.176, 0.118, 0.126, 2.5),
                ((0, 0, 1.52), 0.10, 0.085, 0.095, 2.3), ((0, 0.005, 1.59), 0.062, 0.064, 0.068, 2.0)], 28, steps=11,
               fold=(0.02, 8, 0.4)), M['uniform'])
    R.fin(c, 'CHEST', subsurf=1)
    v = Part('MR1_Vest')
    v.add(tube([((0, 0, 1.07), 0.176, 0.134, 0.136, 2.8), ((0, 0, 1.18), 0.180, 0.14, 0.14, 2.8),
                ((0, 0, 1.275), 0.192, 0.146, 0.142, 2.8)], 30, steps=4), M['vest'])
    v.add(plate(0.27, 0.29, 0.03, dome=0.02, p=7, bend=0.33, nu=12, nv=10, cut=0.1), M['vest'], T(0, -0.128, 1.315))
    v.add(plate(0.28, 0.32, 0.03, dome=0.02, p=7, bend=0.34, nu=12, nv=10, cut=0.1), M['vest'], T(0, 0.126, 1.32) @ Rz(180))
    for z in (1.40, 1.37, 1.34):
        v.add(rbox((0.23, 0.005, 0.018), 0.0015, 1), M['tan'], T(0, -0.158, z))
    # three slim pouches across the belly, admin pouch + badge on his left chest, radio on his right
    for i in range(3):
        x = -0.085 + 0.085 * i
        v.pouch((x, -0.175, 1.165), (0.075, 0.045, 0.13), (x * 0.8, -1, 0), M['sage'], M['sage'], M['tan'], M['bare'], puff=0.1)
    v.pouch((0.07, -0.168, 1.30), (0.10, 0.035, 0.085), (0.1, -1, 0), M['sage'], M['sage'], M['tan'], M['bare'], puff=0.08)
    _badge(v, (0.072, -0.1765, 1.395), (0.1, -1, 0.05), 0.05, M)
    v.add(rbox((0.042, 0.03, 0.085), 0.004, 2), M['steel'], T(-0.085, -0.165, 1.37))
    v.add(cyl((-0.098, -0.165, 1.41), (-0.098, -0.165, 1.50), 0.004, seg=8), M['rubber'])
    v.add(rbox((0.03, 0.008, 0.03), 0.002, 1), M['bare'], T(-0.085, -0.182, 1.385))
    for s in (1, -1):
        path = [(s * 0.098, -0.15, 1.40), (s * 0.106, -0.10, 1.505), (s * 0.11, -0.01, 1.545), (s * 0.108, 0.08, 1.51),
                (s * 0.10, 0.13, 1.40)]
        v.add(sweep(path, rect=(0.058, 0.018), normal=lambda p: p - Vector((p.x * 0.85, 0.0, 1.36)), smooth=6), M['tan'])
        v.buckle((s * 0.1, -0.146, 1.44), (0, -1, 0.3), M['bare'], 0.036, 0.024)
    R.fin(v, 'CHEST')
    k = Part('MR1_Scarf')
    ring = [(0.112 * cos(a), 0.012 + 0.105 * sin(a), 1.555 + 0.022 * sin(a) + 0.012 * sin(3 * a))
            for a in [2 * pi * i / 14 for i in range(14)]]
    k.add(sweep(ring + [ring[0]], r=0.044, seg=10, closed=True, smooth=3), M['uniform'])
    ring2 = [(0.098 * cos(a), 0.0 + 0.092 * sin(a), 1.60 + 0.02 * sin(a) + 0.01 * sin(2 * a + 1))
             for a in [2 * pi * i / 12 for i in range(12)]]
    k.add(sweep(ring2 + [ring2[0]], r=0.034, seg=10, closed=True, smooth=3), M['uniform'])
    R.fin(k, 'CHEST', subsurf=1)


def _label(R, text, joint, Mw, size, mat):
    cu = bpy.data.curves.new('MR1_Label_' + text, 'FONT')
    cu.body = text
    cu.size = size
    cu.extrude = 0.0015
    cu.align_x = 'CENTER'
    cu.align_y = 'CENTER'
    tmp = bpy.data.objects.new('MR1_tmp_text', cu)
    R.sc.collection.objects.link(tmp)
    bpy.context.view_layer.update()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    bpy.data.objects.remove(tmp, do_unlink=True)
    bpy.data.curves.remove(cu)
    me.name = 'MR1_Label_' + text
    me.materials.append(mat)
    ob = bpy.data.objects.new('MR1_Label_' + text, me)
    R.sc.collection.objects.link(ob)
    ob.matrix_world = Mw @ Rx(90)
    bpy.context.view_layer.update()
    mw = ob.matrix_world.copy()
    e = R.E[joint]
    ob.parent = e
    ob.matrix_parent_inverse = e.matrix_world.inverted()
    ob.matrix_world = mw
    R.objs.append(ob)
    return ob


def _backpack(R, M):
    p = Part('MR1_MedPack')
    C = Vector((0, 0.235, 1.30))
    p.add(rbox((0.30, 0.16, 0.46), 0.02, 3, puff=0.1), M['sage'], T(C))
    p.add(rbox((0.31, 0.165, 0.10), 0.012, 2, puff=0.12), M['vest'], T(C + Vector((0, 0.0, 0.215))))            # lid
    # main off-white medical compartment with teal binding, lower pocket, side pockets
    F = C + Vector((0, 0.105, 0.045))
    p.add(rbox((0.235, 0.075, 0.21), 0.014, 3, puff=0.14), M['canvas_white'], T(F))
    for sx in (1, -1):
        p.add(rbox((0.012, 0.08, 0.215), 0.003, 1), M['teal'], T(F + Vector((sx * 0.118, 0, 0))))
    for sz in (1, -1):
        p.add(rbox((0.24, 0.08, 0.012), 0.003, 1), M['teal'], T(F + Vector((0, 0, sz * 0.106))))
    _badge(p, F + Vector((0, 0.041, 0.035)), (0, 1, 0), 0.085, M)
    p.pouch(C + Vector((0, 0.10, -0.155)), (0.22, 0.06, 0.13), (0, 1, 0), M['sage'], M['canvas_white'], M['teal'], M['bare'],
            puff=0.1)
    for sx in (1, -1):
        p.pouch(C + Vector((sx * 0.168, 0.0, -0.11)), (0.13, 0.05, 0.18), (sx, 0, 0), M['sage'], M['sage'], M['teal'], M['bare'],
                puff=0.1)
        # teal compression straps running over the pack, with buckles
        x = sx * 0.085
        p.add(rbox((0.026, 0.006, 0.47), 0.002, 1), M['teal'], T(x, C.y + 0.084, C.z))
        p.add(rbox((0.026, 0.17, 0.006), 0.002, 1), M['teal'], T(x, C.y, C.z + 0.268))
        p.buckle((x, C.y + 0.148, C.z - 0.07), (0, 1, 0), M['bare'], 0.03, 0.022)
        p.buckle((x, C.y + 0.09, C.z + 0.19), (0, 1, 0.2), M['bare'], 0.03, 0.022)
    # rolled blanket / sleeping mat on top with teal straps
    top = C.z + 0.335
    p.add(cyl((-0.19, C.y - 0.005, top), (0.19, C.y - 0.005, top), 0.062, seg=22, bevel=0.016), M['uniform'])
    for i in range(3):
        rr = 0.05 - 0.016 * i
        for sx in (1, -1):
            p.add(cyl((sx * 0.19, C.y - 0.005, top), (sx * (0.1915 + 0.0012 * i), C.y - 0.005, top), rr, seg=18), M['vest'] if i % 2 else M['uniform'])
    for x in (-0.11, 0.11):
        p.add(cyl((x - 0.013, C.y - 0.005, top), (x + 0.013, C.y - 0.005, top), 0.066, seg=22), M['teal'])
        p.buckle((x, C.y + 0.063, top + 0.012), (0, 1, 0.2), M['bare'], 0.028, 0.02)
    # side equipment: dark flask with teal band (his right), yellow sealed container (his left)
    fx = -0.205
    p.add(cyl((fx, C.y + 0.01, C.z + 0.0), (fx, C.y + 0.01, C.z + 0.17), 0.034, seg=18, bevel=0.008), M['steel'])
    p.add(cyl((fx, C.y + 0.01, C.z + 0.17), (fx, C.y + 0.01, C.z + 0.20), 0.016, seg=12, bevel=0.003), M['bare'])
    p.add(cyl((fx, C.y + 0.01, C.z + 0.07), (fx, C.y + 0.01, C.z + 0.10), 0.0355, seg=18), M['teal_paint'])
    p.add(rbox((0.008, 0.03, 0.2), 0.002, 1), M['teal'], T(fx - 0.034, C.y + 0.01, C.z + 0.08))
    yx = 0.2
    p.add(rbox((0.06, 0.09, 0.15), 0.01, 2), M['yellow'], T(yx, C.y + 0.01, C.z + 0.085))
    p.add(rbox((0.03, 0.03, 0.02), 0.004, 1), M['steel'], T(yx, C.y + 0.01, C.z + 0.17))
    p.add(rbox((0.008, 0.03, 0.17), 0.002, 1), M['teal'], T(yx + 0.032, C.y + 0.01, C.z + 0.085))
    # grab handle, harness: shoulder straps over the vest and a waist strap
    p.add(sweep([(-0.05, C.y - 0.07, C.z + 0.26), (-0.035, C.y - 0.085, C.z + 0.30), (0.035, C.y - 0.085, C.z + 0.30),
                 (0.05, C.y - 0.07, C.z + 0.26)], rect=(0.022, 0.006), normal=lambda q: Vector((0, -1, 0.3)), smooth=4), M['tan'])
    for s in (1, -1):
        p.add(sweep([(s * 0.10, C.y - 0.075, C.z + 0.20), (s * 0.118, 0.07, 1.535), (s * 0.122, -0.02, 1.565),
                     (s * 0.118, -0.10, 1.52), (s * 0.108, -0.155, 1.40), (s * 0.12, -0.165, 1.26)], rect=(0.05, 0.016),
                    normal=lambda q: q - Vector((q.x * 0.8, 0.0, 1.34)), smooth=6), M['vest'])
        p.add(rbox((0.012, 0.11, 0.035), 0.003, 1), M['teal'], T(s * 0.158, C.y - 0.11, 1.08))
    R.fin(p, 'CHEST')
    _label(R, 'MED', 'CHEST', T(0, C.y + 0.1465, C.z - 0.025) @ Rz(180), 0.045, M['teal_paint'])


def _arms(R, M):
    c = Part('MR1_Sleeve_L')
    c.add(tube([((0.198, 0, 1.525), 0.070, 0.078), ((0.208, 0, 1.45), 0.073, 0.078), ((0.206, -0.003, 1.36), 0.062, 0.071),
                ((0.204, 0, 1.25), 0.055, 0.060), ((0.203, 0, 1.165), 0.052, 0.056)], 20, steps=12, fold=(0.05, 7.5, 1.1)),
          M['uniform'])
    R.fin(c, 'UARM_L', subsurf=1, mirror=True)
    a = Part('MR1_ShoulderGear_L')
    Mp = T(0.262, 0.0, 1.475) @ face_to((1, 0, 0.5))
    a.add(plate(0.15, 0.13, 0.014, dome=0.05, p=3.2, cut=0.2, nu=8, nv=8), M['vest'], Mp)
    # off-white identification brassard wrapped round the upper arm, with the medic badge
    a.add(wrap(0.076, 25, 175, 0.105, 0.009, nu=10), M['canvas_white'], T(0.206, 0, 1.36))
    _badge(a, (0.2845, 0.0, 1.36), (1, 0, 0), 0.055, M)
    a.add(tube([((0.206, 0, 1.30), 0.061, 0.066), ((0.206, 0, 1.318), 0.061, 0.066)], 20), M['tan'])
    R.fin(a, 'UARM_L', mirror=True)
    c = Part('MR1_ForearmCloth_L')
    c.add(tube([((0.203, 0, 1.21), 0.054, 0.058), ((0.205, 0, 1.13), 0.058, 0.060), ((0.203, 0, 1.02), 0.046, 0.05),
                ((0.202, 0, 0.93), 0.038, 0.042)], 20, steps=9, fold=(0.05, 8.5, 2.2)), M['charcoal'])
    R.fin(c, 'FARM_L', subsurf=1, mirror=True)
    f = Part('MR1_ForearmGuard_L')
    f.add(plate(0.095, 0.105, 0.016, dome=0.045, p=3.0, cut=0.2), M['charcoal'], T(0.203, 0.064, 1.185) @ Rz(180))
    # off-white padded forearm guard in two overlapping sections with straps
    f.add(wrap(0.063, -20, 230, 0.11, 0.013, r2=0.073, nu=16), M['white'], T(0.203, 0.0, 1.10))
    f.add(wrap(0.056, -20, 230, 0.088, 0.012, r2=0.064, nu=16), M['white'], T(0.203, 0.0, 1.012))
    for z in (0.995, 1.075, 1.13):
        f.add(tube([((0.203, 0, z), 0.059 if z < 1.05 else 0.068, 0.062 if z < 1.05 else 0.071),
                    ((0.203, 0, z + 0.016), 0.06 if z < 1.05 else 0.069, 0.063 if z < 1.05 else 0.072)], 20), M['tan'])
    R.fin(f, 'FARM_L', mirror=True)
    g = Part('MR1_Glove_L')
    W = Vector((0.202, 0, 0.915))
    g.add(tube([((0.202, 0, 0.945), 0.042, 0.046), ((0.202, 0, 0.895), 0.039, 0.043)], 18), M['glove'])
    g.add(tube([((0.202, 0, 0.928), 0.044, 0.048), ((0.202, 0, 0.912), 0.044, 0.048)], 18), M['tan'])
    g.add(K.loft([(-0.046, 0.202, 0.015, 0.888, 0.80), (-0.02, 0.202, 0.019, 0.895, 0.79), (0.012, 0.202, 0.019, 0.895, 0.795),
                  (0.036, 0.202, 0.014, 0.888, 0.81)], 14, p=3.0), M['glove'])
    g.add(plate(0.075, 0.05, 0.008, dome=0.012, p=5, cut=0.15), M['rubber'], T(0.2225, -0.004, 0.83) @ face_to((1, 0, 0)))
    for i in range(4):
        y = -0.036 + 0.0225 * i
        ln = 1.0 - 0.1 * abs(i - 1.2)
        pts = [(0.206, y, 0.797), (0.204, y, 0.762), (0.188, y, 0.735), (0.164 + (1 - ln) * 0.015, y, 0.736),
               (0.151 + (1 - ln) * 0.02, y, 0.756)]
        g.add(sweep(pts, r=0.0098, seg=8, smooth=5), M['glove'])
        for q in pts[1:3]:
            g.add(ball(0.0112, 8, 6), M['glove'], T(q))
        g.add(rbox((0.008, 0.017, 0.022), 0.002, 1), M['rubber'], T(0.2145, y, 0.78))
    g.add(sweep([(0.195, -0.044, 0.865), (0.18, -0.066, 0.838), (0.162, -0.071, 0.805), (0.155, -0.058, 0.785)], r=0.0115, seg=8,
                smooth=5), M['glove'])
    R.fin(g, 'HAND_L', mirror=True)


def _head(R, M):
    h = Part('MR1_Head')
    h.add(ball((0.076, 0.09, 0.104), 24, 14), M['skin'], T(0, -0.004, 1.69))
    h.add(rbox((0.026, 0.026, 0.036), 0.004, 2, puff=0.4), M['skin'], T(0, -0.089, 1.694))
    for s in (1, -1):
        h.add(ball(0.0098, 12, 8), M['eye'], T(s * 0.028, -0.079, 1.712))
        h.add(ball(0.0054, 10, 6), M['iris'], T(s * 0.028, -0.0853, 1.712))
        h.add(rbox((0.034, 0.008, 0.005), 0.0015, 1), M['hair'], T(s * 0.029, -0.0855, 1.7265) @ Ry(s * 8))
        h.add(rbox((0.04, 0.016, 0.012), 0.003, 2, puff=0.3), M['skin'], T(s * 0.03, -0.08, 1.7245) @ Ry(s * 8))
    R.fin(h, 'HEAD')
    b = Part('MR1_Balaclava')
    b.add(ball((0.082, 0.097, 0.11), 28, 16, cut=lambda c: c.y < -0.45 and 0.08 < c.z < 0.36 and abs(c.x) < 0.7), M['charcoal'],
          T(0, -0.004, 1.69))
    R.fin(b, 'HEAD', subsurf=1)
    e = Part('MR1_Helmet')
    e.add(ball((0.113, 0.134, 0.112), 32, 18,
               cut=lambda c: c.z < -0.3 or (c.y < -0.3 and c.z < 0.0) or (abs(c.x) > 0.8 and c.z < -0.02 and -0.35 < c.y < 0.45)),
          M['white'], T(0, 0.012, 1.742))
    R.fin(e, 'HEAD', solidify=0.012)
    g = Part('MR1_HelmetGear')
    _badge(g, (0, -0.078, 1.839), (0, -0.62, 0.78), 0.062, M, up=(0, 0.78, 0.62))
    g.add(rbox((0.045, 0.02, 0.04), 0.004, 2), M['steel'], T(0, -0.131, 1.775) @ Rx(-16))
    g.add(rbox((0.024, 0.14, 0.008), 0.003, 1), M['steel'], T(0, 0.03, 1.856))
    g.add(tube([((0, 0.012, 1.772), 0.1155, 0.1355, 0.137, 2.0), ((0, 0.012, 1.789), 0.114, 0.133, 0.135, 2.0)], 30), M['rubber'])
    g.add(rbox((0.075, 0.03, 0.045), 0.005, 2, puff=0.15), M['vest'], T(0, 0.139, 1.76) @ Rx(12))
    for s in (1, -1):
        g.add(rbox((0.012, 0.10, 0.02), 0.003, 1), M['steel'], T(s * 0.113, 0.01, 1.758))
        for y in (-0.025, 0.045):
            g.bolt((s * 0.119, y, 1.758), (s, 0, 0), M['bare'], 0.005)
        g.add(cyl((s * 0.082, 0.012, 1.70), (s * 0.118, 0.012, 1.70), 0.038, seg=18, bevel=0.006), M['steel'])
        g.add(cyl((s * 0.118, 0.012, 1.70), (s * 0.125, 0.012, 1.70), 0.026, seg=14, bevel=0.003), M['rubber'])
        g.add(sweep([(s * 0.092, -0.03, 1.695), (s * 0.08, -0.058, 1.65), (s * 0.05, -0.084, 1.612)], rect=(0.016, 0.005),
                    normal=lambda q, s=s: Vector((s, -0.5, 0)), smooth=5), M['tan'])
        g.buckle((s * 0.083, -0.052, 1.66), (s, -0.5, 0), M['bare'], 0.016, 0.014)
    # helmet light on his left rail
    g.add(rbox((0.03, 0.03, 0.022), 0.003, 1), M['steel'], T(0.125, -0.04, 1.765))
    g.add(cyl((0.135, -0.035, 1.765), (0.135, -0.095, 1.765), 0.016, seg=14, bevel=0.003), M['steel'])
    g.add(cyl((0.135, -0.095, 1.765), (0.135, -0.101, 1.765), 0.019, seg=14, bevel=0.002), M['bare'])
    g.add(cyl((0.135, -0.1005, 1.765), (0.135, -0.1025, 1.765), 0.0145, seg=14), M['lamp'])
    R.fin(g, 'HEAD')
    r = Part('MR1_Respirator')
    r.add(ball((0.06, 0.052, 0.058), 20, 12), M['rubber'], T(0, -0.077, 1.652))
    r.add(rbox((0.036, 0.026, 0.034), 0.004, 2, puff=0.4), M['rubber'], T(0, -0.106, 1.688))
    r.add(cyl((0, -0.11, 1.645), (0, -0.138, 1.642), 0.024, seg=18, bevel=0.004), M['steel'])
    r.add(cyl((0, -0.138, 1.642), (0, -0.142, 1.642), 0.017, seg=18, bevel=0.002), M['rubber'])
    for s in (1, -1):
        a0, a1 = Vector((s * 0.04, -0.095, 1.642)), Vector((s * 0.096, -0.128, 1.626))
        d = (a1 - a0).normalized()
        r.add(cyl(a0, a1, 0.031, seg=20, bevel=0.004), M['steel'])
        r.add(cyl(a1 - d * 0.02, a1 - d * 0.008, 0.0325, seg=20), M['teal_paint'])
        r.add(cyl(a1, a1 + d * 0.005, 0.025, seg=20, bevel=0.002), M['bare'])
        for i in range(6):
            a = 2 * pi * i / 6
            u = d.orthogonal().normalized()
            w = d.cross(u)
            q = a1 + d * 0.005 + (u * cos(a) + w * sin(a)) * 0.014
            r.add(cyl(q, q + d * 0.002, 0.0045, seg=6), M['rubber'])
        r.add(sweep([(s * 0.055, -0.058, 1.668), (s * 0.085, 0.0, 1.676), (s * 0.076, 0.075, 1.68)], rect=(0.018, 0.005),
                    normal=lambda q, s=s: Vector((s, -0.2, 0)), smooth=5), M['rubber'])
    r.add(cyl((0, -0.098, 1.606), (0, -0.112, 1.59), 0.017, seg=14, bevel=0.003), M['steel'])
    R.fin(r, 'HEAD')


def _stun(R, M):
    g = Part('MR1_StunDevice')
    O = Vector(JOINTS['GUN'][0])
    P = lambda x, y, z: O + Vector((x, y, z))
    g.add(rbox((0.044, 0.19, 0.062), 0.006, 2), M['steel'], T(P(0, -0.055, 0.058)))                       # housing
    g.add(rbox((0.036, 0.048, 0.105), 0.008, 2, taper=(1.0, 1.2)), M['rubber'], T(P(0, 0.012, -0.012)) @ Rx(-14))
    for i in range(4):
        g.add(rbox((0.039, 0.05, 0.006), 0.001, 1), M['steel'], T(P(0, 0.016 - 0.004 * i, -0.045 + 0.02 * i)) @ Rx(-14))
    g.add(rbox((0.012, 0.15, 0.008), 0.002, 1), M['steel'], T(P(0, -0.055, 0.093)))                       # top rail
    for i in range(6):
        g.add(rbox((0.016, 0.008, 0.004), 0.001, 1), M['rubber'], T(P(0, -0.005 - 0.02 * i, 0.098)))
    g.add(rbox((0.048, 0.03, 0.068), 0.004, 2), M['steel'], T(P(0, 0.048, 0.058)))                        # rear cap
    g.add(cyl(P(0, 0.062, 0.058), P(0, 0.07, 0.058), 0.012, seg=12, bevel=0.002), M['bare'])
    g.add(rbox((0.002, 0.045, 0.022), 0.0005, 1), M['yellow'], T(P(-0.0225, -0.02, 0.06)))                # warning plate
    g.add(rbox((0.002, 0.045, 0.022), 0.0005, 1), M['yellow'], T(P(0.0225, -0.02, 0.06)))
    for sx in (-1, 1):
        g.add(prism([(-0.008, -0.006), (0.008, -0.006), (0, 0.008)], 0.001), M['steel'], T(P(sx * 0.0238, -0.02, 0.06)) @ Rz(sx * 90),
              smooth=False)
        g.bolt(P(sx * 0.022, -0.115, 0.075), (sx, 0, 0), M['bare'], 0.004, 0.002)
        g.bolt(P(sx * 0.022, 0.02, 0.075), (sx, 0, 0), M['bare'], 0.004, 0.002)
    # energy chamber: lit core in a caged tube between two insulated collars
    g.add(cyl(P(0, -0.15, 0.058), P(0, -0.165, 0.058), 0.03, seg=20, bevel=0.003), M['rubber'])
    g.add(cyl(P(0, -0.165, 0.058), P(0, -0.25, 0.058), 0.019, seg=20), M['cyan'])
    for i in range(5):
        y = -0.176 - 0.016 * i
        g.add(cyl(P(0, y, 0.058), P(0, y - 0.005, 0.058), 0.0225, seg=20), M['teal_paint'])
    for a in (45, 135, 225, 315):
        ca, sa = cos(radians(a)) * 0.026, sin(radians(a)) * 0.026
        g.add(cyl(P(ca, -0.162, 0.058 + sa), P(ca, -0.253, 0.058 + sa), 0.0032, seg=6), M['steel'])
    g.add(cyl(P(0, -0.25, 0.058), P(0, -0.265, 0.058), 0.03, seg=20, bevel=0.003), M['rubber'])
    # emitter: steel muzzle ring and two insulated electrodes
    g.add(cyl(P(0, -0.265, 0.058), P(0, -0.285, 0.058), 0.024, seg=20, bevel=0.003), M['steel'])
    for sx in (-1, 1):
        g.add(cyl(P(sx * 0.012, -0.285, 0.058), P(sx * 0.012, -0.305, 0.058), 0.005, seg=8), M['yellow'])
        g.add(cyl(P(sx * 0.012, -0.305, 0.058), P(sx * 0.012, -0.318, 0.058), 0.003, seg=8), M['bare'])
    g.add(sweep([P(0, -0.03, 0.028), P(0, -0.045, -0.006), P(0, -0.085, -0.008), P(0, -0.1, 0.028)], r=0.004, seg=8, smooth=5),
          M['steel'])
    g.add(rbox((0.006, 0.009, 0.024), 0.001, 1), M['bare'], T(P(0, -0.062, 0.012)) @ Rx(16))
    g.add(rbox((0.03, 0.06, 0.02), 0.004, 1), M['steel'], T(P(0, -0.12, 0.02)))                           # battery under-barrel
    g.add(rbox((0.022, 0.004, 0.012), 0.001, 1), M['cyan'], T(P(0, 0.064, 0.082)))                        # charge indicator
    R.fin(g, 'GUN')


def build():
    importlib.reload(K)
    sc = _scene()
    bpy.context.window_manager.windows[0].scene = sc
    M = materials()
    R = Rig(sc)
    _legs(R, M)
    _hips(R, M)
    _torso(R, M)
    _arms(R, M)
    _head(R, M)
    _stun(R, M)
    bpy.context.view_layer.update()
    _backpack(R, M)
    bpy.context.view_layer.update()
    return R


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
        e = bpy.data.objects.new('MR1_J_' + name, None)
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
        self.E = ({o.name[6:]: o for o in sc.objects if o.name.startswith('MR1_J_')} if existing else _empties(sc))
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
            tex = bpy.data.textures.get('MR1_Folds')
            if tex is None:
                tex = bpy.data.textures.new('MR1_Folds', 'WOOD')
                tex.wood_type = 'BANDNOISE'
                tex.noise_scale = 0.11
                tex.turbulence = 9.0
            fine = bpy.data.textures.get('MR1_FoldsFine')
            if fine is None:
                fine = bpy.data.textures.new('MR1_FoldsFine', 'CLOUDS')
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


# ============================================================================== posing (review aid)
def _E(name):
    return bpy.data.objects['MR1_J_' + name]


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
    off = Vector((HOLD.x * side, HOLD.y, HOLD.z)) - Vector((WRIST.x * side, WRIST.y, WRIST.z))
    wrist = Vector(hold_world) - Rm @ off
    return Matrix.Translation(wrist) @ Rm.to_4x4(), wrist


def pose(kind='ready'):
    sc = bpy.data.scenes[SCENE]
    win = bpy.context.window_manager.windows[0]
    win.scene = sc
    vl = bpy.context.view_layer
    for o in sc.objects:
        if o.name.startswith('MR1_J_'):
            o.matrix_basis = Matrix.Translation(Vector(o['rest']) - (Vector(o.parent['rest']) if o.parent else Vector()))
    vl.update()
    if kind == 'rest':
        return
    # legs: wide planted stance
    for tag, s, yaw, pitch in (('_L', 1, 12, -9), ('_R', -1, -8, -4)):
        hip = Vector(_E('THIGH' + tag)['rest'])
        if kind == 'apose':
            _set('THIGH' + tag, T(hip) @ Ry(-s * 5))
            continue
        _set('THIGH' + tag, T(hip) @ Rz(yaw) @ Ry(-s * 6) @ Rx(pitch))
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
        _E('CHEST').rotation_euler = (radians(3), 0, radians(-12))
        _E('HEAD').rotation_euler = (radians(-2), 0, radians(8))
        vl.update()
        Mg = T(-0.13, -0.27, 1.13) @ Rz(26) @ Rx(6)
        _set('GUN', Mg @ T(-Vector(JOINTS['GUN'][0])) @ T(Vector(JOINTS['GUN'][0])))
        f = (Mg.to_3x3() @ Vector((0, -1, 0))).normalized()
        gup = (Mg.to_3x3() @ Vector((0, 0, 1))).normalized()
        gside = (Mg.to_3x3() @ Vector((1, 0, 0))).normalized()
        # right hand on the pistol grip, left hand under the pump
        Hr, wr = _hand_matrix(-1, Mg @ Vector((0.0, 0.012, -0.012)), -gside, -f * 1.0 + gup * 0.25)
        Hl, wl = _hand_matrix(1, Mg @ GUN_FORE, -gup, gside * 1.0 + f * 0.35)
        for tag, s, H, w, pole in (('_R', -1, Hr, wr, (-1, 0.9, -0.2)), ('_L', 1, Hl, wl, (1.0, 0.1, -0.9))):
            sh = _E('UARM' + tag).matrix_world.translation.copy()
            el, wr2 = _two_bone(sh, w, 0.29, 0.265, pole)
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
VIEWS = dict(front=(0, 3, 85, 8.8), back=(180, 3, 85, 8.8), left=(90, 3, 85, 8.8), right=(-90, 3, 85, 8.8),
             hero=(-40, 5, 70, 7.0), gameplay=(-30, 45, 85, 9.4))


def studio(samples=64, res=(900, 1200)):
    sc = bpy.data.scenes[SCENE]
    for o in list(sc.objects):
        if o.name.startswith('MR1_Studio'):
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
    w = bpy.data.worlds.get('MR1_World') or bpy.data.worlds.new('MR1_World')
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
        o.parent = bpy.data.objects.get('MR1_StudioPivot')
        return o
    piv = bpy.data.objects.new('MR1_StudioPivot', None)
    sc.collection.objects.link(piv)
    light('MR1_StudioKey', (-2.6, -3.2, 3.6), 560, 2.2, (1.0, 0.95, 0.88))
    light('MR1_StudioFill', (3.2, -2.2, 1.6), 40, 4.0, (0.9, 0.95, 1.0))
    light('MR1_StudioRim', (1.5, 3.4, 3.0), 380, 2.5)
    me = bpy.data.meshes.new('MR1_StudioFloor')
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=30.0)
    bm.to_mesh(me)
    bm.free()
    fl = bpy.data.objects.new('MR1_StudioFloor', me)
    sc.collection.objects.link(fl)
    fl.is_shadow_catcher = True
    cam = bpy.data.objects.new('MR1_StudioCam', bpy.data.cameras.new('MR1_StudioCam'))
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
    tgt = Vector((0, 0.04, target_z))
    cam.location = tgt + d * dist * dist_scale
    cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    # lights follow the camera so every view is lit like the hero view
    piv = bpy.data.objects['MR1_StudioPivot']
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
        if o.type != 'MESH' or o.name.startswith('MR1_Studio'):
            continue
        me = o.evaluated_get(dg).to_mesh()
        tris = sum(len(p.vertices) - 2 for p in me.polygons)
        o.evaluated_get(dg).to_mesh_clear()
        tot += tris
        out.append((o.name, tris, len(o.data.materials)))
    return tot, out


