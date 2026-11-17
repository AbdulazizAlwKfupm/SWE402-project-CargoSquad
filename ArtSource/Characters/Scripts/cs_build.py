"""Cargo Squad - build / export entry points. Run inside Blender:

    import sys; sys.path.insert(0, r"<repo>/ArtSource/Characters/Scripts")
    import cs_build; cs_build.build_survivor_assets('Navy')
"""
import os, importlib
import bpy
import cs_core as C, cs_survivors as SV, cs_anim as AN

SRC = os.path.join(C.REPO, 'ArtSource', 'Characters')
ART = os.path.join(C.REPO, 'CargoSquad', 'Assets', '_CargoSquad', 'Art')
SPECS = {}


def reload_all():
    for m in (C, SV, AN):
        importlib.reload(m)
    SPECS.clear()
    for key in ('NAVY', 'HEAVY', 'SCOUT', 'MEDIC'):
        if hasattr(SV, key):
            SPECS[getattr(SV, key)['name']] = getattr(SV, key)


def export_group(res):
    o = res['objs']
    objs = [res['arm']] + [x for x in o.values() if x] + list(res['sockets'].values())
    objs += [c for x in o.values() if x for c in x.children]
    return objs


def save_sources(names):
    out = {}
    for n in names:
        sc = bpy.data.scenes['CS_' + n]
        extra = [a for a in bpy.data.actions if not a.name.startswith(('Wolf_', 'Armored_', 'Spitter_'))] if n == 'Navy' else []
        p = os.path.join(SRC, 'Survivors', n + '.blend')
        out[n] = (p, C.save_blend(p, [sc], extra))
    return out


SURVIVORS = ('Navy', 'Heavy', 'Scout', 'Medic')
MATERIAL_JSON = os.path.join(ART, 'Shared', 'Materials', 'cs_materials.json')



def build_survivor_assets(name, export=True, anim=False):
    reload_all()
    res = SV.build_survivor(SPECS[name])
    rep = SV.report(res)
    out = dict(report=rep, materials=C.write_material_lib(MATERIAL_JSON))
    if anim:
        acts = AN.bake_set(res['arm'], AN.ALL_CLIPS)
        res['arm'].animation_data.action = None
        out['clips'] = [(a.name, int(a.frame_end), bool(a.get('cs_loop'))) for a in acts]
    if export:
        AN.rest(res['arm'])
        fbx = os.path.join(ART, 'Characters', 'Survivors', name, name + '.fbx')
        group = export_group(res)
        with C.clean_names(group):
            C.export_fbx(res['scene'], fbx, group, anim=anim)
        out['fbx'] = (fbx, os.path.getsize(fbx))
    return res, out


def build_all_survivors():
    """Navy is the reference rig: its FBX also carries every shared humanoid clip (one take per action)."""
    out = {}
    for n in SURVIVORS:
        res, o = build_survivor_assets(n, anim=(n == 'Navy'))
        for e in res['sockets'].values():
            if e.name.startswith('Cargo_'):
                e.hide_viewport = True
        out[n] = dict(materials=o['materials'], fbx=o['fbx'], tris=sum(r['tris'] for r in o['report']),
                      unweighted=sum(r['unweighted'] for r in o['report']), zmin=o['report'][0]['zmin'],
                      clips=o.get('clips'))
    out['blend'] = save_sources(SURVIVORS)
    return out


CREATURES = ('MutatedWolf', 'ArmoredCreature', 'AcidSpitter')


def build_creature_assets(name, export=True):
    import cs_creatures as CR
    reload_all()
    importlib.reload(CR)
    res = CR.BUILDERS[name]()
    acts = CR.bake_creature(res)
    out = dict(report=[C.mesh_report(o, res['arm']) for o in res['objs']], materials=C.write_material_lib(MATERIAL_JSON),
               clips=[(a.name, int(a.frame_end), bool(a.get('cs_loop'))) for a in acts], bones=len(res['arm'].data.bones))
    if export:
        fbx = os.path.join(ART, 'Characters', 'Enemies', name, name + '.fbx')
        group = [res['arm']] + res['objs'] + res['sockets']
        with C.clean_names(group):
            C.export_fbx(res['scene'], fbx, group, anim=True)
        out['fbx'] = (fbx, os.path.getsize(fbx))
        p = os.path.join(SRC, 'Creatures', name + '.blend')
        out['blend'] = (p, C.save_blend(p, [res['scene']], acts))
    return res, out
