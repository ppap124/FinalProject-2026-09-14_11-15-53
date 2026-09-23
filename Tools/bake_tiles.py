"""
이음매 없는 PBR 타일 텍스처를 절차적으로 굽는다.

베이스컬러 / 노멀 / 러프니스 3장을 PNG로 낸다.

**타일링이 수학적으로 완벽하다.** 일반 노이즈 텍스처는 UV 0..1 밖으로 이어지지 않아
이음매가 생기므로, 노이즈를 4D로 쓰되 좌표를 원환면(torus)에 감는다:

    X = cos(2πu)   Y = sin(2πu)   Z = cos(2πv)   W = sin(2πv)

u 가 0 에서 1 로 갈 때 (X, Y) 가 원을 한 바퀴 돌아 제자리로 오므로
좌우·상하가 반드시 맞물린다. 벽돌 텍스처는 원래 UV 주기적이라 그대로 쓴다.

사용:
  blender --background --python Tools/bake_tiles.py -- <출력폴더> [해상도]
"""
import bpy
import sys
import os
import math

argv = sys.argv[sys.argv.index("--") + 1:]
OUT = argv[0]
RES = int(argv[1]) if len(argv) > 1 else 1024

os.makedirs(OUT, exist_ok=True)


def srgb(hexstr):
    """#RRGGBB 를 블렌더가 쓰는 선형 색으로. 노드 색 입력은 sRGB 가 아니라 선형이다.

    이걸 안 거치고 sRGB 값을 그대로 넣으면 세 배쯤 밝게 구워진다.
    """
    h = hexstr.lstrip("#")
    out = []
    for i in (0, 2, 4):
        c = int(h[i:i + 2], 16) / 255.0
        out.append(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4)
    return (out[0], out[1], out[2], 1.0)


def clear():
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.images, bpy.data.node_groups):
        for item in list(block):
            if item.users == 0:
                block.remove(item)


def seamless_noise(nt, uvnode, scale, detail, tag):
    """원환면에 감은 4D 노이즈. 이음매가 생기지 않는다."""
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(uvnode.outputs["UV"], sep.inputs["Vector"])

    def angle(src):
        m = nt.nodes.new("ShaderNodeMath")
        m.operation = 'MULTIPLY'
        m.inputs[1].default_value = math.tau
        nt.links.new(src, m.inputs[0])
        return m

    au = angle(sep.outputs["X"])
    av = angle(sep.outputs["Y"])

    def trig(src, op):
        m = nt.nodes.new("ShaderNodeMath")
        m.operation = op
        nt.links.new(src, m.inputs[0])
        return m

    cu, su = trig(au.outputs[0], 'COSINE'), trig(au.outputs[0], 'SINE')
    cv, sv = trig(av.outputs[0], 'COSINE'), trig(av.outputs[0], 'SINE')

    comb = nt.nodes.new("ShaderNodeCombineXYZ")
    nt.links.new(cu.outputs[0], comb.inputs["X"])
    nt.links.new(su.outputs[0], comb.inputs["Y"])
    nt.links.new(cv.outputs[0], comb.inputs["Z"])

    n = nt.nodes.new("ShaderNodeTexNoise")
    n.name = "noise_" + tag
    n.noise_dimensions = '4D'
    nt.links.new(comb.outputs["Vector"], n.inputs["Vector"])
    nt.links.new(sv.outputs[0], n.inputs["W"])
    n.inputs["Scale"].default_value = scale
    n.inputs["Detail"].default_value = detail
    return n


