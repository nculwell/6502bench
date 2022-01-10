using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SourceGen
{
    internal class JsonSerializer
    {
        public string Serialize(object obj) => JSON.Serialize(obj);
        public T Deserialize<T>(string jsonStr) => JSON.Deserialize<T>(jsonStr);
    }
}
