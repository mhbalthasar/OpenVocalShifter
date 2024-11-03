using DotnetWorld.API.Structs;
using DotnetWorld.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics;

namespace OpenVoiceShifter
{
    public class WorldParameters
    {
        public double frame_period = 5;
        public int fs = 44100;

        public double[] f0;
        public double[] time_axis;
        public int f0_length;

        public double[] pitch;
        public double[] gender;

        public double[,] spectrogram;
        public double[,] aperiodicity;
        public int fft_size;

        public int synthesisedLength => (int)((f0_length - 1) * frame_period / 1000.0 * fs);
    }
    public class WorldSample(int sampleRate=44100,double framePeriod=5)
    {
        WorldParameters world_parameters = new WorldParameters()
        {
            fs = sampleRate,
            frame_period= framePeriod
        };

        public WorldParameters WorldArgs { get => world_parameters; set => world_parameters = value; }

        private double MaxF0 { get; set; } = 800;
        private double MinF0 { get; set; } = 71;
        private double Q1 { get; set; } = -0.15;

        public void setAnaPrm_MinF0(int Pitch)
        {
            MinF0 = 440.0 * Math.Pow(2, (Pitch - 69) / 12.0);
        }
        public void setAnaPrm_MaxF0(int Pitch)
        {
            MaxF0 = 440.0 * Math.Pow(2, (Pitch - 69) / 12.0);
        }
        public void setAnaPrm_MinF0(double F0)
        {
            MinF0 = F0;
        }
        public void setAnaPrm_MaxF0(double F0)
        {
            MaxF0 = F0;
        }
        public void setAnaPrm_Q1(double Q)
        {
            Q1 = Q;
        }
        public void DisplayInformation(int fs, int nbit, int x_length)
        {
            System.Console.WriteLine("File information");
            System.Console.WriteLine($"Sampling : {fs} Hz {nbit} Bit");
            System.Console.WriteLine($"Length {x_length} [sample]");
            System.Console.WriteLine($"Lenght {((double)x_length / fs)} [sec]");
        }

        public void F0EstimationDio(double[] x, int x_length)
        {
            var option = new DioOption();

            Core.InitializeDioOption(option);

            option.frame_period = world_parameters.frame_period;
            option.speed = 1;
            option.f0_floor = MinF0;
            option.f0_ceil = MaxF0;
            option.allowed_range = 0.1;

            world_parameters.f0_length = Core.GetSamplesForDIO(world_parameters.fs,
                x_length, world_parameters.frame_period);
            world_parameters.f0 = new double[world_parameters.f0_length];
            world_parameters.time_axis = new double[world_parameters.f0_length];
            double[] refined_f0 = new double[world_parameters.f0_length];

            Core.Dio(x, x_length, world_parameters.fs, option, world_parameters.time_axis,
                world_parameters.f0);

            Core.StoneMask(x, x_length, world_parameters.fs, world_parameters.time_axis,
                world_parameters.f0, world_parameters.f0_length, refined_f0);

            for (var i = 0; i < world_parameters.f0_length; ++i)
                world_parameters.f0[i] = refined_f0[i];
        }

        public void F0EstimationHarvest(float[] x, int x_length)
        {
            var option = new HarvestOption();

            Core.InitializeHarvestOption(option);

            option.frame_period = world_parameters.frame_period;
            option.f0_floor = MinF0;
            option.f0_ceil = MaxF0;

            world_parameters.f0_length = Core.GetSamplesForDIO(world_parameters.fs,
                x_length, world_parameters.frame_period);
            world_parameters.f0 = new double[world_parameters.f0_length];
            world_parameters.time_axis = new double[world_parameters.f0_length];

            System.Console.WriteLine("Analysis");

            double[] x1 = x.Select(p => (double)p).ToArray();
            Core.Harvest(x1, x_length, world_parameters.fs, option,
                world_parameters.time_axis, world_parameters.f0);
            F0toPitch();
        }

