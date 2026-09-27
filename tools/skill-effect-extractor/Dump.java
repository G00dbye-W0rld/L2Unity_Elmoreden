import acmi.l2.clientmod.io.UnrealPackage;
import java.io.File;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import java.util.*;

// Lit les proprietes taguees d'un objet Unreal 2 et les rend en texte.
public class Dump {
    static UnrealPackage up;
    static List<UnrealPackage.NameEntry> names;

    static String name(int i) {
        return (i >= 0 && i < names.size()) ? names.get(i).getName() : ("?" + i);
    }

    // Index compact signe d'Unreal 1/2.
    static int compact(ByteBuffer b) {
        int b0 = b.get() & 0xFF;
        boolean neg = (b0 & 0x80) != 0;   // bit 7 = signe
        int v = b0 & 0x3F;
        if ((b0 & 0x40) != 0) {           // bit 6 = suite

            int shift = 6;
            for (int i = 0; i < 4; i++) {
                int bi = b.get() & 0xFF;
                v |= (bi & 0x7F) << shift;
                shift += 7;
                if ((bi & 0x80) == 0) break;
            }
        }
        return neg ? -v : v;
    }

    static String objRef(int ref) {
        if (ref == 0) return "None";
        try {
            if (ref < 0) return up.getImportTable().get(-ref - 1).getObjectFullName();
            return up.getExportTable().get(ref - 1).getObjectFullName();
        } catch (Exception e) { return "ref" + ref; }
    }

    static final java.util.Set<String> ATOMIC = new java.util.HashSet<>(java.util.Arrays.asList(
        "Vector", "Rotator", "Color"));

    // Rend une valeur pour l'interieur d'une structure : flottants a six
    // decimales, enumerations par leur nom, vecteurs en notation Unreal.
    static String fmt(String[] kv) {
        String v = kv[1];
        String nm = GenSkillEffects.enumFor(kv[0], v);
        if (nm != null) return nm;
        if ("4".equals(kv[2])) return String.format(java.util.Locale.ROOT, "%.6f", Float.parseFloat(v));
        if (v.startsWith("Vector(")) return GenSkillEffects.vector(v);
        return v;
    }

    static boolean lastEndedOnNone;
    static List<String[]> props(byte[] data) {
        lastEndedOnNone = false;
        List<String[]> out = new ArrayList<>();
        ByteBuffer b = ByteBuffer.wrap(data).order(ByteOrder.LITTLE_ENDIAN);
        while (b.remaining() > 0) {
            int ni = compact(b);
            String pn = name(ni);
            if ("None".equalsIgnoreCase(pn)) { lastEndedOnNone = (b.remaining() == 0); break; }
            if (b.remaining() < 1) break;
            int info = b.get() & 0xFF;
            int type = info & 0x0F;
            int sizeCode = (info >> 4) & 0x07;
            boolean flag = (info & 0x80) != 0;

            String structName = null;
            if (type == 10) structName = name(compact(b));     // StructProperty

            int size;
            switch (sizeCode) {
                case 0: size = 1; break;
                case 1: size = 2; break;
                case 2: size = 4; break;
                case 3: size = 12; break;
                case 4: size = 16; break;
                case 5: size = b.get() & 0xFF; break;
                case 6: size = b.getShort() & 0xFFFF; break;
                default: size = b.getInt(); break;
            }
            if (flag && type != 3) compact(b);                  // index de tableau

            String val;
            int start = b.position();
            switch (type) {
                case 1: val = String.valueOf(b.get() & 0xFF); break;                  // Byte
                case 2: val = String.valueOf(b.getInt()); break;                      // Int
                case 3: val = flag ? "True" : "False"; break;                         // Bool
                case 4: val = String.valueOf(b.getFloat()); break;                    // Float
                case 5: case 8: val = objRef(compact(b)); break;                      // Object/Class
                case 6: val = name(compact(b)); break;                                // Name
                case 10: {                                                            // Struct
                    if (ATOMIC.contains(structName)) {
                        StringBuilder sb = new StringBuilder(structName + "(");
                        int n = size / 4;
                        for (int i = 0; i < n && b.remaining() >= 4; i++)
                            sb.append(i > 0 ? "," : "").append(b.getFloat());
                        val = sb.append(")").toString();
                    } else {
                        byte[] inner = new byte[Math.min(size, b.remaining())];
                        b.get(inner);
                        StringBuilder sb = new StringBuilder("(");
                        boolean first = true;
                        for (String[] kv : props(inner)) {
                            if (!first) sb.append(",");
                            sb.append(kv[0]).append("=").append(fmt(kv));
                            first = false;
                        }
                        val = sb.append(")").toString();
                    }
                    break;
                }
                case 9: {                                                             // Array
                    int n = compact(b);
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < n; i++)
                        sb.append(i > 0 ? "," : "").append(objRef(compact(b)));
                    val = sb.toString();
                    break;
                }
                default: val = "<type" + type + " size" + size + ">"; break;
            }
            int consumed = b.position() - start;
            if (type != 3 && consumed != size) b.position(start + size);              // recalage
            out.add(new String[]{pn, val, String.valueOf(type)});
        }
        return out;
    }

    public static void main(String[] a) throws Exception {
        try (UnrealPackage p = new UnrealPackage(new File(a[0]), true)) {
            up = p;
            names = p.getNameTable();
            String filter = a.length > 1 ? a[1] : "";
            for (UnrealPackage.ExportEntry e : p.getExportTable()) {
                String fn = e.getObjectFullName();
                if (!fn.contains(filter)) continue;
                if (!e.getFullClassName().endsWith("L2EffectEmitter")) continue;
                System.out.println("-- " + fn);
                for (String[] kv : props(e.getObjectRawData()))
                    System.out.println("     " + kv[0] + " = " + kv[1]);
            }
        }
    }
}
