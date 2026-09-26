# Complete les emotes manquantes des races jouables depuis le client Orfen, qui les a
# toutes. Le script ne prend que ce qui manque : les clips deja presents restent intacts.
#
# Le FBX produit porte un nom a part (<mesh>_emotes.fbx) pour ne rien ecraser des modeles
# existants, mais garde le meme nom d'armature : les clips sortent donc avec le nommage
# deja en place, "<mesh>.ao_<sequence>".
#
# blender --background --python OrfenEmoteImport.py

import bpy, os, re, glob, shutil, subprocess

ORFEN = r"F:\Lineage\Client\Lineage II - Tale Of Aden - Salvation - Etinas Fate (Orfen)"
PROJECT = r"D:\Jeux\PROJET_L2UNITY\l2-unity-main\l2-unity\Assets"
WORK = r"D:\Jeux\MAP_L2Unity\export_emotes"

UMODEL = os.path.join(ORFEN, "umodel_win32", "umodel_64.exe")
ANIM_DIR = os.path.join(ORFEN, "Animations")
ANIMATIONS = os.path.join(PROJECT, "Resources", "Data", "Animations")

# Les 17 emotes que ActionName declare, dans l'ordre de leur identifiant social (2 a 21).
WANTED = ["Social_Nod", "Social_Victory", "Social_Atk", "Social_no", "Social_yes",
          "Social_bow", "Social_unaware", "Social_waiting_a", "Social_laugh",
          "Social_clap", "Social_dance", "Social_sad", "Social_charming",
          "Social_shy", "Social_Propose", "Social_Provocation", "Social_Beauty",
          "Social_Couple_Bow", "Social_Couple_Hifive", "Social_Couple_Dance"]

RACES = [
    ("MFighter", "Fighter", "Fighter"), ("FFighter", "Fighter", "Fighter"),
    ("MMagic", "Magic", "Magic"), ("FMagic", "Magic", "Magic"),
    ("MElf", "Elf", "Elf"), ("FElf", "Elf", "Elf"),
    ("MDarkElf", "DarkElf", "DarkElf"), ("FDarkElf", "DarkElf", "DarkElf"),
    ("MDwarf", "Dwarf", "Dwarf"), ("FDwarf", "Dwarf", "Dwarf"),
    ("MOrc", "Orc", "Orc"), ("FOrc", "Orc", "Orc"),
    ("MShaman", "Shaman", "Shaman"), ("FShaman", "Shaman", "Shaman"),
]

if "io_import_scene_unreal_psa_psk_280" not in bpy.context.preferences.addons:
    bpy.ops.preferences.addon_enable(module="io_import_scene_unreal_psa_psk_280")
import io_import_scene_unreal_psa_psk_280 as psk


def umodel(*params):
    cmd = [UMODEL, "-game=l2", "-path=" + ANIM_DIR] + list(params)
    res = subprocess.run(cmd, cwd=ANIM_DIR, capture_output=True, text=True, errors="replace")
    return res.stdout + res.stderr


def find_file(root, name, ext):
    for path in glob.glob(os.path.join(root, "**", "*" + ext), recursive=True):
        if os.path.splitext(os.path.basename(path))[0].lower() == name.lower():
            return path
    return None


def race_dir(race, group):
    return os.path.join(ANIMATIONS, group, race)


def base_mesh(race, group):
    # Le prefixe des clips existants donne le maillage sur lequel la race est montee.
    for path in glob.glob(os.path.join(race_dir(race, group), "Clips", "*.anim")):
        name = os.path.basename(path)
        if ".ao_" in name:
            return name.split(".ao_")[0]
    return None


def canonical_bones(race, group, mesh):
    # La casse des os telle que le squelette de la race la porte. Le psk d'Orfen nomme
    # l'os racine "bip01" alors que les squelettes orc et chaman du projet portent
    # "Bip01" : Unity lie les courbes par comparaison sensible a la casse, et un seul
    # ecart suffit a ce que le clip ne pilote plus rien (T-pose silencieuse).
    # On lit un clip de combat, jamais un clip social, qui pourrait deja etre fautif.
    for path in sorted(glob.glob(os.path.join(race_dir(race, group), "Clips", "*.anim"))):
        if not os.path.basename(path).startswith(mesh + ".ao_"):
            continue
        if "social" in os.path.basename(path).lower():
            continue
        names = {}
        with open(path, "r", errors="replace") as fh:
            for line in fh:
                if "path: " in line:
                    for seg in line.split("path: ", 1)[1].strip().split("/"):
                        if seg:
                            names[seg.lower()] = seg
        if names:
            return names
    return {}



