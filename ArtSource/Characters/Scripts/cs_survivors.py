"""Cargo Squad - survivor builder: shared humanoid skeleton + parametric body + per-role gear.

All dimensions below are authored in *base units* for a 1.80 m body and scaled by k = H / 1.80
when the meshes / rig are created, so every survivor shares bone names, hierarchy and rest pose (T-pose).
"""
import bpy
from math import radians
from mathutils import Vector, Matrix
import cs_core as C
from cs_core import Builder, R, V, material

SIDES = (('Left', 1.0), ('Right', -1.0))
WEAPON_SOCKET_R = Matrix(((0, 1, 0), (0, 0, -1), (-1, 0, 0)))  # cols: local X,Y,Z in armature space (T-pose)
PALM = Vector((0.07, 0.0, -0.032))  # palm hollow offset from wrist for the left hand (x mirrored for right)


def joints(S):
    sw, hw = S.get('sw', 0.19), S.get('hw', 0.095)
    ua, la, hl = 0.29 * S.get('arm', 1.0), 0.26 * S.get('arm', 1.0), 0.09
    J = {}
    J['Hips'] = ((0, 0, 0.98), (0, 0, 1.08), None)
    J['Spine'] = ((0, 0, 1.08), (0, 0, 1.26), 'Hips')
    J['Chest'] = ((0, 0, 1.26), (0, 0, 1.50), 'Spine')
    J['Neck'] = ((0, 0, 1.50), (0, 0, 1.59), 'Chest')
    J['Head'] = ((0, 0, 1.59), (0, 0, 1.80), 'Neck')
    zs = 1.44
    for s, d in SIDES:
        J[s + 'Shoulder'] = ((d * 0.03, 0, zs + 0.01), (d * (sw - 0.02), 0, zs + 0.01), 'Chest')
        J[s + 'UpperArm'] = ((d * sw, 0, zs), (d * (sw + ua), 0, zs), s + 'Shoulder')
        J[s + 'LowerArm'] = ((d * (sw + ua), 0, zs), (d * (sw + ua + la), 0, zs), s + 'UpperArm')
        J[s + 'Hand'] = ((d * (sw + ua + la), 0, zs), (d * (sw + ua + la + hl), 0, zs), s + 'LowerArm')
        J[s + 'UpperLeg'] = ((d * hw, 0, 0.94), (d * hw, -0.005, 0.51), 'Hips')
        J[s + 'LowerLeg'] = ((d * hw, -0.005, 0.51), (d * hw, 0.01, 0.09), s + 'UpperLeg')
        J[s + 'Foot'] = ((d * hw, 0.01, 0.09), (d * hw, -0.13, 0.025), s + 'LowerLeg')
        J[s + 'Toes'] = ((d * hw, -0.13, 0.025), (d * hw, -0.22, 0.025), s + 'Foot')
    return J


# torso profile: z, half-width, front depth, back depth (base units)
TORSO = [(0.86, 0.120, 0.080, 0.090), (0.90, 0.158, 0.098, 0.108), (0.96, 0.172, 0.105, 0.116),
         (1.04, 0.160, 0.100, 0.102), (1.12, 0.150, 0.096, 0.096), (1.22, 0.166, 0.110, 0.100),
         (1.33, 0.186, 0.124, 0.106), (1.42, 0.190, 0.114, 0.106), (1.475, 0.150, 0.090, 0.096),
         (1.51, 0.072, 0.066, 0.070), (1.56, 0.058, 0.058, 0.062)]


def _lerp(a, b, t):
    return a + (b - a) * t


class G:
    """Per-character build context."""

    def __init__(self, S):
        self.S = S
        self.name = S['name']
        self.k = S['H'] / 1.80
        self.J = joints(S)
        self.sw, self.hw = S.get('sw', 0.19), S.get('hw', 0.095)
        self.bulk = S.get('bulk', 1.0)
        self.tw, self.td = S.get('tw', 1.0), S.get('td', 1.0)
        self.wrist = self.sw + (0.29 + 0.26) * S.get('arm', 1.0)
        self.elbow = self.sw + 0.29 * S.get('arm', 1.0)
        self.body, self.gear, self.head = Builder(), Builder(), Builder()
        self.pack, self.weapon = Builder(), Builder()
        self.M = {}
        self.sockets = {}

    def mat(self, key, hexc=None, rough=0.8, metal=0.0, shared=False, emit=None):
        if key not in self.M:
            self.M[key] = material(('CS_' if shared else self.name + '_') + key, hexc, rough, metal, emit)
        return self.M[key]

    def torso_r(self, z):
        T = TORSO
        if z <= T[0][0]:
            r = T[0][1:]
        elif z >= T[-1][0]:
            r = T[-1][1:]
        else:
            for a, b in zip(T, T[1:]):
                if a[0] <= z <= b[0]:
                    t = (z - a[0]) / (b[0] - a[0])
                    r = tuple(_lerp(a[i], b[i], t) for i in (1, 2, 3))
                    break
        # widen the shoulder line with sw, keep neck unaffected
        shoulder = max(0.0, min(1.0, (z - 1.12) / 0.2)) * max(0.0, min(1.0, (1.50 - z) / 0.03))
        wx = self.tw * _lerp(1.0, self.sw / 0.19, shoulder)
        neck = 1.0 if z < 1.48 else 0.0
        return (r[0] * _lerp(1.0, wx, neck) if z >= 1.48 else r[0] * wx,
                r[1] * (self.td if z < 1.48 else 1.0), r[2] * (self.td if z < 1.48 else 1.0))

    @staticmethod
    def torso_w(z):
        if z < 1.00:
            return {'Hips': 1.0}
        if z < 1.10:
            t = (z - 1.00) / 0.10
            return {'Hips': 1 - t, 'Spine': t}
        if z < 1.20:
            return {'Spine': 1.0}
        if z < 1.30:
            t = (z - 1.20) / 0.10
            return {'Spine': 1 - t, 'Chest': t}
        if z < 1.48:
            return {'Chest': 1.0}
        if z < 1.54:
            t = (z - 1.48) / 0.06
            return {'Chest': 1 - t, 'Neck': t}
        return {'Neck': 1.0}

    def shell(self, builder, zs, thick, mat, seg=16, p=2.5):
        """Tube that hugs the torso between heights zs (list of (z, extra_thickness))."""
        rings = []
        for z, t in zs:
            rx, rf, rb = self.torso_r(z)
            rings.append(R((0, 0, z), rx + t + thick, rf=rf + t + thick, rb=rb + t + thick, w=self.torso_w(z), p=p))
        builder.tube(rings, mat, seg=seg)


