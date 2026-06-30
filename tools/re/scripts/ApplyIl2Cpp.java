// Headless post-script: apply Il2CppDumper symbols to the imported GameAssembly.dll.
// Names every il2cpp method (Type$$Method) and labels string literals with their value
// as an EOL comment (for string xrefs). Struct/signature typing from il2cpp.h is skipped:
// named functions are the readability win and the 34MB C-header parse is the fragile part.
//
// Streams the 73MB script.json with Gson (bundled in Ghidra) so memory stays flat.
// Usage (analyzeHeadless -postScript): ApplyIl2Cpp.java <path-to-script.json>
//@category IL2CPP
import java.io.BufferedReader;
import java.io.FileInputStream;
import java.io.InputStreamReader;

import com.google.gson.stream.JsonReader;
import com.google.gson.stream.JsonToken;

import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.symbol.SourceType;

public class ApplyIl2Cpp extends GhidraScript {

    @Override
    public void run() throws Exception {
        String jsonPath = getScriptArgs()[0];
        Address base = currentProgram.getImageBase();
        SourceType user = SourceType.USER_DEFINED;

        int methods = 0;
        int strings = 0;

        JsonReader r = new JsonReader(new BufferedReader(
                new InputStreamReader(new FileInputStream(jsonPath), "UTF-8")));
        r.beginObject();
        while (r.hasNext()) {
            String section = r.nextName();
            if (section.equals("ScriptMethod")) {
                r.beginArray();
                while (r.hasNext()) {
                    long addr = -1;
                    String name = null;
                    r.beginObject();
                    while (r.hasNext()) {
                        String k = r.nextName();
                        if (k.equals("Address")) addr = readLong(r);
                        else if (k.equals("Name")) name = r.nextString();
                        else r.skipValue();
                    }
                    r.endObject();
                    if (addr >= 0 && name != null) {
                        Address a = base.add(addr);
                        ensureFunction(a);
                        try {
                            createLabel(a, name.replace(' ', '-'), true, user);
                        } catch (Exception e) { /* unrenamable symbol; skip */ }
                        methods++;
                    }
                }
                r.endArray();
            } else if (section.equals("ScriptString")) {
                r.beginArray();
                int idx = 1;
                while (r.hasNext()) {
                    long addr = -1;
                    String value = null;
                    r.beginObject();
                    while (r.hasNext()) {
                        String k = r.nextName();
                        if (k.equals("Address")) addr = readLong(r);
                        else if (k.equals("Value")) value = r.nextString();
                        else r.skipValue();
                    }
                    r.endObject();
                    if (addr >= 0) {
                        Address a = base.add(addr);
                        try {
                            createLabel(a, "StringLiteral_" + idx, true, user);
                            if (value != null) setEOLComment(a, value);
                        } catch (Exception e) { /* skip */ }
                        strings++;
                    }
                    idx++;
                }
                r.endArray();
            } else {
                r.skipValue();
            }
        }
        r.endObject();
        r.close();

        println("ApplyIl2Cpp: named " + methods + " methods, labeled " + strings + " strings");
    }

    private long readLong(JsonReader r) throws Exception {
        if (r.peek() == JsonToken.STRING) {
            return Long.decode(r.nextString());
        }
        return r.nextLong();
    }

    private void ensureFunction(Address a) {
        if (getFunctionAt(a) == null) {
            try {
                createFunction(a, null);
            } catch (Exception e) { /* not a valid function start; skip */ }
        }
    }
}
