"""Builds the shrimp in Blender (bpy), renders previews, exports FBX + palettes.

usage: python3 build_blender.py <side: R|L> <outdir> [render]
"""
import sys, os, random
from math import sin, cos, pi, radians
import numpy as np
import bpy, bmesh
from mathutils import Vector, Matrix, Quaternion

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import shrimp_geo as G

SIDE = sys.argv[1] if len(sys.argv) > 1 else 'R'
OUT = sys.argv[2] if len(sys.argv) > 2 else 'out'
RENDER = len(sys.argv) > 3 and sys.argv[3] == 'render'
os.makedirs(OUT, exist_ok=True)

SW = 8                      # swatch size in px
COLS = 4                    # shades per role
ROWS = 32


def B(p):                   # authoring (R,U,F) -> Blender (x,y,z)
    return Vector((-p[0], -p[2], p[1]))


# ------------------------------------------------------------------ palettes
def hex2rgb(h):
    return [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]


def write_palette(name, pal, path):
    w, h = COLS * SW, ROWS * SW
    px = np.zeros((h, w, 4), dtype=np.float32); px[..., 3] = 1
    shades = [1.0, 1.07, 0.92, 0.985]
    for ri, role in enumerate(G.ROLES):
        base = np.array(hex2rgb(pal[role]))
        for ci, sh in enumerate(shades):
            c = np.clip(base * sh, 0, 1)
            y0 = ri * SW; x0 = ci * SW
            px[y0:y0 + SW, x0:x0 + SW, :3] = c
    img = bpy.data.images.new('pal_' + name, w, h, alpha=False)
    img.colorspace_settings.name = 'sRGB'
    # bpy images store linear floats for byte images? -> they store sRGB-encoded values for 8-bit buffers
    img.pixels.foreach_set(px.ravel())
    img.filepath_raw = path; img.file_format = 'PNG'; img.save()
    return img


def uv_for(role, shade):
    ri = G.ROLES.index(role)
    return ((shade + 0.5) / COLS, (ri + 0.5) / ROWS)


# ------------------------------------------------------------------ mesh
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
parts, bones = G.build(1 if SIDE == 'R' else -1)

rng = random.Random(7)
all_v, all_f, all_uv, all_grp = [], [], [], []
for part in parts:
    bm = bmesh.new()
    vs = [bm.verts.new(B(p)) for p in part['verts']]
    lay = bm.faces.layers.int.new('role')
    for f, r in zip(part['faces'], part['roles']):
        try:
            face = bm.faces.new([vs[i] for i in f])
        except ValueError:
            continue
        face[lay] = G.ROLES.index(r)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.triangulate(bm, faces=bm.faces, quad_method='FIXED', ngon_method='BEAUTY')
    bm.verts.index_update()
    # split every face (hard edges, per-face UV) -> unique verts per triangle
    for face in bm.faces:
        role = G.ROLES[face[lay]]
        uv = uv_for(role, rng.choice([0, 0, 1, 2, 3]))
        idx = []
        for v in face.verts:
            idx.append(len(all_v)); all_v.append(v.co.copy()); all_grp.append(part['bone'])
        all_f.append(idx); all_uv.append(uv)
    bm.free()

me = bpy.data.meshes.new('PistolShrimp')
me.from_pydata([tuple(v) for v in all_v], [], all_f)
me.update()
uvl = me.uv_layers.new(name='UVMap')
for poly, uv in zip(me.polygons, all_uv):
    poly.use_smooth = False
    for li in poly.loop_indices:
        uvl.data[li].uv = uv
ob = bpy.data.objects.new('PistolShrimp', me)
scene.collection.objects.link(ob)
for b in bones:
    ob.vertex_groups.new(name=b['name'])
for i, g in enumerate(all_grp):
    ob.vertex_groups[g].add([i], 1.0, 'REPLACE')
print('TRIS', len(me.polygons), 'VERTS', len(me.vertices), 'BONES', len(bones))

# ------------------------------------------------------------------ palettes + material
imgs = {}
for name, pal in G.PALETTES.items():
    imgs[name] = write_palette(name, pal, os.path.abspath(os.path.join(OUT, 'ShrimpPalette_%s.png' % name)))


def make_mat(img):
    m = bpy.data.materials.new('ShrimpPalette')
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes.get('Principled BSDF')
    tex = nt.nodes.new('ShaderNodeTexImage'); tex.image = img; tex.interpolation = 'Closest'
    nt.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = 0.85
    return m


mat = make_mat(imgs['CandyStripe'])
me.materials.append(mat)

# ------------------------------------------------------------------ armature
arm = bpy.data.armatures.new('ShrimpRig')
aob = bpy.data.objects.new('ShrimpRig', arm)
scene.collection.objects.link(aob)
bpy.context.view_layer.objects.active = aob
bpy.ops.object.mode_set(mode='EDIT')
for b in bones:
    eb = arm.edit_bones.new(b['name'])
    eb.head = B(b['head']); eb.tail = B(b['tail'])
    eb.roll = 0.0
    if b['parent']:
        eb.parent = arm.edit_bones[b['parent']]
        eb.use_connect = False
bpy.ops.object.mode_set(mode='OBJECT')
mod = ob.modifiers.new('Armature', 'ARMATURE'); mod.object = aob

# ------------------------------------------------------------------ idle animation
FPS, LEN = 30, 60
scene.render.fps = FPS; scene.frame_start = 0; scene.frame_end = LEN
aob.animation_data_create()
act = bpy.data.actions.new('Idle')
aob.animation_data.action = act
AX = dict(R=Vector((-1, 0, 0)), U=Vector((0, 0, 1)), F=Vector((0, -1, 0)))


