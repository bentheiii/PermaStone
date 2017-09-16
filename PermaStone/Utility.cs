using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PermaStone
{
    internal static class Utility
    {
        public static string MutateFileName(string path, Func<string, string> mut)
        {
            var root = Path.GetDirectoryName(path);
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);

            return Path.Combine(root, mut(name) + ext);
        }
    }
}
