import acmi.l2.clientmod.io.UnrealPackage;
import java.io.File;
import java.util.*;

// Les proprietes par defaut d'une Class sont en queue de son bloc brut, apres un
// en-tete de taille variable. On cherche le plus petit decalage qui se decode
// proprement et finit pile sur le terminateur.
public class Cls {
    public static void main(String[] a) throws Exception {
        try (UnrealPackage p = new UnrealPackage(new File(a[0]), true)) {
            Dump.up = p; Dump.names = p.getNameTable();
            for (UnrealPackage.ExportEntry e : p.getExportTable()) {
                if (!e.getObjectFullName().contains(a[1])) continue;
                if (!e.getFullClassName().endsWith(".Class")) continue;
                byte[] raw = e.getObjectRawData();
                System.out.println("== " + e.getObjectFullName() + " (" + raw.length + " octets)");
                for (int off = 0; off < raw.length - 2; off++) {
                    byte[] tail = Arrays.copyOfRange(raw, off, raw.length);
                    try {
                        List<String[]> pr = Dump.props(tail);
                        if (pr.size() >= 2 && Dump.lastEndedOnNone) {
                            System.out.println("   decalage " + off + " :");
                            for (String[] kv : pr) System.out.println("     " + kv[0] + " = " + kv[1]);
                            break;
                        }
                    } catch (Exception ignored) {}
                }
            }
        }
    }
}
