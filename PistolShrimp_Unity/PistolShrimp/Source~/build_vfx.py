import bpy, bmesh, sys, os
from mathutils import Vector
out=sys.argv[1]
bpy.ops.wm.read_factory_settings(use_empty=True)
sc=bpy.context.scene
def flat(ob):
    for p in ob.data.polygons: p.use_smooth=False
# Bubble: icosphere, 20 faces, diameter 1
bm=bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=1, radius=0.5)
me=bpy.data.meshes.new('Bubble'); bm.to_mesh(me); bm.free()
ob=bpy.data.objects.new('Bubble',me); sc.collection.objects.link(ob); flat(ob)
# WaterJet: faceted spindle, length 1 along Unity +Z (Blender -Y), fat end forward
bm=bmesh.new()
import math
rings=[(-0.5,0.0),(-0.15,0.10),(0.25,0.17),(0.5,0.0)]   # (forward, radius)
n=5; vs=[]
for f,r in rings:
    if r==0: vs.append([bm.verts.new(Vector((0,-f,0)))])
    else: vs.append([bm.verts.new(Vector((r*math.cos(2*math.pi*k/n), -f, r*math.sin(2*math.pi*k/n)))) for k in range(n)])
for i in range(len(vs)-1):
    a,b=vs[i],vs[i+1]
    for k in range(n):
        k2=(k+1)%n
        if len(a)==1: bm.faces.new([a[0],b[k],b[k2]])
        elif len(b)==1: bm.faces.new([a[k],a[k2],b[0]])
        else: bm.faces.new([a[k],a[k2],b[k2],b[k]])
bmesh.ops.recalc_face_normals(bm,faces=bm.faces); bmesh.ops.triangulate(bm,faces=bm.faces)
me=bpy.data.meshes.new('WaterJet'); bm.to_mesh(me); bm.free()
ob=bpy.data.objects.new('WaterJet',me); sc.collection.objects.link(ob); flat(ob)
# ShockRing: flat faceted ring, outer diameter 1, facing Unity +Z
bm=bmesh.new(); n=10
o=[bm.verts.new(Vector((0.5*math.cos(2*math.pi*k/n),0,0.5*math.sin(2*math.pi*k/n)))) for k in range(n)]
i_=[bm.verts.new(Vector((0.40*math.cos(2*math.pi*(k+.5)/n),0,0.40*math.sin(2*math.pi*(k+.5)/n)))) for k in range(n)]
for k in range(n):
    k2=(k+1)%n
    bm.faces.new([o[k],o[k2],i_[k]]); bm.faces.new([i_[k],o[k2],i_[k2]])
me=bpy.data.meshes.new('ShockRing'); bm.to_mesh(me); bm.free()
ob=bpy.data.objects.new('ShockRing',me); sc.collection.objects.link(ob); flat(ob)
bpy.ops.export_scene.fbx(filepath=os.path.join(out,'ShrimpVFXMeshes.fbx'), object_types={'MESH'}, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y', mesh_smooth_type='FACE', bake_anim=False, bake_space_transform=True)
for m in bpy.data.meshes: print(m.name, len(m.polygons))
