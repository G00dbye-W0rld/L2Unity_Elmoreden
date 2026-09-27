import acmi.l2.clientmod.io.UnrealPackage;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.*;
import java.util.regex.*;

// Genere un json par effet visuel depuis LineageSkillEffect.u du client Orfen.
// Format identique aux fichiers faits a la main : le client coupe EffectClass
// sur le point, donc le prefixe "Class" est conserve pour rester homogene.
public class GenSkillEffects {

    // Enumerations lues dans Engine.u, dans l'ordre de leurs valeurs.
    static final String[] ATTACH = {"AM_None","AM_Location","AM_RH","AM_LH","AM_RA","AM_LA",
        "AM_Wing","AM_BoneSpecified","AM_AliasSpecified","AM_Trail","AM_BoneLocation",
        "AM_AliasLocation","AM_Agathion","AM_DependOnMagicInfoInClient"};
    static final String[] ETC = {"EET_None","EET_FireCracker","EET_SoulShot","EET_SpiritShot",
        "EET_Cubic","EET_SoundCrystal","EET_JewelShot","EET_PetJewelShot"};
    static final String[] ETCINFO = {"EEP_None","EEP_FireCrackerSmall","EEP_FireCrackerMiddle",
        "EEP_FireCrackerLarge","EEP_GradeNone","EEP_GradeD","EEP_GradeC","EEP_GradeB",
        "EEP_GradeA","EEP_GradeS","EEP_GradeR"};

    static final String[] EPLT = {"EPLT_DAMAGE","EPLT_ABNORMAL","EPLT_SKILL"};
    static final String[] LT = {"LT_None","LT_Steady","LT_Pulse","LT_Blink","LT_Flicker","LT_Strobe",
        "LT_BackdropLight","LT_SubtlePulse","LT_TexturePaletteOnce","LT_TexturePaletteLoop",
        "LT_FadeOut","LT_Fade","LT_SpawnedLight"};
    static final String[] LE = {"LE_None","LE_TorchWaver","LE_FireWaver","LE_WateryShimmer",
        "LE_Searchlight","LE_SlowWave","LE_FastWave","LE_CloudCast","LE_StaticSpot","LE_Shock",
        "LE_Disco","LE_Warp","LE_Spotlight","LE_NonIncidence","LE_Shell","LE_OmniBumpMap",
        "LE_Interference","LE_Cylinder","LE_Rotor","LE_Sunlight","LE_QuadraticNonIncidence"};
    // Ordre lu dans l'Enum du paquet : il est inverse par rapport a la table de noms.
    static final String[] PTCS = {"PTCS_Independent","PTCS_Relative","PTCS_Absolute",
        "PTCS_RelativeRotation","PTCS_Spray","PTCS_RelativePosition","PTCS_ScreenAbsolute",
        "PTCS_ScreenRelative"};

    // Nom d'enumeration attendu pour une propriete donnee, sinon null.
    static String enumFor(String prop, String raw) {
        String[] t = switch (prop) {
            case "AttachOn" -> ATTACH;
            case "EtcEffect" -> ETC;
            case "EtcEffectInfo" -> ETCINFO;
            case "PawnLightType" -> EPLT;
            case "LightType" -> LT;
            case "LightEffectType" -> LE;
            case "LightCoordSystem" -> PTCS;
            default -> null;
        };
        if (t == null) return null;
        String n = enumName(t, raw);
        return n.equals(raw) ? null : n;
    }

    static String enumName(String[] table, String rawValue) {
        try { int i = Integer.parseInt(rawValue); if (i >= 0 && i < table.length) return table[i]; }
        catch (NumberFormatException ignored) {}
        return rawValue;
    }

    static final Set<String> KNOWN = new HashSet<>(Arrays.asList(
        "SkillID", "Desc", "FlyingTime", "CastingAction", "PreshotAction", "ShotAction",
        "ExplosionAction",
        // Reconnues pour l'alignement, mais le client ne lit que les trois phases
        // ci-dessus : on les compte plutot que de les perdre en silence.
        "ChannelingAction", "UnionTargetAction", "ShotCameraEffectInfoClass",
        "CastingCameraEffectInfoClass", "ExplosionCameraEffectInfoClass"));

    static final Pattern CLS = Pattern.compile("^l2_([0-9]+)(_[a-z0-9]+)?_skill$");

