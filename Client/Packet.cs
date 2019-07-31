using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Client
{
    class Packet
    {
        public string commanda;
        public byte[] data;
    }

    class FileInfoKratko
    {
        public string name;
        public long size;
    }
}
