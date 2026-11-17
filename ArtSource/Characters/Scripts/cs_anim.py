"""Cargo Squad - procedural humanoid clip authoring.

Each clip is a Python function phase(0..1) -> control dict. A small analytic solver (FK torso, two-bone IK
limbs, explicit hand/foot orientation) turns the controls into bone rotations which are baked as dense
keyframes into Blender actions. All clips are IN PLACE: only Hips carries (small) translation.

Armature axes: Z up, character faces -Y, +X = character left. Rotation lists are [(axis, degrees), ...]
applied in order in armature axes relative to the T-pose.
"""
import bpy
from math import sin, cos, pi, radians, sqrt
from mathutils import Vector, Matrix, Quaternion

I3 = Matrix.Identity(3)
FPS = 30


def rot(spec):
    if spec is None:
        return I3.copy()
    if isinstance(spec, Matrix):
        return spec
    m = I3.copy()
    for ax, deg in spec:
        m = Matrix.Rotation(radians(deg), 3, ax) @ m
    return m


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def clamp01(t):
    return max(0.0, min(1.0, t))


def frame(f, p):
    """Orthonormal basis with columns (f, p', f x p')."""
    f = Vector(f).normalized()
    p = Vector(p)
    p = (p - f * p.dot(f)).normalized()
    return Matrix((f, p, f.cross(p))).transposed()


def swing(a, b):
    return Vector(a).normalized().rotation_difference(Vector(b).normalized()).to_matrix()


def two_bone(root, target, l1, l2, pole):
    d = target - root
    L = max(abs(l1 - l2) + 1e-4, min(d.length, l1 + l2 - 1e-4))
    dn = d.normalized()
    a = (l1 * l1 - l2 * l2 + L * L) / (2 * L)
    h = sqrt(max(l1 * l1 - a * a, 0.0))
    pp = Vector(pole) - dn * Vector(pole).dot(dn)
    if pp.length < 1e-6:
        pp = Vector((0, -1, 0))
    pp.normalize()
    return root + dn * a + pp * h, root + dn * L


