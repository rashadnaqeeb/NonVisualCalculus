using System;
using System.Collections.Generic;
using System.IO;
using NonVisualCalculus.Core.Modularity;
using NonVisualCalculus.Core.World;

namespace NonVisualCalculus.Module.World
{
    /// <summary>
    /// The bookmarks file on disk (BepInEx's config folder, beside the mod's settings), in Core's
    /// <see cref="BookmarkFile"/> format. Read fresh on every query and rewritten whole on every change:
    /// the list is a handful of lines, and re-reading keeps no mod-side copy a hand edit could stale
    /// out. A missing file is simply "no bookmarks yet"; an IO failure is logged and reads empty.
    /// </summary>
    internal sealed class BookmarkStore
    {
        private readonly IModHost _host;
        private readonly string _path;

        public BookmarkStore(IModHost host)
        {
            _host = host;
            _path = Path.Combine(BepInEx.Paths.ConfigPath, "NonVisualCalculus.bookmarks.txt");
        }

        /// <summary>Every bookmark in the file, file order.</summary>
        public List<Bookmark> LoadAll()
        {
            try
            {
                if (!File.Exists(_path)) return new List<Bookmark>();
                return BookmarkFile.Parse(File.ReadAllText(_path), w => _host.LogWarning("BookmarkStore: " + w));
            }
            catch (Exception e)
            {
                _host.LogError($"BookmarkStore: reading '{_path}' failed: {e}");
                return new List<Bookmark>();
            }
        }

        /// <summary>The one scene's bookmarks, file order.</summary>
        public List<Bookmark> ForScene(string scene)
        {
            List<Bookmark> all = LoadAll();
            all.RemoveAll(b => b.Scene != scene);
            return all;
        }

        /// <summary>Append one bookmark and rewrite the file. False (logged) when the write failed.</summary>
        public bool Add(Bookmark bookmark)
        {
            List<Bookmark> all = LoadAll();
            all.Add(bookmark);
            return Write(all);
        }

        /// <summary>Remove one bookmark (matched whole: scene, name, and position) and rewrite the file.
        /// False when nothing matched (the file changed under the menu) or the write failed, both logged.</summary>
        public bool Remove(Bookmark bookmark)
        {
            List<Bookmark> all = LoadAll();
            int removed = all.RemoveAll(b => b.Scene == bookmark.Scene && b.Name == bookmark.Name
                                             && b.Position == bookmark.Position);
            if (removed == 0)
            {
                _host.LogWarning($"BookmarkStore: no stored bookmark matches '{bookmark.Name}' on {bookmark.Scene}; nothing removed.");
                return false;
            }
            return Write(all);
        }

        private bool Write(List<Bookmark> all)
        {
            try
            {
                File.WriteAllText(_path, BookmarkFile.Serialize(all));
                return true;
            }
            catch (Exception e)
            {
                _host.LogError($"BookmarkStore: writing '{_path}' failed: {e}");
                return false;
            }
        }
    }
}
