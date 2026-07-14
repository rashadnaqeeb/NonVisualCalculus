using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace NonVisualCalculus.Dev
{
    /// <summary>
    /// The /type lookup: find a type by simple name across everything loaded (plus the interop proxies,
    /// loaded on demand) and print its full name, assembly, and public surface. Exists because namespace
    /// guessing in the REPL is a round trip per guess - MovementMode lives in Sunshine, ViewsPagesBridge
    /// in the global namespace - and the decompiled tree answers it slower than reflection can.
    /// </summary>
    internal static class TypeFinder
    {
        private const int MaxMatches = 25;
        private const int MaxMemberLines = 250;

        private static bool _interopLoaded;

        public static string Describe(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "[empty] pass ?name=TypeName\n";
            name = name.Trim();

            List<Type> matches = Search(name);
            if (matches.Count == 0)
            {
                // The interop proxies load lazily; pull the rest in so a game type not yet touched by
                // any code is still findable. One-time cost, and a dev session loads most of them anyway.
                LoadAllInterop();
                matches = Search(name);
            }
            if (matches.Count == 0)
                return "[none] no loaded type named '" + name + "' (interop included)\n";

            var sb = new StringBuilder();
            foreach (Type t in matches.Take(MaxMatches))
                sb.Append(t.FullName).Append(" (").Append(t.Assembly.GetName().Name).Append(")\n");
            if (matches.Count > MaxMatches)
                sb.Append("... ").Append(matches.Count - MaxMatches).Append(" more\n");

            if (matches.Count == 1)
                AppendMembers(sb, matches[0]);
            return sb.ToString();
        }

        // Exact simple-name match (case-insensitive), including nested types via the FullName suffix.
        private static List<Type> Search(string name)
        {
            var found = new List<Type>();
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic)
                    continue;
                Type[] types;
                try
                {
                    types = asm.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types.Where(t => t != null).ToArray();
                }
                catch
                {
                    continue;
                }
                foreach (Type t in types)
                    if (string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))
                        found.Add(t);
            }
            return found;
        }

        private static void LoadAllInterop()
        {
            if (_interopLoaded)
                return;
            _interopLoaded = true;
            string interop = Path.Combine(BepInEx.Paths.BepInExRootPath, "interop");
            if (!Directory.Exists(interop))
                return;
            foreach (string dll in Directory.GetFiles(interop, "*.dll"))
            {
                try
                {
                    Assembly.LoadFrom(dll);
                }
                catch
                {
                    // not loadable (native, corrupt); skip - the search covers whatever did load
                }
            }
        }

        // The type's public surface, compact: enum values inline; otherwise declared fields, properties,
        // and methods (accessors and Object plumbing skipped), one per line, capped.
        private static void AppendMembers(StringBuilder sb, Type t)
        {
            if (t.IsEnum)
            {
                sb.Append("enum: ");
                string[] names = Enum.GetNames(t);
                Array values = Enum.GetValues(t);
                var parts = new List<string>(names.Length);
                for (int i = 0; i < names.Length; i++)
                    parts.Add(names[i] + "=" + Convert.ToInt64(values.GetValue(i)));
                sb.Append(string.Join(" ", parts)).Append('\n');
                return;
            }

            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
                | BindingFlags.DeclaredOnly;
            int lines = 0;

            foreach (FieldInfo f in t.GetFields(flags))
            {
                if (++lines > MaxMemberLines) { sb.Append("...\n"); return; }
                sb.Append("  field ").Append(f.IsStatic ? "static " : "").Append(f.Name)
                  .Append(" : ").Append(Short(f.FieldType)).Append('\n');
            }
            foreach (PropertyInfo p in t.GetProperties(flags))
            {
                if (++lines > MaxMemberLines) { sb.Append("...\n"); return; }
                sb.Append("  prop ").Append(p.Name).Append(" : ").Append(Short(p.PropertyType)).Append('\n');
            }
            foreach (MethodInfo m in t.GetMethods(flags))
            {
                if (m.IsSpecialName) continue; // property/event accessors
                if (++lines > MaxMemberLines) { sb.Append("...\n"); return; }
                sb.Append("  ").Append(m.IsStatic ? "static " : "").Append(m.Name).Append('(')
                  .Append(string.Join(", ", m.GetParameters().Select(pi => Short(pi.ParameterType))))
                  .Append(") : ").Append(Short(m.ReturnType)).Append('\n');
            }
        }

        private static string Short(Type t) => t.IsGenericType
            ? t.Name.Split('`')[0] + "<" + string.Join(", ", t.GetGenericArguments().Select(Short)) + ">"
            : t.Name;
    }
}