class Solver:
    def __init__(self, arm):
        self.arm = arm
        B = arm.data.bones
        self.order = [b.name for b in B]
        self.head = {b.name: b.head_local.copy() for b in B}
        self.tail = {b.name: b.tail_local.copy() for b in B}
        self.O = {b.name: b.matrix_local.to_3x3() for b in B}
        self.parent = {b.name: (b.parent.name if b.parent else None) for b in B}
        self.len = {b.name: b.length for b in B}
        self.k = arm.get('cs_k', 1.0)
        self.sock = Vector(arm.get('cs_weapon_socket', (-0.81, 0, 1.408)))
        self.sockR = Matrix(((0, 1, 0), (0, 0, -1), (-1, 0, 0)))
        self.support = Vector(arm.get('cs_support', (0, -0.30, 0.036)))
        self.palm = {1: Vector((0.062, 0, -0.022)) * self.k, -1: Vector((-0.062, 0, -0.022)) * self.k}

    # -- arm spec resolution -> (wrist position, hand delta matrix, pole)
    def _arm(self, spec, side, A, P, st):
        S = 'Left' if side > 0 else 'Right'
        kind = spec[0]
        if kind == 'mix':
            a = self._arm(spec[1], side, A, P, st)
            b = self._arm(spec[2], side, A, P, st)
            s = spec[3]
            q = a[1].to_quaternion().slerp(b[1].to_quaternion(), s)
            return a[0].lerp(b[0], s), q.to_matrix(), a[2].lerp(b[2], s)
        if kind == 'at':
            _, pos, fingers, palm, pole, space = spec
            pos, fingers, palm, pole = Vector(pos), Vector(fingers), Vector(palm), Vector(pole)
            if space == 'chest':
                Ac = A['Chest']
                pos, fingers, palm, pole = P['Chest'] + Ac @ pos, Ac @ fingers, Ac @ palm, Ac @ pole
            elif space == 'shoulder':
                Ac = A['Chest']
                pos, fingers, palm, pole = P[S + 'UpperArm'] + Ac @ pos, Ac @ fingers, Ac @ palm, Ac @ pole
            Ah = frame(fingers, palm) @ frame((side, 0, 0), (0, 0, -1)).transposed()
            return pos, Ah, pole
        if kind == 'weapon':  # right hand drives the weapon: grip position + aim in chest space
            _, grip, fwd, up, pole = spec
            Ac = A['Chest']
            grip = P['Chest'] + Ac @ Vector(grip)
            f = (Ac @ Vector(fwd)).normalized()
            u = Ac @ Vector(up)
            u = (u - f * u.dot(f)).normalized()
            T = Matrix(((-f).cross(u), -f, u)).transposed()
            Ah = T @ self.sockR.transposed()
            st['weapon'] = (grip, T)
            return grip - Ah @ (self.sock - self.head['RightHand']), Ah, Ac @ Vector(pole)
        if kind == 'support':  # left hand on the weapon's support point
            _, pole = spec[:2]
            grip, T = st['weapon']
            sp = grip + T @ (self.support + Vector(spec[2]) if len(spec) > 2 else self.support)
            x, f, u = T.col[0], -T.col[1], T.col[2]
            Ah = frame(-x * 1.0 + f * 0.45, u) @ frame((1, 0, 0), (0, 0, -1)).transposed()
            return sp - Ah @ self.palm[1] - u * 0.012 * self.k, Ah, A['Chest'] @ Vector(pole)
        raise ValueError(kind)

    def solve(self, c):
        A, P, st = {}, {}, {}
        A['Hips'] = rot(c.get('Hips'))
        P['Hips'] = self.head['Hips'] + Vector(c.get('hips_off', (0, 0, 0)))

        def fk(b, d=None):
            p = self.parent[b]
            A[b] = A[p] @ (rot(c.get(b)) if d is None else d)
            P[b] = P[p] + A[p] @ (self.head[b] - self.head[p])

        for b in ('Spine', 'Chest', 'Neck', 'Head'):
            fk(b)
        for S, side in (('Right', -1), ('Left', 1)):
            sh, ua, la, hd = S + 'Shoulder', S + 'UpperArm', S + 'LowerArm', S + 'Hand'
            fk(sh)
            fk(ua, I3)
            spec = c.get('arm' + S)
            if spec is None:
                fk(la, I3)
                fk(hd, I3)
            else:
                wrist, Ah, pole = self._arm(spec, side, A, P, st)
                mid, end = two_bone(P[ua], wrist, self.len[ua], self.len[la], pole)
                rd = Vector((side, 0, 0))
                A[ua] = swing(A[sh] @ rd, mid - P[ua]) @ A[sh]
                A[la] = swing(A[ua] @ rd, end - mid) @ A[ua]
                # share the wrist twist with the forearm
                rel = (A[la].transposed() @ Ah).to_quaternion()
                tw = rel.to_swing_twist('X')[1]
                A[la] = A[la] @ Matrix.Rotation(tw * 0.65, 3, 'X')
                A[hd] = Ah
                P[la], P[hd] = mid, end
        for S, side in (('Left', 1), ('Right', -1)):
            ul, ll, ft, to = S + 'UpperLeg', S + 'LowerLeg', S + 'Foot', S + 'Toes'
            fk(ul, I3)
            f = c.get('foot' + S) or dict(pos=self.head[ft])
            pole = A['Hips'] @ Vector((side * f.get('knee_out', 0.12), -1, 0))
            mid, end = two_bone(P[ul], Vector(f['pos']), self.len[ul], self.len[ll], pole)
            A[ul] = swing(A['Hips'] @ (self.tail[ul] - self.head[ul]), mid - P[ul]) @ A['Hips']
            A[ll] = swing(A[ul] @ (self.tail[ll] - self.head[ll]), end - mid) @ A[ul]
            A[ft] = rot([('X', f.get('pitch', 0.0)), ('Z', f.get('yaw', 0.0))])
            A[to] = A[ft] @ rot([('X', f.get('toe', 0.0))])
            P[ll], P[ft] = mid, end
        out = {}
        for b in self.order:
            p = self.parent[b]
            Ap = A[p] if p else I3
            O = self.O[b]
            out[b] = (O.transposed() @ Ap.transposed() @ A[b] @ O).to_quaternion()
        loc = self.O['Hips'].transposed() @ Vector(c.get('hips_off', (0, 0, 0)))
        return out, loc, dict(A=A, P=P, st=st)


def bake(arm, name, fn, seconds, loop=True):
    sv = Solver(arm)
    n = int(round(seconds * FPS))
    old = bpy.data.actions.get(name)
    if old is not None:
        bpy.data.actions.remove(old)
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    arm.animation_data_create()
    arm.animation_data.action = act
    prev = {}
    for pb in arm.pose.bones:
        pb.rotation_mode = 'QUATERNION'
    for fr in range(n + 1):
        ph = fr / n
        q, loc, _ = sv.solve(fn(sv, (ph % 1.0) if loop else ph))
        for b, pb in arm.pose.bones.items():
            qq = q[b]
            if b in prev and prev[b].dot(qq) < 0:
                qq.negate()
            prev[b] = qq
            pb.rotation_quaternion = qq
            pb.keyframe_insert('rotation_quaternion', frame=fr)
        hp = arm.pose.bones['Hips']
        hp.location = loc
        hp.keyframe_insert('location', frame=fr)
    act.use_frame_range = True
    act.frame_start, act.frame_end = 0, n
    act['cs_loop'] = bool(loop)
    return act


