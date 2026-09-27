import acmi.l2.clientmod.io.UnrealPackage;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.*;
import java.util.regex.*;

// Ecrit une recette .uc par classe d'effet, dans la syntaxe que lit
// L2ParticleEmitterParser. Le source UnrealScript n'est pas conserve dans le
// paquet : tout est reconstitue depuis les proprietes.
public class GenRecipes {

    static final String[] PTDS = {"PTDS_Regular", "PTDS_AlphaBlend", "PTDS_Modulated", "PTDS_Translucent",
        "PTDS_AlphaModulate_MightNotFogCorrectly", "PTDS_Darken", "PTDS_Brighten"};
    static final String[] PTVD = {"PTVD_None", "PTVD_StartPositionAndOwner", "PTVD_OwnerAndStartPosition", "PTVD_AddRadial"};
    static final String[] PTLS = {"PTLS_Box", "PTLS_Sphere", "PTLS_Polar", "PTLS_All"};
    // Ordre lu dans l'Enum du paquet : inverse par rapport a la table de noms.
    static final String[] PTCS = {"PTCS_Independent", "PTCS_Relative", "PTCS_Absolute",
        "PTCS_RelativeRotation", "PTCS_Spray", "PTCS_RelativePosition", "PTCS_ScreenAbsolute",
        "PTCS_ScreenRelative"};
    static final String[] PTDU = {"PTDU_None", "PTDU_Up", "PTDU_Right", "PTDU_Forward", "PTDU_Normal",
        "PTDU_UpAndNormal", "PTDU_RightAndNormal", "PTDU_Scale"};

    // Proprietes de classe relevees sur les recettes d'origine. Seule DrawScale
    // est lue par le parseur, mais il faut les connaitre pour reperer le debut du
    // bloc : un decalage errone injecte des valeurs inventees.
    static final Set<String> CLASS_PROPS = new HashSet<>(Arrays.asList(
        "AccSpeed", "AutoReplay", "AutoReset", "DrawScale", "Location", "Physics",
        "RotPerSecond", "Rotation", "Speed", "SwayRotationOrig", "Tag", "TexModifyInfo",
        "LifeSpan", "RemoteRole", "Style", "AmbientGlow", "LightType", "LightEffect",
        "LightBrightness", "LightHue", "LightSaturation", "LightRadius", "CollisionRadius",
        "CollisionHeight", "SoundVolume", "SoundRadius", "AmbientSound", "Skins", "Mesh",
        "DrawType", "ScaleGlow", "Texture", "Name"));
    // Les drapeaux d'acteur (bXxx) sont admis en bloc : ils sont nombreux et inoffensifs.
    static final Pattern FLAG = Pattern.compile("^b[A-Z][A-Za-z0-9_]*$");
    static final Pattern IDENT = Pattern.compile("^[A-Za-z][A-Za-z0-9_]*$");

    static String enumName(String[] t, String raw) {
        try {
            int i = Integer.parseInt(raw);
            if (i >= 0 && i < t.length) return t[i];
        } catch (NumberFormatException ignored) {}
        return raw;
    }

    static String enumFor(String prop, String raw) {
        switch (prop) {
            case "DrawStyle": return enumName(PTDS, raw);
            case "GetVelocityDirectionFrom": return enumName(PTVD, raw);
            case "StartLocationShape": return enumName(PTLS, raw);
            case "UseDirectionAs": return enumName(PTDU, raw);
            case "CoordinateSystem": return enumName(PTCS, raw);
            default: return null;
        }
    }

    // Rend une valeur dans la syntaxe .uc.
    static String render(String prop, String val, String type) {
        String en = enumFor(prop, val);
        if (en != null) return en;
        if (prop.equals("Name")) return "\"" + val + "\"";
        if (prop.equals("StaticMesh")) return "StaticMesh'" + val + "'";
        if (prop.equals("Texture")) return "Texture'" + val + "'";
        if (prop.equals("VertexMesh")) return "VertMesh'" + val + "'";
        if (val.startsWith("Vector(")) return vector(val);
        if ("4".equals(type)) return String.format(Locale.ROOT, "%.6f", Float.parseFloat(val));
        return val;
    }

    // (X=..,Y=..,Z=..) sans les composantes nulles, comme le client l'ecrit.
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

