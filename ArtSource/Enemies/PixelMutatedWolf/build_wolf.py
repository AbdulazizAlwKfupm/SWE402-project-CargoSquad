"""Build the original Cargo Squad wolf from its authored sections and pose curves.
Run in Blender 5.3 with --background --factory-startup --python build_wolf.py.
Coordinates in this file are (side, forward, height), in metres.
"""
from pathlib import Path
import math
import random
import bpy
import bmesh
from mathutils import Vector, Matrix, Quaternion
from math import sin, cos, pi, sqrt

OUT = Path(__file__).resolve().parent
PREV = OUT / "Previews"
PREV.mkdir(exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.fps = 30
scene.unit_settings.system = 'METRIC'
bpy.context.preferences.filepaths.save_version = 0

def xyz(p):
    return Vector((p[0], -p[1], p[2]))

# Each atlas panel has a hand-directed motif. Highlights follow the nap of
# the coat. The markings stay in the UVs as the skin articulates.
palettes = [
    ('413847','342c39','504454','655368'), # flank
    ('65536a','4d4054','806b84','a489a0'), # mantle
    ('504355','3a303f','66556b','806c81'), # haunch
    ('332b37','29232c','463b47','594754'), # belly
    ('9b7778','785867','b48d85','d6a997'), # cheeks and stockings
    ('26202c','19151d','403140','57414d'), # sockets
    ('522431','371c29','772b38','9e3540'), # wound margin
    ('aa323c','722737','dd514a','ff8b66'), # muscle
    ('513040','322131','803741','a24b51'), # gums
    ('dcc3a3','b59b8d','f3dcc0','fff0d3'), # horn
    ('fbb82e','e8831b','ffcd38','ffeb8c'), # luminous iris
    ('b18b91','926e7e','caa3a1','e4bbb0'), # muzzle
    ('443541','322735','614653','855965'), # pads
    ('78586a','624658','967283','b78c98'), # ear
    ('bf514c','8a3242','eb7761','ffc197'), # wound hotspot
    ('211e28','18151d','322c38','473846'), # nose
]
def rgba(h):
    return tuple(int(h[i:i+2],16)/255 for i in (0,2,4)) + (1,)
rng = random.Random(402)
atlas = [None]*(128*128)
glow = [rgba('000000')]*(128*128)
for tile, colors in enumerate(palettes):
    ox, oy = tile%4*32, tile//4*32
    grid = [[0]*32 for _ in range(32)]
    for j in range(34 if tile < 5 else 19):
        u,v = rng.randrange(32),rng.randrange(32)
        col = rng.choices([1,2,3],[.46,.44,.1])[0]
        length = rng.randint(3,8) if tile < 5 else rng.randint(2,5)
        for k in range(length):
            row=(v+k//2)%32
            grid[row][(u+k)%32]=col
            if 1<=k<length-1: grid[(row+1)%32][(u+k)%32]=col
    for v in range(32):
        for u in range(32):
            idx=(oy+v)*128+ox+u
            atlas[idx]=rgba(colors[grid[v][u]])
            if tile==10: glow[idx]=rgba('ffad21')
def raster(name, pixels):
    img=bpy.data.images.new(name,128,128,alpha=False)
    img.colorspace_settings.name='sRGB'
    img.pixels.foreach_set([v for color in pixels for v in color])
    img.filepath_raw=str(OUT/(name+'.png'))
    img.file_format='PNG'
    img.save()
    img.pack()
    return img
base=raster('PeltAtlas',atlas)
emission=raster('IrisEmission',glow)
mat=bpy.data.materials.new('Ashhide • fur, flesh and horn')
mat.use_nodes=True
nt=mat.node_tree
bs=nt.nodes.get('Principled BSDF')
bs.inputs['Roughness'].default_value=.87
for img,socket in [(base,'Base Color'),(emission,'Emission Color')]:
    tex=nt.nodes.new('ShaderNodeTexImage')
    tex.image=img
    tex.interpolation='Closest'
    nt.links.new(tex.outputs['Color'],bs.inputs[socket])
bs.inputs['Emission Strength'].default_value=1.3
nt.nodes.active=next(n for n in nt.nodes if n.type=='TEX_IMAGE' and n.image==base)

# A single skinned mesh with section islands. Tufts overlap in the direction
# of growth, so seams remain covered when shoulders, cheeks and hocks move.
verts, faces, coords, weights = [], [], [], []
def loft(sections,tile,bone,n=8,phase=0):
    """Sweep elliptical cross sections along a centreline. Sizes are radii."""
    centers=[xyz(s[:3]) for s in sections]
    idx=len(verts)
    distance=[0.]
    for a,b in zip(centers,centers[1:]): distance.append(distance[-1]+(b-a).length)
    girth=max(2*pi*(s[3]+s[4])*.5 for s in sections)
    density=min(32,28/max(distance[-1],girth,.01))
    ox=(tile%4)*32+2
    oy=(tile//4)*32+2
    def uv(i,j):
        return ((ox+girth*density*i/n)/128,(oy+distance[j]*density)/128)
    def face(ids,uvs):
        faces.append(tuple(idx+i for i in ids)); coords.append(uvs)
    for j,s in enumerate(sections):
        tangent=(centers[min(j+1,len(centers)-1)]-centers[max(0,j-1)]).normalized()
        guide=Vector((1,0,0)) if abs(tangent.x)<.90 else Vector((0,1,0))
        x=(guide-tangent*guide.dot(tangent)).normalized()
        up=tangent.cross(x)
        for i in range(n):
            a=2*pi*i/n+phase
            verts.append(centers[j]+x*(s[3]*cos(a))+up*(s[4]*sin(a)))
            w=bone[j] if isinstance(bone,list) else bone
            weights.append({w:1} if isinstance(w,str) else w)
        if j:
            for i in range(n):
                k=(i+1)%n
                face([(j-1)*n+i,(j-1)*n+k,j*n+k,j*n+i],[uv(i,j-1),uv(i+1,j-1),uv(i+1,j),uv(i,j)])
    for j in (0,len(sections)-1):
        order=list(range(n)) if j else list(reversed(range(n)))
        face([j*n+i for i in order],[((ox+13+min(12,sections[j][3]*density)*cos(2*pi*i/n))/128,
                                        (oy+13+min(12,sections[j][4]*density)*sin(2*pi*i/n))/128) for i in order])

def tuft(a,b,w,h,tile,bone):
    mid=tuple(v*.60+d*.40 for v,d in zip(a,b))
    loft([(*a,w*.72,h*.72),(*mid,w,h),(*b,.005,.004)],tile,bone,5)
def oval(a,b,w,h,tile,bone,n=7):
    sections=[]
    for t,r in [(0,.22),(.28,.96),(.63,1),(1,.22)]:
        p=tuple(v+(d-v)*t for v,d in zip(a,b))
        sections.append((*p,w*r,h*r))
    loft(sections,tile,bone,n)

bones={}
def joint(name,a,b,parent=None):
    bones[name]=(xyz(a),xyz(b),parent)
joint('Root',(0,0,0),(0,.25,0))
joint('Hips',(0,-.54,.83),(0,-.21,.93),'Root')
joint('Spine',(0,-.21,.93),(0,.21,1.01),'Hips')
joint('Chest',(0,.21,1.01),(0,.52,1.09),'Spine')
joint('Neck',(0,.52,1.09),(0,.79,1.10),'Chest')
joint('Head',(0,.79,1.1),(0,1.21,1.03),'Neck')
joint('Jaw',(0,.90,1.015),(0,1.30,.85),'Head')
tail=[(0,-.71,.95),(.02,-1.02,.81),(.065,-1.35,.59),(.13,-1.69,.45)]
for i in range(3): joint('Brush'+str(i+1),tail[i],tail[i+1],'Hips' if not i else 'Brush'+str(i))
legs={}
for s,side in [(-1,'R'),(1,'L')]:
    for fore in (True,False):
        stem=('Fore' if fore else 'Hind')+side
        p=[(.23*s,.45,1.02),(.30*s,.28,.56),(.32*s,.53,.18),(.32*s,.55,.09),(.32*s,.76,.065)] if fore else [
           (.21*s,-.57,.90),(.27*s,-.32,.53),(.30*s,-.75,.32),(.30*s,-.68,.09),(.30*s,-.48,.065)]
        legs[stem]=p
        parent='Chest' if fore else 'Hips'
        for i,label in enumerate(['Upper','Lower','Ankle','Paw']):
            joint(stem+label,p[i],p[i+1],parent)
            parent=stem+label

# Tucked abdomen flows into a high thorax, then a sloping neck and low face.
loft([(0,-.83,.85,.09,.14),(0,-.66,.88,.275,.275),(0,-.43,.90,.29,.29),
      (0,-.18,.95,.24,.235),(0,.06,.99,.275,.28),(0,.31,1.01,.34,.35),
      (0,.51,1.06,.30,.31),(0,.67,1.10,.225,.23)],0,
      ['Hips','Hips',{'Hips':.72,'Spine':.28},'Spine',{'Spine':.5,'Chest':.5},'Chest','Chest','Neck'],12)
loft([(0,.43,1.10,.265,.26),(0,.59,1.12,.28,.28),(0,.77,1.10,.245,.24),
      (0,.86,1.11,.19,.185)],1,['Chest','Neck','Neck','Head'],12)
loft([(0,.69,1.11,.15,.16),(0,.82,1.12,.246,.215),(0,.98,1.10,.23,.185),
      (0,1.10,1.06,.163,.128),(0,1.19,1.045,.123,.088)],2,'Head',12)
loft([(0,1.025,1.026,.174,.113),(0,1.13,1.024,.155,.095),(0,1.26,1.006,.131,.078),
      (0,1.36,1.012,.093,.066)],11,'Head',10)
loft([(0,1.29,1.03,.109,.061),(0,1.345,1.038,.114,.063),(0,1.39,1.04,.089,.045)],15,'Head',8)
loft([(0,.89,.976,.136,.075),(0,1.02,.929,.138,.066),(0,1.16,.877,.117,.047),
      (0,1.30,.854,.078,.039)],4,'Jaw',10)
loft([(0,.96,.992,.115,.055),(0,1.09,.941,.126,.037),(0,1.23,.899,.090,.023)],8,'Jaw',8)
oval((0,1.06,.94),(0,1.25,.893),.075,.017,7,'Jaw')
for side in (-1,1):
    s=side
    # Ears are asymmetric wedges with an inset pink cartilage plane.
    loft([(.148*s,.795,1.24,.094,.098),(.177*s,.765,1.34,.077,.07),
          (.227*s,.69,1.505,.008,.009)],2,'Head',5)
    loft([(.152*s,.848,1.285,.060,.042),(.18*s,.804,1.356,.050,.031),
          (.219*s,.719,1.464,.005,.004)],13,'Head',5)
    # Orbital rim, glowing wedge, upper lid and tiny specular.
    oval((.216*s,.934,1.151),(.165*s,1.093,1.116),.053,.058,5,'Head')
    oval((.238*s,.968,1.154),(.175*s,1.094,1.127),.044,.037,10,'Head')
    oval((.256*s,1.009,1.163),(.244*s,1.028,1.140),.011,.017,15,'Head')
    oval((.226*s,.921,1.205),(.159*s,1.103,1.149),.05,.034,1,'Head')
    oval((.236*s,.977,1.165),(.228*s,1.005,1.162),.012,.012,9,'Head',6)
    # Forward upper canines taper down; lower canines interlock behind them.
    for j,(f,length) in enumerate([(1.07,.040),(1.13,.055),(1.20,.061),(1.28,.095)]):
        w=(.144-(f-1.07)*.22)*s
        tuft((w,f,.964),(w*1.02,f-.017,.964-length),.018 if j<3 else .027,.019,9,'Head')
    for j in range(3):
        f=1.085+j*.073; w=(.118-j*.011)*s
        tuft((w,f,.925-j*.014),(w*1.03,f+.012,.963-j*.008),.016,.014,9,'Jaw')
    # Paired moustache and cheek ruffs, layered away from the mouth.
    for j in range(4):
        tuft(((.15+.016*j)*s,1.02-.033*j,1.05-.012*j),
             ((.28+.024*j)*s,.842-.051*j,1.01-.059*j),.062,.060,4 if j<3 else 2,'Head')
    for j in range(4):
        tuft((.22*s,.75-j*.085,1.18-j*.028),((.31+.011*j)*s,.50-j*.087,1.11-j*.038),.077,.073,1 if j%2==0 else 2,'Neck' if j<2 else 'Chest')
    # Ragged dorsal cape, staggered from both sides of the withers.
    for j in range(7):
        f=.62-j*.17
        z=1.28-.14*max(0,.37-f)
        length=[.18,.29,.34,.24,.30,.21,.23][j]
        tuft(((.09+.025*(j%2))*s,f,z),((.16+.027*(j%3))*s,f-length,z+.15-.051*(j%3)),
             .091,.078,1 if j%3 else 2,'Neck' if j==0 else 'Chest' if j<4 else 'Spine' if j<6 else 'Hips')
    # Exposed intercostal bars set in a dark wound bed.
    oval((.253*s,-.25,.99),(.291*s,.22,1.005),.049,.148,6,'Spine',10)
    for j in range(4):
        f=-.19+j*.099
        loft([((.265+.008*j)*s,f,1.113,.022,.030),((.294+.009*j)*s,f+.017,1.060,.026,.035),
              ((.295+.009*j)*s,f+.012,.987,.026,.035),((.26+.01*j)*s,f-.025,.90,.012,.016)],7,'Spine',7)
for j in range(6):
    f=.54-j*.193; z=1.32-.14*max(0,.28-f)
    tuft((0,f,z),(0,f-.25,z+.17-.012*j),.088,.078,1,'Chest' if j<3 else 'Spine' if j<5 else 'Hips')
for stem,p in legs.items():
    s=1 if stem.endswith('L') else -1
    fore=stem.startswith('Fore')
    u,l,a,foot=[stem+k for k in ('Upper','Lower','Ankle','Paw')]
    A,B,C,D,E=p
    def mix(a,b,t): return tuple(x+(y-x)*t for x,y in zip(a,b))
    mid=mix(A,B,.52)
    loft([(*mix(A,B,-.29),.12,.15),(*A,.187,.229),(*mid,.153,.18),(*B,.091,.103)],2 if fore else 0,[u,u,u,{u:.6,l:.4}],10)
    loft([(*B,.10,.115),(*mix(B,C,.31),.103,.115),(*mix(B,C,.72),.073,.075),(*C,.064,.068)],4 if fore else 2,[l,l,l,{l:.7,a:.3}],8)
    loft([(*C,.067,.075),(*mix(C,D,.5),.061,.073),(*D,.067,.065)],4,[a,a,foot],8)
    oval((D[0],D[1]-.073,.079),(D[0],D[1]+.19,.074),.119,.074,4,foot,10)
    for k in (-1,0,1):
        x=D[0]+k*.065; f=D[1]+.108-.023*abs(k)
        oval((x,f-.061,.075),(x,f+.098,.062),.045,.052,11,foot)
        tuft((x,f+.055,.067),(x+k*.012,f+.137,.022),.026,.029,9,foot)
    for j in range(3):
        pos=mix(A,B,.04+.24*j)
        tuft((pos[0]+.075*s,pos[1]-.06,pos[2]+.025),(pos[0]+.112*s,pos[1]-.20,pos[2]-.06),
             .058,.050,1 if j==0 else 2,u)
    for pos,r,b in [(mix(A,B,.25),.106,u),(mix(B,C,.23),.053,l)]:
        x=pos[0]+(.164 if b==u else .082)*s
        for layer,(shift,fac,t) in enumerate([(0,1.13,5),(.014,.94,6),(.031,.69,7),(.042,.34,14)]):
            cx=x+shift*s
            oval((cx,pos[1]-.028,pos[2]-r*fac),(cx,pos[1]+.028,pos[2]+r*fac),.022,r*.72*fac,t,b)
        if b==u:
            for k in (-1,1):
                tuft((x+.014*s,pos[1]+k*.037,pos[2]+.03*k),
                     (x+.012*s,pos[1]+k*.085,pos[2]+.105*k),.016,.024,6,b)
# Brush constructed with weighted continuous core and broken, swept outer locks.
loft([(*tail[0],.094,.105),(*tail[1],.15,.157),(*tail[2],.133,.138),
      (*tail[3],.024,.028)],2,['Brush1',{'Brush1':.5,'Brush2':.5},{'Brush2':.5,'Brush3':.5},'Brush3'],10)
for i in range(3):
    for s in (-1,1):
        p=tail[i]; k=tail[i+1]
        tuft((p[0]+.055*s,p[1]-.06,p[2]+.05),(k[0]+.115*s,k[1]-.14,k[2]-.055*(i+1)),.082,.083,1 if i==0 else 2,'Brush'+str(i+1))
tuft((.08,-1.33,.66),(.16,-1.78,.37),.088,.097,1,'Brush3')

mesh=bpy.data.meshes.new('Ashhide_Skin')
mesh.from_pydata(verts,[],faces)
mesh.update()
uvs=mesh.uv_layers.new(name='Pelt atlas')
for poly,face_uvs in zip(mesh.polygons,coords):
    for idx,uv in zip(poly.loop_indices,face_uvs): uvs.data[idx].uv=uv
bm=bmesh.new(); bm.from_mesh(mesh)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(mesh); bm.free()
skin=bpy.data.objects.new('Ashhide',mesh); scene.collection.objects.link(skin)
mesh.materials.append(mat)
arm=bpy.data.armatures.new('Ashhide Skeleton')
rig=bpy.data.objects.new('PixelWolfRig',arm); scene.collection.objects.link(rig)
bpy.context.view_layer.objects.active=rig
rig.select_set(True); bpy.ops.object.mode_set(mode='EDIT')
for name,(start,end,parent) in bones.items():
    eb=arm.edit_bones.new(name); eb.head=start; eb.tail=end
    if parent: eb.parent=arm.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT')
groups={name:skin.vertex_groups.new(name=name) for name in bones}
for i,ws in enumerate(weights):
    for name,value in ws.items(): groups[name].add([i],value,'REPLACE')
skin.parent=rig
mod=skin.modifiers.new('Original quadruped skeleton','ARMATURE'); mod.object=rig
# Pose generation. Solve the elbow/stifle towards its anatomical pole at
# every sampled frame; pad positions follow explicit stance and swing arcs.
rad=math.radians
rest={b.name:b.matrix_local.copy() for b in arm.bones}
basis={name:mat.to_quaternion() for name,mat in rest.items()}
ident=Quaternion()
def at(position,rotation):
    return Matrix.LocRotScale(position,rotation,None)
def rot(axis,angle):
    return Quaternion(axis,rad(angle))
def curve(t,points):
    for (a,x),(b,y) in zip(points,points[1:]):
        if t<=b:
            f=max(0,min(1,(t-a)/(b-a)))
            return x+(y-x)*f*f*(3-2*f)
    return points[-1][1]

clips={'Idle':60,'Walk':24,'Run':15,'Bite':30,'Hurt':10,'Stunned':36,'Death':32}
actions={}
def pose(clip,t):
    phase=t*2*pi
    step=sin(phase)
    move=Vector((0,0,0)); pitch=roll=head=neck=jaw=0
    tail_swing=4*sin(phase)
    weight=0.
    if clip=='Idle':
        move.z=.012*step; neck=-2*step; head=1.8*cos(phase); jaw=2+2*step
    elif clip in ('Walk','Run'):
        fast=clip=='Run'
        move.z=(.038 if fast else .018)*(-cos(phase if fast else phase*2))
        pitch=(5 if fast else 1.8)*step
        head=-pitch*.65; neck=-pitch*.25; tail_swing=7*step
    elif clip=='Bite':
        wind=curve(t,[(0,0),(.22,1),(.44,0),(1,0)])
        snap=curve(t,[(0,0),(.24,0),(.45,1),(.57,.95),(1,0)])
        move.y=.082*wind-.20*snap; move.z=.055*wind-.030*snap
        pitch=-8*wind+6*snap; neck=-10*wind+5*snap; head=-8*wind+5*snap
        jaw=curve(t,[(0,0),(.25,28),(.35,31),(.45,-13),(.53,-13),(.72,3),(1,0)])
    elif clip=='Hurt':
        weight=curve(t,[(0,0),(.25,1),(.60,.55),(1,0)])
        move.y=.105*weight; move.z=-.055*weight; neck=-8*weight; roll=9*weight; jaw=10*weight
    elif clip=='Stunned':
        weight=curve(t,[(0,0),(.12,1),(1,1)])
        move.z=-.105*weight; move.x=.045*sin(phase*2)*weight
        roll=7*sin(phase*2)*weight; neck=9*weight; head=7*cos(phase*2)*weight; jaw=8*weight
    elif clip=='Death':
        weight=curve(t,[(0,0),(.17,.12),(.52,.74),(.74,1),(.86,.975),(1,1)])
        move.z=-.588*weight; move.x=.075*weight; pitch=2*weight
        roll=18*weight; neck=1*weight; head=1*weight; jaw=4*weight
    target={}
    def fk(name,local_delta=ident,offset=None):
        parent=bones[name][2]
        m=(target[parent]@rest[parent].inverted()@rest[name]) if parent else rest[name].copy()
        if offset is not None: m.translation+=offset
        # Authored angles are in rest-model axes.
        target[name]=at(m.translation,m.to_quaternion()@basis[name].inverted()@local_delta@basis[name])
    fk('Root'); fk('Hips',rot((0,1,0),roll)@rot((1,0,0),pitch),move)
    fk('Spine',rot((1,0,0),pitch*.22))
    fk('Chest',rot((1,0,0),pitch*.12))
    fk('Neck',rot((1,0,0),neck)); fk('Head',rot((1,0,0),head))
    fk('Jaw',rot((1,0,0),jaw))
    for i in range(3):
        wag=tail_swing*(.5+i*.3)
        if clip=='Death': wag=weight*(13-i*3)
        tail_pitch=[11,6,-7][i]*weight if clip=='Death' else 1.6*sin(phase+i*.5)
        fk('Brush'+str(i+1),rot((0,0,1),wag)@rot((1,0,0),tail_pitch))
    for stem,points in legs.items():
        a,b,c,d,e=[xyz(v) for v in points]
        u,l,h,paw=[stem+k for k in ('Upper','Lower','Ankle','Paw')]
        s=1 if stem.endswith('L') else -1
        fore=stem.startswith('Fore')
        anchor=(target[bones[u][2]]@rest[bones[u][2]].inverted())@a
        sole=d.copy(); ankle=c-d; pad_dir=e-d
        if clip in ('Walk','Run'):
            fast=clip=='Run'
            phase_offset=((.09 if s<0 else 0) + (.52 if fore else 0)) if fast else ((.5 if s<0 else 0)+(.64 if fore else 0))
            tt=(t+phase_offset)%1
            stance=.39 if fast else .60
            stride=(4.5*.50 if fast else 1.5*.80)*stance
            if tt<stance:
                f=tt/stance
                travel=(.5-f)*stride; lift=0
            else:
                f=(tt-stance)/(1-stance)
                travel=(f*f*(3-2*f)-.5)*stride
                lift=(.19 if fast else .11)*sin(pi*f)**2
            sole.y-=travel; sole.z+=lift
            ankle=rot((1,0,0),(-25 if not fore else 12)*sin(2*pi*tt))@ankle
        elif clip=='Death':
            sole.x+=s*(.19 if fore else .16)*weight
            sole.y+=(-.33 if fore else .31)*weight
            ankle=rot((1,0,0),(32 if fore else -49)*weight)@ankle
        end=sole+ankle
        line=end-anchor
        ab=(b-a).length; bc=(c-b).length
        dist=max(abs(ab-bc)+.0001,min(ab+bc-.0001,line.length))
        direction=line.normalized()
        pole=Vector((s*.12,1 if fore else -1,0))
        if clip=='Death':
            pole+=Vector((s*.9,0,1.7))*weight
        pole=(pole-direction*direction.dot(pole)).normalized()
        along=(ab*ab-bc*bc+dist*dist)/(2*dist)
        knee=anchor+direction*along+pole*sqrt(max(0,ab*ab-along*along))
        end=anchor+direction*dist
        actual_sole=end-ankle
        for name,start,finish in [(u,anchor,knee),(l,knee,end),(h,end,actual_sole),(paw,actual_sole,actual_sole+pad_dir)]:
            original=bones[name][1]-bones[name][0]
            target[name]=at(start,original.rotation_difference(finish-start)@basis[name])
    for name,m in target.items():
        parent=bones[name][2]
        rel=(rest[parent].inverted()@rest[name]).inverted()@target[parent].inverted()@m if parent else rest[name].inverted()@m
        pb=rig.pose.bones[name]
        pb.location=rel.translation
        q=rel.to_quaternion()
        q.make_compatible(pb.rotation_quaternion)
        pb.rotation_quaternion=q
        pb.keyframe_insert(data_path='location',group=name)
        pb.keyframe_insert(data_path='rotation_quaternion',group=name)

rig.animation_data_create()
for name,frames in clips.items():
    act=bpy.data.actions.new(name); act.use_fake_user=True
    rig.animation_data.action=act
    for f in range(frames+1):
        scene.frame_set(f+1); pose(name,f/frames)
    actions[name]=act
rig.animation_data.action=actions['Idle']; scene.frame_start=1; scene.frame_end=61; scene.frame_set(1)
for obj in bpy.context.selected_objects: obj.select_set(False)
rig.select_set(True); skin.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(OUT/'PixelMutatedWolf.fbx'),use_selection=True,
    object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',
    add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,
    bake_anim_simplify_factor=0.0,path_mode='STRIP',use_custom_props=False)
mesh.calc_loop_triangles()
print('PIXEL_WOLF',len(mesh.loop_triangles),'triangles;',len(bones),'bones;',list(actions))
# A separate studio collection stays out of the game export.
studio=bpy.data.collections.new('Review studio'); scene.collection.children.link(studio)
def studio_obj(name,data):
    obj=bpy.data.objects.new(name,data); studio.objects.link(obj); return obj
def look(obj,point):
    obj.rotation_euler=(Vector(point)-obj.location).to_track_quat('-Z','Y').to_euler()
cam=studio_obj('Review lens',bpy.data.cameras.new('Orthographic')); scene.camera=cam; cam.data.type='ORTHO'
def camera(direction,scale=3.5):
    target=Vector((0,.12,.72))
    cam.location=target+Vector(direction).normalized()*7
    look(cam,target); cam.data.ortho_scale=scale
camera((-4,-2.8,1.7))
def area(name,location,energy,color,size):
    light=bpy.data.lights.new(name,'AREA'); light.energy=energy; light.color=color; light.shape='DISK'; light.size=size
    obj=studio_obj(name,light); obj.location=location; look(obj,(0,0,.7))
area('Warm key',(-3,-3.5,5),410,(1,.87,.80),4)
area('Lavender rim',(2.4,2.9,4),620,(.78,.68,1),3)
area('Soft bounce',(2,-3,2.2),160,(.82,.88,1),3)
plane=bpy.data.meshes.new('Ground'); plane.from_pydata([(-100,-100,-.008),(100,-100,-.008),(100,100,-.008),(-100,100,-.008)],[],[(0,1,2,3)])
ground=studio_obj('Studio floor',plane)
floor=bpy.data.materials.new('Studio • basalt'); floor.use_nodes=True
floor.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.038,.032,.046,1)
plane.materials.append(floor)
scene.world=bpy.data.worlds.new('Studio ambient'); scene.world.color=(.20,.17,.22)
scene.render.engine='CYCLES'; scene.cycles.samples=6; scene.cycles.use_denoising=True
scene.view_settings.view_transform='AgX'
scene.render.resolution_x=720; scene.render.resolution_y=540; scene.render.resolution_percentage=100
for a in bpy.context.screen.areas:
    if a.type=='VIEW_3D':
        a.spaces.active.region_3d.view_rotation=cam.rotation_euler.to_quaternion()
        a.spaces.active.region_3d.view_distance=3.6; a.spaces.active.region_3d.view_location=(0,.1,.74)
        a.spaces.active.overlay.show_overlays=False; a.spaces.active.shading.color_type='TEXTURE'
rig.show_in_front=False; arm.display_type='STICK'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'PixelMutatedWolf.blend'))


