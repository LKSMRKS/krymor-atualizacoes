"""bbox (m, mundo do glb: X largura, Y altura, Z profundidade) de cada .glb — usado para a colisão das peças no servidor"""
import json, struct, sys, glob, os
import numpy as np
def quat_mat(q):
    x, y, z, w = q
    return np.array([[1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w)], [2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w)], [2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y)]])
def node_m(n):
    if "matrix" in n: return np.array(n["matrix"]).reshape(4, 4).T
    M = np.eye(4); R = quat_mat(n.get("rotation", [0, 0, 0, 1])); S = np.diag(n.get("scale", [1, 1, 1]))
    M[:3, :3] = R @ S; M[:3, 3] = n.get("translation", [0, 0, 0]); return M
def bbox(path):
    b = open(path, "rb").read(); ln = struct.unpack("<I", b[12:16])[0]; g = json.loads(b[20:20 + ln])
    pts = []
    def walk(i, M):
        n = g["nodes"][i]; M = M @ node_m(n)
        if "mesh" in n:
            for p in g["meshes"][n["mesh"]]["primitives"]:
                a = g["accessors"][p["attributes"]["POSITION"]]
                mn, mx = a["min"], a["max"]
                for cx in (mn[0], mx[0]):
                    for cy in (mn[1], mx[1]):
                        for cz in (mn[2], mx[2]): pts.append((M @ np.array([cx, cy, cz, 1]))[:3])
        for c in n.get("children", []): walk(c, M)
    sc = g["scenes"][g.get("scene", 0)]
    for r in sc["nodes"]: walk(r, np.eye(4))
    P = np.array(pts); return P.min(0).round(2).tolist(), P.max(0).round(2).tolist()
if __name__ == "__main__":
    out = {}
    for f in sorted(glob.glob(sys.argv[1] + "/**/*.glb", recursive=True)):
        if f.endswith("_colisao.glb"): continue
        try: out[os.path.basename(f)[:-4]] = bbox(f)
        except Exception as e: print("ERRO", f, e)
    json.dump(out, open(sys.argv[2], "w"), indent=0)
    for k, (mn, mx) in out.items(): print(k, "larg", round(mx[0]-mn[0], 2), "alt", round(mx[1]-mn[1], 2), "prof", round(mx[2]-mn[2], 2), "min", mn)