def pose_at(arm, fn, ph):
    """Apply a single pose (no keys) - used for previews / deformation checks."""
    sv = Solver(arm)
    if arm.animation_data:
        arm.animation_data.action = None
    q, loc, dbg = sv.solve(fn(sv, ph))
    for b, pb in arm.pose.bones.items():
        pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = q[b]
        pb.location = (0, 0, 0)
    arm.pose.bones['Hips'].location = loc
    return dbg


def rest(arm):
    if arm.animation_data:
        arm.animation_data.action = None
    for pb in arm.pose.bones:
        pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.location = (0, 0, 0)


# ============================================================================ building blocks
def stand(sv, c, width=1.0, yaw=7.0):
    for S, side in (('Left', 1), ('Right', -1)):
        r = sv.head[S + 'Foot']
        c['foot' + S] = dict(pos=Vector((r.x * width, r.y, r.z)), yaw=side * yaw)
    return c


def loco(sv, ph, sf, sb, duty, lift, base, bob, sway, yaw, lean, kick, width=1.0, bob_phase=0.0, arms_free=0.0):
    k = sv.k
    c = {}
    for S, side, off in (('Left', 1, 0.0), ('Right', -1, 0.5)):
        p = (ph + off) % 1.0
        r = sv.head[S + 'Foot']
        if p < duty:
            s = p / duty
            y = -sf + (sf + sb) * s
            z = 0.0
            pitch = -14 * (1 - smooth(s / 0.22)) + 30 * smooth((s - 0.62) / 0.38)
            toe = -max(0.0, pitch)
        else:
            s = (p - duty) / (1 - duty)
            e = 0.5 - 0.5 * cos(pi * s)
            y = sb - (sf + sb) * e
            z = lift * sin(pi * s)
            pitch = 30 * (1 - s) + kick * sin(pi * min(1.0, s * 1.5)) - 14 * smooth((s - 0.7) / 0.3)
            toe = -30 * (1 - smooth(s / 0.25))
        z += max(0.0, sin(radians(pitch))) * 0.12
        c['foot' + S] = dict(pos=Vector((r.x * width, r.y + y * k, r.z + z * k)), pitch=pitch, toe=toe, knee_out=0.08)
    cy = cos(2 * pi * ph)
    c['hips_off'] = Vector((sway * sin(2 * pi * ph), 0, base - bob * cos(4 * pi * (ph - bob_phase)))) * k
    c['Hips'] = [('X', lean[0]), ('Z', -yaw * cy)]
    c['Spine'] = [('X', lean[1]), ('Z', yaw * 0.9 * cy)]
    c['Chest'] = [('X', lean[2]), ('Z', yaw * 0.9 * cy)]
    c['Neck'] = [('X', -sum(lean) * 0.3), ('Z', -yaw * 0.4 * cy)]
    c['Head'] = [('X', -sum(lean) * 0.3), ('Z', -yaw * 0.4 * cy)]
    return c


def hang(side, swing_deg=0.0, bend=0.50, out=0.05):
    a = radians(swing_deg)
    v = Vector((side * out, -sin(a) * bend, -cos(a) * bend))
    return ('at', v, v.normalized(), (-side, 0.15, 0), (side * 0.25, 1, 0.1), 'shoulder')


def arms_rifle(sv, c, grip=(-0.16, -0.17, -0.06), fwd=(0.62, -0.70, -0.35), up=(-0.12, 0, 1), bobz=0.0):
    k = sv.k
    g = Vector(grip) * k + Vector((0, 0, bobz * k))
    c['armRight'] = ('weapon', g, fwd, up, (-1, 0.6, -0.5))
    c['armLeft'] = ('support', (1, 0.3, -0.8))
    return c


def carry_spec(sv, side, raise_=0.0):
    k = sv.k
    return ('at', Vector((side * 0.23 * k, -0.02 * k, 2.02 + raise_)), (-side * 0.35, 0.94, 0), (0, 0, 1),
            (side, -0.45, -0.15), 'world')


def arms_carry(sv, c, amount=1.0):
    for S, side in (('Left', 1), ('Right', -1)):
        c['arm' + S] = carry_spec(sv, side)
        c[S + 'Shoulder'] = [('Y', -side * 18 * amount)]
    return c


# ============================================================================ clips (Navy rifle set = shared core)
def idle(sv, ph):
    c = stand(sv, {}, 1.12)
    br = sin(4 * pi * ph)
    c['hips_off'] = Vector((0.006 * sin(2 * pi * ph), 0, -0.014 + 0.004 * br)) * sv.k
    c['Hips'] = [('X', 1.0)]
    c['Spine'] = [('X', 1.0 + 0.6 * br)]
    c['Chest'] = [('X', 1.0 + 0.8 * br)]
    c['Head'] = [('X', -2.0), ('Z', 9 * sin(2 * pi * ph))]
    return arms_rifle(sv, c, bobz=0.004 * br)


