"""Cargo Squad - creature builders (Mutated Wolf, Armored Creature, Acid Spitter).

Anatomy-specific rigs (NOT the survivor humanoid). Bone names are prefixed per creature (W_, A_, S_) so each
FBX only receives its own actions. Clips are first-pass FK animations with clear telegraph poses.
Conventions as for survivors: metres, Z up, facing -Y, feet at Z=0.
"""
import bpy, random
from math import sin, cos, pi, radians
from mathutils import Vector, Matrix
import cs_core as C
from cs_core import Builder, R, material

FPS = 30


def path_tube(b, pts, mat, seg=12, ref=(1, 0, 0), p=2.0):
    """pts: list of (pos, rx, ry, weights). rx is measured along `ref` (lateral), ry perpendicular to it."""
    ref = Vector(ref)
    P = [Vector(q[0]) for q in pts]
    rings = []
    for i, (pos, rx, ry, w) in enumerate(pts):
        t = (P[min(i + 1, len(P) - 1)] - P[max(i - 1, 0)]).normalized()
        u = (ref - t * ref.dot(t)).normalized()
        v = t.cross(u)
        rings.append(R(P[i], rx, ry, w=w, u=u, v=v, p=p))
    b.tube(rings, mat, seg=seg)


def limb(b, joints, radii, bones, mat, seg=10, ref=(1, 0, 0)):
    """joints: 4 points (root, mid, end, tip); radii: 4 (rx, ry) tuples; bones: 3 bone names."""
    j = [Vector(q) for q in joints]
    b0, b1, b2 = bones
    r = radii
    mix = lambda a, c, t: (a[0] + (c[0] - a[0]) * t, a[1] + (c[1] - a[1]) * t)
    pts = [(j[0], *r[0], b0), (j[0].lerp(j[1], 0.5), *mix(r[0], r[1], 0.45), b0),
           (j[0].lerp(j[1], 0.88), *mix(r[0], r[1], 0.9), {b0: 0.75, b1: 0.25}), (j[1], *r[1], {b0: 0.5, b1: 0.5}),
           (j[1].lerp(j[2], 0.12), *mix(r[1], r[2], 0.1), {b0: 0.25, b1: 0.75}), (j[1].lerp(j[2], 0.55), *mix(r[1], r[2], 0.5), b1),
           (j[2], *r[2], {b1: 0.5, b2: 0.5}), (j[2].lerp(j[3], 0.5), *mix(r[2], r[3], 0.5), b2), (j[3], *r[3], b2)]
    path_tube(b, pts, mat, seg=seg, ref=ref)


def rig_and_object(name, J, builder, scene, extra=()):
    arm = C.build_rig(name + '_Rig', J, scene)
    ob = builder.to_object(name + '_Body', scene.collection, arm)
    return arm, ob


# ============================================================================ FK clip baking
def fk_bake(arm, name, fn, seconds, loop):
    n = int(round(seconds * FPS))
    old = bpy.data.actions.get(name)
    if old is not None:
        bpy.data.actions.remove(old)
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    arm.animation_data_create()
    arm.animation_data.action = act
    O = {b.name: b.matrix_local.to_3x3() for b in arm.data.bones}
    root = next(b.name for b in arm.data.bones if b.parent is None)
    prev = {}
    for pb in arm.pose.bones:
        pb.rotation_mode = 'QUATERNION'
    for fr in range(n + 1):
        ph = fr / n
        c = fn((ph % 1.0) if loop else ph)
        scales = c.get('scale', {})
        for b, pb in arm.pose.bones.items():
            D = Matrix.Identity(3)
            for ax, deg in c.get(b, ()):
                D = Matrix.Rotation(radians(deg), 3, ax) @ D
            q = (O[b].transposed() @ D @ O[b]).to_quaternion()
            if b in prev and prev[b].dot(q) < 0:
                q.negate()
            prev[b] = q
            pb.rotation_quaternion = q
            pb.keyframe_insert('rotation_quaternion', frame=fr)
            if b in scales or fr == 0 and any(True for _ in ()):
                pass
        for b in arm.get('cs_scale_bones', []):
            s = scales.get(b, 1.0)
            arm.pose.bones[b].scale = (s, s, s)
            arm.pose.bones[b].keyframe_insert('scale', frame=fr)
        rp = arm.pose.bones[root]
        rp.location = O[root].transposed() @ Vector(c.get('off', (0, 0, 0)))
        rp.keyframe_insert('location', frame=fr)
    act.use_frame_range = True
    act.frame_start, act.frame_end = 0, n
    act['cs_loop'] = bool(loop)
    return act


def fk_rest(arm):
    if arm.animation_data:
        arm.animation_data.action = None
    for pb in arm.pose.bones:
        pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.location = (0, 0, 0)
        pb.scale = (1, 1, 1)


