# Переодевает VRoid-модель (VRM) в волейбольную форму: убирает платья/юбки/худи и «выращивает» из кожи тела
# облегающую футболку с коротким рукавом, шорты и кроссовки. Новые куски кожи наследуют веса костей, поэтому
# двигаются вместе с телом; имена материалов (Tops/Bottoms/Shoes) игра узнаёт и красит в цвета команды.
#
# Нужен Blender 4.2+ с дополнением VRM (extensions.blender.org, «VRM format»).
#   blender -b -P volley_uniform.py -- вход.vrm выход.vrm [превью.png]
import sys, math
import bpy, bmesh

argv = sys.argv[sys.argv.index("--") + 1:]
src, dst = argv[0], argv[1]
preview = argv[2] if len(argv) > 2 else None

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.preferences.addon_enable(module="bl_ext.blender_org.vrm")
bpy.ops.import_scene.vrm(filepath=src)
arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
body = bpy.data.objects["Body"]


def bone(*names):
    for n in names:
        b = arm.data.bones.get(n)
        if b: return arm.matrix_world @ b.head_local
    raise KeyError(names)


hips = bone("J_Bip_C_Hips").z
neck = bone("J_Bip_C_Neck").z
up_leg = bone("J_Bip_L_UpperLeg").z
knee = bone("J_Bip_L_LowerLeg").z
ankle = bone("J_Bip_L_Foot").z
sh_x = abs(bone("J_Bip_L_UpperArm").x)
el_x = abs(bone("J_Bip_L_LowerArm").x)

# область -> (низ, верх, макс. |x| — подол и рукав, отступ от кожи, имя материала)
REGIONS = [
    (hips + 0.04, neck - 0.015, sh_x + 0.4 * (el_x - sh_x), 0.016, "Uniform_Tops_01_CLOTH"),
    (up_leg - 0.45 * (up_leg - knee), hips + 0.10, 9.0, 0.026, "Uniform_Bottoms_01_CLOTH"),
    (-9.0, ankle + 0.06, 9.0, 0.012, "Uniform_Shoes_01_CLOTH"),
]

me = body.data
skin = {i for i, m in enumerate(me.materials) if m and "SKIN" in m.name}
cloth = {i for i, m in enumerate(me.materials) if m and "CLOTH" in m.name}
assert skin, "у тела нет материала кожи (SKIN) — это не VRoid-модель?"
proto = me.materials[next(iter(cloth))] if cloth else me.materials[next(iter(skin))]

bm = bmesh.new(); bm.from_mesh(me)
skin_faces = [f for f in bm.faces if f.material_index in skin]
mw = body.matrix_world; mwi = mw.inverted(); m3 = mw.to_3x3()   # границы заданы в мировых координатах
inside = lambda co, lo, hi, xm: lo < co.z < hi and abs(co.x) < xm
for lo, hi, xm, push, name in REGIONS:
    mat = proto.copy(); mat.name = name
    ext = getattr(mat, "vrm_addon_extension", None)
    if ext is not None:   # одноцветная ткань: цвет задаёт игра, старая текстура платья по UV кожи дала бы мусор
        ext.mtoon1.pbr_metallic_roughness.base_color_texture.index.source = None
        ext.mtoon1.extensions.vrmc_materials_mtoon.shade_multiply_texture.index.source = None
    me.materials.append(mat); mi = len(me.materials) - 1
    faces = [f for f in skin_faces if any(inside(mw @ v.co, lo, hi, xm) for v in f.verts)]
    if not faces: continue
    normals = {v: v.normal.copy() for f in faces for v in f.verts}
    dup = bmesh.ops.duplicate(bm, geom=faces)
    vmap = dup["vert_map"]
    for v_old, v_new in vmap.items():
        if v_old not in normals: continue
        c = mw @ v_old.co   # точки за краем ставим ровно на линию подола/рукава — край получается ровным
        c.z = min(max(c.z, lo), hi)
        if abs(c.x) > xm: c.x = math.copysign(xm, c.x)
        v_new.co = mwi @ (c + (m3 @ normals[v_old]).normalized() * push)
    for f in dup["face_map"].values(): f.material_index = mi
    inner = [v for v in vmap.values() if not v.is_boundary]   # ткань не облегает каждую выпуклость тела — сглаживаем
    for _ in range(3): bmesh.ops.smooth_vert(bm, verts=inner, factor=0.4, use_axis_x=True, use_axis_y=True, use_axis_z=True)
bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.material_index in cloth], context="FACES")
bm.to_mesh(me); bm.free()
bpy.ops.object.select_all(action="DESELECT")

bpy.ops.export_scene.vrm(filepath=dst)
print(f"[volley_uniform] {src} -> {dst}")

if preview:   # быстрый снимок спереди для проверки глазами
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"; sc.display.shading.color_type = "MATERIAL"; sc.display.shading.light = "STUDIO"
    for m in me.materials:
        if m and m.name.startswith("Uniform_"): m.diffuse_color = {"T": (0.15, 0.3, 0.9, 1), "B": (0.1, 0.1, 0.15, 1), "S": (0.95, 0.95, 0.95, 1)}[m.name[8]]
    sc.render.resolution_x, sc.render.resolution_y = 300, 520
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.type = "ORTHO"; cam.data.ortho_scale = 1.95
    cam.location = (0, -4.0, 0.85); cam.rotation_euler = (math.radians(90), 0, 0)
    sc.render.filepath = preview; bpy.ops.render.render(write_still=True)