def missing_sequences(race, group, mesh):
    # Certaines races ont des clips suffixes ".001" : on compare sans ce suffixe.
    have = set()
    for path in glob.glob(os.path.join(race_dir(race, group), "Clips", "*.anim")):
        name = os.path.splitext(os.path.basename(path))[0]
        have.add(re.sub(r"\.\d+$", "", name).lower())

    missing = []
    for seq in WANTED:
        if ("%s.ao_%s_%s" % (mesh, seq, race)).lower() not in have:
            missing.append("%s_%s" % (seq, race))
    return missing


def convert(race, group, package):
    mesh = base_mesh(race, group)
    if not mesh:
        return "aucun clip existant, maillage de base inconnu"

    missing = missing_sequences(race, group, mesh)
    if not missing:
        return "complet"

    out = os.path.join(WORK, race)
    shutil.rmtree(out, ignore_errors=True)
    umodel("-export", "-out=" + out, package + ".ukx", mesh)
    umodel("-export", "-out=" + out, package + ".ukx", race + "_anim")

    psk_file = find_file(out, mesh, ".psk")
    psa_file = find_file(out, race + "_anim", ".psa")
    if not psk_file or not psa_file:
        return "psk ou psa introuvable"

    bpy.ops.wm.read_homefile(use_empty=True)
    psk.pskimport(psk_file, context=bpy.context, bImportmesh=True, bImportbone=True, fBonesize=5.0,
                  fBonesizeRatio=0.4, bDontInvertRoot=True, bScaleDown=True, bToSRGB=True)
    armature = next((o for o in bpy.data.objects if o.type == "ARMATURE"), None)
    if armature is None:
        return "armature introuvable"

    psk.psaimport(psa_file, context=bpy.context, oArmature=armature, bDontInvertRoot=True, bScaleDown=True)
    armature.animation_data_create()
    armature.animation_data.action = None

    # Le squelette importe suit le nommage d'Orfen. On le realigne sur celui de la race
    # avant l'export, sinon les courbes ne se lient a rien du tout cote Unity.
    canon = canonical_bones(race, group, mesh)
    renames = []
    for bone in armature.data.bones:
        want = canon.get(bone.name.lower())
        if want and want != bone.name:
            renames.append((bone.name, want))
    for old, new in renames:
        armature.data.bones[old].name = new

    # Blender corrige normalement les chemins des actions au renommage ; on s'en assure.
    for action in bpy.data.actions:
        for curve in action.fcurves:
            for old, new in renames:
                curve.data_path = curve.data_path.replace(
                    'pose.bones["%s"]' % old, 'pose.bones["%s"]' % new)

    # On ne garde que les sequences manquantes, sinon le FBX embarque tout le jeu d'animations.
    keep = {m.lower() for m in missing}
    for action in list(bpy.data.actions):
        if action.name.lower() not in keep:
            bpy.data.actions.remove(action)

    kept = len(bpy.data.actions)
    if kept == 0:
        return "aucune des %d sequences manquantes n'est dans le psa" % len(missing)

    target = os.path.join(race_dir(race, group), "Models", mesh + "_emotes.fbx")
    os.makedirs(os.path.dirname(target), exist_ok=True)
    for obj in bpy.context.scene.objects:
        obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=target, use_selection=True, bake_anim=True,
                             bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False)
    note = "" if not renames else ", os realignes : " + ", ".join("%s->%s" % r for r in renames)
    return "ok (%d/%d sequences)%s" % (kept, len(missing), note)


for race, group, package in RACES:
    try:
        print("%-10s : %s" % (race, convert(race, group, package)), flush=True)
    except Exception as e:
        print("%-10s : ECHEC %s" % (race, e), flush=True)