def build(name, dark, light, mortar_col, tiles, mortar_size, squash, rough_lo, rough_hi, bump_strength, target_hex):
    """벽돌 패턴 + 이음매 없는 노이즈로 석재 재질을 만든다."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()

    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])

    uv = nt.nodes.new("ShaderNodeTexCoord")

    # 돌 모양 — 벽돌 텍스처는 UV 주기적이라 그대로 이음매가 맞는다
    def brick(c1, c2, mort, tag):
        b = nt.nodes.new("ShaderNodeTexBrick")
        b.name = "brick_" + tag
        b.offset = 0.5
        b.offset_frequency = 2
        b.squash = squash
        b.squash_frequency = 2
        nt.links.new(uv.outputs["UV"], b.inputs["Vector"])
        b.inputs["Color1"].default_value = c1
        b.inputs["Color2"].default_value = c2
        b.inputs["Mortar"].default_value = mort
        b.inputs["Scale"].default_value = tiles
        b.inputs["Mortar Size"].default_value = mortar_size
        b.inputs["Mortar Smooth"].default_value = 0.1
        b.inputs["Bias"].default_value = 0.0
        b.inputs["Brick Width"].default_value = 0.5
        b.inputs["Row Height"].default_value = 0.25
        return b

    white = (1, 1, 1, 1)
    black = (0, 0, 0, 1)

    # 줄눈 마스크 — 돌은 흰색, 줄눈은 검은색
    joint = brick(white, white, black, "joint")

    # 돌마다 밝기가 조금씩 다르게 — 같은 색이 반복되면 눈에 띈다
    vary = brick((0.55, 0.55, 0.55, 1), (1, 1, 1, 1), white, "vary")

    grunge = seamless_noise(nt, uv, 9.0, 6.0, "grunge")
    fine = seamless_noise(nt, uv, 42.0, 3.0, "fine")

    # 돌 색 = 어두운색 ~ 밝은색 사이를 돌별 밝기와 얼룩으로 섞는다
    mix_var = nt.nodes.new("ShaderNodeMix")
    mix_var.data_type = 'RGBA'
    mix_var.inputs["A"].default_value = dark
    mix_var.inputs["B"].default_value = light
    nt.links.new(vary.outputs["Color"], mix_var.inputs["Factor"])

    # 얼룩은 **섞는 게 아니라 곱한다.** 노이즈 값(0~1)을 색에 직접 섞으면
    # 어두운 돌(선형 0.03)이 노이즈 평균(0.5) 쪽으로 끌려가 회색이 돼버린다.
    # 0.78~1.25 배율로 바꿔 곱하면 밝기는 지키고 얼룩만 생긴다.
    grunge_amt = nt.nodes.new("ShaderNodeMapRange")
    grunge_amt.inputs["From Min"].default_value = 0.0
    grunge_amt.inputs["From Max"].default_value = 1.0
    grunge_amt.inputs["To Min"].default_value = 0.78
    grunge_amt.inputs["To Max"].default_value = 1.25
    nt.links.new(grunge.outputs["Fac"], grunge_amt.inputs["Value"])

    grime = nt.nodes.new("ShaderNodeMix")
    grime.data_type = 'RGBA'
    grime.blend_type = 'MULTIPLY'
    grime.inputs["Factor"].default_value = 1.0
    nt.links.new(mix_var.outputs["Result"], grime.inputs["A"])
    nt.links.new(grunge_amt.outputs["Result"], grime.inputs["B"])

    # 줄눈을 끼워넣는다
    stone = nt.nodes.new("ShaderNodeMix")
    stone.data_type = 'RGBA'
    stone.inputs["A"].default_value = mortar_col
    nt.links.new(joint.outputs["Color"], stone.inputs["Factor"])
    nt.links.new(grime.outputs["Result"], stone.inputs["B"])

    # 밝기 보정 자리. 한 번 구워 평균을 재고 여기 배율을 넣은 뒤 다시 굽는다.
    # 노드 여러 겹을 거치며 밝기가 어디서 얼마나 붙는지 일일이 따지는 것보다
    # 결과를 재서 맞추는 편이 확실하고, 파라미터를 바꿔도 계속 맞는다.
    cal = nt.nodes.new("ShaderNodeMix")
    cal.name = "albedo_out"            # 굽는 쪽에서 찾아 쓴다
    cal.data_type = 'RGBA'
    cal.blend_type = 'MULTIPLY'
    cal.inputs["Factor"].default_value = 1.0
    cal.inputs["B"].default_value = (1.0, 1.0, 1.0, 1.0)
    nt.links.new(stone.outputs["Result"], cal.inputs["A"])
    nt.links.new(cal.outputs["Result"], bsdf.inputs["Base Color"])

    # 러프니스 — 줄눈이 돌보다 거칠다
    rough = nt.nodes.new("ShaderNodeMapRange")
    rough.inputs["From Min"].default_value = 0.0
    rough.inputs["From Max"].default_value = 1.0
    rough.inputs["To Min"].default_value = rough_hi   # 줄눈
    rough.inputs["To Max"].default_value = rough_lo   # 돌
    nt.links.new(joint.outputs["Color"], rough.inputs["Value"])
    nt.links.new(rough.outputs["Result"], bsdf.inputs["Roughness"])
    bsdf.inputs["Metallic"].default_value = 0.0

    # 높이 = 돌은 솟고 줄눈은 파이고, 표면에 잔주름
    h_mix = nt.nodes.new("ShaderNodeMix")
    h_mix.name = "height_out"          # 굽는 쪽에서 찾아 쓴다
    h_mix.data_type = 'FLOAT'
    h_mix.inputs["Factor"].default_value = 0.25
    nt.links.new(joint.outputs["Color"], h_mix.inputs["A"])
    nt.links.new(fine.outputs["Fac"], h_mix.inputs["B"])

    bump = nt.nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value = bump_strength
    bump.inputs["Distance"].default_value = 0.08
    nt.links.new(h_mix.outputs["Result"], bump.inputs["Height"])
    nt.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])

    return mat


def bake(mat, base, target_hex, bump_amount=28.0):
    """평면 하나에 재질을 물리고 세 장을 굽는다."""
    bpy.ops.mesh.primitive_plane_add(size=2)
    plane = bpy.context.object
    plane.data.materials.append(mat)

    nt = mat.node_tree
    img_node = nt.nodes.new("ShaderNodeTexImage")
    nt.nodes.active = img_node

    scn = bpy.context.scene
    scn.render.engine = 'CYCLES'
    scn.cycles.device = 'CPU'
    scn.cycles.samples = 16
    scn.render.bake.margin = 16
    scn.render.bake.use_selected_to_active = False

    made = []

    # 베이스컬러는 DIFFUSE 가 아니라 **EMIT 으로 굽는다.**
    # DIFFUSE 는 조명 패스를 꺼도 월드 환경광이 섞여 들어와 밝아진다.
    # 색 노드를 그대로 발광으로 내보내면 조명과 무관한 순수한 알베도가 나온다.
    #
    # 먼저 한 번 굽고 평균 밝기를 재서 목표 색에 맞춘 뒤 다시 굽는다.
    probe = bake_socket(nt, img_node, "albedo_out", "Result", base, "P",
                        is_color=True, save=False)
    k = calibrate(probe, target_hex)
    cal = nt.nodes.get("albedo_out")
    cal.inputs["B"].default_value = (k[0], k[1], k[2], 1.0)
    print("CALIBRATE %s  배율 %.3f %.3f %.3f" % (base, k[0], k[1], k[2]))
    bpy.data.images.remove(probe)

    made.append(bake_socket(nt, img_node, "albedo_out", "Result",
                            base, "BaseColor", is_color=True))

    # 러프니스는 데이터 패스라 조명 영향이 없다
    img = bpy.data.images.new(base + "_Roughness", RES, RES, alpha=False,
                              float_buffer=False, is_data=True)
    img_node.image = img
    bpy.ops.object.bake(type='ROUGHNESS')
    path = os.path.join(OUT, base + "_Roughness.png")
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    made.append(path)
    print("BAKED", path)

    # ── 노멀맵 ──
    # Bump 노드를 그대로 구우면 UV 경계에서 기울기를 반대편까지 못 보고 끊긴다.
    # 그래서 **높이만 굽고**(높이는 색과 같은 노드 그래프라 이음매가 완벽하다)
    # 기울기를 여기서 감아서(wrap) 계산한다.
    himg = bake_socket(nt, img_node, "height_out", "Result", base, "H",
                       is_color=False, save=False)
    made.append(height_to_normal(himg, base, bump_amount))

    bpy.data.objects.remove(plane, do_unlink=True)
    return made


def calibrate(img, target_hex):
    """구워진 이미지의 평균이 목표 색이 되도록 채널별 배율을 구한다.

    블렌더는 sRGB 이미지 버퍼에 이미 sRGB 로 인코딩해 넣으므로
    픽셀값을 선형으로 되돌려 평균을 내야 한다.
    """
    w, h = img.size
    px = list(img.pixels)

    def s2l(c):
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4

    acc = [0.0, 0.0, 0.0]
    n = 0
    for y in range(0, h, 8):
        for x in range(0, w, 8):
            i = (y * w + x) * 4
            for ch in range(3):
                acc[ch] += s2l(px[i + ch])
            n += 1

    tgt = srgb(target_hex)
    out = []
    for ch in range(3):
        mean = acc[ch] / n
        out.append(1.0 if mean < 1e-6 else min(8.0, tgt[ch] / mean))
    return out


def bake_socket(nt, img_node, node_name, socket, base, suffix, is_color, save=True):
    """어떤 노드의 출력이든 발광으로 내보내 그대로 굽는다. 조명이 섞이지 않는다."""
    src = nt.nodes.get(node_name)
    out_node = next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL')
    saved = out_node.inputs["Surface"].links[0].from_socket

    emit = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(src.outputs[socket], emit.inputs["Color"])
    nt.links.new(emit.outputs["Emission"], out_node.inputs["Surface"])

    img = bpy.data.images.new(base + "_" + suffix, RES, RES, alpha=False,
                              float_buffer=False, is_data=not is_color)
    img_node.image = img
    bpy.ops.object.bake(type='EMIT')

    nt.links.new(saved, out_node.inputs["Surface"])
    nt.nodes.remove(emit)

    if not save:
        return img

    path = os.path.join(OUT, base + "_" + suffix + ".png")
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    print("BAKED", path)
    return path


def height_to_normal(himg, base, strength):
    """높이맵을 노멀맵으로. 가장자리에서 반대편을 참조해 이음매가 생기지 않는다."""
    w, h = himg.size
    src = list(himg.pixels)

    def H(x, y):
        return src[((y % h) * w + (x % w)) * 4]      # % 가 감아주는 부분

    out = [0.0] * (w * h * 4)
    for y in range(h):
        row = y * w
        for x in range(w):
            dx = (H(x + 1, y) - H(x - 1, y)) * strength
            dy = (H(x, y + 1) - H(x, y - 1)) * strength

            nx, ny, nz = -dx, -dy, 1.0
            inv = 1.0 / math.sqrt(nx * nx + ny * ny + 1.0)
            nx *= inv
            ny *= inv
            nz *= inv

            i = (row + x) * 4
            out[i]     = nx * 0.5 + 0.5
            out[i + 1] = ny * 0.5 + 0.5
            out[i + 2] = nz * 0.5 + 0.5
            out[i + 3] = 1.0

    nimg = bpy.data.images.new(base + "_Normal", w, h, alpha=False, float_buffer=False, is_data=True)
    nimg.pixels = out

    path = os.path.join(OUT, base + "_Normal.png")
    nimg.filepath_raw = path
    nimg.file_format = 'PNG'
    nimg.save()
    print("BAKED", path)
    return path


clear()

# 전투 바닥 — 어두운 회녹색 판석. 시안의 #353936 을 한가운데 두고 위아래로 흔든다
ground = build(
    "TileGround",
    dark=srgb("#2E322F"),
    light=srgb("#3F443D"),
    mortar_col=srgb("#14181A"),           # 줄눈은 거의 검정
    tiles=5.0, mortar_size=0.022, squash=0.85,
    rough_lo=0.78, rough_hi=0.96, bump_strength=0.55, target_hex="#353936",
)

# 길 — 베이지 포석. 시안의 #B8A375 가 한가운데
road = build(
    "TileRoad",
    dark=srgb("#A38F66"),
    light=srgb("#C6B184"),
    mortar_col=srgb("#4A4030"),
    tiles=9.0, mortar_size=0.030, squash=1.0,
    rough_lo=0.70, rough_hi=0.95, bump_strength=0.7, target_hex="#B8A375",
)

clear_after = []
clear_after += bake(ground, "TileGround", "#353936", 22.0)
clear_after += bake(road, "TileRoad", "#B8A375", 30.0)

print("DONE", len(clear_after), "files")
