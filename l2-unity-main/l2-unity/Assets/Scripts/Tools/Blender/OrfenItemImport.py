# Import des objets depuis le client Orfen : icones, armes, pieces d'armure et leurs textures.
# Seuls les objets connus du serveur sont traites. Les FBX et manifestes sont finalises
# dans Unity par L2 > Objets > Importer les objets Orfen convertis.
#
# blender --background --python OrfenItemImport.py -- --icons --weapons --armors [--limit N]

import bpy, numpy, os, sys, re, json, glob, shutil, subprocess, argparse

ORFEN = r"F:\Lineage\Client\Lineage II - Tale Of Aden - Salvation - Etinas Fate (Orfen)"
PROJECT = r"D:\Jeux\PROJET_L2UNITY\l2-unity-main\l2-unity\Assets"
SERVER_ITEMS = r"D:\Jeux\PROJET_L2UNITY\l2-unity-gameserver-master\gameserver\data\xml\items"
WORK = r"D:\Jeux\MAP_L2Unity\export_items"

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
parser = argparse.ArgumentParser()
parser.add_argument("--icons", action="store_true")
parser.add_argument("--weapons", action="store_true")
parser.add_argument("--armors", action="store_true")
parser.add_argument("--force", action="store_true")
parser.add_argument("--limit", type=int, default=0)
args = parser.parse_args(argv)

UMODEL = os.path.join(ORFEN, "umodel_win32", "umodel_64.exe")
META = os.path.join(PROJECT, "StreamingAssets", "Data", "Meta")
ANIMATIONS = os.path.join(PROJECT, "Resources", "Data", "Animations")
SYSTEX = os.path.join(PROJECT, "Resources", "Data", "SysTextures")
ICON_PACKAGES = ["Icon", "BranchIcon", "branchSys", "BranchSys2", "BranchSys3", "br_cashtex"]
# Seuls les champs lus par le client (ArmorgrpTable) : les 7 races Interlude, hommes et femmes.
CLIENT_RACES = {s + r for s in ("m_", "f_") for r in ("HumnFigh", "HumnMyst", "Elf", "DarkElf", "OrcFigh", "OrcMage", "Dorf")}

if "io_import_scene_unreal_psa_psk_280" not in bpy.context.preferences.addons:
    bpy.ops.preferences.addon_enable(module="io_import_scene_unreal_psa_psk_280")
import io_import_scene_unreal_psa_psk_280 as psk


def umodel(*params):
    cmd = [UMODEL, "-game=l2", "-path=" + ORFEN] + list(params)
    res = subprocess.run(cmd, cwd=os.path.join(ORFEN, "Animations"), capture_output=True, text=True, errors="replace")
    return res.stdout + res.stderr


def server_ids():
    ids = set()
    for f in glob.glob(os.path.join(SERVER_ITEMS, "*.xml")):
        with open(f, encoding="utf-8", errors="replace") as h:
            ids.update(int(x) for x in re.findall(r'<item id="(\d+)"', h.read()))
    return ids


def grp_lines(name, ids):
    with open(os.path.join(META, name + "_Classic.txt"), encoding="utf-8", errors="replace") as h:
        for line in h:
            m = re.search(r"\bobject_id=(\d+)", line)
            if m and int(m.group(1)) in ids:
                yield line


def refs(text):
    # "None" et les noms sans paquet ne designent rien a exporter
    return [r for r in re.findall(r"\[([^\]]+)\]", text or "") if "." in r]


def find_file(root, sub, stem, ext):
    hits = glob.glob(os.path.join(root, "**", sub, stem + ext), recursive=True)
    if not hits:
        low = (stem + ext).lower()
        hits = [p for p in glob.glob(os.path.join(root, "**", sub, "*" + ext), recursive=True) if os.path.basename(p).lower() == low]
    return hits[0] if hits else None


def exists_ci(folder, name):
    if not os.path.isdir(folder):
        return False
    low = name.lower()
    return any(f.lower() == low for f in os.listdir(folder))


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


