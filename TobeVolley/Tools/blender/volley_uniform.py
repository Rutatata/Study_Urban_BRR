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

# область -> (низ, верх, макс. |x| — подол и рукав, отступ от кожи, проходы сглаживания, имя материала)
REGIONS = [
    (hips + 0.06, neck - 0.015, sh_x + 0.4 * (el_x - sh_x), 0.016, 12, "Uniform_Tops_01_CLOTH"),
    (up_leg - 0.45 * (up_leg - knee), hips + 0.12, 9.0, 0.026, 4, "Uniform_Bottoms_01_CLOTH"),
    (-9.0, ankle + 0.06, 9.0, 0.012, 3, "Uniform_Shoes_01_CLOTH"),
]


def drape(verts, lo, hi):
    """Ткань спереди не лезет в ложбинки: от самой выступающей точки груди спадает вниз и натянута между сторонами.
    Перёд модели смотрит в -Y, поэтому «выступ» = -y."""
    front = [(v, mw @ v.co) for v in verts]
    front = [(v, w) for v, w in front if abs(w.x) < sh_x and lo < w.z < hi - 0.08 and w.y < arm.matrix_world.translation.y]
    band = [(v, w) for v, w in front if lo + 0.2 * (hi - lo) < w.z]
    if not band: return
    apex = {}
    for v, w in band:   # самая выступающая точка слева и справа
        sd = w.x > 0
        if sd not in apex or -w.y > -apex[sd].y: apex[sd] = w
    if len(apex) < 2: return
    ad = min(-apex[True].y, -apex[False].y); az = 0.5 * (apex[True].z + apex[False].z); ax = 0.5 * (abs(apex[True].x) + abs(apex[False].x))
    def env(x, z):   # огибающая: вниз от вершины пологий спад, вверх — круче (к ключицам), между сторонами — натянуто
        side = max(0.0, abs(x) - ax)
        if z >= az: return ad - 1.2 * (z - az) - 2.0 * side if side == 0.0 else -1e9
        return ad - 0.35 * (az - z) - 0.6 * side
    fv = [v for v, _ in front]
    for it in range(3):
        for v in fv:
            w = mw @ v.co; e = env(w.x, w.z)
            if -w.y < e: w.y = -e; v.co = mwi @ w
        if it < 2: bmesh.ops.smooth_vert(bm, verts=fv, factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)   # смягчить складку


me = body.data
skin = {i for i, m in enumerate(me.materials) if m and "SKIN" in m.name}
cloth = {i for i, m in enumerate(me.materials) if m and "CLOTH" in m.name}
assert skin, "у тела нет материала кожи (SKIN) — это не VRoid-модель?"
proto = me.materials[next(iter(cloth))] if cloth else me.materials[next(iter(skin))]

bm = bmesh.new(); bm.from_mesh(me)
skin_faces = [f for f in bm.faces if f.material_index in skin]
mw = body.matrix_world; mwi = mw.inverted(); m3 = mw.to_3x3()   # границы заданы в мировых координатах
inside = lambda co, lo, hi, xm: lo < co.z < hi and abs(co.x) < xm
for lo, hi, xm, push, passes, name in REGIONS:
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
    base = {}   # новая точка -> (точка на коже, нормаль) в мировых координатах
    for v_old, v_new in vmap.items():
        if v_old not in normals: continue
        c = mw @ v_old.co   # точки за краем ставим ровно на линию подола/рукава — край получается ровным
        c.z = min(max(c.z, lo), hi)
        if abs(c.x) > xm: c.x = math.copysign(xm, c.x)
        n = (m3 @ normals[v_old]).normalized()
        base[v_new] = (c, n)
        v_new.co = mwi @ (c + n * push)
    for f in dup["face_map"].values(): f.material_index = mi
    inner = [v for v in base if not v.is_boundary]   # ткань не облегает каждую выпуклость тела — сглаживаем,
    for _ in range(passes):                           # но не ближе отступа к коже, чтобы тело не проступало
        bmesh.ops.smooth_vert(bm, verts=inner, factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
        for v in inner:
            c, n = base[v]; w = mw @ v.co; d = (w - c).dot(n)
            if d < push: v.co = mwi @ (w + n * (push - d))
    if "Tops" in name:
        drape(inner, lo, hi)
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
