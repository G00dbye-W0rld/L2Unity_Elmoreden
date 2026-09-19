# Import des modeles de PNJ et de monstres depuis le client Orfen.
# umodel exporte mesh (psk), animations (psa) et textures ; Blender les convertit en FBX
# dans Resources/Data/Animations, avec un manifeste lu par l'outil Unity (L2 > PNJ).
#
# blender --background --python OrfenNpcImport.py -- --meshes LineageNpcs.heroes_obelisk_human_m00
# blender --background --python OrfenNpcImport.py -- --missing      (tous les modeles absents du projet)

import bpy, numpy, os, sys, re, json, glob, shutil, subprocess, argparse

ORFEN = r"F:\Lineage\Client\Lineage II - Tale Of Aden - Salvation - Etinas Fate (Orfen)"
PROJECT = r"D:\Jeux\PROJET_L2UNITY\l2-unity-main\l2-unity\Assets"
SERVER_NPCS = r"D:\Jeux\PROJET_L2UNITY\l2-unity-gameserver-master\gameserver\data\xml\npcs"
WORK = r"D:\Jeux\MAP_L2Unity\export_npc"

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
parser = argparse.ArgumentParser()
parser.add_argument("--meshes", default="", help="Paquet.mesh separes par des virgules")
parser.add_argument("--missing", action="store_true", help="Tous les modeles de Npcgrp absents du projet")
parser.add_argument("--force", action="store_true", help="Refaire meme si le FBX existe")
parser.add_argument("--limit", type=int, default=0)
args = parser.parse_args(argv)

UMODEL = os.path.join(ORFEN, "umodel_win32", "umodel_64.exe")
ANIM_DIR = os.path.join(ORFEN, "Animations")
META = os.path.join(PROJECT, "StreamingAssets", "Data", "Meta")

if "io_import_scene_unreal_psa_psk_280" not in bpy.context.preferences.addons:
    bpy.ops.preferences.addon_enable(module="io_import_scene_unreal_psa_psk_280")
import io_import_scene_unreal_psa_psk_280 as psk


def umodel(*params):
    cmd = [UMODEL, "-game=l2", "-path=" + ORFEN] + list(params)
    res = subprocess.run(cmd, cwd=ANIM_DIR, capture_output=True, text=True, errors="replace")
    return res.stdout + res.stderr


def read_npcgrp():
    # mesh -> (liste des npc_id, type) ; seuls les PNJ connus du serveur comptent
    server_ids = set()
    for f in glob.glob(os.path.join(SERVER_NPCS, "*.xml")):
        with open(f, encoding="utf-8", errors="replace") as h:
            server_ids.update(int(x) for x in re.findall(r'<npc id="(\d+)"', h.read()))
    meshes = {}
    with open(os.path.join(META, "Npcgrp_Classic.txt"), encoding="utf-8", errors="replace") as h:
        for line in h:
            m = re.search(r"\bnpc_id=(\d+)", line)
            mesh = re.search(r"mesh_name=\[([^\]]+)\]", line)
            cls = re.search(r"class_name=\[([^.\]]+)", line)
            if m and mesh and int(m.group(1)) in server_ids:
                entry = meshes.setdefault(mesh.group(1), {"ids": [], "cls": cls.group(1) if cls else ""})
                entry["ids"].append(int(m.group(1)))
    return meshes


def fbx_path(package, name):
    return os.path.join(PROJECT, "Resources", "Data", "Animations", package, name, "Model", name + ".fbx")


def prefab_exists(package, name):
    base = os.path.join(PROJECT, "Resources", "Data", "Animations")
    if os.path.exists(os.path.join(base, package, name, name + ".prefab")):
        return True
    root = package.rstrip("0123456789")
    return root != package and os.path.exists(os.path.join(base, root, name, name + ".prefab"))


def candidate_packages(package):
    # le paquet de Npcgrp d'abord, puis ceux de la meme famille (LineageNPCs2, 3...)
    root = package.rstrip("0123456789").lower()
    files = [os.path.splitext(f)[0] for f in os.listdir(ANIM_DIR) if f.lower().endswith(".ukx")]
    first = [f for f in files if f.lower() == package.lower()]
    rest = sorted(f for f in files if f.lower().startswith(root) and f not in first)
    return first + rest


def export_mesh(package, name, out):
    for pkg in candidate_packages(package):
        log = umodel("-export", "-png", "-out=" + out, pkg + ".ukx", name)
        if "Exporting SkeletalMesh" in log:
            anim = re.search(r"Loading MeshAnimation (\S+) from package Animations/(\S+)\.ukx", umodel("-dump", pkg + ".ukx", name))
            return pkg, anim.groups() if anim else None
    return None, None


def find_file(root, sub, stem, ext):
    hits = glob.glob(os.path.join(root, "**", sub, stem + ext), recursive=True)
    return hits[0] if hits else None


def alpha_min(image):
    pixels = numpy.empty(len(image.pixels), dtype=numpy.float32)
    image.pixels.foreach_get(pixels)
    return float(pixels[3::4].min()) if len(pixels) else 1.0


