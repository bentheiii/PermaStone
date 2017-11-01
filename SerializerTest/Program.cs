using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PermaStone.Enumerable;
using CipherStone;

namespace SerializerTest
{
    class Program
    {
        static void Main(string[] args)
        {
            var toEncode = new PermaList<Torrent_Notifier_3.Entry>.PermaObjArrayData(100, 610);
            var bytes= DotNetSerializer.Serialize(toEncode);
            File.WriteAllBytes(".out",bytes);
        }
    }
}