def build_body(g):
    b, S, bulk = g.body, g.S, g.bulk
    m_jacket, m_pants = g.M['jacket'], g.M['pants']
    m_boot, m_glove, m_skin = g.M['boot'], g.M['glove'], g.M['skin']
    # torso
    rings = []
    for z, *_ in TORSO:
        rx, rf, rb = g.torso_r(z)
        rings.append(R((0, 0, z), rx, rf=rf, rb=rb, w=g.torso_w(z), p=2.4 if z < 1.48 else 2.0))
    b.tube(rings[:4], m_pants, seg=16)        # pelvis (trousers)
    b.tube(rings[3:], m_jacket, seg=16)       # jacket up to the neck
    hs = S.get('head', 1.0)
    # head
    hr = [(1.555, 0.056, 0.060, 0.062, 0.0, {'Neck': 0.5, 'Head': 0.5}), (1.60, 0.066, 0.084, 0.072, 0.0, 'Head'),
          (1.64, 0.074, 0.097, 0.086, 0.0, 'Head'), (1.69, 0.078, 0.100, 0.095, 0.0, 'Head'),
          (1.74, 0.076, 0.094, 0.095, 0.0, 'Head'), (1.775, 0.060, 0.074, 0.078, 0.002, 'Head'),
          (1.797, 0.034, 0.040, 0.044, 0.004, 'Head')]
    b.tube([R((0, y, 1.59 + (z - 1.59) * hs), rx * hs, rf=rf * hs, rb=rb * hs, w=w) for z, rx, rf, rb, y, w in hr],
           m_skin, seg=14)
    for d in (1, -1):
        b.sphere((d * 0.031 * hs, -0.088 * hs, 1.59 + 0.112 * hs), 0.013 * hs, 'Head', g.M['dark'], useg=8, vseg=6)
    b.box((0, -0.100 * hs, 1.59 + 0.085 * hs), (0.024, 0.03, 0.045), 'Head', m_skin, bevel=0.008)
    for s, d in SIDES:
        sw, el, wr = g.sw, g.elbow, g.wrist
        z = 1.44
        ua = s + 'UpperArm'
        la = s + 'LowerArm'
        u, v = (0, 1, 0), (0, 0, 1)
        arm = [(sw - 0.085, 0.030, {s + 'Shoulder': 0.6, 'Chest': 0.4}),
               (sw - 0.045, 0.060, {s + 'Shoulder': 0.6, ua: 0.4}),
               (sw + 0.01, 0.066, {ua: 0.8, s + 'Shoulder': 0.2}), (sw + 0.07, 0.060, ua),
               ((sw + el) / 2 + 0.03, 0.053, ua), (el - 0.045, 0.047, {ua: 0.85, la: 0.15}),
               (el, 0.046, {ua: 0.5, la: 0.5}), (el + 0.045, 0.049, {ua: 0.15, la: 0.85}),
               ((el + wr) / 2, 0.045, la), (wr - 0.04, 0.037, la), (wr - 0.005, 0.034, {la: 0.6, s + 'Hand': 0.4})]
        b.tube([R((d * x, 0, z), r * bulk, w=w, u=u, v=v) for x, r, w in arm], m_jacket, seg=12)
        # glove cuff + hand (relaxed grip; no finger bones)
        h = s + 'Hand'
        b.tube([R((d * (wr - 0.03), 0, z), 0.040 * bulk, w=la, u=u, v=v),
                R((d * (wr + 0.01), 0, z), 0.040 * bulk, w=h, u=u, v=v)], m_glove, seg=10)
        hb = S.get('hand', 1.0)
        b.box((d * (wr + 0.045), 0, z - 0.004), (0.085 * hb, 0.082 * hb, 0.034 * hb), h, m_glove, bevel=0.012)
        b.box((d * (wr + 0.098), 0, z - 0.030), (0.030 * hb, 0.080 * hb, 0.072 * hb), h, m_glove, bevel=0.012)
        b.box((d * (wr + 0.074), 0, z - 0.063), (0.046 * hb, 0.078 * hb, 0.024 * hb), h, m_glove, bevel=0.010)
        b.box((d * (wr + 0.045), -0.050 * hb, z - 0.028), (0.030 * hb, 0.024 * hb, 0.056 * hb), h, m_glove, bevel=0.010)
        # leg
        hw = g.hw
        ul, ll, ft, to = s + 'UpperLeg', s + 'LowerLeg', s + 'Foot', s + 'Toes'
        leg = [(0.97, 0.090, 0.100, 0.0, {'Hips': 0.7, ul: 0.3}), (0.89, 0.094, 0.104, 0.0, {'Hips': 0.3, ul: 0.7}),
               (0.76, 0.083, 0.092, -0.002, ul), (0.60, 0.067, 0.073, -0.004, ul),
               (0.55, 0.061, 0.066, -0.005, {ul: 0.85, ll: 0.15}), (0.51, 0.059, 0.063, -0.006, {ul: 0.5, ll: 0.5}),
               (0.47, 0.058, 0.062, -0.004, {ul: 0.15, ll: 0.85}), (0.38, 0.059, 0.066, 0.004, ll),
               (0.24, 0.047, 0.052, 0.008, ll), (0.14, 0.043, 0.047, 0.010, ll)]
        b.tube([R((d * (hw + (0.012 if z > 0.85 else 0.0)), y, z), rx * bulk, ry * bulk, w=w) for z, rx, ry, y, w in leg],
               m_pants, seg=12)
        bs = S.get('boot', 1.0)
        boot = [(0.28, 0.052, 0.058, ll), (0.20, 0.050, 0.056, ll), (0.13, 0.047, 0.054, {ll: 0.7, ft: 0.3}),
                (0.09, 0.046, 0.058, {ll: 0.35, ft: 0.65}), (0.045, 0.044, 0.062, ft)]
        b.tube([R((d * hw, 0.010, z), rx * bs * bulk, ry * bs * bulk, w=w) for z, rx, ry, w in boot], m_boot, seg=12)
        foot = [(0.078, 0.036, 0.040, ft), (0.045, 0.046, 0.056, ft), (-0.02, 0.051, 0.052, ft),
                (-0.09, 0.054, 0.042, {ft: 0.7, to: 0.3}), (-0.13, 0.054, 0.037, {ft: 0.5, to: 0.5}),
                (-0.18, 0.051, 0.033, to), (-0.222, 0.040, 0.026, to)]
        b.tube([R((d * hw, y, rz * bs + 0.001), rx * bs, rz * bs, w=w, u=(1, 0, 0), v=(0, 0, 1), p=3.2)
                for y, rx, rz, w in foot], m_boot, seg=12)