    public static void main(String[] args) throws Exception {
        Path out = Paths.get(args[1]);
        Files.createDirectories(out);
        Set<String> wanted = null;
        if (args.length > 2 && !args[2].equals("-")) {
            wanted = new HashSet<>();
            for (String l : Files.readAllLines(Paths.get(args[2]))) {
                String t = l.trim();
                if (!t.isEmpty()) wanted.add(t);
            }
        }

        int written = 0, noEmitter = 0, noClassBlock = 0;
        SortedMap<String, Integer> types = new TreeMap<>();
        List<String> missingBlock = new ArrayList<>();

        try (UnrealPackage p = new UnrealPackage(new File(args[0]), true)) {
            Dump.up = p;
            Dump.names = p.getNameTable();

            // Emetteurs groupes par classe, dans l'ordre du paquet.
            Map<String, List<UnrealPackage.ExportEntry>> emitters = new LinkedHashMap<>();
            Map<String, UnrealPackage.ExportEntry> classes = new LinkedHashMap<>();
            for (UnrealPackage.ExportEntry e : p.getExportTable()) {
                String cls = e.getFullClassName();
                String[] parts = e.getObjectFullName().split("[.]");
                if (cls.endsWith(".Class") && parts.length == 2) {
                    classes.put(parts[1], e);
                } else if (cls.endsWith("Emitter") && parts.length >= 3) {
                    emitters.computeIfAbsent(parts[1], k -> new ArrayList<>()).add(e);
                }
            }

            for (Map.Entry<String, UnrealPackage.ExportEntry> en : classes.entrySet()) {
                String name = en.getKey();
                if (wanted != null && !wanted.contains(name)) continue;
                List<UnrealPackage.ExportEntry> ems = emitters.get(name);
                if (ems == null || ems.isEmpty()) {
                    noEmitter++;
                    continue;
                }

                StringBuilder sb = new StringBuilder();
                sb.append("class ").append(name).append(" extends Emitter;\n\n");
                sb.append("defaultproperties\n{\n");

                for (int i = 0; i < ems.size(); i++) {
                    UnrealPackage.ExportEntry em = ems.get(i);
                    String type = em.getFullClassName();
                    type = type.substring(type.lastIndexOf('.') + 1);
                    types.merge(type, 1, Integer::sum);
                    String objName = em.getObjectName().getName();

                    sb.append("     Begin Object Class=").append(type).append(" Name=").append(objName).append("\n");
                    for (String[] kv : Dump.props(em.getObjectRawData())) {
                        if ("9".equals(kv[2])) {
                            String[] items = kv[1].split("\u0001", -1);
                            for (int k = 0; k < items.length; k++) {
                                if (items[k].isEmpty()) continue;
                                sb.append("         ").append(kv[0]).append("(").append(k).append(")=")
                                  .append(items[k]).append("\n");
                            }
                        } else {
                            sb.append("         ").append(kv[0]).append("=")
                              .append(render(kv[0], kv[1], kv[2])).append("\n");
                        }
                    }
                    sb.append("     End Object\n");
                    sb.append("     Emitters(").append(i).append(")=").append(type).append("'")
                      .append(objName).append("'\n");
                }

                List<String[]> cp = classProps(en.getValue().getObjectRawData());
                if (cp == null) {
                    noClassBlock++;
                    if (missingBlock.size() < 10) missingBlock.add(name);
                } else {
                    for (String[] kv : cp) {
                        if ("9".equals(kv[2])) continue;
                        sb.append("     ").append(kv[0]).append("=")
                          .append(render(kv[0], kv[1], kv[2])).append("\n");
                    }
                }

                sb.append("}\n");
                Files.write(out.resolve(name + ".uc"), sb.toString().getBytes(StandardCharsets.UTF_8));
                written++;
            }
        }

        System.out.println("recettes ecrites : " + written);
        System.out.println("classes sans emetteur : " + noEmitter);
        System.out.println("sans bloc de proprietes de classe : " + noClassBlock + " " + missingBlock);
        System.out.println("emetteurs par type : " + types);
    }

    static List<String[]> classProps(byte[] raw) {
        for (int off = 0; off < raw.length - 2; off++) {
            try {
                List<String[]> pr = Dump.props(Arrays.copyOfRange(raw, off, raw.length));
                if (!Dump.lastEndedOnNone || pr.isEmpty()) continue;
                boolean ok = true;
                for (String[] kv : pr) {
                    if (!CLASS_PROPS.contains(kv[0]) && !FLAG.matcher(kv[0]).matches()) {
                        ok = false;
                        break;
                    }
                }
                if (ok) return pr;
            } catch (Exception ignored) {}
        }
        return null;
    }
}
