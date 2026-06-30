// Headless post-script: import il2cpp types and apply per-method signatures so the decompiler
// shows named struct fields (this->fields.x) instead of raw offsets. Run AFTER ApplyIl2Cpp
// (which named the functions) on the saved project; this only adds types, never renames.
//
// Needs the Ghidra-flattened header from Il2CppDumper's il2cpp_header_to_ghidra.py
// (C++ inheritance rewritten to a `super` member), not the raw il2cpp.h.
//
// Usage (analyzeHeadless -postScript): ApplyStructs.java <script.json> <il2cpp_ghidra.h>
//@category IL2CPP
import java.io.BufferedReader;
import java.io.FileInputStream;
import java.io.InputStreamReader;

import com.google.gson.stream.JsonReader;
import com.google.gson.stream.JsonToken;

import ghidra.app.cmd.function.ApplyFunctionSignatureCmd;
import ghidra.app.cmd.function.FunctionRenameOption;
import ghidra.app.script.GhidraScript;
import ghidra.app.services.DataTypeManagerService;
import ghidra.app.util.cparser.C.CParserUtils;
import ghidra.program.model.address.Address;
import ghidra.program.model.data.DataTypeManager;
import ghidra.program.model.data.FunctionDefinitionDataType;
import ghidra.program.model.symbol.SourceType;

public class ApplyStructs extends GhidraScript {

    @Override
    public void run() throws Exception {
        String jsonPath = getScriptArgs()[0];
        String headerPath = getScriptArgs()[1];
        Address base = currentProgram.getImageBase();
        SourceType user = SourceType.USER_DEFINED;
        DataTypeManager dtm = currentProgram.getDataTypeManager();

        // Idempotent: only parse the header if the types aren't already in the project.
        if (getDataTypes("Il2CppObject").length == 0) {
            println("ApplyStructs: parsing header (this is the slow, fragile step) ...");
            try {
                CParserUtils.parseHeaderFiles(new DataTypeManager[] {},
                        new String[] { headerPath }, new String[] {}, dtm, monitor);
            } catch (Throwable t) {
                // The parser collects per-type errors and keeps the types it could build; press on.
                println("ApplyStructs: header parse reported issues: " + t.getMessage());
            }
        } else {
            println("ApplyStructs: types already present, skipping header parse");
        }
        println("ApplyStructs: applying signatures ...");

        int sigOk = 0;
        int sigFail = 0;
        int meta = 0;
        JsonReader r = new JsonReader(new BufferedReader(
                new InputStreamReader(new FileInputStream(jsonPath), "UTF-8")));
        r.beginObject();
        while (r.hasNext()) {
            String section = r.nextName();
            if (section.equals("ScriptMethod")) {
                r.beginArray();
                while (r.hasNext()) {
                    long addr = -1;
                    String sig = null;
                    r.beginObject();
                    while (r.hasNext()) {
                        String k = r.nextName();
                        if (k.equals("Address")) addr = readLong(r);
                        else if (k.equals("Signature")) sig = r.nextString();
                        else r.skipValue();
                    }
                    r.endObject();
                    if (addr >= 0 && sig != null) {
                        if (applySig(base.add(addr), sig)) sigOk++;
                        else sigFail++;
                    }
                }
                r.endArray();
            } else if (section.equals("ScriptMetadata")) {
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
                        try {
                            createLabel(base.add(addr), name.replace(' ', '-'), true, user);
                        } catch (Exception e) { /* skip */ }
                        meta++;
                    }
                }
                r.endArray();
            } else {
                r.skipValue();
            }
        }
        r.endObject();
        r.close();

        println("ApplyStructs: signatures ok=" + sigOk + " fail=" + sigFail
                + ", metadata labeled=" + meta);
    }

    // Types the function's params (so `this` becomes Type_o* and fields get names) without
    // renaming the function or its existing label.
    private boolean applySig(Address addr, String sig) {
        if (getFunctionAt(addr) == null) {
            return false;
        }
        String s = sig.endsWith(";") ? sig.substring(0, sig.length() - 1) : sig;
        try {
            FunctionDefinitionDataType def =
                    CParserUtils.parseSignature((DataTypeManagerService) null, currentProgram, s, false);
            if (def == null) {
                return false;
            }
            return new ApplyFunctionSignatureCmd(addr, def, SourceType.USER_DEFINED, false,
                    FunctionRenameOption.NO_CHANGE).applyTo(currentProgram);
        } catch (Exception e) {
            return false;
        }
    }

    private long readLong(JsonReader r) throws Exception {
        if (r.peek() == JsonToken.STRING) {
            return Long.decode(r.nextString());
        }
        return r.nextLong();
    }
}