def walk(sv, ph):
    c = loco(sv, ph, 0.30, 0.30, 0.60, 0.10, -0.050, 0.020, 0.020, 6.0, (1.5, 1.5, 1.0), 12.0)
    return arms_rifle(sv, c, bobz=0.006 * cos(4 * pi * ph))


def run(sv, ph):
    c = loco(sv, ph, 0.26, 0.44, 0.38, 0.20, -0.085, 0.035, 0.012, 10.0, (6.0, 5.0, 4.0), 48.0, bob_phase=0.19)
    return arms_rifle(sv, c, grip=(-0.15, -0.20, 0.0), fwd=(0.72, -0.50, 0.42), up=(-0.25, -0.2, 1),
                      bobz=0.015 * cos(4 * pi * ph))


def carry_idle(sv, ph):
    c = stand(sv, {}, 1.35, 10.0)
    br = sin(4 * pi * ph)
    c['hips_off'] = Vector((0.008 * sin(2 * pi * ph), 0, -0.022 + 0.004 * br)) * sv.k
    c['Spine'] = [('X', -1.5)]
    c['Chest'] = [('X', -2.0 + 0.6 * br)]
    c['Head'] = [('X', 6.0), ('Z', 5 * sin(2 * pi * ph))]
    return arms_carry(sv, c)


def carry_walk(sv, ph):
    c = loco(sv, ph, 0.25, 0.25, 0.63, 0.08, -0.060, 0.014, 0.026, 3.0, (0.0, -1.0, -1.5), 8.0, width=1.25)
    c['Head'] = [('X', 6.0)]
    return arms_carry(sv, c)


def carry_run(sv, ph):
    c = loco(sv, ph, 0.24, 0.36, 0.42, 0.16, -0.085, 0.022, 0.018, 5.0, (3.0, 1.0, -1.0), 36.0, width=1.2,
             bob_phase=0.21)
    c['Head'] = [('X', 4.0)]
    return arms_carry(sv, c)


def reach_spec(sv, side, y=-0.38, z=1.05):
    k = sv.k
    return ('at', Vector((side * 0.27, y, z)) * k, (0, -1, 0.25), (-side, 0, 0), (side, 0.7, -0.4), 'world')


def pickup(sv, ph):
    c = stand(sv, {}, 1.35, 10.0)
    dip = sin(pi * clamp01(ph / 0.8))
    a = smooth(ph / 0.22)
    b = smooth((ph - 0.2) / 0.62)
    c['hips_off'] = Vector((0, 0.03 * dip, -0.022 - 0.15 * dip)) * sv.k
    c['Hips'] = [('X', 12 * dip)]
    c['Spine'] = [('X', 8 * dip - 1.5 * b)]
    c['Chest'] = [('X', 6 * dip - 2.0 * b)]
    c['Head'] = [('X', -10 * dip * (1 - b) + 6 * b)]
    for S, side in (('Left', 1), ('Right', -1)):
        c['arm' + S] = ('mix', ('mix', hang(side), reach_spec(sv, side), a), carry_spec(sv, side), b)
        c[S + 'Shoulder'] = [('Y', -side * 18 * b)]
    return c


def drop(sv, ph):
    c = stand(sv, {}, 1.35, 10.0)
    dip = sin(pi * clamp01(ph / 0.85))
    a = smooth(ph / 0.5)
    b = smooth((ph - 0.6) / 0.4)
    c['hips_off'] = Vector((0, 0.03 * dip, -0.022 - 0.20 * dip)) * sv.k
    c['Hips'] = [('X', 16 * dip)]
    c['Spine'] = [('X', 9 * dip - 1.5 * (1 - a))]
    c['Chest'] = [('X', 7 * dip - 2.0 * (1 - a))]
    c['Head'] = [('X', 6 * (1 - a) - 8 * dip)]
    for S, side in (('Left', 1), ('Right', -1)):
        c['arm' + S] = ('mix', ('mix', carry_spec(sv, side), reach_spec(sv, side, -0.44, 0.80), a), hang(side), b)
        c[S + 'Shoulder'] = [('Y', -side * 18 * (1 - a))]
    return c


CORE = [('Idle', idle, 3.0, True), ('Walk', walk, 1.0, True), ('Run', run, 0.6667, True),
        ('CarryIdle', carry_idle, 3.0, True), ('CarryWalk', carry_walk, 1.0667, True),
        ('CarryRun', carry_run, 0.7333, True), ('Pickup', pickup, 0.6, False), ('Drop', drop, 0.7, False)]


