"""Cargo Squad - shared Blender build helpers (Blender 5.x).

Conventions: metres, Z up, characters face -Y, character-left = +X, feet at Z=0.
Exported with FBX axis_forward='Z', axis_up='Y' so Unity sees Y up / +Z forward.
"""
import bpy, bmesh, math
from math import sin, cos, pi, radians
from mathutils import Vector, Matrix, Euler

REPO = r"C:\kfupm\T261\SWE402\SWE402-project-CargoSquad"


def _lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def hexcol(h):
    h = h.lstrip('#')
    return tuple(_lin(int(h[i:i + 2], 16) / 255) for i in (0, 2, 4)) + (1.0,)


MATERIAL_LIB = {}


def material(name, hexc, rough=0.8, metal=0.0, emit=None):
    MATERIAL_LIB[name] = dict(name=name, hex='#' + hexc.lstrip('#').upper(), rough=rough, metal=metal,
                              emit=('#' + emit.lstrip('#').upper()) if emit else '')
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    if m.node_tree is None:
        m.use_nodes = True
    b = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    c = hexcol(hexc)
    if b:
        b.inputs['Base Color'].default_value = c
        b.inputs['Roughness'].default_value = rough
        b.inputs['Metallic'].default_value = metal
        if emit:
            b.inputs['Emission Color'].default_value = hexcol(emit)
            b.inputs['Emission Strength'].default_value = 1.0
    m.diffuse_color = c
    m.roughness = rough
    m.metallic = metal
    return m


def get_scene(name, clear=True):
    sc = bpy.data.scenes.get(name) or bpy.data.scenes.new(name)
    if clear:
        for ob in list(sc.collection.all_objects):
            data = ob.data
            bpy.data.objects.remove(ob, do_unlink=True)
            if data is not None and data.users == 0:
                if isinstance(data, bpy.types.Mesh):
                    bpy.data.meshes.remove(data)
                elif isinstance(data, bpy.types.Armature):
                    bpy.data.armatures.remove(data)
    sc.render.fps = 30
    sc.unit_settings.system = 'METRIC'
    sc.unit_settings.scale_length = 1.0
    return sc


def ctx(scene, active=None, selected=None):
    win = bpy.context.window_manager.windows[0]
    if win.scene != scene:
        win.scene = scene
    d = dict(window=win, screen=win.screen, scene=scene, view_layer=scene.view_layers[0])
    area = next((a for a in win.screen.areas if a.type == 'VIEW_3D'), None)
    if area:
        d['area'] = area
        d['region'] = next(r for r in area.regions if r.type == 'WINDOW')
    if active is not None:
        d['active_object'] = active
        d['object'] = active
    if selected is not None:
        d['selected_objects'] = selected
        d['selected_editable_objects'] = selected
    return bpy.context.temp_override(**d)


def V(x, y=None, z=None):
    return Vector(x) if y is None else Vector((x, y, z))


def rotm(rot):
    """rot: None | Matrix | (rx,ry,rz) degrees XYZ euler."""
    if rot is None:
        return Matrix.Identity(4)
    if isinstance(rot, Matrix):
        return rot.to_4x4()
    return Euler([radians(a) for a in rot], 'XYZ').to_matrix().to_4x4()


def R(c, rx, ry=None, w=None, rf=None, rb=None, u=(1, 0, 0), v=(0, 1, 0), p=2.0):
    """Tube ring. v is 'back' for torso-like rings (rf = front radius, rb = back radius)."""
    ry = rx if ry is None else ry
    return dict(c=Vector(c), rx=rx, rf=ry if rf is None else rf, rb=ry if rb is None else rb,
                w=w, u=Vector(u), v=Vector(v), p=p)


