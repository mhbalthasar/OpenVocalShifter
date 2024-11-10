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
        private object sampleBound;

        public List<OVSPoint> Items { get; private set; }
        public WorldFunctionSwitcher FunctionSwitcher { get => hVsprj.FunctionSwitcher; set => hVsprj.FunctionSwitcher = value; }

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
                var ret = new OVSPoint(i, hVsprj.WorldArgs);
                {
                    if (FunctionSwitcher.Enable_PitchChange)
                    {
                        ret.pit_Edt = hVsprj.WorldArgs.pitch[i];
                        ret.pit_Org = hVsprj.WorldArgs.pitch[i];
                    }
                    if (FunctionSwitcher.Enable_GenderChange)
                    {
                        ret.gen_Edt = 0;
                    }
                    if (FunctionSwitcher.Enable_FormantChange)
                    {
                        ret.fmt_Edt = new double[4]
                        {
                            0,0,0,0
                        };
                        ret.fmtBand_Edt = new WorldFormantBands();
                    }
                };
                lock (Items) Items.Add(ret);
            });
            Items = Items.OrderBy(p => p.index).ToList();
        }

        public void ApplyData()
        {
            if (FunctionSwitcher.Enable_PitchChange)
            {
                var pitCL = Items.Where(p => (p.pit_Edt != p.pit_Org));
                bool pitChanged = pitCL.Count() > 0;
                if (pitChanged)
                {
                    foreach (var p in pitCL)
                    {
                        hVsprj.WorldArgs.pitch[p.index] = p.pit_Edt;
                    }
                    hVsprj.PitchApplyToF0();
                }
            }
            if (FunctionSwitcher.Enable_FormantChange)
            {
                var fmtCL = Items.Where(p => (p.fmt_Edt.Length == 4 && (p.fmt_Edt.Where(n => n != 0).Count() > 0)));
                bool fmtChanged = fmtCL.Count() > 0;
                if (fmtChanged)
                {
                    foreach (var p in fmtCL)
                    {
                        hVsprj.WorldArgs.f1shifter[p.index] = p.fmt_Edt[0];
                        hVsprj.WorldArgs.f2shifter[p.index] = p.fmt_Edt[1];
                        hVsprj.WorldArgs.f3shifter[p.index] = p.fmt_Edt[2];
                        hVsprj.WorldArgs.f4shifter[p.index] = p.fmt_Edt[3];
                        hVsprj.WorldArgs.formants_bands[p.index] = p.fmtBand_Edt;
                    }
                    hVsprj.FormantsApplyToSP();
                }
            }
            if (FunctionSwitcher.Enable_GenderChange)
            {
                var genCL = Items.Where(p => (p.gen_Edt != 0));
                bool genChanged = genCL.Count() > 0;
                if (genChanged)
                {
                    foreach (var p in genCL)
                    {
                        hVsprj.WorldArgs.gender[p.index] = p.gen_Edt;
                    }
                    hVsprj.GenderApplyToSP();
                }
            }
        }

    }
}
