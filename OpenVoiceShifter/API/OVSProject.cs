using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace OpenVoiceShifter.API
{
    public class OVSProject
    {
        public WorldSample? hVsprj;

        public List<OVSPoint> Items { get; private set; }

        public OVSProject()
        {
            WorldInitialize.Initialize();
            hVsprj = new WorldSample();
            Items = new List<OVSPoint>();
        }

        public float[] SynthesisData()
        {
            int y_length = hVsprj.WorldArgs.synthesisedLength + 1;
            float[] y = new float[y_length];
            for (var i = 0; i < y.Length; i++)
                y[i] = 0.0f;
            hVsprj.WaveformSynthesis(y_length, y);
            return y;
        }

        public void LoadData(float[] samples, int sampleRate = 44100, int channels = 2)
        {
            hVsprj.F0EstimationHarvest(samples, samples.Length);
            hVsprj.SpectralEnvelopeEstimation(samples, samples.Length);
            hVsprj.AperiodicityEstimation(samples, samples.Length);
            InitData();
        }

        private void InitData()
        {
            Parallel.For(0, hVsprj.WorldArgs.f0_length, (i) =>
            {
                var ret = new OVSPoint(i)
                {
                    pit_Edt = hVsprj.WorldArgs.pitch[i],
                    pit_Org = hVsprj.WorldArgs.pitch[i],
                    gen_Edt = hVsprj.WorldArgs.gender[i],
                    gen_Org = hVsprj.WorldArgs.gender[i],
                };
                lock (Items) Items.Add(ret);
            });
            Items = Items.OrderBy(p => p.index).ToList();
        }

        public void ApplyData()
        {
            bool pitChanged = Items.Where(p => (p.pit_Edt != p.pit_Org)).Count() > 0;
            if(pitChanged)hVsprj.PitchApplyToF0();

            bool genChanged = Items.Where(p => (p.gen_Edt != p.gen_Org)).Count() > 0;
            if (genChanged) hVsprj.GenderApplyToSP();

        }

    }
}
