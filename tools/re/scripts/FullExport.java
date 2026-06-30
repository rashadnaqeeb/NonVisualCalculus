// Headless script: decompile every il2cpp method in the binary to a browsable reference tree,
// one .c file per type under <outdir>/<namespace dirs>/<Type>.c. Parallel across cores, chunked
// so memory stays flat. Run with -process against the analyzed+typed project.
//
// Usage (analyzeHeadless -process -postScript): FullExport.java <outdir>
//@category IL2CPP
import java.io.BufferedWriter;
import java.io.File;
import java.io.FileOutputStream;
import java.io.OutputStreamWriter;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.List;

import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileResults;
import ghidra.app.decompiler.parallel.DecompileConfigurer;
import ghidra.app.decompiler.parallel.DecompilerCallback;
import ghidra.app.decompiler.parallel.ParallelDecompiler;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.FunctionIterator;

public class FullExport extends GhidraScript {

    private static final int CHUNK = 4000;

    // One decompiled function plus the type it belongs to, so results can be grouped to files.
    private static class Unit {
        final String type;
        final String c;
        Unit(String type, String c) { this.type = type; this.c = c; }
    }

    @Override
    public void run() throws Exception {
        String outdir = getScriptArgs()[0];
        new File(outdir).mkdirs();

        // Collect every il2cpp-named method (Type$$Method) and sort by name so each type is contiguous.
        List<Function> funcs = new ArrayList<>();
        FunctionIterator it = currentProgram.getFunctionManager().getFunctions(true);
        for (Function f : it) {
            if (f.getName().contains("$$")) {
                funcs.add(f);
            }
        }
        funcs.sort(Comparator.comparing(Function::getName));
        println("FullExport: " + funcs.size() + " methods to decompile");

        DecompileConfigurer configurer = new DecompileConfigurer() {
            @Override
            public void configure(DecompInterface d) {
                d.toggleCCode(true);
                d.openProgram(currentProgram);
            }
        };
        DecompilerCallback<Unit> callback = new DecompilerCallback<Unit>(currentProgram, configurer) {
            @Override
            public Unit process(DecompileResults results, ghidra.util.task.TaskMonitor m) {
                Function f = results.getFunction();
                String name = f.getName();
                String type = name.substring(0, name.indexOf("$$"));
                String c = results.decompileCompleted()
                        ? results.getDecompiledFunction().getC()
                        : "// FAILED: " + name + " (" + results.getErrorMessage() + ")\n";
                return new Unit(type, c);
            }
        };
        callback.setTimeout(120);

        int written = 0;
        int types = 0;
        try {
            // Process in type-aligned chunks: never split a type across chunks, so each type's
            // file is opened and closed exactly once.
            int i = 0;
            while (i < funcs.size()) {
                int end = Math.min(i + CHUNK, funcs.size());
                while (end < funcs.size()
                        && typeOf(funcs.get(end)).equals(typeOf(funcs.get(end - 1)))) {
                    end++;
                }
                List<Function> batch = funcs.subList(i, end);
                List<Unit> units = ParallelDecompiler.decompileFunctions(callback, batch, monitor);
                units.sort(Comparator.comparing(u -> u.type));

                String curType = null;
                BufferedWriter w = null;
                for (Unit u : units) {
                    if (!u.type.equals(curType)) {
                        if (w != null) w.close();
                        w = openTypeFile(outdir, u.type);
                        curType = u.type;
                        types++;
                    }
                    w.write(u.c);
                    w.write("\n\n");
                    written++;
                }
                if (w != null) w.close();

                monitor.setMessage("FullExport " + written + "/" + funcs.size());
                i = end;
            }
        } finally {
            callback.dispose();
        }
        println("FullExport: wrote " + written + " methods across " + types + " type files to " + outdir);
    }

    private String typeOf(Function f) {
        String n = f.getName();
        return n.substring(0, n.indexOf("$$"));
    }

    // Maps "Namespace.Sub.Type<Args>" to <outdir>/Namespace/Sub/Type_Args_.c, keeping generic
    // angle-bracket content in the filename rather than splitting it into directories.
    private BufferedWriter openTypeFile(String outdir, String type) throws Exception {
        int lt = type.indexOf('<');
        String dotted = lt < 0 ? type : type.substring(0, lt);
        String tail = lt < 0 ? "" : type.substring(lt);
        String[] parts = dotted.split("\\.");
        File dir = new File(outdir);
        for (int k = 0; k < parts.length - 1; k++) {
            dir = new File(dir, sanitize(parts[k]));
        }
        dir.mkdirs();
        String fileName = sanitize(parts[parts.length - 1] + tail) + ".c";
        File f = new File(dir, fileName);
        BufferedWriter w = new BufferedWriter(new OutputStreamWriter(new FileOutputStream(f), "UTF-8"));
        w.write("// " + type + "\n\n");
        return w;
    }

    private String sanitize(String s) {
        StringBuilder b = new StringBuilder(s.length());
        for (int k = 0; k < s.length(); k++) {
            char c = s.charAt(k);
            b.append((Character.isLetterOrDigit(c) || c == '_' || c == '-') ? c : '_');
        }
        String out = b.toString();
        return out.length() > 180 ? out.substring(0, 180) : out;
    }
}