def std_materials(g, pal):
    g.M['jacket'] = material(g.name + '_Jacket', pal['jacket'], 0.85)
    g.M['pants'] = material(g.name + '_Pants', pal['pants'], 0.88)
    g.M['gear'] = material(g.name + '_Gear', pal['gear'], 0.8)
    g.M['accent'] = material(g.name + '_Accent', pal['accent'], 0.7)
    g.M['pack'] = material(g.name + '_Pack', pal.get('pack', pal['gear']), 0.85)
    g.M['helmet'] = material(g.name + '_Headgear', pal['helmet'], 0.65)
    g.M['skin'] = material('CS_Skin_' + pal.get('skin_name', 'A'), pal.get('skin', '#A9805F'), 0.75)
    g.M['boot'] = material('CS_Boot', '#2A2D2F', 0.7)
    g.M['glove'] = material('CS_Glove', '#2F3437', 0.8)
    g.M['dark'] = material('CS_DarkRubber', '#17191B', 0.6)
    g.M['metal'] = material('CS_Gunmetal', '#2A2D30', 0.45, 0.8)
    g.M['strap'] = material('CS_Strap', '#3A3B36', 0.9)
    g.M['filter'] = material('CS_FilterGrey', '#5A5F5E', 0.6, 0.3)


# ----------------------------------------------------------------------------- shared gear pieces
def respirator(g, hs=1.0, filters=2):
    h, dk, fl = g.head, g.M['dark'], g.M['filter']
    zc = 1.59 + 0.045 * hs
    h.box((0, -0.098 * hs, zc), (0.092, 0.062, 0.074), 'Head', dk, bevel=0.02)
    h.cyl((0, -0.12 * hs, zc - 0.005), (0, -0.150 * hs, zc - 0.012), 0.022, 'Head', fl, seg=10)
    for d in ((1, -1) if filters == 2 else (1,)):
        h.cyl((d * 0.035, -0.105 * hs, zc - 0.012), (d * 0.092, -0.118 * hs, zc - 0.03), 0.030, 'Head', fl, seg=10)
    # straps round the head
    h.tube([R((0, 0.004, zc + 0.012), 0.081 * hs, rf=0.097 * hs, rb=0.092 * hs, w='Head'),
            R((0, 0.004, zc + 0.030), 0.082 * hs, rf=0.098 * hs, rb=0.094 * hs, w='Head')], g.M['strap'], seg=14)


def belt(g, mat=None, pouches=True):
    ge = g.gear
    g.shell(ge, [(0.985, 0.0), (1.035, 0.0)], 0.014, mat or g.M['strap'])
    rf = g.torso_r(1.01)[1]
    ge.box((0, -(rf + 0.02), 1.01), (0.05, 0.016, 0.04), 'Hips', g.M['metal'], bevel=0.005)
    if pouches:
        rx = g.torso_r(0.97)[0]
        for d in (1, -1):
            ge.box((d * (rx + 0.022), 0.01, 0.955), (0.05, 0.11, 0.115), 'Hips', g.M['gear'], bevel=0.014)
        ge.box((0, g.torso_r(0.97)[2] + 0.028, 0.955), (0.16, 0.055, 0.10), 'Hips', g.M['gear'], bevel=0.016)


def knee_pads(g, mat=None, size=1.0):
    for s, d in SIDES:
        g.gear.box((d * g.hw, -0.060 * g.bulk, 0.495), (0.105 * size, 0.04, 0.125 * size), s + 'LowerLeg',
                   mat or g.M['gear'], bevel=0.016, rot=(8, 0, 0))


def build_rifle(g, long=True):
    """Weapon space: forward = -Y, up = +Z, origin at the pistol grip."""
    w, mt, dk = g.weapon, g.M['metal'], g.M['dark']
    w.box((0, 0.012, -0.012), (0.030, 0.042, 0.095), None, dk, rot=(-14, 0, 0), bevel=0.008)       # grip
    w.box((0, -0.065, 0.066), (0.040, 0.27, 0.066), None, mt, bevel=0.008)                          # receiver
    w.box((0, -0.105, -0.028), (0.026, 0.052, 0.135), None, mt, rot=(10, 0, 0), bevel=0.006)        # magazine
    w.box((0, -0.305, 0.070), (0.046, 0.21, 0.052), None, dk, bevel=0.010)                          # handguard
    w.cyl((0, -0.40, 0.076), (0, -0.60, 0.076), 0.011, None, mt, seg=8)                             # barrel
    w.cyl((0, -0.58, 0.076), (0, -0.635, 0.076), 0.017, None, mt, seg=8)                            # muzzle device
    w.box((0, 0.165, 0.052), (0.034, 0.21, 0.060), None, dk, bevel=0.010)                           # stock
    w.box((0, 0.275, 0.040), (0.038, 0.03, 0.105), None, dk, bevel=0.008)                           # butt pad
    w.box((0, -0.08, 0.108), (0.024, 0.20, 0.014), None, mt, bevel=0.003)                           # rail
    w.cyl((0, -0.03, 0.136), (0, -0.14, 0.136), 0.019, None, dk, seg=8)                             # optic
    w.box((0, -0.385, 0.108), (0.012, 0.016, 0.036), None, mt, bevel=0.003)                         # front sight
    return dict(support=Vector((0, -0.30, 0.036)), muzzle=Vector((0, -0.64, 0.076)))


# ----------------------------------------------------------------------------- NAVY
NAVY = dict(name='Navy', H=1.80, sw=0.19, hw=0.095, bulk=1.0, tw=1.0, td=1.0,
            pal=dict(jacket='#344754', pants='#3A4A57', gear='#737B7B', accent='#86A4B3', pack='#5C6668',
                     helmet='#3C5261', skin='#A9805F'))