def bake_set(arm, clips, prefix=''):
    acts = []
    for name, fn, secs, loop in clips:
        acts.append(bake(arm, prefix + name, fn, secs, loop))
    rest(arm)
    return acts



# ============================================================================ Gate B: weapon sets + action clips
def arms_pistol(sv, c, sw=0.0, bobz=0.0):
    """One-handed pistol held low at the right side, left arm free."""
    k = sv.k
    c['armRight'] = ('weapon', Vector((-0.235, -0.10 - 0.10 * sw, -0.30 + bobz)) * k, (0.10, -0.62, -0.78), (0, -0.78, 0.62),
                     (-0.6, 1, 0.1))
    c['armLeft'] = hang(1, -22 * sw + 4, 0.50, 0.06)
    return c


def arms_stun(sv, c, bobz=0.0, **_):
    """Stun device held two-handed close to the body centre."""
    k = sv.k
    c['armRight'] = ('weapon', Vector((-0.07, -0.24, -0.11 + bobz)) * k, (0.22, -0.90, -0.30), (-0.05, 0, 1), (-1, 0.5, -0.6))
    c['armLeft'] = ('support', (1, 0.3, -0.8), Vector((0, 0.18, -0.014)) * k)
    return c


def aim_pose(sv, c, ph, kind, recoil=0.0):
    """Shared firing stance. recoil 0..1 pushes the weapon back / muzzle up."""
    k = sv.k
    L, Rr = sv.head['LeftFoot'], sv.head['RightFoot']
    c['footLeft'] = dict(pos=Vector((L.x * 1.25, L.y - 0.13 * k, L.z)), yaw=4)
    c['footRight'] = dict(pos=Vector((Rr.x * 1.35, Rr.y + 0.12 * k, Rr.z)), yaw=-28)
    br = sin(2 * pi * ph)
    c['hips_off'] = Vector((0, -0.01 + 0.02 * recoil, -0.045 + 0.003 * br)) * k
    c['Hips'] = [('X', 4.0), ('Z', -10.0)]
    c['Spine'] = [('X', 4.0 - 3 * recoil), ('Z', -4.0)]
    c['Chest'] = [('X', 4.0 - 4 * recoil), ('Z', -4.0)]
    c['Neck'] = [('Z', 8.0)]
    c['Head'] = [('X', -4.0), ('Z', 10.0)]
    # chest is yawed ~-18 deg: aim vector expressed in chest space so the weapon points along world -Y
    fy = Vector((0.30, -0.95, 0.02 + 0.10 * recoil))
    if kind == 'rifle':
        c['armRight'] = ('weapon', Vector((-0.115, -0.20 + 0.035 * recoil, 0.135 + 0.012 * recoil)) * k, fy, (0, 0, 1), (-1, 0.3, -0.7))
        c['armLeft'] = ('support', (1, 0.1, -1.0))
    elif kind == 'pistol':
        c['armRight'] = ('weapon', Vector((-0.02, -0.50 + 0.05 * recoil, 0.17 + 0.03 * recoil)) * k,
                         Vector((0.30, -0.95, 0.02 + 0.22 * recoil)), (0, 0, 1), (-1, 0.2, -0.8))
        c['armLeft'] = ('support', (1, 0.1, -1.0), Vector((-0.012, 0.0, 0.0)) * k)
    else:  # stun device: short thrust forward, centred
        c['armRight'] = ('weapon', Vector((0.03, -0.40 + 0.05 * recoil, 0.04 + 0.01 * recoil)) * k, fy, (0, 0, 1), (-1, 0.3, -0.7))
        c['armLeft'] = ('support', (1, 0.1, -1.0), Vector((0, 0.18, -0.014)) * k)
    return c