def resolve_texture(out, name):
    # Texture, Shader, FinalBlend ou Combiner -> png diffuse, plus alpha et double face
    info = {"name": name, "texture": "", "alphaTest": False, "alphaRef": 0, "twoSided": False}
    diffuse = name
    shader_found = False
    for kind in ("FinalBlend", "Shader", "Combiner"):
        mat = find_file(out, kind, name, ".mat")
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
        dest = os.path.join(SYSTEX, tex_pkg)
        os.makedirs(dest, exist_ok=True)
        shutil.copy2(png, os.path.join(dest, os.path.basename(png)))
        info["texture"] = "Data/SysTextures/%s/%s" % (tex_pkg, os.path.splitext(os.path.basename(png))[0])
    return info


def export_texture(package, name):
    out = os.path.join(WORK, "tex", package)
    if not find_file(out, "*", name, ".*"):
        umodel("-export", "-png", "-out=" + out, package, name)
    return resolve_texture(out, name)


def convert_mesh(package, name, target):
    out = os.path.join(WORK, "mesh", name)
    shutil.rmtree(out, ignore_errors=True)
    log = umodel("-export", "-png", "-out=" + out, package + ".ukx", name)
    if "Exporting SkeletalMesh" not in log and "Exporting StaticMesh" not in log:
        return None, []
    mesh_file = find_file(out, "SkeletalMesh", name, ".psk") or find_file(out, "StaticMesh", name, ".pskx")

    bpy.ops.wm.read_homefile(use_empty=True)
    psk.pskimport(mesh_file, context=bpy.context, bImportmesh=True, bImportbone=True, fBonesize=5.0,
                  fBonesizeRatio=0.4, bDontInvertRoot=True, bScaleDown=True, bToSRGB=True)
    mesh_obj = next((o for o in bpy.data.objects if o.type == "MESH"), None)
    slots = [m.name for m in mesh_obj.data.materials] if mesh_obj else []

    os.makedirs(os.path.dirname(target), exist_ok=True)
    for obj in bpy.context.scene.objects:
        obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=target, use_selection=True, bake_anim=False)
    return out, slots


def write_manifest(target, data):
    with open(os.path.splitext(target)[0] + ".import.json", "w", encoding="utf-8") as h:
        json.dump(data, h, indent=2)


def import_icons(ids):
    wanted = {}
    for grp in ("Weapongrp", "Armorgrp", "EtcItemgrp"):
        for line in grp_lines(grp, ids):
            m = re.search(r"\ticon=\{\[([^\].]+)\.([^\]]+)\]", line)
            if m:
                wanted[m.group(2).lower()] = m.group(2)
    dest = os.path.join(SYSTEX, "Icon")
    missing = {k: v for k, v in wanted.items() if not exists_ci(dest, v + ".png")}
    print("Icones : %d referencees, %d manquantes" % (len(wanted), len(missing)), flush=True)

    out = os.path.join(WORK, "icons")
    for pkg in ICON_PACKAGES:
        if not glob.glob(os.path.join(out, pkg, "**", "*.png"), recursive=True):
            umodel("-export", "-png", "-out=" + out, pkg)
    found = {}
    for png in glob.glob(os.path.join(out, "**", "*.png"), recursive=True):
        found.setdefault(os.path.splitext(os.path.basename(png))[0].lower(), png)
    copied = 0
    for key, name in missing.items():
        if key in found:
            shutil.copy2(found[key], os.path.join(dest, name + ".png"))
            copied += 1
    print("Icones copiees : %d / %d" % (copied, len(missing)), flush=True)
    return {"icons": copied, "missing": len(missing) - copied}


