"""
뱀형 메시에 척추 본 체인을 깔고 자동 웨이트를 건다.

VARCO 오토리깅은 humanoid 골격만 준다 — 뱀에 씌우면 팔다리 본이 몸통에 박힌다.
대신 긴 축을 따라 단면 중심을 구해서 그 점들을 잇는 본 체인을 만든다.
뱀은 팔다리 분기가 없어서 사람보다 오히려 쉽다.

사용: blender --background --python rig_serpent.py -- <입력.glb> <출력.glb> [본개수]
"""
import bpy, sys
from mathutils import Vector

argv = sys.argv[sys.argv.index("--")+1:]
src, dst = argv[0], argv[1]
NBONE = int(argv[2]) if len(argv) > 2 else 16

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete()

bpy.ops.import_scene.gltf(filepath=src)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
if not meshes:
    print("FAIL 메시 없음"); sys.exit(1)

# 여러 조각이면 하나로 합친다 — 본 하나에 여러 오브젝트를 물리면 관리가 번거롭다
if len(meshes) > 1:
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes: o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
mesh = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
bpy.context.view_layer.objects.active = mesh
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

world = [mesh.matrix_world @ v.co for v in mesh.data.vertices]
mn = Vector((min(v.x for v in world), min(v.y for v in world), min(v.z for v in world)))
mx = Vector((max(v.x for v in world), max(v.y for v in world), max(v.z for v in world)))
size = mx - mn

# 가장 긴 축이 몸통 방향이다
li = max(range(3), key=lambda i: size[i])
print("LONG_AXIS", "XYZ"[li], "%.3f" % size[li])

# 긴 축을 잘라 각 단면의 중심을 구한다 — 이 점들이 척추가 된다
lo, hi = mn[li], mx[li]
pts = []
for i in range(NBONE + 1):
    a = lo + (hi - lo) * i / (NBONE + 1)
    b = lo + (hi - lo) * (i + 1) / (NBONE + 1)
    sel = [v for v in world if a <= v[li] < b] or [v for v in world if abs(v[li] - a) < (hi-lo)/NBONE]
    if not sel:
        continue
    c = Vector((sum(v.x for v in sel)/len(sel),
                sum(v.y for v in sel)/len(sel),
                sum(v.z for v in sel)/len(sel)))
    c[li] = (a + b) * 0.5      # 긴 축 위치는 구간 한가운데로 고정
    pts.append(c)

print("SPINE_POINTS", len(pts))
if len(pts) < 3:
    print("FAIL 단면이 너무 적다"); sys.exit(1)

arm_data = bpy.data.armatures.new("SerpentArmature")
arm = bpy.data.objects.new("Armature", arm_data)
bpy.context.collection.objects.link(arm)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='EDIT')

# Root — 유니티가 기대하는 최상위 본. 몸 아래 바닥에 둔다
ground = Vector(((mn.x+mx.x)*0.5, (mn.y+mx.y)*0.5, mn.z))
root = arm_data.edit_bones.new("Root")
root.head = ground
root.tail = ground + Vector((0, 0, max(0.05, size.z * 0.25)))

prev = None
for i in range(len(pts) - 1):
    b = arm_data.edit_bones.new("Spine_%02d" % i)
    b.head = pts[i]
    b.tail = pts[i + 1]
    if prev is None:
        b.parent = root
        b.use_connect = False
    else:
        b.parent = prev
        b.use_connect = True
    prev = b

bpy.ops.object.mode_set(mode='OBJECT')
print("BONES", len(arm_data.bones))

# 자동 웨이트. 실패하면 엔벨로프로 물러선다 — 생성 메시는 비다양체인 경우가 있다
bpy.ops.object.select_all(action='DESELECT')
mesh.select_set(True)
arm.select_set(True)
bpy.context.view_layer.objects.active = arm
ok = False
try:
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')
    ok = True
    print("WEIGHTS automatic")
except Exception as e:
    print("AUTO_FAILED", e)
    bpy.ops.object.select_all(action='DESELECT')
    mesh.select_set(True); arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.parent_set(type='ARMATURE_ENVELOPE')
    ok = True
    print("WEIGHTS envelope")

# 웨이트가 실제로 붙었는지 확인
ngroups = len(mesh.vertex_groups)
unweighted = 0
for v in mesh.data.vertices:
    if not v.groups or sum(g.weight for g in v.groups) < 1e-4:
        unweighted += 1
print("VERTEX_GROUPS", ngroups)
print("UNWEIGHTED", unweighted, "/", len(mesh.data.vertices))

bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.gltf(filepath=dst, export_format='GLB', use_selection=True)
print("EXPORTED", dst)