def gear_navy(g):
    ge, hd, pk = g.gear, g.head, g.pack
    M = g.M
    # plate carrier / tactical vest
    g.shell(ge, [(1.085, -0.012), (1.11, 0.0), (1.22, 0.002), (1.34, 0.002), (1.425, 0.0), (1.46, -0.014)], 0.024, M['gear'])
    rf = lambda z: g.torso_r(z)[1]
    rb = lambda z: g.torso_r(z)[2]
    ge.box((0, -(rf(1.33) + 0.036), 1.325), (0.25, 0.034, 0.20), 'Chest', M['gear'], bevel=0.014)
    for x in (-0.088, 0.0, 0.088):  # magazine pouches
        ge.box((x, -(rf(1.16) + 0.046), 1.165), (0.072, 0.048, 0.118), 'Spine', M['gear'], bevel=0.010)
        ge.box((x, -(rf(1.16) + 0.050), 1.215), (0.076, 0.052, 0.022), 'Spine', M['strap'], bevel=0.006)
    ge.box((0.062, -(rf(1.36) + 0.058), 1.36), (0.095, 0.018, 0.07), 'Chest', M['strap'], bevel=0.006)   # admin panel
    ge.box((-0.105, -(rf(1.39) + 0.050), 1.395), (0.048, 0.036, 0.105), 'Chest', M['dark'], bevel=0.008)  # sealed radio
    ge.cyl((-0.118, -(rf(1.39) + 0.050), 1.44), (-0.118, -(rf(1.39) + 0.050), 1.56), 0.005, 'Chest', M['dark'], seg=6)
    ge.box((0, rb(1.33) + 0.030, 1.33), (0.25, 0.026, 0.22), 'Chest', M['gear'], bevel=0.012)            # back plate
    for d in (1, -1):  # shoulder straps + modest pads + sleeve patches
        ge.box((d * 0.105, 0.0, 1.492), (0.062, 0.235, 0.022), 'Chest', M['gear'], bevel=0.008, rot=(0, d * 14, 0))
        ge.box((d * (g.sw + 0.02), 0, 1.502), (0.125, 0.135, 0.032), ('Left' if d > 0 else 'Right') + 'Shoulder',
               M['helmet'], bevel=0.012, rot=(0, d * 10, 0))
        ge.box((d * (g.sw + 0.13), 0, 1.44 + 0.054 * g.bulk), (0.075, 0.062, 0.010),
               ('Left' if d > 0 else 'Right') + 'UpperArm', M['accent'], bevel=0.003)
    # wet-weather collar strip (pale blue, visible from above)
    ge.tube([R((0, 0.004, 1.478), 0.100, rf=0.092, rb=0.098, w='Chest'),
             R((0, 0.004, 1.535), 0.082, rf=0.078, rb=0.086, w={'Chest': 0.5, 'Neck': 0.5})], M['accent'], seg=14)
    belt(g)
    knee_pads(g)
    ge.box((g.hw + 0.082, -0.01, 0.775), (0.036, 0.095, 0.135), 'LeftUpperLeg', M['gear'], bevel=0.010)   # thigh utility
    ge.box((-(g.hw + 0.080), -0.01, 0.80), (0.022, 0.04, 0.19), 'RightUpperLeg', M['dark'], bevel=0.006)  # knife sheath
    # helmet with light + comms
    hd.sphere((0, 0.006, 1.722), (0.099, 0.116, 0.098), 'Head', M['helmet'], useg=16, vseg=10,
              cut=lambda co: co.z < -0.22 + (0.18 if co.y < -0.55 else 0.0))
    hd.box((0, 0.012, 1.817), (0.045, 0.15, 0.008), 'Head', M['accent'], bevel=0.003)                      # top ID strip
    for d in (1, -1):
        hd.box((d * 0.097, 0.012, 1.722), (0.014, 0.10, 0.022), 'Head', M['dark'], bevel=0.004)           # rails
        hd.box((d * 0.092, 0.02, 1.672), (0.034, 0.062, 0.072), 'Head', M['dark'], bevel=0.014)           # comms cups
    hd.box((0.062, -0.088, 1.752), (0.036, 0.05, 0.034), 'Head', M['dark'], bevel=0.008)                   # helmet light
    hd.box((0, -0.108, 1.752), (0.04, 0.022, 0.034), 'Head', M['dark'], bevel=0.006)                       # mount shroud
    respirator(g)
    # medium rectangular backpack (local origin = Back_Pack socket)
    pk.box((0, 0.088, 0.0), (0.30, 0.17, 0.42), None, M['pack'], bevel=0.03)
    pk.box((0, 0.083, 0.212), (0.31, 0.19, 0.055), None, M['pack'], bevel=0.02)
    pk.box((0, 0.083, 0.243), (0.21, 0.125, 0.012), None, M['accent'], bevel=0.004)
    pk.box((0, 0.176, 0.03), (0.20, 0.014, 0.25), None, M['accent'], bevel=0.005)
    for d in (1, -1):
        pk.box((d * 0.172, 0.088, -0.06), (0.062, 0.125, 0.23), None, M['gear'], bevel=0.018)
    pk.box((0, 0.178, -0.135), (0.22, 0.045, 0.115), None, M['gear'], bevel=0.016)
    pk.cyl((0.115, 0.03, 0.20), (0.115, 0.03, 0.56), 0.006, None, M['dark'], seg=6)                        # antenna
    pk.cyl((0.115, 0.03, 0.20), (0.115, 0.03, 0.26), 0.014, None, M['dark'], seg=8)
    pk.cyl((-0.207, 0.088, 0.115), (-0.165, 0.088, 0.115), 0.062, None, M['strap'], seg=12)               # rope coil
    pk.cyl((0.205, 0.088, 0.075), (0.205, 0.088, 0.20), 0.034, None, M['filter'], seg=10)                 # water filter
    info = build_rifle(g)
    return dict(pack_pos=(0, rb(1.33) + 0.045, 1.33), weapon=info, weapon_name='Rifle')


NAVY['gear_fn'] = gear_navy


# ----------------------------------------------------------------------------- HEAVY
HEAVY = dict(name='Heavy', H=1.90, sw=0.238, hw=0.112, bulk=1.28, tw=1.20, td=1.24, hand=1.15, boot=1.12,
             pal=dict(jacket='#303A32', pants='#3A4439', gear='#82775F', accent='#B86A3B', pack='#4C5546',
                      helmet='#48523F', skin='#9C7658'))


def full_mask(g):
    h, dk, fl = g.head, g.M['dark'], g.M['filter']
    h.box((0, -0.090, 1.668), (0.135, 0.07, 0.15), 'Head', dk, bevel=0.03)
    for d in (1, -1):
        h.cyl((d * 0.034, -0.118, 1.698), (d * 0.036, -0.134, 1.698), 0.026, 'Head', g.M['lens'], seg=10)
        h.cyl((d * 0.040, -0.112, 1.628), (d * 0.105, -0.120, 1.600), 0.036, 'Head', fl, seg=10)
    h.cyl((0, -0.118, 1.630), (0, -0.165, 1.618), 0.030, 'Head', fl, seg=10)


def build_shotgun(g):
    w, mt, dk = g.weapon, g.M['metal'], g.M['dark']
    wd = g.mat('wood', '#5B4634', 0.7, shared=True)
    w.box((0, 0.012, -0.014), (0.034, 0.046, 0.10), None, wd, rot=(-16, 0, 0), bevel=0.01)          # grip
    w.box((0, -0.07, 0.066), (0.050, 0.26, 0.078), None, mt, bevel=0.01)                            # receiver
    w.cyl((0, -0.19, 0.086), (0, -0.56, 0.086), 0.017, None, mt, seg=10)                            # barrel
    w.cyl((0, -0.19, 0.048), (0, -0.50, 0.048), 0.015, None, mt, seg=10)                            # magazine tube
    w.box((0, -0.32, 0.050), (0.056, 0.15, 0.056), None, wd, bevel=0.014)                           # pump
    w.box((0, 0.15, 0.048), (0.040, 0.17, 0.070), None, wd, bevel=0.012, rot=(-6, 0, 0))            # short stock
    w.box((0, 0.24, 0.036), (0.044, 0.03, 0.11), None, dk, bevel=0.008)
    w.box((0, -0.545, 0.108), (0.012, 0.014, 0.022), None, mt, bevel=0.003)
    return dict(support=Vector((0, -0.32, 0.020)), muzzle=Vector((0, -0.57, 0.086)))


