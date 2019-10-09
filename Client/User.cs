using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Client
{
    class User
    {
        public string Name { get; set; }
        public int Password { get; set; }
        public byte[] key { get; set; }
    }
}
