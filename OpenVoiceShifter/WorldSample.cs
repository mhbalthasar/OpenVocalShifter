﻿using DotnetWorld.API.Structs;
using DotnetWorld.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;

namespace OpenVoiceShifter
{
    public class WorldFunctionSwitcher
    {
        public string Option_CacheFilePath { get; set; } = "";
        public bool Enable_PitchChange { get; set; } = true;
        public bool Enable_GenderChange { get; set; } = true;
        public bool Enable_FormantChange { get; set; } = true;
    }
    public class WorldFormantBands
    {
        public int f1_Freq = 625;
        public int f1_bandWidth = 750; //300-1000

        public int f2_Freq = 1750;
        public int f2_bandWidth = 1500; //1000-2500

        public int f3_Freq = 3000;
        public int f3_bandWidth = 1000; //2500-3500

        public int f4_Freq = 4000;
        public int f4_bandWidth = 1000; //3500-4500
        public int[] f1_bands => [f1_Freq - f1_bandWidth / 2, f1_Freq + f1_bandWidth / 2];
        public int[] f2_bands => [f2_Freq - f2_bandWidth / 2, f2_Freq + f2_bandWidth / 2];
        public int[] f3_bands => [f3_Freq - f3_bandWidth / 2, f3_Freq + f3_bandWidth / 2];
        public int[] f4_bands => [f4_Freq - f4_bandWidth / 2, f4_Freq + f4_bandWidth / 2];
    }
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
        
        public double[][] sp_freq;
        public WorldFormantBands[] formants_bands;
        public double[] f1shifter;//300-1000
        public double[] f2shifter;//1000-2500
        public double[] f3shifter;//2500-3200
        public double[] f4shifter;//3200-4500
        
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

        WorldFunctionSwitcher world_functionswitcher = new WorldFunctionSwitcher();
        public WorldParameters WorldArgs { get => world_parameters; set => world_parameters = value; }

        private double MaxF0 { get; set; } = 800;
        private double MinF0 { get; set; } = 71;
        private double Q1 { get; set; } = -0.15;
        public WorldFunctionSwitcher FunctionSwitcher { get => world_functionswitcher; set => world_functionswitcher = value; }

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

/*        public void F0EstimationDio(double[] x, int x_length)
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
        }*/

