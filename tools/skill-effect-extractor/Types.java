import acmi.l2.clientmod.io.UnrealPackage;
import java.io.*;
import java.util.*;

// Pour chaque classe d'effet, la liste des types d'emetteurs qu'elle contient.
public class Types {
    public static void main(String[] a) throws Exception {
        Map<String, Map<String, Integer>> byClass = new TreeMap<>();
        try (UnrealPackage p = new UnrealPackage(new File(a[0]), true)) {
            for (UnrealPackage.ExportEntry e : p.getExportTable()) {
                String cls = e.getFullClassName();
                if (!cls.endsWith("Emitter")) continue;
                String full = e.getObjectFullName();          // Paquet.Classe.Emetteur0
                String[] parts = full.split("[.]");
                if (parts.length < 3) continue;
                String owner = parts[1];
                String type = cls.substring(cls.lastIndexOf('.') + 1);
                byClass.computeIfAbsent(owner, k -> new TreeMap<>()).merge(type, 1, Integer::sum);
            }
        }
        try (PrintWriter w = new PrintWriter(a[1], "UTF-8")) {
            for (Map.Entry<String, Map<String, Integer>> en : byClass.entrySet()) {
                StringBuilder sb = new StringBuilder(en.getKey());
                for (Map.Entry<String, Integer> t : en.getValue().entrySet())
                    sb.append("\t").append(t.getKey()).append(":").append(t.getValue());
                w.println(sb);
            }
        }
        System.out.println("classes avec emetteurs : " + byClass.size());
    }
}
