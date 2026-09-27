import acmi.l2.clientmod.io.UnrealPackage;
import java.io.File;
public class Names {
    public static void main(String[] a) throws Exception {
        try (UnrealPackage p = new UnrealPackage(new File(a[0]), true)) {
            int i = 0;
            for (UnrealPackage.NameEntry n : p.getNameTable()) {
                if (n.getName().startsWith(a[1])) System.out.println("  " + i + " = " + n.getName());
                i++;
            }
        }
    }
}