    public static void main(String[] args) throws Exception {
        Path out = Paths.get(args[1]);
        Files.createDirectories(out);
        Set<Integer> wanted = null;
        if (args.length > 2 && !args[2].equals("-")) {
            wanted = new HashSet<>();
            for (String l : Files.readAllLines(Paths.get(args[2])))
                if (!l.isBlank()) wanted.add(Integer.parseInt(l.trim()));
        }

        Set<String> unknown = new TreeSet<>();
        Map<String, Integer> skipped = new TreeMap<>();
        int written = 0, variants = 0, unaligned = 0, noAction = 0;
        List<String> report = new ArrayList<>();

        try (UnrealPackage p = new UnrealPackage(new File(args[0]), true)) {
            Dump.up = p; Dump.names = p.getNameTable();
            Map<String, UnrealPackage.ExportEntry> byName = new HashMap<>();
            for (UnrealPackage.ExportEntry e : p.getExportTable()) byName.put(e.getObjectFullName(), e);

            for (UnrealPackage.ExportEntry e : p.getExportTable()) {
                if (!e.getFullClassName().endsWith(".Class")) continue;
                String simple = e.getObjectName().getName();
                Matcher m = CLS.matcher(simple);
                if (!m.matches()) continue;
                int id = Integer.parseInt(m.group(1));
                boolean isVariant = m.group(2) != null;
                if (wanted != null && !wanted.contains(id)) continue;
                if (isVariant) { variants++; continue; }

                List<String[]> props = classProps(e.getObjectRawData(), id);
                if (props == null) { unaligned++; report.add(id + " : proprietes non alignees"); continue; }

                Map<String, List<String>> phases = new LinkedHashMap<>();
                phases.put("CastingActions", new ArrayList<>());
                phases.put("ShotActions", new ArrayList<>());
                phases.put("ExplosionActions", new ArrayList<>());
                for (String[] kv : props) {
                    String key = switch (kv[0]) {
                        case "CastingAction" -> "CastingActions";
                        case "PreshotAction", "ShotAction" -> "ShotActions";
                        case "ExplosionAction" -> "ExplosionActions";
                        default -> null;
                    };
                    if (key == null) {
                        if (!KNOWN.contains(kv[0])) unknown.add(kv[0]);
                        else if (kv[0].endsWith("Action") || kv[0].endsWith("Class"))
                            skipped.merge(kv[0], 1, Integer::sum);
                        continue;
                    }
                    if (kv[1].isEmpty()) continue;
                    for (String ref : kv[1].split(",")) {
                        UnrealPackage.ExportEntry em = byName.get(ref);
                        if (em != null) phases.get(key).add(emitterJson(em));
                    }
                }
                if (phases.values().stream().allMatch(List::isEmpty)) { noAction++; continue; }

                StringBuilder sb = new StringBuilder("{\n\"SkillId\": " + id + ",\n");
                int k = 0;
                for (Map.Entry<String, List<String>> ph : phases.entrySet()) {
                    sb.append("\"").append(ph.getKey()).append("\": [")
                      .append(String.join(",", ph.getValue())).append("]")
                      .append(++k < 3 ? ",\n" : "\n");
                }
                sb.append("}\n");
                Files.write(out.resolve(id + ".json"), sb.toString().getBytes(StandardCharsets.UTF_8));
                written++;
            }
        }
        System.out.println("ecrits : " + written);
        System.out.println("variantes ignorees (_a_skill etc) : " + variants);
        System.out.println("sans aucune action : " + noAction);
        System.out.println("non alignes : " + unaligned);
        for (String r : report.subList(0, Math.min(10, report.size()))) System.out.println("  " + r);
        if (!unknown.isEmpty()) System.out.println("proprietes de classe INCONNUES : " + unknown);
        if (!skipped.isEmpty()) System.out.println("reconnues mais non versees dans les phases : " + skipped);
    }

    // Les proprietes par defaut sont en queue du bloc. On retient le premier
    // decalage qui finit pile sur le terminateur et porte le bon SkillID.
    // SkillID n'est serialise que s'il differe du defaut : beaucoup de classes ne
    // l'ont pas, l'identifiant vient alors du nom. On retient donc le premier
    // decalage qui finit pile sur le terminateur, ne porte que des proprietes
    // connues, et decrit au moins une phase.
    static List<String[]> classProps(byte[] raw, int id) {
        for (int off = 0; off < raw.length - 2; off++) {
            try {
                List<String[]> pr = Dump.props(Arrays.copyOfRange(raw, off, raw.length));
                if (!Dump.lastEndedOnNone || pr.isEmpty()) continue;
                boolean ok = true, action = false;
                for (String[] kv : pr) {
                    if (!KNOWN.contains(kv[0])) { ok = false; break; }
                    if (kv[0].equals("SkillID") && !kv[1].equals(String.valueOf(id))) { ok = false; break; }
                    if (kv[0].endsWith("Action")) action = true;
                }
                if (ok && action) return pr;
            } catch (Exception ignored) {}
        }
        return null;
    }

    static String emitterJson(UnrealPackage.ExportEntry em) {
        StringBuilder sb = new StringBuilder("{\"Name\": \"" + em.getObjectName().getName() + "\"");
        for (String[] kv : Dump.props(em.getObjectRawData())) {
            String k = kv[0], v = kv[1];
            String en = enumFor(k, v);
            if (en != null) v = en;
            if ("4".equals(kv[2])) v = String.format(Locale.ROOT, "%.6f", Float.parseFloat(v));
            if (v.startsWith("Vector(")) v = vector(v);
            else if (v.startsWith("LineageEffect")) v = "Class" + v;
            sb.append(", \"").append(k).append("\": \"").append(v).append("\"");
        }
        return sb.append("}").toString();
    }

    // (X=3.000000) : seules les composantes non nulles, comme le client l'ecrit.
    static String vector(String v) {
        String[] c = v.substring(v.indexOf('(') + 1, v.lastIndexOf(')')).split(",");
        String[] ax = {"X", "Y", "Z"};
        StringBuilder sb = new StringBuilder("(");
        for (int i = 0; i < c.length && i < 3; i++) {
            float f = Float.parseFloat(c[i]);
            if (f == 0f) continue;
            if (sb.length() > 1) sb.append(",");
            sb.append(ax[i]).append("=").append(String.format(Locale.ROOT, "%.6f", f));
        }
        return sb.append(")").toString();
    }
}