        public void F0EstimationHarvest(float[] x, int x_length)
        {
            string f0cache = "";
            if (FunctionSwitcher.Option_CacheFilePath.Length > 3) f0cache = FunctionSwitcher.Option_CacheFilePath + ".f0";
            bool cached = false;
            if (File.Exists(f0cache))
            {
                int f0_len=(int)Tools.GetHeaderInformation(f0cache, "NOF ");
                double[] f0 = new double[f0_len];
                double[] temporal_positions = new double[f0_len];
                if(f0_len>1 && Tools.ReadF0(f0cache, temporal_positions, f0) == 1)
                {
                    world_parameters.f0_length = f0_len;
                    world_parameters.f0 = f0;
                    world_parameters.time_axis = temporal_positions;
                    cached = true;
                }
            }
            if (!cached)
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

                if (f0cache.Length > 6)
                {
                    int text_flag = 0;
                    Tools.WriteF0(f0cache, world_parameters.f0_length, world_parameters.frame_period,
                        world_parameters.time_axis, world_parameters.f0,
                        text_flag);
                }
            }
            if(FunctionSwitcher.Enable_PitchChange)F0toPitch();
            else world_parameters.pitch = new double[world_parameters.f0_length];
        }

        public void F0toPitch()
        {
            world_parameters.pitch = world_parameters.f0.Select(p => (
                69 + 12 * Math.Log2(p/440.0)
            )).ToArray();
        }
        public void PitchApplyToF0()
        {
            if (!FunctionSwitcher.Enable_PitchChange) return;
            world_parameters.f0 = world_parameters.pitch.Select(p => (
                440.0 * Math.Pow(2, ( p - 69 )/12.0)
            )).ToArray();
        }
        public void GenderApplyToSP()
        {
            if (!FunctionSwitcher.Enable_GenderChange) return;
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
        public void FormantsApplyToSP()
        {
            if (!FunctionSwitcher.Enable_FormantChange) return;

            // 对每一帧进行插值
            for (int frame = 0; frame < world_parameters.spectrogram.GetLength(0); frame++)
            {
                var fmtBands = world_parameters.formants_bands[frame];

                double f1_index0 = world_parameters.sp_freq.Where(p => p[1] >= fmtBands.f1_bands[0]).First()[0];
                double f1_index1 = world_parameters.sp_freq.Where(p => p[1] >= fmtBands.f1_bands[1]).First()[0];
                double f2_index0 = world_parameters.sp_freq.Where(p => p[1] >= fmtBands.f2_bands[0]).First()[0];
                double f2_index1 = world_parameters.sp_freq.Where(p => p[1] >= fmtBands.f2_bands[1]).First()[0];
                double f3_index0 = world_parameters.sp_freq.Where(p => p[1] >= fmtBands.f3_bands[0]).First()[0];
                double f3_index1 = world_parameters.sp_freq.Where(p => p[1] >= fmtBands.f3_bands[1]).First()[0];
                double f4_index0 = world_parameters.sp_freq.Where(p => p[1] >= fmtBands.f4_bands[0]).First()[0];
                double f4_index1 = world_parameters.sp_freq.Where(p => p[1] >= fmtBands.f4_bands[1]).First()[0];

                // 计算性别调整因子
                double f1 = Math.Pow(2, world_parameters.f1shifter[frame] / 100);
                double f2 = Math.Pow(2, world_parameters.f2shifter[frame] / 100);
                double f3 = Math.Pow(2, world_parameters.f3shifter[frame] / 100);
                double f4 = Math.Pow(2, world_parameters.f4shifter[frame] / 100);

                // 创建频率数组
                double[] freq1X = Generate.LinearSpaced(world_parameters.spectrogram.GetLength(1), 0, 1);

                // 根据性别调整拉伸频谱包络
                double[] freq1Clipped = new double[world_parameters.spectrogram.GetLength(1)];
                double[] freq2Clipped = new double[world_parameters.spectrogram.GetLength(1)];
                double[] freq3Clipped = new double[world_parameters.spectrogram.GetLength(1)];
                double[] freq4Clipped = new double[world_parameters.spectrogram.GetLength(1)];
                for (int i = 0; i < freq1Clipped.Length; i++)
                {
                      freq1Clipped[i] = Math.Clamp(i * f1 / world_parameters.spectrogram.GetLength(1), 0, 1); // 限制值的范围
                      freq2Clipped[i] = Math.Clamp(i * f2 / world_parameters.spectrogram.GetLength(1), 0, 1); // 限制值的范围
                      freq3Clipped[i] = Math.Clamp(i * f3 / world_parameters.spectrogram.GetLength(1), 0, 1); // 限制值的范围
                      freq4Clipped[i] = Math.Clamp(i * f4 / world_parameters.spectrogram.GetLength(1), 0, 1); // 限制值的范围
                }

                // 获取当前帧的频谱包络
                double[] currentFrame = new double[world_parameters.spectrogram.GetLength(1)];
                for (int bin = 0; bin < world_parameters.spectrogram.GetLength(1); bin++)
                {
                    currentFrame[bin] = world_parameters.spectrogram[frame, bin];
                }

                // 创建立方样条插值器
                var Interpolator = Interpolate.CubicSpline(freq1X, currentFrame);

                // 对当前帧进行插值
                for (int bin = 0; bin < world_parameters.spectrogram.GetLength(1); bin++)
                {
                    if (bin >= f1_index0 && bin < f1_index1) if (world_parameters.f1shifter[frame] != 0) world_parameters.spectrogram[frame, bin] = Interpolator.Interpolate(freq1Clipped[bin]);
                    if (bin >= f2_index0 && bin < f2_index1) if (world_parameters.f2shifter[frame] != 0) world_parameters.spectrogram[frame, bin] = Interpolator.Interpolate(freq2Clipped[bin]);
                    if (bin >= f3_index0 && bin < f3_index1) if (world_parameters.f3shifter[frame] != 0) world_parameters.spectrogram[frame, bin] = Interpolator.Interpolate(freq3Clipped[bin]);
                    if (bin >= f4_index0 && bin < f4_index1) if (world_parameters.f4shifter[frame] != 0) world_parameters.spectrogram[frame, bin] = Interpolator.Interpolate(freq4Clipped[bin]);
                }

                List<int[]> ConvertToGroups(double[] bIndex, double bjVal = 5)
                {
                    List<int[]> result = new List<int[]>();
                    List<int> currentGroup = new List<int> { 0 }; // 初始化第一个元素的下标

                    for (int i = 1; i < bIndex.Length; i++)
                    {
                        if (bIndex[i] == bIndex[i - 1]) { }
                        else if (bIndex[i] - bIndex[i - 1] < bjVal)
                        {
                            currentGroup.Add(i); // 如果相邻差值小于 5，加入当前组
                        }
                        else
                        {
                            if (currentGroup.Count > 0) result.Add(currentGroup.ToArray()); // 否则结束当前组并创建新组
                            currentGroup = new List<int> { i };
                        }
                    }

                    if (currentGroup.Count > 0) result.Add(currentGroup.OrderBy(p => p).ToArray()); // 添加最后一个组
                    return result;
                }
                double[] bIndex = [f1_index0, f1_index1, f2_index0, f2_index1, f3_index0, f3_index1, f4_index0, f4_index1];
                bIndex = bIndex.OrderBy(p => p).ToArray();
                var cIndex = ConvertToGroups(bIndex,6);
                //Smooth
                for (int i = 0; i < cIndex.Count; i++)
                {
                    int margin = 2;
                    int[] bndIdx = cIndex[i];
                    int sBin = (int)bIndex[bndIdx.First()] - margin;
                    int eBin = (int)bIndex[bndIdx.Last()] + margin;
                    int stepBin = eBin - sBin;
                    double hBin = world_parameters.spectrogram[frame, eBin] - world_parameters.spectrogram[frame, sBin];
                    double phBin = hBin / stepBin;
                    for(int j = 0; j < stepBin; j++)
                    {
                        int kBin = sBin + j;
                        world_parameters.spectrogram[frame, kBin] = world_parameters.spectrogram[frame, sBin] + phBin * j;
                    }
                }
            }
        }

        public void SpectralEnvelopeEstimation(float[] x, int x_length)
        {
            string spcache = "";
            if (FunctionSwitcher.Option_CacheFilePath.Length > 3) spcache = FunctionSwitcher.Option_CacheFilePath + ".sp";
            bool cached = false;
            if (File.Exists(spcache))
            {
                int fft_size = (int)Tools.GetHeaderInformation(spcache, "FFT ");
                double[,] sp = new double[world_parameters.f0_length, fft_size / 2 + 1];
                if (fft_size>256 && Tools.ReadSpectralEnvelope(spcache,sp) == 1)
                {
                    world_parameters.fft_size = fft_size;
                    world_parameters.spectrogram = sp;
                    cached = true;
                }
            }
            if (!cached)
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

                if (spcache.Length > 6)
                {
                    Tools.WriteSpectralEnvelope(spcache, world_parameters.fs, world_parameters.f0_length, world_parameters.frame_period,
                        world_parameters.fft_size, 0, world_parameters.spectrogram);
                }
            }

            world_parameters.sp_freq = new double[world_parameters.fft_size / 2 + 1][];

            for (int n = 0; n <= world_parameters.fft_size / 2; n++)
            {
                world_parameters.sp_freq[n] = [n, (double)n * world_parameters.fs / world_parameters.fft_size]; // 计算频率
            }

            world_parameters.gender = new double[world_parameters.f0_length];

            world_parameters.f1shifter = new double[world_parameters.f0_length];
            world_parameters.f2shifter = new double[world_parameters.f0_length];
            world_parameters.f3shifter = new double[world_parameters.f0_length];
            world_parameters.f4shifter = new double[world_parameters.f0_length];

            world_parameters.formants_bands=Enumerable.Repeat(new WorldFormantBands() ,world_parameters.f0_length).ToArray();
        }

        public void AperiodicityEstimation(float[] x, int x_length)
        {
            string apcache = "";
            if (FunctionSwitcher.Option_CacheFilePath.Length > 3) apcache = FunctionSwitcher.Option_CacheFilePath + ".ap";
            bool cached = false;
            if (File.Exists(apcache))
            {
                int fft_size = (int)Tools.GetHeaderInformation(apcache, "FFT ");
                double[,] ap = new double[world_parameters.f0_length, fft_size / 2 + 1];
                if (fft_size>256 && Tools.ReadAperiodicity(apcache,ap) == 1)
                {
                    world_parameters.fft_size = fft_size;
                    world_parameters.aperiodicity = ap;
                    cached = true;
                }
            }
            if (!cached)
            {
                var option = new D4COption();

                Core.InitializeD4COption(option);
                option.threshold = 0.85;

                world_parameters.aperiodicity = new double[world_parameters.f0_length, world_parameters.fft_size / 2 + 1];

                double[] x1 = x.Select(p => (double)p).ToArray();
                Core.D4C(x1, x_length, world_parameters.fs, world_parameters.time_axis,
                    world_parameters.f0, world_parameters.f0_length,
                    world_parameters.fft_size, option, world_parameters.aperiodicity);

                if (apcache.Length > 6) {
                    Tools.WriteAperiodicity(apcache, world_parameters.fs, world_parameters.f0_length, world_parameters.frame_period,
                        world_parameters.fft_size, 0, world_parameters.aperiodicity);
                }
            }
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
