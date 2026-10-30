import sys, math, random
sys.path.insert(0, sys.argv[1])
import mathutils
from mathutils import Matrix, Vector
import gorilla_hair_motion as hm

class Bone:
    def __init__(s,name,parent,local,length): s.name,s.parent,s.matrix_local,s.length=name,parent,local,length; s.use_inherit_rotation=True; s.inherit_scale='FULL'
class PoseBone:
    def __init__(s,bone): s.bone=bone; s._basis=Matrix.Identity(4); s.matrix=Matrix.Identity(4)
    @property
    def matrix_basis(s): return s._basis
    @matrix_basis.setter
    def matrix_basis(s,m):  # mimic loc/rot/scale storage with tiny float noise
        l,r,sc=m.decompose(); s._basis=Matrix.LocRotScale(l+Vector((1e-7,0,0)),r,sc)
class Obj: pass

down=Matrix.Rotation(math.pi,4,'X')  # bone Y axis pointing -Y... use rotation so Y -> -Z
down=Matrix.Rotation(-math.pi/2,4,'X')  # local +Y maps to -Z
head=Bone('J_Head',None,Matrix.Identity(4),1.0)
h0=Bone('Hair_b00',head,Matrix.Translation((0,0,0))@down,1.0)
h1=Bone('Hair_b00_00',h0,Matrix.Translation((0,0,-1))@down,1.0)
h2=Bone('Hair_b00_01',h1,Matrix.Translation((0,0,-2))@down,1.0)
bones=[head,h0,h1,h2]
obj=Obj(); obj.type='ARMATURE'; obj.name='rig'; obj.matrix_world=Matrix.Identity(4)
obj.data=Obj(); obj.data.bones=bones
obj.pose=Obj(); obj.pose.bones={b.name:PoseBone(b) for b in bones}
def evaluate():
    for b in bones:
        pb=obj.pose.bones[b.name]
        if b.parent is None: pb.matrix=b.matrix_local@pb.matrix_basis
        else: pb.matrix=obj.pose.bones[b.parent.name].matrix@(b.parent.matrix_local.inverted()@b.matrix_local)@pb.matrix_basis
scene=Obj(); scene.objects=[obj]
m=hm.HairMotion(mathutils, dict(hm.DEFAULTS, enabled='1'))
tip=lambda: (obj.pose.bones['Hair_b00_01'].matrix@Vector((0,1,0)))
log={}
lengths=[]
for f in range(1,301):
    x=min(f,10)*1.0  # head slides +X for 10 frames then stops
    obj.pose.bones['J_Head'].matrix_basis=Matrix.Translation((x,0,0))
    evaluate(); scene.frame_current=f; m.step(scene); evaluate()
    log[f]=tip().copy()
    pts=[obj.pose.bones['J_Head'].matrix.to_translation()]+[obj.pose.bones[n].matrix@Vector((0,1,0)) for n in ('Hair_b00','Hair_b00_00','Hair_b00_01')]
    lengths+= [(pts[i+1]-pts[i]).length for i in range(3)]
target=lambda f: Vector((min(f,10),0,-3))
for f in (5,10,15,25,60,300): print(f, 'tip', tuple(round(v,3) for v in log[f]), 'animated', tuple(target(f)), 'lag', round((log[f]-target(f)).length,3))
assert log[10].x < 10-0.5, 'hair should lag while the head moves'
assert abs(log[300].x-10)<0.05 and abs((log[300]-Vector((10,0,0))).length-3)<1e-3, 'hair must settle and keep its length'
assert (log[300]-log[250]).length<1e-3, 'no drift once settled'
# chain lengths preserved every frame
for f in (10,60,300):
    p=[obj.pose.bones[n].matrix.to_translation() for n in ('Hair_b00','Hair_b00_00','Hair_b00_01')]
seg={}
for f in range(1,301): pass
evaluate()
pts=[obj.pose.bones['J_Head'].matrix.to_translation()]+[obj.pose.bones[n].matrix@Vector((0,1,0)) for n in ('Hair_b00','Hair_b00_00','Hair_b00_01')]
assert all(abs((pts[i+1]-pts[i]).length-1)<1e-3 for i in range(3)), 'each hair bone keeps its length'
assert all(abs(l-1)<1e-3 for l in lengths), 'bone lengths preserved every frame'
print('max bone length error', max(abs(l-1) for l in lengths)); print('PASS')
