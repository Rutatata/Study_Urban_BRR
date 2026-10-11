# Чистка анимации (FBX) для TobeVolley: убирает дрожание, обрезает, приводит к 30 к/с.
# Нужна для анимаций из видео (QuickMagic, Plask, Rokoko Vision): у них дрожат кисти и стопы.
#
# Запуск (Blender ставить с blender.org):
#   blender -b -P clean_mocap.py -- вход.fbx выход.fbx [--smooth 1.5] [--start 10] [--end 80]
#     --smooth  сила сглаживания в кадрах (0 = без сглаживания), по умолчанию 1.5
#     --start/--end  обрезка по кадрам исходника
# Готовый файл класть в Assets/_Tobe/Resources/Anim — игра подхватит его сама.
import sys, math, argparse
import bpy

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("src"); ap.add_argument("dst")
ap.add_argument("--smooth", type=float, default=1.5)
ap.add_argument("--start", type=float, default=None)
ap.add_argument("--end", type=float, default=None)
a = ap.parse_args(argv)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=a.src, automatic_bone_orientation=False)
arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
act = arm.animation_data.action


def fcurves(action):
    if hasattr(action, "layers") and action.layers:   # Blender 4.4+: слоистые действия
        out = []
        for layer in action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags: out.extend(bag.fcurves)
        return out
    return list(action.fcurves)


fcs = fcurves(act)
scene = bpy.context.scene
src_fps = scene.render.fps / scene.render.fps_base
f0, f1 = act.frame_range
f0 = a.start if a.start is not None else f0
f1 = a.end if a.end is not None else f1
n = int((f1 - f0) / src_fps * 30) + 1      # число кадров при 30 к/с
frames = [f0 + i * src_fps / 30 for i in range(n)]

# снимаем значения; кватернионы одной кости выравниваем по знаку (q и -q — один поворот), иначе сглаживание «перекрутит»
data = {fc: [fc.evaluate(f) for f in frames] for fc in fcs}
quats = {}
for fc in fcs:
    if fc.data_path.endswith("rotation_quaternion"): quats.setdefault(fc.data_path, [None] * 4)[fc.array_index] = fc
for comps in quats.values():
    if None in comps: continue
    for i in range(1, n):
        dot = sum(data[c][i] * data[c][i - 1] for c in comps)
        if dot < 0:
            for c in comps: data[c][i] = -data[c][i]


def jitter(vals):   # средняя «вторая разность» — мера дрожания
    return sum(abs(vals[i + 1] - 2 * vals[i] + vals[i - 1]) for i in range(1, len(vals) - 1)) / max(1, len(vals) - 2)


before = sum(jitter(v) for v in data.values())
if a.smooth > 0:
    r = int(math.ceil(a.smooth * 3))
    w = [math.exp(-0.5 * (k / a.smooth) ** 2) for k in range(-r, r + 1)]
    for fc, v in data.items():
        out = []
        for i in range(n):
            s = ws = 0.0
            for k in range(-r, r + 1):
                j = min(n - 1, max(0, i + k)); s += v[j] * w[k + r]; ws += w[k + r]
            out.append(s / ws)
        data[fc] = out
for comps in quats.values():    # после сглаживания кватернион снова единичный
    if None in comps: continue
    for i in range(n):
        l = math.sqrt(sum(data[c][i] ** 2 for c in comps)) or 1.0
        for c in comps: data[c][i] /= l
after = sum(jitter(v) for v in data.values())

for fc, v in data.items():
    fc.keyframe_points.clear()
    fc.keyframe_points.add(n)
    for i, val in enumerate(v):
        kp = fc.keyframe_points[i]; kp.co = (i + 1, val); kp.interpolation = "LINEAR"
    fc.update()
scene.render.fps, scene.render.fps_base = 30, 1
scene.frame_start, scene.frame_end = 1, n

bpy.ops.export_scene.fbx(filepath=a.dst, object_types={"ARMATURE", "MESH"}, add_leaf_bones=False,
                         bake_anim=True, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False,
                         bake_anim_simplify_factor=0.0)
print(f"[clean_mocap] кадров {n} (30 к/с), дрожание {before:.4f} -> {after:.4f}")
assert a.smooth <= 0 or after <= before + 1e-9, "сглаживание должно уменьшать дрожание"
