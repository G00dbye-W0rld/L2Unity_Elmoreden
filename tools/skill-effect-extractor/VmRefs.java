import acmi.l2.clientmod.io.UnrealPackage;
import java.io.*;
import java.util.*;

// Une seule passe : pour chaque VertMeshEmitter, sa classe et son maillage anime.
public class VmRefs {
    public static void main(String[] a) throws Exception {
        try (UnrealPackage p = new UnrealPackage(new File(a[0]), true)) {
            Dump.up = p; Dump.names = p.getNameTable();
            try (PrintWriter w = new PrintWriter(a[1], "UTF-8")) {
                for (UnrealPackage.ExportEntry e : p.getExportTable()) {
                    if (!e.getFullClassName().endsWith("VertMeshEmitter")) continue;
                    String[] parts = e.getObjectFullName().split("[.]");
                    if (parts.length < 3) continue;
                    for (String[] kv : Dump.props(e.getObjectRawData()))
                        if (kv[0].equals("VertexMesh")) w.println(parts[1] + "\t" + kv[1]);
                }
            }
        }
        System.out.println("fait");
    }
}
