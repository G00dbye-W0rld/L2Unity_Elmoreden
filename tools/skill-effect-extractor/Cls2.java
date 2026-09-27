import acmi.l2.clientmod.io.UnrealPackage;
import java.io.File;
import java.util.*;

// Proprietes par defaut d'une classe d'effet : on garde le decalage qui finit
// pile sur le terminateur et ne porte que des noms plausibles.
public class Cls2 {
    static final Set<String> OK = new HashSet<>(Arrays.asList(
        "DrawScale", "bNoDelete", "bSunAffect", "bDirectional", "bLightChanged",
        "bHidden", "Emitters", "bNetTemporary", "Physics", "LifeSpan", "RemoteRole",
        "bUnlit", "Style", "AmbientGlow", "bParticleLight", "LightType", "LightBrightness",
        "LightHue", "LightSaturation", "LightRadius", "LightEffect", "bDynamicLight"));

    public static void main(String[] a) throws Exception {
        try (UnrealPackage p = new UnrealPackage(new File(a[0]), true)) {
            Dump.up = p; Dump.names = p.getNameTable();
            for (UnrealPackage.ExportEntry e : p.getExportTable()) {
                if (!e.getFullClassName().endsWith(".Class")) continue;
                if (!e.getObjectName().getName().equals(a[1])) continue;
                byte[] raw = e.getObjectRawData();
                for (int off = 0; off < raw.length - 2; off++) {
                    try {
                        List<String[]> pr = Dump.props(Arrays.copyOfRange(raw, off, raw.length));
                        if (!Dump.lastEndedOnNone || pr.isEmpty()) continue;
                        boolean ok = true;
                        for (String[] kv : pr) if (!OK.contains(kv[0])) { ok = false; break; }
                        if (!ok) continue;
                        System.out.println("decalage " + off);
                        for (String[] kv : pr) System.out.println("  " + kv[0] + " = " + kv[1]);
                        return;
                    } catch (Exception ignored) {}
                }
                System.out.println("(aucun bloc reconnu)");
            }
        }
    }
}