def gear_heavy(g):
    ge, hd, pk, M = g.gear, g.head, g.pack, g.M
    vest = g.mat('vest', '#3B463A', 0.9)
    g.mat('lens', '#7C8A7C', 0.25, 0.2, shared=True)
    rf = lambda z: g.torso_r(z)[1]
    rb = lambda z: g.torso_r(z)[2]
    rx = lambda z: g.torso_r(z)[0]
    bk = g.bulk
    # heavy ballistic vest + plates
    g.shell(ge, [(1.03, -0.02), (1.06, 0.0), (1.22, 0.004), (1.36, 0.004), (1.44, 0.0), (1.475, -0.024)], 0.042, vest, p=3.0)
    ge.box((0, -(rf(1.33) + 0.058), 1.335), (0.33, 0.045, 0.24), 'Chest', M['gear'], bevel=0.018)
    ge.box((0, -(rf(1.14) + 0.056), 1.135), (0.28, 0.04, 0.14), 'Spine', M['gear'], bevel=0.016)
    ge.box((0, rb(1.33) + 0.050, 1.33), (0.34, 0.04, 0.28), 'Chest', M['gear'], bevel=0.016)
    for x in (-0.10, 0.10):  # shell pouches
        ge.box((x, -(rf(1.14) + 0.092), 1.12), (0.085, 0.04, 0.085), 'Spine', vest, bevel=0.010)
    ge.box((0.0, -(rf(1.33) + 0.084), 1.37), (0.11, 0.018, 0.05), 'Chest', M['strap'], bevel=0.006)
    for s, d in SIDES:
        # broad squared shoulder guards (two stacked plates) + orange ID on the left
        ge.box((d * (g.sw + 0.015), 0, 1.522), (0.215, 0.215, 0.050), s + 'Shoulder', M['gear'], bevel=0.018, rot=(0, d * 8, 0))
        ge.box((d * (g.sw + 0.105), 0, 1.47), (0.11, 0.20, 0.10), s + 'UpperArm', M['gear'], bevel=0.02, rot=(0, d * 20, 0))
        ge.box((d * (g.sw + 0.015), 0, 1.551), (0.12, 0.215, 0.012), s + 'Shoulder', M['accent'] if d > 0 else vest, bevel=0.004,
               rot=(0, d * 8, 0))
        # forearm guards, cross straps
        ge.box((d * (g.elbow + g.wrist) / 2, 0, 1.44 + 0.048 * bk), (0.19, 0.105, 0.03), s + 'LowerArm', M['gear'], bevel=0.012)
        ge.box((d * (g.elbow + g.wrist) / 2, -0.048 * bk, 1.44), (0.19, 0.03, 0.09), s + 'LowerArm', M['gear'], bevel=0.012)
        ge.box((d * 0.115, 0.0, 1.50), (0.075, 0.26, 0.026), 'Chest', M['strap'], bevel=0.008, rot=(0, d * 16, 0))
        # thigh / knee / shin armour
        ge.box((d * (g.hw + 0.012), -0.092 * bk, 0.74), (0.135, 0.035, 0.22), s + 'UpperLeg', M['gear'], bevel=0.016, rot=(4, 0, 0))
        ge.box((d * (g.hw + 0.088 * bk), 0.0, 0.78), (0.035, 0.12, 0.20), s + 'UpperLeg', vest, bevel=0.014)
        ge.box((d * g.hw, -0.064 * bk, 0.33), (0.105, 0.035, 0.20), s + 'LowerLeg', M['gear'], bevel=0.014, rot=(-4, 0, 0))
        ge.box((d * g.hw, -0.085, 0.06), (0.115, 0.07, 0.045), s + 'Toes', M['metal'], bevel=0.016)   # steel toe caps
    knee_pads(g, size=1.25)
    # padded load belt
    g.shell(ge, [(0.965, 0.0), (1.045, 0.0)], 0.024, vest, p=3.0)
    ge.box((0, -(rf(1.0) + 0.032), 1.005), (0.07, 0.02, 0.055), 'Hips', M['metal'], bevel=0.006)
    for d in (1, -1):
        ge.box((d * (rx(0.97) + 0.04), 0.0, 0.95), (0.065, 0.14, 0.13), 'Hips', M['gear'], bevel=0.016)
    ge.box((0, rb(0.97) + 0.045, 0.955), (0.24, 0.07, 0.11), 'Hips', vest, bevel=0.02)
    for i in range(3):
        ge.cyl((0.20 + i * 0.0, -0.06 + i * 0.03, 0.905), (0.20, -0.06 + i * 0.03, 0.87), 0.010, 'Hips', M['metal'], seg=6)
    # helmet with broad top plate + full respirator
    hd.sphere((0, 0.008, 1.725), (0.106, 0.124, 0.104), 'Head', M['helmet'], useg=16, vseg=10, cut=lambda co: co.z < -0.30)
    hd.box((0, 0.012, 1.822), (0.13, 0.17, 0.018), 'Head', M['helmet'], bevel=0.008)
    hd.box((0, 0.012, 1.834), (0.05, 0.17, 0.008), 'Head', M['accent'], bevel=0.003)
    hd.box((0, 0.105, 1.69), (0.17, 0.05, 0.10), 'Head', vest, bevel=0.02)   # neck guard
    full_mask(g)
    # wide rigid back-frame pack with twin uprights + orange crossbar
    pk.box((0, 0.115, -0.02), (0.44, 0.22, 0.50), None, M['pack'], bevel=0.03)
    pk.box((0, 0.232, -0.06), (0.30, 0.03, 0.30), None, M['gear'], bevel=0.012)
    for d in (1, -1):
        pk.box((d * 0.19, 0.03, 0.06), (0.035, 0.035, 0.84), None, M['metal'], bevel=0.008)
        pk.box((d * 0.245, 0.115, -0.10), (0.07, 0.15, 0.24), None, M['gear'], bevel=0.02)
    pk.box((0, 0.03, 0.455), (0.44, 0.04, 0.04), None, M['accent'], bevel=0.008)
    pk.box((0, 0.03, 0.22), (0.40, 0.03, 0.03), None, M['metal'], bevel=0.006)
    pk.cyl((-0.21, 0.115, 0.285), (0.21, 0.115, 0.285), 0.075, None, vest, seg=12)                    # roll
    for x in (-0.12, 0.12):
        pk.box((x, 0.115, 0.285), (0.03, 0.16, 0.16), None, M['strap'], bevel=0.006)
    info = build_shotgun(g)
    return dict(pack_pos=(0, rb(1.33) + 0.072, 1.31), weapon=info, weapon_name='Shotgun',
                back_weapon=dict(pos=(-0.30, rb(1.33) + 0.20, 1.28), fwd=(0.15, 0.0, 0.98), up=(-1, 0, 0)))