class Builder:
    """Accumulates primitives into one mesh with per-vertex bone weights and material slots."""

    def __init__(self):
        self.bm = bmesh.new()
        self.dl = self.bm.verts.layers.deform.verify()
        self.groups = []
        self.mats = []

    def gi(self, bone):
        if bone not in self.groups:
            self.groups.append(bone)
        return self.groups.index(bone)

    def mi(self, mat):
        if mat not in self.mats:
            self.mats.append(mat)
        return self.mats.index(mat)

    def _setw(self, v, w):
        if w is None:
            return
        if callable(w):
            w = w(v.co)
        if isinstance(w, str):
            w = {w: 1.0}
        tot = sum(w.values())
        for b, x in w.items():
            if x > 0:
                v[self.dl][self.gi(b)] = x / tot

    def tube(self, rings, mat, seg=14, cap0=True, cap1=True):
        bm, mi = self.bm, self.mi(mat)
        loops = []
        for r in rings:
            e = 2.0 / r['p']
            vs = []
            for i in range(seg):
                a = 2 * pi * i / seg
                ca, sa = cos(a), sin(a)
                x = math.copysign(abs(ca) ** e, ca) * r['rx']
                y = math.copysign(abs(sa) ** e, sa) * (r['rb'] if sa >= 0 else r['rf'])
                v = bm.verts.new(r['c'] + r['u'] * x + r['v'] * y)
                self._setw(v, r['w'])
                vs.append(v)
            loops.append(vs)
        for j in range(len(loops) - 1):
            a, b = loops[j], loops[j + 1]
            rc = (rings[j]['c'] + rings[j + 1]['c']) * 0.5
            for i in range(seg):
                f = bm.faces.new((a[i], a[(i + 1) % seg], b[(i + 1) % seg], b[i]))
                f.material_index = mi
                f.smooth = True
                f.normal_update()
                if f.normal.dot(f.calc_center_median() - rc) < 0:
                    f.normal_flip()
        for cap, vs, c_in, c_out in ((cap0, loops[0], rings[1]['c'], rings[0]['c']),
                                     (cap1, loops[-1], rings[-2]['c'], rings[-1]['c'])):
            if cap:
                f = bm.faces.new(vs)
                f.material_index = mi
                f.smooth = True
                f.normal_update()
                d = c_out - c_in
                if d.length > 1e-6 and f.normal.dot(d) < 0:
                    f.normal_flip()

    def _merge(self, t, M, w, mat):
        bm, mi = self.bm, self.mi(mat)
        mp = {}
        flip = M.determinant() < 0
        for v in t.verts:
            nv = bm.verts.new(M @ v.co)
            self._setw(nv, w)
            mp[v] = nv
        for f in t.faces:
            vs = [mp[v] for v in f.verts]
            if flip:
                vs.reverse()
            try:
                nf = bm.faces.new(vs)
            except ValueError:
                continue
            nf.material_index = mi
            nf.smooth = True
        t.free()

    def box(self, c, size, w, mat, rot=None, bevel=0.012, taper=1.0, taper_y=None, M=None):
        t = bmesh.new()
        bmesh.ops.create_cube(t, size=1.0)
        ty = taper if taper_y is None else taper_y
        for v in t.verts:
            top = v.co.z > 0
            v.co.x *= size[0] * (taper if top else 1.0)
            v.co.y *= size[1] * (ty if top else 1.0)
            v.co.z *= size[2]
        if bevel > 0:
            bevel = min(bevel, min(size) * 0.45)
            bmesh.ops.bevel(t, geom=t.edges[:], offset=bevel, offset_type='OFFSET', segments=1,
                            profile=0.5, affect='EDGES', clamp_overlap=True)
        m = Matrix.Translation(Vector(c)) @ rotm(rot)
        if M is not None:
            m = M @ m
        self._merge(t, m, w, mat)

    def cyl(self, p0, p1, r, w, mat, r2=None, seg=10, M=None):
        p0, p1 = Vector(p0), Vector(p1)
        d = p1 - p0
        t = bmesh.new()
        bmesh.ops.create_cone(t, cap_ends=True, cap_tris=False, segments=seg, radius1=r,
                              radius2=r if r2 is None else r2, depth=d.length)
        m = Matrix.Translation((p0 + p1) * 0.5) @ Vector((0, 0, 1)).rotation_difference(d.normalized()).to_matrix().to_4x4()
        if M is not None:
            m = M @ m
        self._merge(t, m, w, mat)

    def sphere(self, c, radii, w, mat, useg=14, vseg=9, cut=None, rot=None, M=None):
        """cut: callable(co_unit)->True to delete vertex (in unit-sphere coords)."""
        t = bmesh.new()
        bmesh.ops.create_uvsphere(t, u_segments=useg, v_segments=vseg, radius=1.0)
        if cut:
            bmesh.ops.delete(t, geom=[v for v in t.verts if cut(v.co)], context='VERTS')
        if isinstance(radii, (int, float)):
            radii = (radii, radii, radii)
        m = Matrix.Translation(Vector(c)) @ rotm(rot) @ Matrix.Diagonal((radii[0], radii[1], radii[2], 1.0))
        if M is not None:
            m = M @ m
        self._merge(t, m, w, mat)

    def to_object(self, name, coll, arm=None, sharp_angle=38.0):
        me = bpy.data.meshes.get(name)
        if me is not None and me.users == 0:
            bpy.data.meshes.remove(me)
        me = bpy.data.meshes.new(name)
        self.bm.normal_update()
        self.bm.to_mesh(me)
        self.bm.free()
        for m in self.mats:
            me.materials.append(m)
        try:
            me.set_sharp_from_angle(angle=radians(sharp_angle))
        except Exception as e:  # pragma: no cover
            print("set_sharp_from_angle failed:", e)
        ob = bpy.data.objects.new(name, me)
        coll.objects.link(ob)
        for g in self.groups:
            ob.vertex_groups.new(name=g)
        if arm is not None:
            ob.parent = arm
            mod = ob.modifiers.new("Armature", 'ARMATURE')
            mod.object = arm
        return ob


