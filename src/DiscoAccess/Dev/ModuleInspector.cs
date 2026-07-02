using System;
using System.IO;
using System.Reflection;
using System.Text;
using DiscoAccess.Modularity;
using HarmonyLib;

namespace DiscoAccess.Dev
{
    /// <summary>
    /// The /module readout: which module is live (type, load generation), which bytes it came from (the
    /// DLL's write time, so a driver can tell a reload picked up the build it just made rather than a
    /// stale deploy), and the process-wide Harmony patch table with LIVE counts per method. The patch
    /// table is the tripwire for the reload-strips-patches class of bug: a method listed with zero live
    /// patches means something applied and later removed them, which from gameplay is indistinguishable
    /// from silence.
    /// </summary>
    internal static class ModuleInspector
    {
        public static string Describe(ModuleLoader loader)
        {
            var sb = new StringBuilder();

            if (loader.Module == null)
                sb.Append("module: [not loaded]\n");
            else
                sb.Append("module: ").Append(loader.Module.GetType().FullName)
                  .Append(" (generation ").Append(loader.Generation).Append(")\n");

            try
            {
                var dll = new FileInfo(loader.ModulePath);
                if (dll.Exists)
                {
                    TimeSpan age = DateTime.UtcNow - dll.LastWriteTimeUtc;
                    sb.Append("dll: ").Append(loader.ModulePath)
                      .Append("\ndll written: ").Append(dll.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"))
                      .Append(" (").Append(FormatAge(age)).Append(" ago)\n");
                }
                else
                    sb.Append("dll: missing at ").Append(loader.ModulePath).Append('\n');
            }
            catch (Exception e)
            {
                sb.Append("dll: [unreadable] ").Append(e.Message).Append('\n');
            }

            AppendPatches(sb);
            return sb.ToString();
        }

        // Every method Harmony has ever patched this process, with live prefix/postfix/etc counts and
        // owner ids. Methods our own hot-reload has unpatched stay listed with zero counts - exactly the
        // state worth surfacing.
        private static void AppendPatches(StringBuilder sb)
        {
            try
            {
                int methods = 0, live = 0;
                var lines = new StringBuilder();
                foreach (MethodBase m in Harmony.GetAllPatchedMethods())
                {
                    methods++;
                    Patches info = Harmony.GetPatchInfo(m);
                    int count = info == null ? 0 : info.Prefixes.Count + info.Postfixes.Count
                        + info.Transpilers.Count + info.Finalizers.Count;
                    live += count;
                    lines.Append("  ").Append(m.DeclaringType?.FullName).Append('.').Append(m.Name)
                         .Append(": ");
                    if (count == 0)
                    {
                        lines.Append("0 live (was patched, later removed)\n");
                        continue;
                    }
                    if (info.Prefixes.Count > 0) lines.Append(info.Prefixes.Count).Append(" prefix ");
                    if (info.Postfixes.Count > 0) lines.Append(info.Postfixes.Count).Append(" postfix ");
                    if (info.Transpilers.Count > 0) lines.Append(info.Transpilers.Count).Append(" transpiler ");
                    if (info.Finalizers.Count > 0) lines.Append(info.Finalizers.Count).Append(" finalizer ");
                    lines.Append('[').Append(string.Join(", ", info.Owners)).Append("]\n");
                }
                sb.Append("patches: ").Append(live).Append(" live across ").Append(methods).Append(" methods\n");
                sb.Append(lines);
            }
            catch (Exception e)
            {
                sb.Append("patches: [unreadable] ").Append(e.Message).Append('\n');
            }
        }

        private static string FormatAge(TimeSpan age)
        {
            if (age.TotalSeconds < 0) return "in the future?";
            if (age.TotalMinutes < 1) return (int)age.TotalSeconds + "s";
            if (age.TotalHours < 1) return (int)age.TotalMinutes + "m " + age.Seconds + "s";
            return (int)age.TotalHours + "h " + age.Minutes + "m";
        }
    }
}