def make_set(prefix, arms, kind, walk_bob=0.006):
    def _idle(sv, ph):
        c = stand(sv, {}, 1.12)
        br = sin(4 * pi * ph)
        c['hips_off'] = Vector((0.006 * sin(2 * pi * ph), 0, -0.014 + 0.004 * br)) * sv.k
        c['Hips'] = [('X', 1.0)]
        c['Spine'] = [('X', 1.0 + 0.6 * br)]
        c['Chest'] = [('X', 1.0 + 0.8 * br)]
        c['Head'] = [('X', -2.0), ('Z', 9 * sin(2 * pi * ph))]
        return arms(sv, c, bobz=0.004 * br, sw=0.0) if kind != 'rifle' else arms(sv, c, bobz=0.004 * br)

    def _walk(sv, ph):
        c = loco(sv, ph, 0.30, 0.30, 0.60, 0.10, -0.050, 0.020, 0.020, 6.0, (1.5, 1.5, 1.0), 12.0)
        b = walk_bob * cos(4 * pi * ph)
        return arms(sv, c, bobz=b, sw=cos(2 * pi * ph)) if kind != 'rifle' else arms(sv, c, bobz=b)

    def _run(sv, ph):
        c = loco(sv, ph, 0.30, 0.50, 0.38, 0.21, -0.085, 0.035, 0.012, 10.0, (6.0, 5.0, 4.0), 48.0, bob_phase=0.19)
        b = 0.015 * cos(4 * pi * ph)
        if kind == 'rifle':
            return arms(sv, c, grip=(-0.15, -0.20, 0.0), fwd=(0.72, -0.50, 0.42), up=(-0.25, -0.2, 1), bobz=b)
        return arms(sv, c, bobz=b, sw=1.6 * cos(2 * pi * ph))

    def _aim(sv, ph):
        return aim_pose(sv, {}, ph, kind)

    def _shoot(sv, ph):
        if kind == 'stun':   # prepare -> emit -> recover
            r = (-0.6 * smooth(ph / 0.25) * (1 - smooth((ph - 0.25) / 0.12))
                 + 1.0 * smooth((ph - 0.28) / 0.10) * (1 - smooth((ph - 0.45) / 0.5)))
        else:
            r = smooth(ph / 0.12) * (1 - smooth((ph - 0.12) / 0.7))
        return aim_pose(sv, {}, 0.0, kind, r)

    def _hit(sv, ph):
        f = smooth(ph / 0.25) * (1 - smooth((ph - 0.3) / 0.7))
        c = stand(sv, {}, 1.12)
        L = sv.head['RightFoot']
        c['footRight'] = dict(pos=Vector((L.x * 1.2, L.y + 0.16 * f * sv.k, L.z)), yaw=-12)
        c['hips_off'] = Vector((0.02 * f, 0.07 * f, -0.014 - 0.05 * f)) * sv.k
        c['Hips'] = [('X', 1.0 - 9 * f), ('Z', 8 * f)]
        c['Spine'] = [('X', 1.0 - 8 * f)]
        c['Chest'] = [('X', 1.0 - 7 * f), ('Z', 10 * f)]
        c['Head'] = [('X', 12 * f - 2.0), ('Z', -8 * f)]
        return arms(sv, c, bobz=-0.03 * f, sw=0.0) if kind != 'rifle' else arms(sv, c, bobz=-0.03 * f)

    P = prefix
    return [(P + 'Idle', _idle, 3.0, True), (P + 'Walk', _walk, 1.0, True), (P + 'Run', _run, 0.6667, True),
            (P + 'Aim', _aim, 2.0, True), (P + 'Shoot', _shoot, 0.8 if kind == 'stun' else 0.4, False),
            (P + 'Hit', _hit, 0.5, False)]


def regrip(sv, ph):
    c = stand(sv, {}, 1.35, 10.0)
    d = sin(pi * clamp01(ph / 0.35))
    up = sin(pi * clamp01((ph - 0.25) / 0.5))
    c['hips_off'] = Vector((0, 0, -0.022 - 0.07 * d + 0.012 * up)) * sv.k
    c['Spine'] = [('X', -1.5 + 4 * d)]
    c['Chest'] = [('X', -2.0 + 3 * d - 3 * up)]
    c['Head'] = [('X', 6.0 - 10 * up)]
    for S, side in (('Left', 1), ('Right', -1)):
        c['arm' + S] = carry_spec(sv, side, -0.05 * d + 0.03 * up)
        c[S + 'Shoulder'] = [('Y', -side * (18 + 8 * up))]
    return c


def cargo_slip(sv, ph):
    a = smooth(ph / 0.45)
    b = smooth((ph - 0.5) / 0.5)
    lurch = sin(pi * clamp01(ph / 0.9))
    c = stand(sv, {}, 1.35, 10.0)
    L = sv.head['LeftFoot']
    c['footLeft'] = dict(pos=Vector((L.x * 1.35, L.y - 0.22 * lurch * sv.k, L.z + 0.03 * sin(pi * clamp01(ph / 0.3)) * sv.k)), yaw=10)
    c['hips_off'] = Vector((0.03 * lurch, -0.08 * lurch, -0.022 - 0.10 * lurch)) * sv.k
    c['Hips'] = [('X', 14 * lurch), ('Z', -8 * lurch)]
    c['Spine'] = [('X', 10 * lurch)]
    c['Chest'] = [('X', 10 * lurch), ('Z', 10 * lurch)]
    c['Head'] = [('X', 6 * (1 - a) - 14 * lurch)]
    for S, side in (('Left', 1), ('Right', -1)):
        flail = ('at', Vector((side * (0.38 + 0.1 * side), -0.36, 1.25 - 0.25 * side)) * sv.k, (0, -1, 0.2), (0, 0, -1),
                 (side, 0.6, -0.3), 'world')
        c['arm' + S] = ('mix', ('mix', carry_spec(sv, side), flail, a), hang(side, 6), b)
        c[S + 'Shoulder'] = [('Y', -side * 18 * (1 - a))]
    return c


