using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenVoiceShifter.API
{
    public class OVSPoint(int index)
    {
        public int index { get; private set; }
        public double pit_Org { get; set; }
        public double gen_Edt { get; set; }
        public double pit_Edt { get; set; }
        public double[] fmt_Edt { get; set; }
    }
}