def key(bname, frame, rots):
    """rots: list of (axis_name, degrees) applied in armature space about the bone head."""
    pb = aob.pose.bones[bname]
    rest = pb.bone.matrix_local.to_3x3()
    q = Quaternion()
    for ax, deg in rots:
        q = Quaternion(AX[ax], radians(deg)) @ q
    local = rest.inverted() @ q.to_matrix() @ rest
    pb.rotation_mode = 'QUATERNION'
    pb.rotation_quaternion = local.to_quaternion()
    pb.keyframe_insert('rotation_quaternion', frame=frame)


for fr in range(0, LEN + 1, 2):
    t = 2 * pi * fr / LEN
    for i in range(4):
        for tag, sg in (('R', 1), ('L', -1)):
            key('Pleo_%s%d' % (tag, i + 1), fr, [('R', 26 * sin(2 * t - i * 0.9))])
            ph = i * 1.3 + (0 if tag == 'R' else pi)
            key('Leg_%s%d_Upper' % (tag, i + 1), fr, [('F', -sg * 4 * sin(t + ph)), ('U', 5 * sin(t + ph + 1.2))])
            key('Leg_%s%d_Lower' % (tag, i + 1), fr, [('F', -sg * 5 * sin(t + ph + 0.8))])
    for tag, sg in (('R', 1), ('L', -1)):
        ph = 0 if tag == 'R' else 0.9
        key('Antenna_%s_1' % tag, fr, [('U', 5 * sin(t + ph)), ('R', 3 * sin(2 * t + ph))])
        key('Antenna_%s_2' % tag, fr, [('U', 7 * sin(t + ph - 0.7)), ('R', 4 * sin(2 * t + ph - 0.7))])
        key('Antenna_%s_3' % tag, fr, [('U', 9 * sin(t + ph - 1.4)), ('R', 5 * sin(2 * t + ph - 1.4))])
        key('Antennule_%s' % tag, fr, [('R', 6 * sin(2 * t + ph)), ('U', 4 * sin(t + ph))])
    for i in range(5):
        key('Abd%d' % (i + 1), fr, [('R', 1.6 * sin(t - i * 0.6))])
    key('Tail', fr, [('R', 5 * sin(t - 3.2))])
scene.frame_set(0)

# ------------------------------------------------------------------ export
fbx = os.path.abspath(os.path.join(OUT, 'PistolShrimp_%s.fbx' % ('RightSnapper' if SIDE == 'R' else 'LeftSnapper')))
bpy.ops.export_scene.fbx(
    filepath=fbx, use_selection=False, object_types={'ARMATURE', 'MESH'},
    apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
    add_leaf_bones=False, mesh_smooth_type='FACE', use_armature_deform_only=False,
    bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
    bake_anim_use_all_bones=False, bake_anim_simplify_factor=0.0, bake_anim_step=1.0,
    primary_bone_axis='Y', secondary_bone_axis='X', path_mode='STRIP', embed_textures=False)
print('EXPORTED', fbx)

# ------------------------------------------------------------------ previews
if RENDER:
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 24
    scene.render.resolution_x, scene.render.resolution_y = 900, 600
    world = bpy.data.worlds.new('w'); scene.world = world; world.use_nodes = True
    bg = world.node_tree.nodes['Background']
    bg.inputs[0].default_value = (0.05, 0.22, 0.34, 1); bg.inputs[1].default_value = 1.6
    sun = bpy.data.objects.new('sun', bpy.data.lights.new('sun', 'SUN'))
    sun.data.energy = 3.5; sun.rotation_euler = (radians(40), radians(15), radians(-30))
    scene.collection.objects.link(sun)
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
    scene.collection.objects.link(cam); scene.camera = cam

    def shot(name, pos, target, lens=50, variant='CandyStripe', pose=None):
        mat.node_tree.nodes['Image Texture'].image = imgs[variant]
        cam.location = B(pos)
        d = B(target) - B(pos)
        cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
        cam.data.lens = lens; cam.data.clip_start = 0.02
        if pose:
            pose()
        scene.render.filepath = os.path.abspath(os.path.join(OUT, name + '.png'))
        bpy.ops.render.render(write_still=True)

    def open_claws(deg):
        def f():
            aob.animation_data.action = None
            for b, a in (('Snap_Dactyl', deg), ('Pinch_Dactyl', deg * 0.5)):
                pb = aob.pose.bones[b]; rest = pb.bone.matrix_local.to_3x3()
                q = Quaternion(AX['R'], radians(deg if b == 'Snap_Dactyl' else deg * 0.6)).to_matrix()
                pb.rotation_mode = 'QUATERNION'
                pb.rotation_quaternion = (rest.inverted() @ q @ rest).to_quaternion()
        return f

    sfx = '_' + SIDE
    sg = 1 if SIDE == 'R' else -1
    shot('prev_34' + sfx, (1.9, 0.95, 2.1), (0, -0.05, 0.25), 50)
    shot('prev_side' + sfx, (3.2, 0.05, 0.25), (0, -0.05, 0.25), 55, 'TigerOrange')
    shot('prev_top' + sfx, (0.0, 3.4, 0.26), (0, 0, 0.25), 50, 'GlassRedBand')
    shot('prev_fp' + sfx, (0, 0.15, 0.30), (0, -0.30, 1.3), 14, 'CandyStripe', open_claws(80))
    shot('prev_open' + sfx, (1.05 * sg, 0.30, 1.75), (0.22 * sg, -0.03, 1.05), 50, 'TigerOrange')
    shot('prev_back' + sfx, (-1.7, 0.8, -1.5), (0, -0.05, 0.3), 45, 'GlassRedBand')
