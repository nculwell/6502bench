using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SourceGen
{
    public class JSON
    {
        public static string Serialize(object obj)
        {
            return JsonConvert.SerializeObject(obj);
        }
        public static T Deserialize<T>(string jsonStr)
        {
            T? t = JsonConvert.DeserializeObject<T>(jsonStr);
            if (t == null)
                throw new Exception("Deserialized to null.");
            return t;
        }
    }
}
