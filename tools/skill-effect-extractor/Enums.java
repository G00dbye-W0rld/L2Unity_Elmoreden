import acmi.l2.clientmod.io.UnrealPackage;
import java.io.File;
import java.nio.*;

public class Enums {
    public static void main(String[] a) throws Exception {
        try (UnrealPackage p = new UnrealPackage(new File(a[0]), true)) {
            Dump.up = p; Dump.names = p.getNameTable();
            for (UnrealPackage.ExportEntry e : p.getExportTable()) {
                if (!e.getFullClassName().endsWith(".Enum")) continue;
                try {
                    byte[] raw = e.getObjectRawData();
                    ByteBuffer b = ByteBuffer.wrap(raw).order(ByteOrder.LITTLE_ENDIAN);
                    Dump.compact(b); Dump.compact(b); Dump.compact(b);
                    int n = Dump.compact(b);
                    if (n <= 0 || n > 300) continue;
                    StringBuilder sb = new StringBuilder();
                    boolean hit = false;
                    for (int i = 0; i < n; i++) {
                        if (b.remaining() < 1) { hit = false; break; }
                        String nm = Dump.names.get(Dump.compact(b)).getName();
                        if (nm.startsWith(a[1])) hit = true;
                        sb.append(i).append("=").append(nm).append(" ");
                    }
                    if (hit) System.out.println(e.getObjectName().getName() + " (" + n + ") : " + sb);
                } catch (Exception ignored) {}
            }
        }
    }
}