def import_weapons(ids):
    todo = {}
    for line in grp_lines("Weapongrp", ids):
        meshes = re.search(r"wp_mesh=\{\{([^}]*)\}", line)
        textures = re.search(r"\ttexture=\{([^}]*)\}", line)
        for ref in refs(meshes.group(1) if meshes else ""):
            todo.setdefault(ref, refs(textures.group(1) if textures else ""))

    results = {}
    items = [(r, t) for r, t in sorted(todo.items())
             if args.force or not os.path.exists(os.path.join(ANIMATIONS, *r.split(".", 1)) + ".prefab")
             and not os.path.exists(os.path.join(ANIMATIONS, r.split(".", 1)[0], "Models", r.split(".", 1)[1] + ".import.json"))]
    if args.limit:
        items = items[:args.limit]
    for i, (ref, textures) in enumerate(items, 1):
        package, name = ref.split(".", 1)
        target = os.path.join(ANIMATIONS, package, "Models", name + ".fbx")
        try:
            out, slots = convert_mesh(package, name, target)
            if out is None:
                results[ref] = "introuvable dans Orfen"
            else:
                materials = []
                for index, slot in enumerate(slots or [None]):
                    info = resolve_texture(out, slot) if slot else {"texture": ""}
                    if not info["texture"] and index < len(textures):
                        info = export_texture(*textures[index].split(".", 1))
                    info["slot"] = slot or ""
                    materials.append(info)
                write_manifest(target, {"kind": "weapon", "mesh": name, "package": package, "materials": materials})
                results[ref] = "ok (%d materiaux)" % len(materials)
        except Exception as e:
            results[ref] = "ECHEC : %s" % e
        print("[arme %d/%d] %s : %s" % (i, len(items), ref, results[ref]), flush=True)
    return results


def import_armors(ids):
    models, textures = {}, set()
    for line in grp_lines("Armorgrp", ids):
        # Une armure complete a plusieurs pieces : {{[haut];[bas]};{[texture haut];[texture bas]}}
        for field, meshes, tex in re.findall(r"\t(\w+)=\{\{([^}]*)\};\{([^}]*)\}\}", line):
            if field not in CLIENT_RACES:
                continue
            for model in refs(meshes):
                models[model] = True
            textures.update(refs(tex))

    results = {}
    items = [m for m in sorted(models) if args.force or not os.path.exists(os.path.join(ANIMATIONS, *m.split(".", 1)) + ".prefab")]
    if args.limit:
        items = items[:args.limit]
    for i, ref in enumerate(items, 1):
        package, name = ref.split(".", 1)
        type_dir = name.split("_")[0] if package.lower() in ("fighter", "magic", "elf", "darkelf", "dwarf", "orc", "shaman") else ""
        target = os.path.join(ANIMATIONS, package, type_dir, "Models", name + ".fbx")
        try:
            out, slots = convert_mesh(package, name, target)
            results[ref] = "introuvable dans Orfen" if out is None else "ok"
            if out is not None:
                write_manifest(target, {"kind": "armor", "mesh": name, "package": package, "materials": []})
        except Exception as e:
            results[ref] = "ECHEC : %s" % e
        print("[armure %d/%d] %s : %s" % (i, len(items), ref, results[ref]), flush=True)

    # Les materiaux d'armure portent le nom de la texture de Armorgrp : le client les charge par ce nom.
    materials = []
    missing = [t for t in sorted(textures) if args.force or not os.path.exists(os.path.join(SYSTEX, t.split(".", 1)[0], "Materials", t.split(".", 1)[1] + ".mat"))]
    for i, ref in enumerate(missing, 1):
        package, name = ref.split(".", 1)
        info = export_texture(package, name)
        info["material"] = "Data/SysTextures/%s/Materials/%s" % (package, name)
        materials.append(info)
        if i % 50 == 0:
            print("[textures d'armure %d/%d]" % (i, len(missing)), flush=True)
    with open(os.path.join(SYSTEX, "armor_materials.import.json"), "w", encoding="utf-8") as h:
        json.dump({"materials": materials}, h, indent=2)
    results["textures"] = "%d materiaux a creer, %d sans texture" % (len(materials), sum(1 for m in materials if not m["texture"]))
    return results


ids = server_ids()
report = {}
if args.icons:
    report["icones"] = import_icons(ids)
if args.weapons:
    report["armes"] = import_weapons(ids)
if args.armors:
    report["armures"] = import_armors(ids)

os.makedirs(WORK, exist_ok=True)
with open(os.path.join(WORK, "rapport.json"), "w", encoding="utf-8") as h:
    json.dump(report, h, indent=2, ensure_ascii=False)
print("Termine, rapport dans %s" % os.path.join(WORK, "rapport.json"))