def death(sv, ph):
    a = smooth(ph / 0.75)           # collapse
    s = smooth((ph - 0.05) / 0.4)   # knees buckle first
    k = sv.k
    c = {}
    for S, side in (('Left', 1), ('Right', -1)):
        r = sv.head[S + 'Foot']
        c['foot' + S] = dict(pos=Vector((r.x * (1.2 + 0.5 * a), r.y - (0.10 + 0.08 * side) * a * k, r.z + 0.01 * a)),
                             pitch=-35 * a, yaw=side * (7 + 25 * a), knee_out=0.12 + 0.5 * a)
        c['arm' + S] = ('mix', hang(side, 0), ('at', Vector((side * (0.50 + 0.05 * side), 0.55, 0.10)) * k,
                                                 (side * 0.8, 0.5, -0.1), (0, 0, -1), (side, 0, -1), 'world'), a)
    c['hips_off'] = Vector((0, 0.42 * a, -0.30 * s * (1 - a) - 0.80 * a)) * k
    c['Hips'] = [('X', 10 * s * (1 - a) - 78 * a)]
    c['Spine'] = [('X', 8 * s * (1 - a) - 6 * a)]
    c['Chest'] = [('X', 8 * s * (1 - a) - 4 * a)]
    c['Neck'] = [('X', -6 * a)]
    c['Head'] = [('X', 14 * s * (1 - a) - 6 * a), ('Z', 14 * a)]
    return c


# ============================================================================ front carry (prototype)
# A 0.60 x 0.45 x 0.45 m crate centred on the Cargo_Front socket (0, -0.45, 1.10). Wrists sit just outside the
# side faces, fingers forward along the crate. The same grip offsets are used by FrontCarryPrototype.cs.
FRONT_GRIP = (0.33, -0.30, 1.06)


def front_spec(sv, side, dz=0.0, dy=0.0):
    k = sv.k
    gx, gy, gz = FRONT_GRIP
    return ('at', Vector((side * gx * k, (gy + dy) * k, gz * k + dz)), (0, -1, -0.22), (-side, 0, 0),
            (side * 0.7, 0.8, -0.7), 'world')


def arms_front(sv, c, amount=1.0, dz=0.0):
    for S, side in (('Left', 1), ('Right', -1)):
        c['arm' + S] = front_spec(sv, side, dz)
        c[S + 'Shoulder'] = [('Z', -side * 10 * amount)]
    return c


def front_carry_idle(sv, ph):
    c = stand(sv, {}, 1.2, 9.0)
    br = sin(4 * pi * ph)
    c['hips_off'] = Vector((0.006 * sin(2 * pi * ph), 0.01, -0.020 + 0.004 * br)) * sv.k
    c['Spine'] = [('X', -3.0)]
    c['Chest'] = [('X', -3.0 + 0.6 * br)]
    c['Head'] = [('X', 3.0), ('Z', 6 * sin(2 * pi * ph))]
    return arms_front(sv, c)


def front_carry_walk(sv, ph):
    c = loco(sv, ph, 0.24, 0.24, 0.63, 0.08, -0.055, 0.014, 0.022, 2.5, (-1.0, -2.0, -2.5), 8.0, width=1.15)
    c['Head'] = [('X', 3.0)]
    return arms_front(sv, c)


def front_carry_run(sv, ph):
    c = loco(sv, ph, 0.24, 0.34, 0.42, 0.15, -0.080, 0.020, 0.015, 4.0, (1.0, -1.0, -2.0), 34.0, width=1.1,
             bob_phase=0.21)
    c['Head'] = [('X', 2.0)]
    return arms_front(sv, c)


def front_reach(sv, side, y=-0.42, z=0.62):
    k = sv.k
    return ('at', Vector((side * 0.33, y, z)) * k, (0, -0.8, -0.6), (-side, 0, 0), (side, 0.7, 0.2), 'world')