HEAVY['gear_fn'] = gear_heavy

# ----------------------------------------------------------------------------- SCOUT
SCOUT = dict(name='Scout', H=1.69, sw=0.166, hw=0.086, bulk=0.86, tw=0.86, td=0.90, head=1.03, hand=0.94, boot=0.93,
             pal=dict(jacket='#2E3539', pants='#353B3F', gear='#594C50', accent='#B48F55', pack='#4A4144',
                      helmet='#594C50', skin='#A07C63'))


def build_pistol(g):
    w, mt, dk = g.weapon, g.M['metal'], g.M['dark']
    w.box((0, 0.010, -0.012), (0.028, 0.040, 0.092), None, dk, rot=(-14, 0, 0), bevel=0.008)
    w.box((0, -0.055, 0.050), (0.028, 0.175, 0.036), None, mt, bevel=0.006)
    w.box((0, -0.045, 0.024), (0.024, 0.10, 0.022), None, dk, bevel=0.005)
    w.box((0, -0.135, 0.070), (0.008, 0.01, 0.01), None, mt, bevel=0.0)
    return dict(support=Vector((0, 0.0, -0.045)), muzzle=Vector((0, -0.145, 0.052)))


def gear_scout(g):
    ge, hd, pk, M = g.gear, g.head, g.pack, g.M
    shell = M['gear']            # faded plum-grey overshell / hood
    rf = lambda z: g.torso_r(z)[1]
    rb = lambda z: g.torso_r(z)[2]
    rx = lambda z: g.torso_r(z)[0]
    tw = lambda co: G.torso_w(co.z)
    # hooded overshell: cowl over shoulders, ragged hem at the waist
    g.shell(ge, [(1.27, -0.012), (1.31, 0.0), (1.40, 0.004), (1.455, 0.008), (1.50, 0.016), (1.535, 0.014)], 0.016, shell)
    g.shell(ge, [(0.90, 0.004), (0.94, 0.0), (1.06, -0.004), (1.10, -0.012)], 0.012, shell)      # hip wrap / jacket hem
    hd.sphere((0, 0.016, 1.708), (0.104, 0.124, 0.118), 'Head', shell, useg=16, vseg=10,
              cut=lambda co: (co.y < -0.50 and -0.62 < co.z < 0.50) or co.z < -0.80)
    hd.cyl((0, 0.075, 1.795), (0, 0.150, 1.812), 0.050, 'Head', shell, r2=0.012, seg=10)            # short hood tip
    hd.box((0, -0.096, 1.775), (0.150, 0.024, 0.022), 'Head', M['accent'], bevel=0.008)             # amber hood edge
    for d in (1, -1):
        hd.box((d * 0.086, -0.088, 1.69), (0.022, 0.024, 0.16), 'Head', M['accent'], bevel=0.008, rot=(0, d * -6, 0))
    hd.box((0, -0.090, 1.636), (0.112, 0.060, 0.086), 'Head', M['dark'], bevel=0.028)               # face wrap
    hd.tube([R((0, 0.0, 1.535), 0.092, rf=0.098, rb=0.092, w={'Neck': 0.6, 'Chest': 0.4}),
             R((0, 0.0, 1.585), 0.080, rf=0.092, rb=0.084, w={'Neck': 0.6, 'Head': 0.4})], g.mat('scarf', '#6A4A43', 0.9), seg=12)
    hd.box((0.0, -0.118, 1.758), (0.040, 0.03, 0.034), 'Head', M['dark'], bevel=0.008)              # headlamp
    hd.cyl((0.0, -0.130, 1.758), (0.0, -0.140, 1.758), 0.013, 'Head', g.mat('lens', '#7C8A7C', 0.25, 0.2, shared=True), seg=8)
    # one asymmetric shoulder pad (left) + small chest inserts
    ge.box((g.sw + 0.02, 0, 1.512), (0.13, 0.14, 0.036), 'LeftShoulder', M['pack'], bevel=0.014, rot=(0, 12, 0))
    ge.box((g.sw + 0.02, 0, 1.533), (0.03, 0.14, 0.008), 'LeftShoulder', M['accent'], bevel=0.003, rot=(0, 12, 0))
    for d in (1, -1):
        ge.box((d * 0.058, -(rf(1.33) + 0.02), 1.33), (0.085, 0.022, 0.10), 'Chest', M['pack'], bevel=0.010)
    # crossbody satchel: strap from right shoulder to left hip
    ge.box((0.02, -(rf(1.25) + 0.016), 1.235), (0.05, 0.014, 0.60), tw, M['strap'], bevel=0.004, rot=(0, 32, 0))
    ge.box((0.02, rb(1.25) + 0.016, 1.235), (0.05, 0.014, 0.60), tw, M['strap'], bevel=0.004, rot=(0, 32, 0))
    ge.box((rx(0.95) + 0.058, -0.01, 0.915), (0.10, 0.22, 0.20), 'Hips', M['pack'], bevel=0.03)
    ge.box((rx(0.95) + 0.070, -0.01, 0.975), (0.105, 0.225, 0.10), 'Hips', M['accent'], bevel=0.02)  # amber flap
    ge.box((rx(0.95) + 0.126, -0.01, 0.94), (0.012, 0.04, 0.04), 'Hips', M['metal'], bevel=0.004)
    # slim belt, tool wallet, cord, holster, light knee guards
    g.shell(ge, [(0.992, 0.0), (1.028, 0.0)], 0.010, M['strap'])
    ge.box((-0.06, -(rf(1.0) + 0.022), 0.985), (0.10, 0.014, 0.075), 'Hips', M['pack'], bevel=0.005)  # lock-pick wallet
    ge.cyl((-(rx(0.96) + 0.01), 0.07, 0.95), (-(rx(0.96) + 0.04), 0.07, 0.95), 0.045, 'Hips', M['strap'], seg=10)
    ge.box((-(g.hw + 0.078 * g.bulk + 0.01), -0.01, 0.80), (0.03, 0.07, 0.15), 'RightUpperLeg', M['pack'], bevel=0.01)
    ge.box((-(g.hw), -0.02, 0.86), (0.19, 0.19, 0.022), 'RightUpperLeg', M['strap'], bevel=0.008)
    knee_pads(g, mat=M['pack'], size=0.82)
    for s, d in SIDES:  # wrapped forearms / shin wraps
        ge.tube([R((d * (g.wrist - 0.11), 0, 1.44), 0.043 * g.bulk, w=s + 'LowerArm', u=(0, 1, 0), v=(0, 0, 1)),
                 R((d * (g.wrist - 0.03), 0, 1.44), 0.040 * g.bulk, w=s + 'LowerArm', u=(0, 1, 0), v=(0, 0, 1))], shell, seg=10)
        ge.tube([R((d * g.hw, 0.008, 0.36), 0.061 * g.bulk, 0.068 * g.bulk, w=s + 'LowerLeg'),
                 R((d * g.hw, 0.008, 0.27), 0.055 * g.bulk, 0.060 * g.bulk, w=s + 'LowerLeg')], shell, seg=10)
    # lightweight pack + cloth roll
    pk.box((0, 0.062, -0.03), (0.22, 0.12, 0.27), None, M['pack'], bevel=0.035)
    pk.cyl((-0.13, 0.062, 0.135), (0.13, 0.062, 0.135), 0.048, None, shell, seg=10)
    for x in (-0.07, 0.07):
        pk.box((x, 0.062, 0.135), (0.02, 0.105, 0.105), None, M['strap'], bevel=0.004)
    pk.box((0, 0.126, -0.02), (0.05, 0.012, 0.05), None, M['accent'], bevel=0.004)
    pk.box((0.0, 0.128, -0.10), (0.15, 0.03, 0.09), None, shell, bevel=0.012)
    info = build_pistol(g)
    return dict(pack_pos=(0, rb(1.30) + 0.030, 1.28), weapon=info, weapon_name='Pistol')


