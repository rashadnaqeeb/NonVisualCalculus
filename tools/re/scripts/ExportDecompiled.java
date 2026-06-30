// Headless script: decompile every function whose name contains <query> and write the C.
// Run with -process against the already-analyzed project so it never re-analyzes.
//
// Usage (analyzeHeadless -process -postScript): ExportDecompiled.java <query> <outdir>
// Example query "SenseOrb$$" dumps every method of SenseOrb.
//@category IL2CPP
import java.io.BufferedWriter;
import java.io.File;
import java.io.FileOutputStream;
import java.io.OutputStreamWriter;
import java.io.PrintWriter;
import java.util.ArrayList;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Set;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileResults;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.FunctionIterator;
import ghidra.program.model.symbol.Symbol;
import ghidra.program.model.symbol.SymbolIterator;
import ghidra.program.model.symbol.SymbolTable;

public class ExportDecompiled extends GhidraScript {

    @Override
    public void run() throws Exception {
        String[] args = getScriptArgs();
        String query = args[0];
        String outdir = args[1];
        new File(outdir).mkdirs();

        DecompInterface decomp = new DecompInterface();
        decomp.openProgram(currentProgram);

        List<Function> matches = new ArrayList<>();
        FunctionIterator it = currentProgram.getFunctionManager().getFunctions(true);
        for (Function f : it) {
            if (f.getName().contains(query)) {
                matches.add(f);
            }
        }

        StringBuilder body = new StringBuilder();
        int ok = 0;
        for (Function f : matches) {
            DecompileResults res = decomp.decompileFunction(f, 120, monitor);
            if (res.decompileCompleted()) {
                body.append(res.getDecompiledFunction().getC()).append("\n\n");
                ok++;
            } else {
                body.append("// FAILED: ").append(f.getName())
                        .append(" (").append(res.getErrorMessage()).append(")\n\n");
            }
        }

        String legend = resolveStringLiterals(body);

        String safe = query.replaceAll("[^A-Za-z0-9._-]", "_");
        File outFile = new File(outdir, safe + ".c");
        PrintWriter out = new PrintWriter(new BufferedWriter(
                new OutputStreamWriter(new FileOutputStream(outFile), "UTF-8")));
        out.println("// Decompiled functions matching '" + query + "' (" + matches.size() + " found)");
        out.println();
        out.print(legend);
        out.print(body);
        out.close();

        println("ExportDecompiled: wrote " + ok + "/" + matches.size() + " functions to "
                + outFile.getAbsolutePath());
    }

    // Builds a legend mapping each StringLiteral_N referenced in the body to its text value,
    // read from the EOL comment ApplyIl2Cpp placed at the literal's address.
    private String resolveStringLiterals(CharSequence body) {
        Set<String> names = new LinkedHashSet<>();
        Matcher m = Pattern.compile("StringLiteral_\\d+").matcher(body);
        while (m.find()) {
            names.add(m.group());
        }
        if (names.isEmpty()) {
            return "";
        }
        SymbolTable st = currentProgram.getSymbolTable();
        StringBuilder legend = new StringBuilder("// --- string literals ---\n");
        for (String name : names) {
            SymbolIterator it = st.getSymbols(name);
            String value = "?";
            if (it.hasNext()) {
                Symbol s = it.next();
                String c = getEOLComment(s.getAddress());
                if (c != null) {
                    value = c.replace("\r", "").replace("\n", "\\n");
                }
            }
            legend.append("// ").append(name).append(" = ").append(value).append('\n');
        }
        legend.append("\n");
        return legend.toString();
    }
}