def sm(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def bump(t, a, b, c, d):
    """0 -> 1 between a..b, hold, 1 -> 0 between c..d."""
    return sm((t - a) / max(b - a, 1e-6)) * (1 - sm((t - c) / max(d - c, 1e-6)))


def make_clips(M, P):
    """M: bone map, P: per-creature parameters. Returns [(name, fn, seconds, loop)]."""
    root, spine, neck, head, jaw = M['root'], M['spine'], M['neck'], M['head'], M.get('jaw')
    F, H = M['F'], M['H']
    tail, ears, sac = M.get('tail', []), M.get('ears', []), M.get('sac')
    pre = P['prefix']

    def add(c, bone, *rots):
        c[bone] = list(c.get(bone, [])) + list(rots)

    def base(c, breathe=0.0, head_yaw=0.0, tail_sway=0.0, sacs=1.0):
        for s in spine:
            add(c, s, ('X', P['breath'] * breathe))
        add(c, head, ('Z', head_yaw))
        for i, t in enumerate(tail):
            add(c, t, ('Z', tail_sway * (1 + 0.5 * i)))
        if sac:
            c.setdefault('scale', {})[sac] = sacs
        return c

    def legs(c, ph, amp=1.0, splay=0.0):
        for (limb_b, off, front) in ((F['L'], 0.0, True), (F['R'], 0.5, True), (H['L'], 0.5, False), (H['R'], 0.0, False)):
            p = (ph + off) % 1.0
            sw = -cos(2 * pi * p)                       # -1 = forward reach, +1 = pushed back
            lift = max(0.0, sin(2 * pi * p + pi))        # swing phase (foot returning forward)
            A = P['stride_f' if front else 'stride_h'] * amp
            add(c, limb_b[0], ('X', A * sw))
            add(c, limb_b[1], ('X', (P['flex_f'] if front else -P['flex_h']) * lift * amp - A * 0.35 * sw))
            add(c, limb_b[2], ('X', (-P['flex_f'] if front else P['flex_h']) * 0.5 * lift * amp - A * 0.4 * sw))
        return c

    def splay_limbs(c, deg):
        for side, sgn in (('L', 1), ('R', -1)):
            add(c, F[side][0], ('Y', -sgn * deg))
            add(c, H[side][0], ('Y', -sgn * deg))
            add(c, F[side][1], ('Y', sgn * deg))
            add(c, H[side][1], ('Y', sgn * deg))
        return c

    def idle(ph):
        c = base({}, sin(2 * pi * ph), P['scan'] * sin(2 * pi * ph), P['tail'] * sin(4 * pi * ph), 1 + 0.04 * sin(2 * pi * ph))
        c['off'] = (0, 0, -P['bob'] * (0.5 + 0.5 * sin(2 * pi * ph)))
        if jaw:
            add(c, jaw, ('X', 4 + 3 * sin(2 * pi * ph)))
        return c

    def move(ph):
        c = base({}, 0.3 * sin(4 * pi * ph), 0.0, P['tail'] * 1.5 * sin(2 * pi * ph), 1 + 0.03 * sin(4 * pi * ph))
        legs(c, ph)
        c['off'] = (P['sway'] * sin(2 * pi * ph), 0, -P['bob'] * 2 + P['bob'] * 1.5 * cos(4 * pi * ph))
        add(c, root, ('Z', P['yaw'] * sin(2 * pi * ph)), ('Y', P['roll'] * sin(2 * pi * ph)))
        for s in spine:
            add(c, s, ('Z', -P['yaw'] * 0.8 * sin(2 * pi * ph)))
        add(c, head, ('X', P['nod'] * cos(4 * pi * ph)))
        return c

    def alert(ph):
        tremble = 0.6 * sin(24 * pi * ph)
        c = base({}, 0.2 * sin(4 * pi * ph), P['alert_yaw'] * sin(2 * pi * ph), 0.0, 1 + 0.05 * sin(4 * pi * ph))
        for nb in neck:
            add(c, nb, ('X', -P['alert_lift'] / len(neck)))
        add(c, head, ('X', P['alert_lift'] * 0.35 + tremble))
        for i, e in enumerate(ears):
            add(c, e, ('X', -P['ear'] + tremble), ('Y', (1 if i == 0 else -1) * 8))
        for t in tail:
            add(c, t, ('X', -P.get('tail_alert', 0)))
        c['off'] = (0, 0, P['alert_rise'])
        for s in spine:
            add(c, s, ('Z', P.get('alert_body', 0.0) * sin(2 * pi * ph)))
        return c

    def pose_windup(c, a):
        w = P['windup']
        c['off'] = (0, w['back'] * a, w['drop'] * a)
        add(c, root, ('X', w['root_pitch'] * a))
        for s in spine:
            add(c, s, ('X', w['spine_pitch'] * a / len(spine)))
        for nb in neck:
            add(c, nb, ('X', w['neck'] * a / len(neck)))
        add(c, head, ('X', w['head'] * a))
        if jaw:
            add(c, jaw, ('X', w['jaw'] * a))
        for side in ('L', 'R'):
            add(c, F[side][0], ('X', w['fore_upper'] * a))
            add(c, F[side][1], ('X', w['fore_lower'] * a))
            add(c, H[side][0], ('X', w['hind_upper'] * a))
            add(c, H[side][1], ('X', -w['hind_upper'] * a * 1.2))
        for e in ears:
            add(c, e, ('X', w.get('ear', 0) * a))
        if sac:
            c.setdefault('scale', {})[sac] = 1 + (w['sac'] - 1) * a
        return c

    def windup(ph):
        a = sm(ph / 0.85)
        c = base({}, 0.0, 0.0, 0.0)
        return pose_windup(c, a + 0.03 * sin(30 * pi * ph) * a)

    def attack(ph):
        k = P['strike']
        hit = bump(ph, 0.0, k['t'], k['t'] + 0.15, 1.0)        # strike pose envelope
        rel = 1 - sm(ph / k['t'])                                # windup releasing
        c = base({}, 0.0, 0.0, 0.0)
        pose_windup(c, rel)
        c['off'] = tuple(Vector(c.get('off', (0, 0, 0))) + Vector((0, k['lunge'] * hit, k['drop'] * hit)))
        add(c, root, ('X', k['root_pitch'] * hit))
        for s in spine:
            add(c, s, ('X', k['spine_pitch'] * hit / len(spine)))
        for nb in neck:
            add(c, nb, ('X', k['neck'] * hit / len(neck)))
        add(c, head, ('X', k['head'] * hit))
        if jaw:
            add(c, jaw, ('X', k['jaw'] * bump(ph, 0.0, k['t'] * 0.6, k['t'], k['t'] + 0.08)))
        for side in ('L', 'R'):
            add(c, F[side][0], ('X', k['fore_upper'] * hit))
            add(c, F[side][1], ('X', k['fore_lower'] * hit))
        if sac:
            c.setdefault('scale', {})[sac] = 1 + (P['windup']['sac'] - 1) * rel - k.get('sac_dump', 0) * hit
        return c

    def hit(ph):
        f = bump(ph, 0.0, 0.2, 0.3, 1.0)
        c = base({}, 0.0, 22 * f, 0.0)
        c['off'] = (0.03 * f, P['flinch'] * f, -P['bob'] * 3 * f)
        add(c, root, ('X', -6 * f), ('Z', 6 * f))
        for s in spine:
            add(c, s, ('X', -5 * f), ('Z', 5 * f))
        add(c, head, ('X', 14 * f))
        if jaw:
            add(c, jaw, ('X', 18 * f))
        return c

    def stunned(ph):
        sway = sin(2 * pi * ph)
        c = base({}, 0.4 * sin(4 * pi * ph), 0.0, 0.0, 0.92)
        splay_limbs(c, P['stun_splay'])
        c['off'] = (0.02 * sway, 0, P['stun_drop'])
        add(c, root, ('Y', 5 * sway), ('X', P['stun_pitch']))
        for nb in neck:
            add(c, nb, ('X', P['stun_neck'] / len(neck)), ('Z', 6 * sway))
        add(c, head, ('X', P['stun_neck'] * 0.4), ('Y', 8 * sway))
        if jaw:
            add(c, jaw, ('X', 16))
        for e in ears:
            add(c, e, ('X', 25))
        for t in tail:
            add(c, t, ('X', 12))
        return c

    def death(ph):
        a = sm(ph / 0.8)
        buck = bump(ph, 0.0, 0.25, 0.3, 0.7)
        c = base({}, 0.0, 0.0, 0.0, 1 - 0.3 * a)
        d = P['death']
        c['off'] = (d['side'] * a, 0, d['drop'] * a - 0.05 * buck)
        add(c, root, ('Y', d['roll'] * a), ('X', 6 * buck))
        for nb in neck:
            add(c, nb, ('X', d['neck'] * a / len(neck)), ('Z', -10 * a))
        add(c, head, ('X', d['neck'] * 0.3 * a))
        if jaw:
            add(c, jaw, ('X', 20 * a))
        for side, sgn in (('L', 1), ('R', -1)):
            add(c, F[side][0], ('X', d['fore'] * a * (1 + 0.2 * sgn)))
            add(c, F[side][1], ('X', -d['fore'] * 0.8 * a))
            add(c, H[side][0], ('X', d['hind'] * a * (1 - 0.2 * sgn)))
            add(c, H[side][1], ('X', -d['hind'] * 0.9 * a))
        for t in tail:
            add(c, t, ('X', 10 * a))
        return c

    T = P['times']
    return [(pre + 'Idle', idle, T['idle'], True), (pre + 'Move', move, T['move'], True), (pre + 'Alert', alert, 2.0, True),
            (pre + 'AttackWindup', windup, T['windup'], False), (pre + 'Attack', attack, T['attack'], False),
            (pre + 'Hit', hit, 0.5, False), (pre + 'Stunned', stunned, 2.0, True), (pre + 'Death', death, T['death'], False)]


# ============================================================================ MUTATED WOLF
def build_wolf():
    name = 'MutatedWolf'
    scene = C.get_scene('CS_' + name)
    rnd = random.Random(7)
    fur = material('Wolf_Fur', '#40372F', 0.95)
    dark = material('Wolf_DarkSkin', '#2A2829', 0.85)
    lesion = material('Wolf_Lesion', '#99973E', 0.55)
    flesh = material('Wolf_Flesh', '#7C423C', 0.6)
    bone = material('Wolf_Bone', '#CBC4A9', 0.6)
    ridge = material('Wolf_SpineRidge', '#978E7B', 0.9)
    eye = material('Wolf_Eye', '#E0B54A', 0.3, emit='#E0B54A')
    J = {}
    J['W_Root'] = ((0, 0.46, 0.82), (0, 0.16, 0.84), None)
    J['W_Spine'] = ((0, 0.16, 0.84), (0, -0.16, 0.86), 'W_Root')
    J['W_Chest'] = ((0, -0.16, 0.86), (0, -0.46, 0.90), 'W_Spine')
    J['W_Neck'] = ((0, -0.46, 0.90), (0, -0.70, 1.02), 'W_Chest')
    J['W_Head'] = ((0, -0.70, 1.02), (0, -0.92, 1.02), 'W_Neck')
    J['W_Jaw'] = ((0, -0.76, 0.975), (0, -1.04, 0.955), 'W_Head')
    J['W_Tail1'] = ((0, 0.62, 0.82), (0, 0.82, 0.74), 'W_Root')
    J['W_Tail2'] = ((0, 0.82, 0.74), (0, 1.02, 0.62), 'W_Tail1')
    J['W_Tail3'] = ((0, 1.02, 0.62), (0, 1.20, 0.50), 'W_Tail2')
    legs = {}
    for s, d in (('L', 1), ('R', -1)):
        J['W_Ear_' + s] = ((d * 0.055, -0.74, 1.10), (d * 0.075, -0.72, 1.26 if d > 0 else 1.21), 'W_Head')
        f = [(d * 0.15, -0.36, 0.80), (d * 0.16, -0.28, 0.50), (d * 0.16, -0.36, 0.13), (d * 0.16, -0.47, 0.03)]
        h = [(d * 0.14, 0.44, 0.76), (d * 0.155, 0.27, 0.47), (d * 0.155, 0.53, 0.24), (d * 0.155, 0.43, 0.03)]
        legs[s] = (f, h)
        for pre, pts, par in (('F', f, 'W_Chest'), ('H', h, 'W_Root')):
            J['W_%sUpper_%s' % (pre, s)] = (pts[0], pts[1], par)
            J['W_%sLower_%s' % (pre, s)] = (pts[1], pts[2], 'W_%sUpper_%s' % (pre, s))
            J['W_%sPaw_%s' % (pre, s)] = (pts[2], pts[3], 'W_%sLower_%s' % (pre, s))
    b = Builder()
    body = [((0, 0.70, 0.80), 0.04, 0.05, 'W_Root'), ((0, 0.60, 0.81), 0.13, 0.15, 'W_Root'), ((0, 0.42, 0.80), 0.165, 0.185, 'W_Root'),
            ((0, 0.22, 0.81), 0.145, 0.15, {'W_Root': 0.5, 'W_Spine': 0.5}), ((0, 0.02, 0.82), 0.135, 0.14, 'W_Spine'),
            ((0, -0.16, 0.82), 0.165, 0.20, {'W_Spine': 0.5, 'W_Chest': 0.5}), ((0, -0.34, 0.84), 0.185, 0.235, 'W_Chest'),
            ((0, -0.46, 0.88), 0.15, 0.19, {'W_Chest': 0.6, 'W_Neck': 0.4}), ((0, -0.58, 0.955), 0.105, 0.13, 'W_Neck'),
            ((0, -0.69, 1.01), 0.092, 0.105, {'W_Neck': 0.5, 'W_Head': 0.5}), ((0, -0.78, 1.035), 0.098, 0.092, 'W_Head'),
            ((0, -0.86, 1.025), 0.085, 0.075, 'W_Head'), ((0, -0.93, 1.005), 0.052, 0.048, 'W_Head'),
            ((0, -1.03, 0.995), 0.042, 0.038, 'W_Head'), ((0, -1.085, 0.99), 0.026, 0.024, 'W_Head')]
    path_tube(b, body, fur, seg=14)
    b.box((0, -1.085, 0.997), (0.04, 0.03, 0.03), 'W_Head', dark, bevel=0.01)                               # nose
    path_tube(b, [((0, -0.80, 0.965), 0.05, 0.02, 'W_Jaw'), ((0, -0.92, 0.955), 0.04, 0.018, 'W_Jaw'),
                  ((0, -1.05, 0.95), 0.026, 0.014, 'W_Jaw')], dark, seg=8)
    for i in range(5):                                                                                       # teeth
        for d in (1, -1):
            y = -0.90 - i * 0.035
            b.cyl((d * 0.030, y, 0.984), (d * 0.030, y, 0.955 if i else 0.94), 0.007, 'W_Head', bone, r2=0.001, seg=5)
            b.cyl((d * 0.026, y - 0.015, 0.962), (d * 0.026, y - 0.015, 0.985), 0.006, 'W_Jaw', bone, r2=0.001, seg=5)
    for s, d in (('L', 1), ('R', -1)):
        top = 1.27 if d > 0 else 1.215                                                                       # asymmetric ears
        path_tube(b, [((d * 0.055, -0.745, 1.09), 0.034, 0.016, 'W_Ear_' + s), ((d * 0.068, -0.735, 1.17), 0.028, 0.012, 'W_Ear_' + s),
                      ((d * 0.078, -0.725, top), 0.006, 0.005, 'W_Ear_' + s)], fur, seg=6, ref=(1, 0, 0))
        b.sphere((d * 0.058, -0.845, 1.058), 0.014, 'W_Head', eye, useg=8, vseg=6)
        f, h = legs[s]
        limb(b, f, [(0.075, 0.10), (0.050, 0.060), (0.034, 0.038), (0.030, 0.026)],
             ('W_FUpper_' + s, 'W_FLower_' + s, 'W_FPaw_' + s), fur)
        limb(b, h, [(0.085, 0.13), (0.055, 0.070), (0.034, 0.038), (0.030, 0.026)],
             ('W_HUpper_' + s, 'W_HLower_' + s, 'W_HPaw_' + s), fur)
        for pts, bn in ((f, 'W_FPaw_' + s), (h, 'W_HPaw_' + s)):
            b.box((pts[3][0], pts[3][1] - 0.035, 0.028), (0.085, 0.13, 0.05), bn, dark, bevel=0.02)
            for cx in (-0.026, 0.0, 0.026):
                b.cyl((pts[3][0] + cx, pts[3][1] - 0.09, 0.03), (pts[3][0] + cx, pts[3][1] - 0.135, 0.006), 0.008, bn, bone, r2=0.001, seg=5)
    path_tube(b, [((0, 0.62, 0.82), 0.055, 0.055, 'W_Tail1'), ((0, 0.82, 0.74), 0.050, 0.050, {'W_Tail1': 0.5, 'W_Tail2': 0.5}),
                  ((0, 1.02, 0.62), 0.040, 0.040, {'W_Tail2': 0.5, 'W_Tail3': 0.5}), ((0, 1.20, 0.50), 0.012, 0.012, 'W_Tail3')], fur, seg=8)
    # dust-pale spine ridge with hardened spikes, matted fur clumps on neck/shoulders
    spine_pts = [(0.58, 0.955, 'W_Root'), (0.42, 0.975, 'W_Root'), (0.24, 0.955, 'W_Spine'), (0.06, 0.955, 'W_Spine'),
                 (-0.12, 1.01, 'W_Spine'), (-0.28, 1.065, 'W_Chest'), (-0.42, 1.06, 'W_Chest'), (-0.56, 1.075, 'W_Neck')]
    for i, (y, z, bn) in enumerate(spine_pts):
        hgt = 0.07 + 0.05 * (i % 2) + (0.05 if -0.45 < y < -0.1 else 0)
        b.cyl((0, y, z - 0.03), (0, y + 0.03, z + hgt), 0.034, bn, ridge, r2=0.004, seg=6)
    for i in range(14):
        y = -0.20 - rnd.random() * 0.42
        d = rnd.choice((1, -1))
        x = d * (0.10 + rnd.random() * 0.07)
        z = 0.95 + (0.10 if y < -0.45 else 0.06) * rnd.random() + (0.06 if y < -0.5 else 0)
        bn = 'W_Neck' if y < -0.47 else 'W_Chest'
        b.cyl((x, y, z - 0.05), (x * 1.25, y + 0.07, z + 0.06), 0.034, bn, fur, r2=0.003, seg=5)
    # contamination: chemical lesions, raw flank patch with exposed ribs (right side), scar plates
    for (x, y, z, r, bn) in ((0.155, 0.36, 0.88, 0.06, 'W_Root'), (-0.12, 0.50, 0.92, 0.05, 'W_Root'), (0.16, -0.30, 0.95, 0.055, 'W_Chest'),
                             (0.06, -0.60, 1.05, 0.035, 'W_Neck'), (0.15, 0.30, 0.56, 0.035, 'W_HUpper_L')):
        b.sphere((x, y, z), (r, r * 1.2, r * 0.8), bn, lesion, useg=8, vseg=6)
    b.sphere((-0.135, 0.06, 0.82), (0.045, 0.16, 0.11), 'W_Spine', flesh, useg=10, vseg=6)
    for i in range(4):
        b.box((-0.168, -0.04 + i * 0.062, 0.82), (0.018, 0.016, 0.17), 'W_Spine', bone, bevel=0.006, rot=(0, 0, 0))
    b.sphere((-0.17, -0.36, 0.86), (0.035, 0.09, 0.10), 'W_Chest', dark, useg=8, vseg=6)
    arm, ob = rig_and_object(name, J, b, scene)
    mouth = C.socket('Mouth', arm, 'W_Head', (0, -1.06, 0.975), None, scene, 0.05)
    M = dict(root='W_Root', spine=['W_Spine', 'W_Chest'], neck=['W_Neck'], head='W_Head', jaw='W_Jaw',
             F=dict(L=['W_FUpper_L', 'W_FLower_L', 'W_FPaw_L'], R=['W_FUpper_R', 'W_FLower_R', 'W_FPaw_R']),
             H=dict(L=['W_HUpper_L', 'W_HLower_L', 'W_HPaw_L'], R=['W_HUpper_R', 'W_HLower_R', 'W_HPaw_R']),
             tail=['W_Tail1', 'W_Tail2', 'W_Tail3'], ears=['W_Ear_L', 'W_Ear_R'])
    P = dict(prefix='Wolf_', breath=1.6, scan=10, tail=7, bob=0.008, sway=0.012, yaw=4, roll=2, nod=3, stride_f=30, stride_h=28,
             flex_f=50, flex_h=45, alert_lift=30, alert_yaw=6, ear=28, alert_rise=0.03, tail_alert=12, flinch=0.10,
             stun_splay=12, stun_drop=-0.14, stun_pitch=4, stun_neck=38,
             windup=dict(back=0.10, drop=-0.13, root_pitch=-7, spine_pitch=12, neck=16, head=-18, jaw=30, fore_upper=-22,
                         fore_lower=48, hind_upper=18, ear=35, sac=1.0),
             strike=dict(t=0.35, lunge=-0.42, drop=0.05, root_pitch=6, spine_pitch=-8, neck=-22, head=-6, jaw=42,
                         fore_upper=-52, fore_lower=30),
             death=dict(side=0.10, drop=-0.62, roll=82, neck=20, fore=28, hind=-24),
             times=dict(idle=3.0, move=0.6, windup=0.5, attack=0.6, death=1.2))
    return dict(name=name, scene=scene, arm=arm, objs=[ob], sockets=[mouth], M=M, P=P)


# ============================================================================ ARMORED CREATURE
def build_armored():
    name = 'ArmoredCreature'
    scene = C.get_scene('CS_' + name)
    rnd = random.Random(21)
    hide = material('Armored_Hide', '#34302C', 0.9)
    plate = material('Armored_Plate', '#5A5046', 0.95)
    plate2 = material('Armored_PlateRust', '#6B4A35', 0.95)
    iron = material('Armored_PlateDark', '#2B2826', 0.9)
    fissure = material('Armored_Fissure', '#7E8A3A', 0.6, emit='#39401A')
    bone = material('Armored_Bone', '#B9B19A', 0.7)
    eye = material('Armored_Eye', '#C9C44A', 0.3, emit='#C9C44A')
    J = {}
    J['A_Root'] = ((0, 0.62, 1.12), (0, 0.22, 1.50), None)
    J['A_Spine'] = ((0, 0.22, 1.50), (0, -0.22, 1.86), 'A_Root')
    J['A_Chest'] = ((0, -0.22, 1.86), (0, -0.62, 1.96), 'A_Spine')
    J['A_Head'] = ((0, -0.68, 1.66), (0, -1.02, 1.50), 'A_Chest')
    J['A_Jaw'] = ((0, -0.76, 1.56), (0, -1.02, 1.40), 'A_Head')
    L = {}
    for s, d in (('L', 1), ('R', -1)):
        f = [(d * 0.62, -0.42, 1.88), (d * 0.86, -0.50, 1.12), (d * 0.80, -0.82, 0.36), (d * 0.80, -0.98, 0.06)]
        h = [(d * 0.36, 0.60, 1.08), (d * 0.46, 0.36, 0.60), (d * 0.46, 0.60, 0.17), (d * 0.46, 0.28, 0.04)]
        L[s] = (f, h)
        J['A_Shoulder_' + s] = ((d * 0.20, -0.36, 1.92), f[0], 'A_Chest')
        for pre, pts, par in (('F', f, 'A_Shoulder_' + s), ('H', h, 'A_Root')):
            J['A_%sUpper_%s' % (pre, s)] = (pts[0], pts[1], par)
            J['A_%sLower_%s' % (pre, s)] = (pts[1], pts[2], 'A_%sUpper_%s' % (pre, s))
            J['A_%sFoot_%s' % (pre, s)] = (pts[2], pts[3], 'A_%sLower_%s' % (pre, s))
    b = Builder()
    torso = [((0, 0.92, 0.98), 0.14, 0.14, 'A_Root'), ((0, 0.74, 1.08), 0.42, 0.40, 'A_Root'), ((0, 0.50, 1.26), 0.50, 0.46, 'A_Root'),
             ((0, 0.22, 1.46), 0.50, 0.46, {'A_Root': 0.5, 'A_Spine': 0.5}), ((0, -0.04, 1.62), 0.58, 0.50, 'A_Spine'),
             ((0, -0.26, 1.74), 0.70, 0.52, {'A_Spine': 0.5, 'A_Chest': 0.5}), ((0, -0.46, 1.78), 0.72, 0.48, 'A_Chest'),
             ((0, -0.64, 1.72), 0.52, 0.38, 'A_Chest'), ((0, -0.76, 1.64), 0.22, 0.20, 'A_Chest')]
    path_tube(b, torso, hide, seg=16, p=2.6)
    # small head sunk between the shoulder shelves
    path_tube(b, [((0, -0.66, 1.68), 0.17, 0.16, {'A_Chest': 0.5, 'A_Head': 0.5}), ((0, -0.80, 1.63), 0.17, 0.15, 'A_Head'),
                  ((0, -0.94, 1.56), 0.14, 0.11, 'A_Head'), ((0, -1.04, 1.51), 0.09, 0.07, 'A_Head')], hide, seg=10, p=2.6)
    path_tube(b, [((0, -0.80, 1.53), 0.13, 0.05, 'A_Jaw'), ((0, -0.93, 1.46), 0.12, 0.05, 'A_Jaw'), ((0, -1.03, 1.42), 0.08, 0.04, 'A_Jaw')],
              hide, seg=8, p=2.6)
    b.box((0, -0.86, 1.70), (0.30, 0.20, 0.07), 'A_Head', plate, bevel=0.025, rot=(-24, 0, 0))               # brow plate
    for d in (1, -1):
        b.sphere((d * 0.085, -0.985, 1.575), 0.022, 'A_Head', eye, useg=8, vseg=6)
        for i in range(3):
            b.cyl((d * (0.04 + i * 0.035), -0.99 + i * 0.03, 1.455 + i * 0.01), (d * (0.04 + i * 0.035), -1.0 + i * 0.03, 1.53), 0.014,
                  'A_Jaw', bone, r2=0.002, seg=5)
    for s, d in (('L', 1), ('R', -1)):
        f, h = L[s]
        path_tube(b, [((d * 0.30, -0.40, 1.90), 0.26, 0.30, {'A_Chest': 0.6, 'A_Shoulder_' + s: 0.4}),
                      ((d * 0.58, -0.42, 1.90), 0.30, 0.34, 'A_Shoulder_' + s), ((d * 0.72, -0.44, 1.78), 0.20, 0.24, 'A_FUpper_' + s)],
                  hide, seg=10, ref=(0, 1, 0))
        limb(b, f, [(0.20, 0.23), (0.17, 0.19), (0.16, 0.17), (0.13, 0.10)], ('A_FUpper_' + s, 'A_FLower_' + s, 'A_FFoot_' + s), hide, seg=10)
        limb(b, h, [(0.22, 0.26), (0.17, 0.19), (0.14, 0.15), (0.13, 0.08)], ('A_HUpper_' + s, 'A_HLower_' + s, 'A_HFoot_' + s), hide, seg=10)
        b.box((f[3][0], f[3][1] - 0.04, 0.12), (0.30, 0.34, 0.24), 'A_FFoot_' + s, iron, bevel=0.06)          # crusted fist
        b.box((h[3][0], h[3][1] - 0.06, 0.07), (0.28, 0.42, 0.14), 'A_HFoot_' + s, iron, bevel=0.05)
        for i in range(3):
            b.cyl((h[3][0] + (i - 1) * 0.08, h[3][1] - 0.24, 0.06), (h[3][0] + (i - 1) * 0.09, h[3][1] - 0.36, 0.01), 0.03,
                  'A_HFoot_' + s, bone, r2=0.004, seg=5)
        # forearm + shin slabs
        for i in range(4):
            t = 0.12 + i * 0.24
            p = Vector(f[1]).lerp(Vector(f[2]), t)
            b.box((p.x + d * 0.13, p.y - 0.02, p.z), (0.10, 0.26, 0.20), 'A_FLower_' + s, plate if i % 2 else plate2, bevel=0.03,
                  rot=(rnd.uniform(-12, 12) + 20, d * 14, rnd.uniform(-10, 10)))
            b.box((p.x, p.y - 0.15, p.z), (0.26, 0.10, 0.20), 'A_FLower_' + s, iron if i % 2 else plate, bevel=0.03,
                  rot=(rnd.uniform(-10, 10) + 22, 0, rnd.uniform(-12, 12)))
        for i in range(2):
            p = Vector(f[0]).lerp(Vector(f[1]), 0.35 + i * 0.4)
            b.box((p.x + d * 0.16, p.y, p.z), (0.11, 0.34, 0.30), 'A_FUpper_' + s, plate2 if i else plate, bevel=0.035,
                  rot=(rnd.uniform(-10, 10), d * 16, rnd.uniform(-10, 10)))
            p = Vector(h[0]).lerp(Vector(h[1]), 0.3 + i * 0.45)
            b.box((p.x + d * 0.17, p.y, p.z), (0.10, 0.32, 0.28), 'A_HUpper_' + s, plate, bevel=0.03, rot=(rnd.uniform(-8, 8) - 20, d * 10, 0))
        # shoulder shelves: big overlapping slabs (move with the shoulder for the wind-up roll)
        for i, (ox, oy, oz, sx, sy, sz) in enumerate(((0.62, -0.40, 2.20, 0.62, 0.66, 0.16), (0.80, -0.30, 2.06, 0.34, 0.60, 0.20),
                                                      (0.50, -0.66, 2.10, 0.46, 0.30, 0.18), (0.44, -0.10, 2.22, 0.50, 0.44, 0.15))):
            b.box((d * ox, oy, oz), (sx, sy, sz), 'A_Shoulder_' + s, (plate, plate2, iron, plate)[i], bevel=0.05,
                  rot=(rnd.uniform(-8, 8), d * (14 + rnd.uniform(-5, 8)), rnd.uniform(-12, 12)), taper=0.78)
    # broken plating over the back: a centre ridge + two flanking rows + fissures between them
    rows = [(0.0, 0.0), (0.36, -0.10), (-0.36, -0.10)]
    spine_line = [((0, 0.72, 1.46), 'A_Root'), ((0, 0.46, 1.68), 'A_Root'), ((0, 0.20, 1.88), 'A_Spine'), ((0, -0.06, 2.06), 'A_Spine'),
                  ((0, -0.30, 2.20), 'A_Chest'), ((0, -0.52, 2.22), 'A_Chest')]
    for i, (pos, bn) in enumerate(spine_line):
        for j, (dx, dz) in enumerate(rows):
            sc = 1.0 + 0.25 * (i >= 3)
            mat = (plate, plate2, iron)[(i + j) % 3]
            b.box((pos[0] + dx * sc, pos[1] + rnd.uniform(-0.04, 0.04), pos[2] + dz + rnd.uniform(-0.03, 0.05)),
                  (0.40 * sc, 0.34, 0.15 + 0.06 * rnd.random()), bn, mat, bevel=0.05, taper=0.72,
                  rot=(-34 + rnd.uniform(-10, 10), dx * 40 + rnd.uniform(-8, 8), rnd.uniform(-15, 15)))
        if i < len(spine_line) - 1:
            nxt = spine_line[i + 1][0]
            mid = (Vector(pos) + Vector(nxt)) * 0.5
            for dx in (0.19, -0.19):
                b.box((mid.x + dx, mid.y, mid.z - 0.06), (0.05, 0.30, 0.05), bn, fissure, bevel=0.012, rot=(-34, 0, rnd.uniform(-20, 20)))
    for i in range(5):  # jagged dorsal shards
        pos, bn = spine_line[1 + i % 5]
        b.cyl((rnd.uniform(-0.2, 0.2), pos[1], pos[2] + 0.02), (rnd.uniform(-0.25, 0.25), pos[1] + 0.10, pos[2] + 0.30 + 0.1 * rnd.random()),
              0.10, bn, iron, r2=0.01, seg=5)
    arm, ob = rig_and_object(name, J, b, scene)
    mouth = C.socket('Mouth', arm, 'A_Head', (0, -1.05, 1.47), None, scene, 0.08)
    fist = C.socket('Fist_R', arm, 'A_FFoot_R', (-0.80, -1.0, 0.12), None, scene, 0.08)
    M = dict(root='A_Root', spine=['A_Spine', 'A_Chest'], neck=['A_Head'], head='A_Head', jaw='A_Jaw',
             F=dict(L=['A_FUpper_L', 'A_FLower_L', 'A_FFoot_L'], R=['A_FUpper_R', 'A_FLower_R', 'A_FFoot_R']),
             H=dict(L=['A_HUpper_L', 'A_HLower_L', 'A_HFoot_L'], R=['A_HUpper_R', 'A_HLower_R', 'A_HFoot_R']))
    P = dict(prefix='Armored_', breath=2.2, scan=7, tail=0, bob=0.02, sway=0.05, yaw=5, roll=3.5, nod=2, stride_f=16, stride_h=18,
             flex_f=18, flex_h=22, alert_lift=10, alert_yaw=22, ear=0, alert_rise=0.04, alert_body=7, flinch=0.06,
             stun_splay=6, stun_drop=-0.16, stun_pitch=10, stun_neck=22,
             windup=dict(back=0.22, drop=0.10, root_pitch=-16, spine_pitch=-22, neck=-6, head=-4, jaw=22, fore_upper=-118,
                         fore_lower=-36, hind_upper=10, sac=1.0),
             strike=dict(t=0.22, lunge=-0.30, drop=-0.22, root_pitch=16, spine_pitch=26, neck=10, head=6, jaw=10,
                         fore_upper=26, fore_lower=-12),
             death=dict(side=0.25, drop=-0.62, roll=58, neck=14, fore=16, hind=-14),
             times=dict(idle=4.0, move=1.6, windup=1.2, attack=0.9, death=1.8))
    return dict(name=name, scene=scene, arm=arm, objs=[ob], sockets=[mouth, fist], M=M, P=P)


# ============================================================================ ACID SPITTER
def build_spitter():
    name = 'AcidSpitter'
    scene = C.get_scene('CS_' + name)
    rnd = random.Random(5)
    hide = material('Spitter_Hide', '#8B8270', 0.85)
    dark = material('Spitter_HideDark', '#514A40', 0.9)
    sacm = material('Spitter_Sac', '#8C9A3A', 0.35, emit='#2E3A10')
    sacy = material('Spitter_SacEdge', '#C8C64C', 0.3, emit='#5A5A16')
    memb = material('Spitter_Membrane', '#A9A078', 0.3)
    bone = material('Spitter_Bone', '#C9C2A6', 0.6)
    eye = material('Spitter_Eye', '#1B1A16', 0.2)
    J = {}
    J['S_Root'] = ((0, 0.40, 1.02), (0, 0.06, 1.16), None)
    J['S_Spine'] = ((0, 0.06, 1.16), (0, -0.28, 1.18), 'S_Root')
    J['S_Chest'] = ((0, -0.28, 1.18), (0, -0.50, 1.12), 'S_Spine')
    J['S_Neck1'] = ((0, -0.50, 1.12), (0, -0.74, 0.96), 'S_Chest')
    J['S_Neck2'] = ((0, -0.74, 0.96), (0, -0.96, 0.82), 'S_Neck1')
    J['S_Head'] = ((0, -0.96, 0.82), (0, -1.28, 0.72), 'S_Neck2')
    J['S_Jaw'] = ((0, -1.00, 0.76), (0, -1.24, 0.64), 'S_Head')
    J['S_Sac'] = ((0, -0.06, 1.28), (0, -0.02, 1.78), 'S_Spine')
    J['S_Tail'] = ((0, 0.46, 1.00), (0, 0.80, 0.86), 'S_Root')
    L = {}
    for s, d in (('L', 1), ('R', -1)):
        f = [(d * 0.17, -0.36, 1.14), (d * 0.56, -0.44, 1.50), (d * 0.62, -0.78, 0.18), (d * 0.62, -0.98, 0.03)]
        h = [(d * 0.15, 0.36, 1.00), (d * 0.52, 0.26, 1.32), (d * 0.58, 0.62, 0.18), (d * 0.58, 0.42, 0.03)]
        L[s] = (f, h)
        for pre, pts, par in (('F', f, 'S_Chest'), ('H', h, 'S_Root')):
            J['S_%sUpper_%s' % (pre, s)] = (pts[0], pts[1], par)
            J['S_%sLower_%s' % (pre, s)] = (pts[1], pts[2], 'S_%sUpper_%s' % (pre, s))
            J['S_%sFoot_%s' % (pre, s)] = (pts[2], pts[3], 'S_%sLower_%s' % (pre, s))
    b = Builder()
    body = [((0, 0.80, 0.86), 0.02, 0.02, 'S_Tail'), ((0, 0.62, 0.94), 0.06, 0.06, 'S_Tail'), ((0, 0.44, 1.01), 0.13, 0.13, 'S_Root'),
            ((0, 0.22, 1.09), 0.15, 0.15, 'S_Root'), ((0, 0.04, 1.15), 0.12, 0.13, {'S_Root': 0.5, 'S_Spine': 0.5}),
            ((0, -0.14, 1.17), 0.15, 0.16, 'S_Spine'), ((0, -0.32, 1.17), 0.19, 0.19, {'S_Spine': 0.5, 'S_Chest': 0.5}),
            ((0, -0.46, 1.13), 0.15, 0.15, 'S_Chest'), ((0, -0.58, 1.07), 0.085, 0.09, {'S_Chest': 0.5, 'S_Neck1': 0.5}),
            ((0, -0.72, 0.975), 0.065, 0.07, 'S_Neck1'), ((0, -0.85, 0.89), 0.06, 0.065, {'S_Neck1': 0.3, 'S_Neck2': 0.7}),
            ((0, -0.96, 0.83), 0.075, 0.08, {'S_Neck2': 0.5, 'S_Head': 0.5}), ((0, -1.06, 0.815), 0.095, 0.085, 'S_Head'),
            ((0, -1.16, 0.775), 0.075, 0.062, 'S_Head'), ((0, -1.26, 0.735), 0.050, 0.044, 'S_Head'),
            ((0, -1.34, 0.71), 0.040, 0.036, 'S_Head')]
    path_tube(b, body, hide, seg=12)
    b.cyl((0, -1.33, 0.712), (0, -1.39, 0.70), 0.034, 'S_Head', dark, r2=0.042, seg=8)                         # mouth nozzle
    path_tube(b, [((0, -1.02, 0.752), 0.065, 0.022, 'S_Jaw'), ((0, -1.14, 0.70), 0.055, 0.02, 'S_Jaw'),
                  ((0, -1.27, 0.655), 0.03, 0.014, 'S_Jaw')], dark, seg=8)
    for d in (1, -1):
        b.sphere((d * 0.072, -1.10, 0.845), 0.022, 'S_Head', eye, useg=8, vseg=6)
        for i in range(4):
            y = -1.13 - i * 0.04
            b.cyl((d * 0.045, y, 0.735 - i * 0.012), (d * 0.045, y - 0.01, 0.685 - i * 0.012), 0.007, 'S_Head', bone, r2=0.001, seg=5)
    for i in range(3):                                                                                         # acid drips
        b.cyl((rnd.uniform(-0.03, 0.03), -1.30 - i * 0.03, 0.69), (rnd.uniform(-0.03, 0.03), -1.30 - i * 0.03, 0.56 - 0.05 * i), 0.012,
              'S_Jaw', sacy, r2=0.002, seg=5)
    # throat reservoir + dorsal sac cluster (driven by S_Sac scale for the inflate telegraph)
    b.sphere((0, -0.66, 0.90), (0.085, 0.13, 0.10), 'S_Neck1', sacm, useg=10, vseg=7)
    b.sphere((0, -0.56, 1.00), (0.10, 0.10, 0.09), 'S_Chest', memb, useg=10, vseg=7)
    blobs = [(0.0, -0.06, 1.46, 0.24), (0.17, 0.08, 1.38, 0.17), (-0.17, 0.04, 1.40, 0.18), (0.10, -0.26, 1.38, 0.16),
             (-0.12, -0.24, 1.40, 0.15), (0.0, 0.22, 1.30, 0.15), (0.02, -0.10, 1.66, 0.14), (-0.20, -0.10, 1.56, 0.10), (0.2, -0.12, 1.54, 0.10)]
    for i, (x, y, z, r) in enumerate(blobs):
        b.sphere((x, y, z), (r, r, r * 0.95), 'S_Sac', sacy if i in (6, 7, 8) else sacm, useg=10, vseg=7)
    for i in range(6):                                                                                         # blisters
        b.sphere((rnd.uniform(-0.14, 0.14), rnd.uniform(-0.4, 0.4), 1.20 + rnd.uniform(0.0, 0.08)), 0.04 + 0.02 * rnd.random(),
                 'S_Spine' if i % 2 else 'S_Root', memb, useg=8, vseg=5)
    for i in range(5):                                                                                         # ribs showing
        for d in (1, -1):
            b.box((d * 0.15, -0.40 + i * 0.09, 1.08), (0.02, 0.022, 0.20), 'S_Spine' if i > 1 else 'S_Chest', bone, bevel=0.007, rot=(0, d * 18, 0))
    for s, d in (('L', 1), ('R', -1)):
        f, h = L[s]
        limb(b, f, [(0.055, 0.06), (0.040, 0.042), (0.028, 0.030), (0.03, 0.02)], ('S_FUpper_' + s, 'S_FLower_' + s, 'S_FFoot_' + s), hide, seg=8)
        limb(b, h, [(0.065, 0.07), (0.044, 0.046), (0.030, 0.032), (0.03, 0.02)], ('S_HUpper_' + s, 'S_HLower_' + s, 'S_HFoot_' + s), hide, seg=8)
        b.sphere(f[1], 0.058, {'S_FUpper_' + s: 0.5, 'S_FLower_' + s: 0.5}, dark, useg=8, vseg=6)             # knobbly joints
        b.sphere(h[1], 0.064, {'S_HUpper_' + s: 0.5, 'S_HLower_' + s: 0.5}, dark, useg=8, vseg=6)
        for pts, bn in ((f, 'S_FFoot_' + s), (h, 'S_HFoot_' + s)):
            for a in (-28, 0, 28):
                dx, dy = sin(radians(a)) * 0.16, -cos(radians(a)) * 0.16
                b.cyl((pts[3][0], pts[3][1], 0.035), (pts[3][0] + dx, pts[3][1] + dy, 0.008), 0.016, bn, bone, r2=0.002, seg=5)
    arm, ob = rig_and_object(name, J, b, scene)
    arm['cs_scale_bones'] = ['S_Sac']
    mouth = C.socket('Mouth', arm, 'S_Head', (0, -1.40, 0.70), None, scene, 0.05)
    M = dict(root='S_Root', spine=['S_Spine', 'S_Chest'], neck=['S_Neck1', 'S_Neck2'], head='S_Head', jaw='S_Jaw',
             F=dict(L=['S_FUpper_L', 'S_FLower_L', 'S_FFoot_L'], R=['S_FUpper_R', 'S_FLower_R', 'S_FFoot_R']),
             H=dict(L=['S_HUpper_L', 'S_HLower_L', 'S_HFoot_L'], R=['S_HUpper_R', 'S_HLower_R', 'S_HFoot_R']),
             tail=['S_Tail'], sac='S_Sac')
    P = dict(prefix='Spitter_', breath=2.0, scan=14, tail=6, bob=0.012, sway=0.02, yaw=4, roll=3, nod=4, stride_f=14, stride_h=14,
             flex_f=16, flex_h=16, alert_lift=34, alert_yaw=16, ear=0, alert_rise=0.03, flinch=0.10,
             stun_splay=10, stun_drop=-0.26, stun_pitch=6, stun_neck=26,
             windup=dict(back=0.10, drop=-0.06, root_pitch=-8, spine_pitch=-10, neck=-52, head=-10, jaw=34, fore_upper=6,
                         fore_lower=-4, hind_upper=-6, sac=1.42),
             strike=dict(t=0.18, lunge=-0.10, drop=-0.03, root_pitch=6, spine_pitch=8, neck=-24, head=14, jaw=46,
                         fore_upper=-6, fore_lower=4, sac_dump=0.22),
             death=dict(side=0.2, drop=-0.78, roll=70, neck=30, fore=-20, hind=18),
             times=dict(idle=3.0, move=1.1, windup=0.9, attack=0.7, death=1.4))
    return dict(name=name, scene=scene, arm=arm, objs=[ob], sockets=[mouth], M=M, P=P)


BUILDERS = dict(MutatedWolf=build_wolf, ArmoredCreature=build_armored, AcidSpitter=build_spitter)


def bake_creature(res):
    acts = []
    for nm, fn, secs, loop in make_clips(res['M'], res['P']):
        acts.append(fk_bake(res['arm'], nm, fn, secs, loop))
    fk_rest(res['arm'])
    return acts


def pose_creature(res, clip, ph):
    """Preview helper: applies one pose of a clip without keying."""
    fn = next(f for n, f, s, l in make_clips(res['M'], res['P']) if n.endswith(clip))
    arm = res['arm']
    if arm.animation_data:
        arm.animation_data.action = None
    O = {b.name: b.matrix_local.to_3x3() for b in arm.data.bones}
    root = next(b.name for b in arm.data.bones if b.parent is None)
    c = fn(ph)
    for b, pb in arm.pose.bones.items():
        D = Matrix.Identity(3)
        for ax, deg in c.get(b, ()):
            D = Matrix.Rotation(radians(deg), 3, ax) @ D
        pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = (O[b].transposed() @ D @ O[b]).to_quaternion()
        s = c.get('scale', {}).get(b, 1.0)
        pb.scale = (s, s, s)
    arm.pose.bones[root].location = O[root].transposed() @ Vector(c.get('off', (0, 0, 0)))
