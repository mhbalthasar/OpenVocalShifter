using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenVoiceShifter.API
{
    public class OVSPoint
    {
        public OVSPoint(int i, WorldParameters WorldArgs)
        {

            sampleBound = [
                (int)(((i     * WorldArgs.frame_period) / 1000.0) * WorldArgs.fs),
                i==WorldArgs.f0_length ? WorldArgs.synthesisedLength:
                (int)((((i+1) * WorldArgs.frame_period) / 1000.0) * WorldArgs.fs)
                ];
            index = i;
            time = (i * WorldArgs.frame_period) / 1000.0;
        }
        public int[] sampleBound { get; private set; }
        public int index { get; private set; }
        public double time { get; private set; }
        public double pit_Org { get; set; }
        public double gen_Edt { get; set; }
        public double pit_Edt { get; set; }
        public double[] fmt_Edt { get; set; }
        public WorldFormantBands fmtBand_Edt { get; set; }
    }
}