def front_pickup(sv, ph):
    c = stand(sv, {}, 1.3, 10.0)
    dip = sin(pi * clamp01(ph / 0.8))
    a = smooth(ph / 0.25)
    b = smooth((ph - 0.25) / 0.6)
    c['hips_off'] = Vector((0, 0.06 * dip, -0.020 - 0.26 * dip)) * sv.k
    c['Hips'] = [('X', 22 * dip)]
    c['Spine'] = [('X', 12 * dip - 3.0 * b)]
    c['Chest'] = [('X', 8 * dip - 3.0 * b)]
    c['Head'] = [('X', -12 * dip * (1 - b) + 3 * b)]
    for S, side in (('Left', 1), ('Right', -1)):
        c['arm' + S] = ('mix', ('mix', hang(side), front_reach(sv, side), a), front_spec(sv, side), b)
        c[S + 'Shoulder'] = [('Z', -side * 10 * b)]
    return c


def front_drop(sv, ph):
    c = stand(sv, {}, 1.3, 10.0)
    dip = sin(pi * clamp01(ph / 0.85))
    a = smooth(ph / 0.5)
    b = smooth((ph - 0.6) / 0.4)
    c['hips_off'] = Vector((0, 0.06 * dip, -0.020 - 0.26 * dip)) * sv.k
    c['Hips'] = [('X', 22 * dip)]
    c['Spine'] = [('X', 12 * dip - 3.0 * (1 - a))]
    c['Chest'] = [('X', 8 * dip - 3.0 * (1 - a))]
    c['Head'] = [('X', 3 * (1 - a) - 10 * dip)]
    for S, side in (('Left', 1), ('Right', -1)):
        c['arm' + S] = ('mix', ('mix', front_spec(sv, side), front_reach(sv, side, -0.46, 0.55), a), hang(side), b)
        c[S + 'Shoulder'] = [('Z', -side * 10 * (1 - a))]
    return c


def front_regrip(sv, ph):
    c = stand(sv, {}, 1.2, 9.0)
    d = sin(pi * clamp01(ph / 0.35))
    up = sin(pi * clamp01((ph - 0.25) / 0.5))
    c['hips_off'] = Vector((0, 0.01, -0.020 - 0.06 * d + 0.010 * up)) * sv.k
    c['Spine'] = [('X', -3.0 + 5 * d - 2 * up)]
    c['Chest'] = [('X', -3.0 + 4 * d - 3 * up)]
    c['Head'] = [('X', 3.0 - 8 * d)]
    return arms_front(sv, c, dz=-0.06 * d + 0.04 * up)


def front_cargo_slip(sv, ph):
    a = smooth(ph / 0.45)
    b = smooth((ph - 0.5) / 0.5)
    lurch = sin(pi * clamp01(ph / 0.9))
    c = stand(sv, {}, 1.3, 10.0)
    L = sv.head['LeftFoot']
    c['footLeft'] = dict(pos=Vector((L.x * 1.3, L.y - 0.22 * lurch * sv.k, L.z + 0.03 * sin(pi * clamp01(ph / 0.3)) * sv.k)), yaw=10)
    c['hips_off'] = Vector((0.03 * lurch, -0.08 * lurch, -0.020 - 0.12 * lurch)) * sv.k
    c['Hips'] = [('X', 16 * lurch), ('Z', -8 * lurch)]
    c['Spine'] = [('X', 12 * lurch - 3 * (1 - a))]
    c['Chest'] = [('X', 10 * lurch - 3 * (1 - a)), ('Z', 10 * lurch)]
    c['Head'] = [('X', 3 * (1 - a) - 14 * lurch)]
    for S, side in (('Left', 1), ('Right', -1)):
        grab = ('at', Vector((side * (0.34 + 0.06 * side), -0.44, 0.70 - 0.12 * side)) * sv.k, (0, -0.7, -0.7), (-side, 0, 0),
                (side, 0.6, 0.1), 'world')
        c['arm' + S] = ('mix', ('mix', front_spec(sv, side), grab, a), hang(side, 6), b)
        c[S + 'Shoulder'] = [('Z', -side * 10 * (1 - a))]
    return c


FRONT_CARRY = [('FrontCarryIdle', front_carry_idle, 3.0, True), ('FrontCarryWalk', front_carry_walk, 1.0667, True),
               ('FrontCarryRun', front_carry_run, 0.7333, True), ('FrontPickup', front_pickup, 0.6, False),
               ('FrontDrop', front_drop, 0.7, False), ('FrontRegrip', front_regrip, 0.8, False),
               ('FrontCargoSlip', front_cargo_slip, 0.9, False)]

SHARED_ACTIONS = [('Regrip', regrip, 0.8, False), ('CargoSlip', cargo_slip, 0.9, False), ('Death', death, 1.3, False)]
CARRY = [c for c in CORE if c[0].startswith('Carry') or c[0] in ('Pickup', 'Drop')]
ALL_CLIPS = (make_set('', arms_rifle, 'rifle') + CARRY + FRONT_CARRY + SHARED_ACTIONS +
             make_set('Pistol_', arms_pistol, 'pistol') + make_set('Stun_', arms_stun, 'stun'))
