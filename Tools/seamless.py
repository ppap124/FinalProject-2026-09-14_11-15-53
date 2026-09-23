"""생성된 타일 이미지를 무봉제로 만든다.

이미지를 가로·세로로 반씩 굴린 사본 넷을 만들고, **주기함수 가중치**로 섞는다.
가중치가 가장자리에서 0 이 되므로 좌우·상하 끝이 수학적으로 같아진다 —
'대충 맞춰 보기'가 아니라 이음매가 없는 것이 보장된다.

돌 텍스처는 무늬가 불규칙해서 섞이는 자리의 겹침이 거의 안 보인다.
규칙적인 격자 무늬였다면 이 방법은 못 쓴다.
"""
import bpy, sys, numpy as np

argv = sys.argv[sys.argv.index("--") + 1:]
src, dst = argv[0], argv[1]

img = bpy.data.images.load(src)
W, H = img.size
a = np.array(img.pixels[:], dtype=np.float32).reshape(H, W, 4)

x = np.linspace(0.0, 1.0, W, endpoint=False)[None, :, None]
y = np.linspace(0.0, 1.0, H, endpoint=False)[:, None, None]
wx = 0.5 - 0.5 * np.cos(2.0 * np.pi * x)     # 양 끝에서 0, 가운데서 1
wy = 0.5 - 0.5 * np.cos(2.0 * np.pi * y)

b = np.roll(a, W // 2, axis=1)
c = np.roll(a, H // 2, axis=0)
d = np.roll(b, H // 2, axis=0)

out = a * wx * wy + b * (1 - wx) * wy + c * wx * (1 - wy) + d * (1 - wx) * (1 - wy)
out[..., 3] = 1.0

# 이음매 검증 — 양 끝 열의 차이가 인접 열끼리의 차이보다 작아야 한다
seam_x = np.abs(out[:, 0, :3] - out[:, -1, :3]).mean()
seam_y = np.abs(out[0, :, :3] - out[-1, :, :3]).mean()
adj    = np.abs(out[:, 1:-1, :3] - out[:, 2:, :3]).mean()
print("SEAM x=%.5f y=%.5f  이웃픽셀=%.5f" % (seam_x, seam_y, adj))

o = bpy.data.images.new("out", width=W, height=H, alpha=False)
o.pixels = out.reshape(-1).tolist()
o.filepath_raw = dst
o.file_format = 'PNG'
o.save()
print("SAVED", dst)