SCOUT['gear_fn'] = gear_scout

# ----------------------------------------------------------------------------- MEDIC
MEDIC = dict(name='Medic', H=1.74, sw=0.172, hw=0.090, bulk=0.90, tw=0.90, td=0.93, head=1.02, hand=0.96,
             pal=dict(jacket='#829186', pants='#75847A', gear='#333D3C', accent='#6BADA5', pack='#B4BAAE',
                      helmet='#D3D8CC', skin='#A9805F'))


def build_stun_device(g):
    w, dk, mt = g.weapon, g.M['dark'], g.M['metal']
    body = g.mat('device', '#4A5552', 0.6, 0.2)
    coil = g.mat('coil', '#9A6B3C', 0.4, 0.8, shared=True)
    w.box((0, 0.010, -0.012), (0.032, 0.044, 0.095), None, dk, rot=(-12, 0, 0), bevel=0.01)          # insulated grip
    w.box((0, -0.045, 0.058), (0.050, 0.20, 0.062), None, body, bevel=0.014)
    w.box((0, 0.075, 0.050), (0.040, 0.06, 0.080), None, dk, bevel=0.012)                            # battery
    w.cyl((0, -0.14, 0.058), (0, -0.25, 0.058), 0.022, None, mt, seg=10)
    for y in (-0.16, -0.19, -0.22):                                                                  # coil rings
        w.cyl((0, y, 0.058), (0, y - 0.014, 0.058), 0.034, None, coil, seg=10)
    for d in (1, -1):                                                                                # prongs
        w.cyl((d * 0.016, -0.25, 0.058), (d * 0.020, -0.31, 0.058), 0.005, None, mt, seg=6)
    w.box((0, -0.02, 0.094), (0.016, 0.03, 0.010), None, g.mat('led', '#E8A23A', 0.4, 0.0, shared=True, emit='#E8A23A'), bevel=0.002)
    return dict(support=Vector((0, -0.12, 0.022)), muzzle=Vector((0, -0.31, 0.058)))


def gear_medic(g):
    ge, hd, pk, M = g.gear, g.head, g.pack, g.M
    white = M['helmet']
    rf = lambda z: g.torso_r(z)[1]
    rb = lambda z: g.torso_r(z)[2]
    rx = lambda z: g.torso_r(z)[0]
    # light protective vest (charcoal) with a pale chest panel + teal geometric icon
    g.shell(ge, [(1.115, -0.010), (1.14, 0.0), (1.24, 0.002), (1.36, 0.002), (1.43, 0.0), (1.46, -0.012)], 0.016, M['gear'])
    ge.box((-0.055, -(rf(1.35) + 0.022), 1.355), (0.10, 0.016, 0.10), 'Chest', white, bevel=0.006)
    ge.box((-0.055, -(rf(1.35) + 0.032), 1.355), (0.05, 0.008, 0.05), 'Chest', M['accent'], bevel=0.003, rot=(0, 45, 0))
    ge.box((0.07, -(rf(1.33) + 0.028), 1.33), (0.07, 0.03, 0.10), 'Chest', M['gear'], bevel=0.010)
    for x in (-0.07, 0.03):
        ge.box((x, -(rf(1.17) + 0.030), 1.17), (0.075, 0.034, 0.085), 'Spine', white, bevel=0.010)    # medical pouches
    ge.box((0.105, -(rf(1.17) + 0.026), 1.17), (0.045, 0.028, 0.10), 'Spine', M['accent'], bevel=0.008)  # tourniquet case
    for s, d in SIDES:
        ua, la = s + 'UpperArm', s + 'LowerArm'
        u, v = (0, 1, 0), (0, 0, 1)
        band = M['accent'] if d > 0 else white      # sleeve marking: teal left / pale right
        ge.tube([R((d * (g.sw + 0.085), 0, 1.44), 0.060 * g.bulk, w=ua, u=u, v=v),
                 R((d * (g.sw + 0.135), 0, 1.44), 0.058 * g.bulk, w=ua, u=u, v=v)], band, seg=12)
        ge.tube([R((d * (g.wrist - 0.15), 0, 1.44), 0.051 * g.bulk, w=la, u=u, v=v),             # gauntlets
                 R((d * (g.wrist - 0.03), 0, 1.44), 0.044 * g.bulk, w=la, u=u, v=v)], M['gear'], seg=12)
        ge.box((d * (g.sw + 0.012), 0, 1.502), (0.10, 0.12, 0.028), s + 'Shoulder', M['gear'], bevel=0.012, rot=(0, d * 12, 0))
        ge.box((d * 0.098, 0.0, 1.484), (0.05, 0.22, 0.02), 'Chest', M['gear'], bevel=0.007, rot=(0, d * 14, 0))
    belt(g, pouches=False)
    for i, x in enumerate((-0.10, -0.055, 0.075, 0.12)):                                              # bandage rolls / containers
        ge.cyl((x, -(rf(0.99) + 0.035), 0.935), (x, -(rf(0.99) + 0.035), 1.0), 0.020, 'Hips', white if i % 2 == 0 else M['accent'], seg=8)
    ge.box((rx(0.97) + 0.03, 0.0, 0.95), (0.05, 0.10, 0.11), 'Hips', white, bevel=0.014)              # first aid kit
    ge.box((rx(0.97) + 0.058, 0.0, 0.955), (0.008, 0.04, 0.04), 'Hips', M['accent'], bevel=0.003, rot=(45, 0, 0))
    ge.box((-(rx(0.97) + 0.03), 0.02, 0.95), (0.045, 0.07, 0.12), 'Hips', M['gear'], bevel=0.012)     # chemical detector
    knee_pads(g, mat=M['gear'], size=0.95)
    ge.box((-(g.hw + 0.072), -0.01, 0.80), (0.03, 0.08, 0.13), 'RightUpperLeg', M['gear'], bevel=0.01)
    # light helmet with raised top strap + teal band
    hd.sphere((0, 0.006, 1.722), (0.097, 0.114, 0.097), 'Head', white, useg=16, vseg=10,
              cut=lambda co: co.z < -0.20 + (0.18 if co.y < -0.55 else 0.0))
    hd.box((0, 0.008, 1.816), (0.035, 0.19, 0.016), 'Head', white, bevel=0.006)
    hd.tube([R((0, 0.006, 1.722), 0.100, rf=0.117, rb=0.117, w='Head'),
             R((0, 0.006, 1.748), 0.095, rf=0.111, rb=0.111, w='Head')], M['accent'], seg=16)
    respirator(g, 1.02, filters=1)
    # tall rounded-rectangular medical pack, sits high; pale top + teal band
    pk.box((0, 0.085, 0.125), (0.27, 0.16, 0.66), None, M['pack'], bevel=0.055)
    pk.box((0, 0.085, 0.452), (0.225, 0.125, 0.022), None, white, bevel=0.008)
    pk.box((0, 0.085, 0.35), (0.278, 0.168, 0.05), None, M['accent'], bevel=0.02)
    pk.box((0, 0.168, 0.05), (0.15, 0.012, 0.15), None, white, bevel=0.006)
    pk.box((0, 0.176, 0.05), (0.075, 0.008, 0.075), None, M['accent'], bevel=0.003, rot=(0, 45, 0))
    for d in (1, -1):
        pk.box((d * 0.118, 0.012, 0.05), (0.022, 0.022, 0.60), None, M['gear'], bevel=0.006)           # quick-release frame
    for y in (0.05, 0.125):                                                                             # side canisters
        pk.cyl((0.165, y, -0.14), (0.165, y, 0.06), 0.032, None, white, seg=10)
        pk.cyl((0.165, y, 0.06), (0.165, y, 0.085), 0.026, None, M['accent'], seg=10)
    pk.box((-0.155, 0.085, -0.08), (0.05, 0.11, 0.18), None, M['gear'], bevel=0.016)
    pk.box((0, 0.172, -0.16), (0.19, 0.035, 0.11), None, M['gear'], bevel=0.014)
    info = build_stun_device(g)
    return dict(pack_pos=(0, rb(1.33) + 0.030, 1.33), weapon=info, weapon_name='StunDevice')


