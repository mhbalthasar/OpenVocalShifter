
using DotnetWorld;
using NAudio.Wave;
using MathNet.Numerics;
using MathNet.Numerics.Interpolation;
using OpenVoiceShifter;
using System.Reflection;
using MathNet.Numerics.IntegralTransforms;
using System.Numerics;

class Program
{
    public static double[,] Bias(double[,] x, double a)
    {
        int rows = x.GetLength(0);
        int cols = x.GetLength(1);
        double[,] result = new double[rows, cols];

        if (a == 0)
            return result; // 返回零矩阵
        if (a == 1)
        {
            for (int i = 0; i < rows; i++)
                for (int j = 0; j < cols; j++)
                    result[i, j] = 1; // 返回全1矩阵
            return result;
        }

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                result[i, j] = x[i, j] / ((1 / a - 2) * (1 - x[i, j]) + 1);
            }
        }
        return result;
    }
    static void Main1(string[] args)
    {
        WorldInitialize.Initialize();

        string inputFilePath = "input.wav"; // 输入文件路径
        string outputFilePath = "output_growl.wav"; // 输出文件路径
        double growlPower = 0.3; // 增强的咆哮效果

        // 读取 WAV 文件
        using (var reader = new AudioFileReader(inputFilePath))
        {
            int sampleRate = reader.WaveFormat.SampleRate;
            int channels = reader.WaveFormat.Channels;

            // 读取样本数据
            var sampleProvider = reader.ToSampleProvider();
            float[] samples = new float[reader.WaveFormat.SampleRate * reader.WaveFormat.Channels * (int)reader.TotalTime.TotalSeconds];
            int sampleCount = sampleProvider.Read(samples, 0, samples.Length);

            var world = new WorldSample(sampleRate, 5.0);//5ms per ControlPoint

            world.F0EstimationHarvest(samples, samples.Length);
            //parameters.pitchNumbers = parameters.pitchNumbers.Select(p => p + 2).ToArray();
            world.PitchApplyToF0();

            world.SpectralEnvelopeEstimation(samples, samples.Length);
            world.AperiodicityEstimation(samples, samples.Length);

            List<double> FindFormants(double[,] sp, int frame, double fs, int fftSize)
            {
                List<double> formants = new List<double>();
                List<double> amp_formants = new List<double>();
                double[] amplitudes = new double[fftSize / 2 + 1];

                // 提取幅度谱
                for (int i = 0; i <= fftSize / 2; i++)
                {
                    amplitudes[i] = sp[frame, i]; // 假设 sp 已经是幅度谱
                }

                // 计算对数幅度谱
                for (int i = 0; i < amplitudes.Length; i++)
                {
                    amplitudes[i] = Math.Log(amplitudes[i] + 1e-10); // 防止对数负无穷
                }

                // 计算反傅里叶变换（即倒谱）
                double[] cepstrum = new double[amplitudes.Length];
                Fourier.Forward(amplitudes, cepstrum, FourierOptions.Default);

                // 查找倒谱中的峰值
                for (int i = 2; i < cepstrum.Length - 2; i++)
                {
                    bool isPeak = cepstrum[i] > cepstrum[i - 1] && cepstrum[i] > cepstrum[i + 1] &&
                                  cepstrum[i - 1] > cepstrum[i - 2] && cepstrum[i + 1] > cepstrum[i + 2];
                    if(isPeak)
                    {
                        // 找到峰值，映射到频率
                        double frequency = (i * fs) / fftSize;
                        amp_formants.Add(Math.Abs(amplitudes[i]));
                        formants.Add(frequency);
                    }
                }
                
                // 只取前几个共振峰
                formants.Sort();
                return formants;
            }
            
            var fnts = FindFormants(world.WorldArgs.spectrogram, 82, world.WorldArgs.fs, world.WorldArgs.fft_size);


            for (int i = 0; i < world.WorldArgs.gender.Length; i++)
            {
                world.WorldArgs.f1shifter[i] = 0.5;// WorldSample.CalculateFormantsShifter(807, 350);//// 117;
                world.WorldArgs.f2shifter[i] = 0;// WorldSample.CalculateFormantsShifter(1227, 2500);
                world.WorldArgs.f3shifter[i] = 0;// WorldSample.CalculateFormantsShifter(2340, 3600);
                world.WorldArgs.f4shifter[i] = 0;
            }
            //world.GenderApplyToSP();
            world.FormantsApplyToSP();

            var fnts2 = FindFormants(world.WorldArgs.spectrogram, 82, world.WorldArgs.fs, world.WorldArgs.fft_size);

            int y_length = world.WorldArgs.synthesisedLength + 1;
            float[] y = new float[y_length];
            for (var i = 0; i < y.Length; i++)
                y[i] = 0.0f;

            world.WaveformSynthesis(y_length, y);


            // 保存处理后的音频
            using (var writer = new WaveFileWriter(outputFilePath, reader.WaveFormat))
            {
                writer.WriteSamples(y, 0, y_length);
            }
        }

        Console.WriteLine("处理完成，已生成增强版咆哮效果的音频文件：" + outputFilePath);
    }
 }