        public void F0toPitch()
        {
            world_parameters.pitch = world_parameters.f0.Select(p => (
                69 + 12 * Math.Log2(p/440.0)
            )).ToArray();
        }
        public void PitchApplyToF0()
        {
            world_parameters.f0 = world_parameters.pitch.Select(p => (
                440.0 * Math.Pow(2, ( p - 69 )/12.0)
            )).ToArray();
        }
        public void GenderApplyToSP()
        {
            // 创建新的数组以存储插值结果
           // double[,] spRenderShifted = new double[world_parameters.spectrogram.GetLength(0), world_parameters.spectrogram.GetLength(1)];

            // 对每一帧进行插值
            for (int frame = 0; frame < world_parameters.spectrogram.GetLength(0); frame++)
            {
                // 计算性别调整因子
                double gender = Math.Pow(2, world_parameters.gender[frame] / 120);

                // 创建频率数组
                double[] freqX = Generate.LinearSpaced(world_parameters.spectrogram.GetLength(1), 0, 1);

                // 根据性别调整拉伸频谱包络
                double[] freqXClipped = new double[world_parameters.spectrogram.GetLength(1)];
                for (int i = 0; i < freqXClipped.Length; i++)
                {
                    freqXClipped[i] = Math.Clamp(i * gender / world_parameters.spectrogram.GetLength(1), 0, 1); // 限制值的范围
                }

                // 获取当前帧的频谱包络
                double[] currentFrame = new double[world_parameters.spectrogram.GetLength(1)];
                for (int bin = 0; bin < world_parameters.spectrogram.GetLength(1); bin++)
                {
                    currentFrame[bin] = world_parameters.spectrogram[frame, bin];
                }

                // 创建立方样条插值器
                var cubeSplineInterpolator = Interpolate.CubicSpline(freqX, currentFrame);

                // 对当前帧进行插值
                for (int bin = 0; bin < world_parameters.spectrogram.GetLength(1); bin++)
                {
                    world_parameters.spectrogram[frame, bin] = cubeSplineInterpolator.Interpolate(freqXClipped[bin]);
                }
            }
        }

        public void SpectralEnvelopeEstimation(float[] x, int x_length)
        {
            var option = new CheapTrickOption();

            Core.InitializeCheapTrickOption(world_parameters.fs, option);

            option.q1 = Q1;
            option.f0_floor = MinF0;

            world_parameters.fft_size = Core.GetFFTSizeForCheapTrick(world_parameters.fs, option);
            world_parameters.spectrogram = new double[world_parameters.f0_length, world_parameters.fft_size / 2 + 1];

            double[] x1 = x.Select(p => (double)p).ToArray();
            Core.CheapTrick(x1, x_length, world_parameters.fs, world_parameters.time_axis,
                world_parameters.f0, world_parameters.f0_length, option,
                world_parameters.spectrogram);

            world_parameters.gender = new double[world_parameters.f0_length];
        }

        public void AperiodicityEstimation(float[] x, int x_length)
        {
            var option = new D4COption();

            Core.InitializeD4COption(option);
            option.threshold = 0.85;

            world_parameters.aperiodicity = new double[world_parameters.f0_length, world_parameters.fft_size / 2 + 1];

            double[] x1 = x.Select(p => (double)p).ToArray();
            Core.D4C(x1, x_length, world_parameters.fs, world_parameters.time_axis,
                world_parameters.f0, world_parameters.f0_length,
                world_parameters.fft_size, option, world_parameters.aperiodicity);
        }

        public void WaveformSynthesis(int y_length, float[] y)
        {
            int fs = world_parameters.fs;
            double[] yx = new double[y_length];
            Core.Synthesis(world_parameters.f0, world_parameters.f0_length,
                world_parameters.spectrogram, world_parameters.aperiodicity,
                world_parameters.fft_size, world_parameters.frame_period, fs,
                y_length, yx);
            Parallel.For(0, y_length, (yi) => { y[yi] = (float)yx[yi]; });
        }

        public void WaveformSynthesis2(int y_length, float[] y)
        {
            int fs = world_parameters.fs;
            var synthesizer = new WorldSynthesizer();
            int buffer_size = 64;
            Core.InitializeSynthesizer(world_parameters.fs, world_parameters.frame_period,
                world_parameters.fft_size, buffer_size, 100, synthesizer);

            Core.AddParameters(world_parameters.f0, world_parameters.f0_length,
                world_parameters.spectrogram, world_parameters.aperiodicity,
                synthesizer);

            int index;
            var _buf = new double[buffer_size];

            for (var i = 0; Core.Synthesis2(synthesizer); ++i)
            {
                index = i * buffer_size;
                synthesizer.CopyFromBufferToArray(_buf);
                for (var j = 0; j < buffer_size; ++j)
                    y[j + index] = (float)_buf[j];
            }

            Core.DestroySynthesizer(synthesizer);
        }
    }
}
