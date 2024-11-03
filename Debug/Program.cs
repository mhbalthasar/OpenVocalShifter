
using DotnetWorld;
using NAudio.Wave;
using MathNet.Numerics;
using MathNet.Numerics.Interpolation;
using OpenVoiceShifter;
using System.Reflection;

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
    static void Main(string[] args)
    {
        WordInitialize.Initialize();

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

            for (int i = 0; i < world.WorldArgs.gender.Length; i++) world.WorldArgs.gender[i] = -100;
            //world.GenderApplyToSP();

            int y_length = world.WorldArgs.synthesisedLength+1;
            float[] y = new float[y_length];
            for (var i = 0; i < y.Length; i++)
                y[i] = 0.0f;


            /*
            if (breath <= 1 && breath>=-1) // 提升功率以平滑较小区域并保持最大非周期性
            {
                Console.WriteLine("正在降低呼吸感。");
                double vbreath = (breath+1) * 50.0 / 100.0;
                world.WorldArgs.aperiodicity = Bias(world.WorldArgs.aperiodicity, vbreath);

                for (int i = 0; i < husk.Length; i++)
                {
                    if (Math.Abs(husk[i] - 1) < 1e-10) // 确保无声区域保持无声
                    {
                        for (int j = 0; j < world.WorldArgs.aperiodicity.GetLength(1); j++)
                        {
                            world.WorldArgs.aperiodicity[i, j] = 1;
                        }
                    }
                }
            }*/


            world.WaveformSynthesis(y_length, y);


            // 保存处理后的音频
            using (var writer = new WaveFileWriter(outputFilePath, reader.WaveFormat))
            {
                writer.WriteSamples(y, 0, y_length);
            }
        }

        Console.WriteLine("处理完成，已生成增强版咆哮效果的音频文件：" + outputFilePath);
    }