def build_rig(name, J, scene):
    """J: ordered dict bone -> (head, tail, parent)."""
    old = bpy.data.armatures.get(name)
    if old is not None and old.users == 0:
        bpy.data.armatures.remove(old)
    ad = bpy.data.armatures.new(name)
    ob = bpy.data.objects.new(name, ad)
    scene.collection.objects.link(ob)
    scene.view_layers[0].objects.active = ob
    with ctx(scene, ob, [ob]):
        bpy.ops.object.mode_set(mode='EDIT')
        for b, (h, t, p) in J.items():
            eb = ad.edit_bones.new(b)
            eb.head, eb.tail, eb.roll = h, t, 0.0
            if p:
                eb.parent = ad.edit_bones[p]
        bpy.ops.object.mode_set(mode='OBJECT')
    ad.display_type = 'STICK'
    ob.show_in_front = True
    return ob


def socket(name, arm, bone, pos, rot3=None, scene=None, size=0.05):
    """Empty parented to a bone (or the rig object when bone is None); pos/rot in armature space."""
    e = bpy.data.objects.new(name, None)
    e['cs_name'] = name
    e.empty_display_type = 'ARROWS'
    e.empty_display_size = size
    scene.collection.objects.link(e)
    e.parent = arm
    M = Matrix.Translation(Vector(pos)) @ (rot3.to_4x4() if rot3 is not None else Matrix.Identity(4))
    if bone:
        e.parent_type = 'BONE'
        e.parent_bone = bone
        b = arm.data.bones[bone]
        pm = b.matrix_local @ Matrix.Translation((0, b.length, 0))
        e.matrix_parent_inverse = Matrix.Identity(4)
        e.matrix_basis = pm.inverted() @ M
    else:
        e.matrix_parent_inverse = Matrix.Identity(4)
        e.matrix_basis = M
    return e


def attach(ob, parent_empty):
    ob.parent = parent_empty
    ob.matrix_parent_inverse = Matrix.Identity(4)
    ob.matrix_basis = Matrix.Identity(4)


def export_fbx(scene, path, objects, anim=False):
    import os
    os.makedirs(os.path.dirname(path), exist_ok=True)
    vl = scene.view_layers[0]
    for o in scene.objects:
        o.select_set(o in objects, view_layer=vl)
    arm = next((o for o in objects if o.type == 'ARMATURE'), None)
    vl.objects.active = arm or objects[0]
    with ctx(scene, vl.objects.active, list(objects)):
        return bpy.ops.export_scene.fbx(
            filepath=path, use_selection=True, object_types={'ARMATURE', 'MESH', 'EMPTY'},
            apply_scale_options='FBX_SCALE_ALL', axis_forward='Z', axis_up='Y',
            use_mesh_modifiers=True, mesh_smooth_type='FACE', use_triangles=True,
            add_leaf_bones=False, use_armature_deform_only=False, primary_bone_axis='Y', secondary_bone_axis='X',
            bake_anim=anim, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
            bake_anim_use_all_actions=anim, bake_anim_force_startend_keying=True, bake_anim_step=1.0,
            bake_anim_simplify_factor=0.0, path_mode='AUTO')


def save_blend(path, scenes, extra=()):
    import os
    os.makedirs(os.path.dirname(path), exist_ok=True)
    ids = set(scenes) | set(extra)
    bpy.data.libraries.write(path, ids, fake_user=True)
    return os.path.getsize(path)


def mesh_report(ob, arm=None):
    me = ob.data
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    unw = 0
    bad = 0
    if arm is not None:
        names = {g.index: g.name for g in ob.vertex_groups}
        bones = set(arm.data.bones.keys())
        for v in me.vertices:
            ws = [g.weight for g in v.groups if names.get(g.group) in bones and g.weight > 0]
            if not ws:
                unw += 1
            elif abs(sum(ws) - 1.0) > 0.01:
                bad += 1
    zs = [v.co.z for v in me.vertices]
    return dict(name=ob.name, verts=len(me.vertices), tris=tris, mats=len(me.materials), unweighted=unw,
                unnormalized=bad, zmin=round(min(zs), 4), zmax=round(max(zs), 4))


def write_material_lib(path):
    """Palette manifest consumed by the Unity pipeline (it creates URP/Lit materials with these sRGB colours)."""
    import os, json
    os.makedirs(os.path.dirname(path), exist_ok=True)
    data = {}
    if os.path.exists(path):
        try:
            with open(path) as f:
                data = {m['name']: m for m in json.load(f)['materials']}
        except Exception:
            data = {}
    data.update(MATERIAL_LIB)
    with open(path, 'w') as f:
        json.dump(dict(materials=sorted(data.values(), key=lambda m: m['name'])), f, indent=1)
    return len(data)


class clean_names:
    """Blender object names are file-global, so repeated socket names get '.001' suffixes. While exporting, give the
    exported objects their intended names (custom property 'cs_name') and park any other holder of that name."""

    def __init__(self, objects):
        self.objects = [o for o in objects if o.get('cs_name')]
        self.undo = []

    def __enter__(self):
        for o in self.objects:
            want = o['cs_name']
            if o.name == want:
                continue
            other = bpy.data.objects.get(want)
            if other is not None:
                self.undo.append((other, other.name))
                other.name = want + '__parked'
            self.undo.append((o, o.name))
            o.name = want
        return self

    def __exit__(self, *a):
        for o, name in reversed(self.undo):
            o.name = name + '__tmp'
        for o, name in reversed(self.undo):
            o.name = name
        return False