MEDIC['gear_fn'] = gear_medic


# ----------------------------------------------------------------------------- assembly
def build_survivor(S):
    g = G(S)
    k, name = g.k, g.name
    scene = C.get_scene('CS_' + name)
    std_materials(g, S['pal'])
    build_body(g)
    info = S['gear_fn'](g)
    J = {b: (Vector(h) * k, Vector(t) * k, p) for b, (h, t, p) in g.J.items()}
    arm = C.build_rig(name + '_Rig', J, scene)
    coll = scene.collection

    def finish(builder, nm, skinned=True):
        if len(builder.bm.verts) == 0:
            builder.bm.free()
            return None
        for v in builder.bm.verts:
            v.co *= k
        return builder.to_object(name + '_' + nm, coll, arm if skinned else None)

    objs = dict(body=finish(g.body, 'Body'), gear=finish(g.gear, 'Gear'), head=finish(g.head, 'Headgear'),
                pack=finish(g.pack, 'Backpack', False), weapon=finish(g.weapon, info['weapon_name'], False))
    z = 1.44
    sk = {}
    wr = g.wrist
    sk['RightHand_Weapon'] = C.socket('RightHand_Weapon', arm, 'RightHand',
                                      Vector((-(wr + PALM.x), 0, z + PALM.z)) * k, WEAPON_SOCKET_R, scene)
    sk['LeftHand_Support'] = C.socket('LeftHand_Support', arm, 'LeftHand', Vector((wr + PALM.x, 0, z + PALM.z)) * k,
                                      Matrix(((0, -1, 0), (0, 0, -1), (1, 0, 0))), scene)
    sk['Back_Pack'] = C.socket('Back_Pack', arm, 'Chest', Vector(info['pack_pos']) * k, None, scene)
    rb = g.torso_r(1.33)[2]
    bw = info.get('back_weapon', dict(pos=(-0.06, rb + 0.27, 1.30), fwd=(0.42, 0.0, 0.91), up=(0, 1, 0)))
    f = Vector(bw['fwd']).normalized()
    u = Vector(bw['up'])
    u = (u - f * u.dot(f)).normalized()
    x = (-f).cross(u)
    sk['Back_Weapon'] = C.socket('Back_Weapon', arm, 'Chest', Vector(bw['pos']) * k,
                                 Matrix((x, -f, u)).transposed(), scene)
    sk['Hip_Sidearm'] = C.socket('Hip_Sidearm', arm, 'RightUpperLeg', Vector((-(g.hw * 1.0 + 0.085 * g.bulk + 0.03), -0.01, 0.84)) * k,
                                 Matrix(((1, 0, 0), (0, 0, -1), (0, 1, 0))), scene)
    sk['Chest_Tool'] = C.socket('Chest_Tool', arm, 'Chest', Vector((0.07, -(g.torso_r(1.36)[1] + 0.075), 1.36)) * k, None, scene)
    sk['Cargo_Front'] = C.socket('Cargo_Front', arm, None, Vector((0, -0.45, 1.10)) * k, None, scene, 0.1)
    sk['Cargo_Back'] = C.socket('Cargo_Back', arm, None, Vector((0, 0.42, 1.30)) * k, None, scene, 0.1)
    sk['Cargo_Overhead'] = C.socket('Cargo_Overhead', arm, None, Vector((0, 0, 2.80)), None, scene, 0.1)
    if objs['pack']:
        C.attach(objs['pack'], sk['Back_Pack'])
    if objs['weapon']:
        C.attach(objs['weapon'], sk['RightHand_Weapon'])
        mz = bpy.data.objects.new('Muzzle', None)
        mz.empty_display_size = 0.03
        scene.collection.objects.link(mz)
        mz['cs_name'] = 'Muzzle'
        mz.parent = objs['weapon']
        mz.matrix_basis = Matrix.Translation(info['weapon']['muzzle'] * k)
    arm['cs_k'] = k
    arm['cs_support'] = list(info['weapon'].get('support', Vector((0, -0.2, 0.03))) * k)
    arm['cs_weapon_socket'] = list(Vector((-(wr + PALM.x), 0, z + PALM.z)) * k)
    return dict(scene=scene, arm=arm, objs=objs, sockets=sk, g=g)


def report(res):
    out = []
    for key in ('body', 'gear', 'head'):
        if res['objs'][key]:
            out.append(C.mesh_report(res['objs'][key], res['arm']))
    for key in ('pack', 'weapon'):
        if res['objs'][key]:
            out.append(C.mesh_report(res['objs'][key]))
    return out