def has_transparency(png):
    # Texture simple sans shader : la transparence vient de pixels a alpha faible (detourage)
    image = bpy.data.images.load(png, check_existing=False)
    try:
        return image.channels == 4 and alpha_min(image) < 0.5
    finally:
        bpy.data.images.remove(image)


def resolve_material(out, slot):
    # FinalBlend ou Shader -> texture diffuse ; sinon la texture porte le nom du materiau
    info = {"slot": slot, "texture": "", "alphaTest": False, "alphaRef": 0, "twoSided": False}
    diffuse = slot
    shader_found = False
    for kind in ("FinalBlend", "Shader", "Combiner"):
        mat = find_file(out, kind, slot, ".mat")
        if mat:
            shader_found = True
            with open(mat, errors="replace") as h:
                d = re.search(r"^Diffuse=(\S+)", h.read(), re.M)
            if d:
                diffuse = d.group(1)
            props = mat[:-4] + ".props.txt"
            if os.path.exists(props):
                with open(props, errors="replace") as h:
                    p = h.read()
                info["alphaTest"] = "AlphaTest = true" in p or "OB_Masked" in p
                ref = re.search(r"AlphaRef = (\d+)", p)
                info["alphaRef"] = int(ref.group(1)) if ref else 0
                info["twoSided"] = "TwoSided = true" in p
            break
    png = find_file(out, "Texture", diffuse, ".png")
    if png:
        if not shader_found and has_transparency(png):
            info["alphaTest"] = True
        tex_pkg = os.path.basename(os.path.dirname(os.path.dirname(png)))
        dest_dir = os.path.join(PROJECT, "Resources", "Data", "SysTextures", tex_pkg)
        os.makedirs(dest_dir, exist_ok=True)
        shutil.copy2(png, os.path.join(dest_dir, os.path.basename(png)))
        info["texture"] = "Data/SysTextures/%s/%s" % (tex_pkg, diffuse)
    return info


def convert(package, name, cls):
    out = os.path.join(WORK, name)
    shutil.rmtree(out, ignore_errors=True)
    found_pkg, anim = export_mesh(package, name, out)
    if not found_pkg:
        return "introuvable dans Orfen"

    psa = None
    if anim:
        umodel("-export", "-out=" + out, anim[1] + ".ukx", anim[0])
        psa = find_file(out, "MeshAnimation", anim[0], ".psa")
    psk_file = find_file(out, "SkeletalMesh", name, ".psk")

    bpy.ops.wm.read_homefile(use_empty=True)
    psk.pskimport(psk_file, context=bpy.context, bImportmesh=True, bImportbone=True, fBonesize=5.0,
                  fBonesizeRatio=0.4, bDontInvertRoot=True, bScaleDown=True, bToSRGB=True)
    armature = next((o for o in bpy.data.objects if o.type == "ARMATURE"), None)
    if psa and armature:
        psk.psaimport(psa, context=bpy.context, oArmature=armature, bDontInvertRoot=True, bScaleDown=True)
        armature.animation_data_create()
        armature.animation_data.action = None

    mesh_obj = next((o for o in bpy.data.objects if o.type == "MESH"), None)
    slots = [m.name for m in mesh_obj.data.materials] if mesh_obj else []

    target = fbx_path(package, name)
    os.makedirs(os.path.dirname(target), exist_ok=True)
    for obj in bpy.context.scene.objects:
        obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=target, use_selection=True, bake_anim=True,
                             bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False)

    manifest = {
        "mesh": name, "package": package, "sourcePackage": found_pkg,
        "animation": anim[0] if anim else "", "clips": len(bpy.data.actions),
        "kind": "monster" if cls.lower().startswith("lineagemonster") else "npc",
        "materials": [resolve_material(out, s) for s in slots],
    }
    with open(os.path.join(os.path.dirname(target), name + ".import.json"), "w", encoding="utf-8") as h:
        json.dump(manifest, h, indent=2)
    return "ok (%d clips, %d materiaux)" % (manifest["clips"], len(slots))


npcgrp = read_npcgrp()
todo = []
if args.meshes:
    todo = [m.strip() for m in args.meshes.split(",") if m.strip()]
elif args.missing:
    todo = sorted(m for m in npcgrp if "." in m and not prefab_exists(*m.split(".", 1)))
if args.limit:
    todo = todo[:args.limit]

results = {}
for i, ref in enumerate(todo, 1):
    package, name = ref.split(".", 1)
    if os.path.exists(fbx_path(package, name)) and not args.force:
        results[ref] = "deja converti"
        continue
    try:
        results[ref] = convert(package, name, npcgrp.get(ref, {}).get("cls", "LineageNPC"))
    except Exception as e:
        results[ref] = "ECHEC : %s" % e
    print("[%d/%d] %s : %s" % (i, len(todo), ref, results[ref]), flush=True)

with open(os.path.join(WORK, "rapport.json"), "w", encoding="utf-8") as h:
    json.dump(results, h, indent=2, ensure_ascii=False)
print("Termine : %d modele(s), rapport dans %s" % (len(todo), os.path.join(WORK, "rapport.json")))
