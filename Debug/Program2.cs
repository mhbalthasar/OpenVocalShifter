using System;
using System.Linq;
using NAudio.Wave;
using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;

class Program2
{
    static void Main2()
    {
        string inputPath = "input.wav";
        string outputPath = "output.wav";
        
        // Load the audio file
        using (var reader = new AudioFileReader(inputPath))
        {
            int sampleRate = reader.WaveFormat.SampleRate;
            int channels = reader.WaveFormat.Channels;
            float[] buffer = new float[reader.Length / sizeof(float)];
            reader.Read(buffer, 0, buffer.Length);
            
            // Convert the audio signal to mono (if necessary)
            float[] monoSignal = ConvertToMono(buffer, channels);

            int stepMs = 5; //220sample=5ms
            // Block size and overlap for windowing
            int blockSize = (int)(sampleRate * (double)(2.0 * stepMs) /1000.0);
            int overlap = blockSize / 2;

            // Output buffer for processed signal
            float[] outputSignal = new float[monoSignal.Length];

            // Process each block with windowing, FFT, frequency shift, and inverse FFT
            for (int start = 0; start < monoSignal.Length; start += overlap)
            {
                int blockLength = Math.Min(blockSize, monoSignal.Length - start);
                float[] block = new float[blockLength];
                Array.Copy(monoSignal, start, block, 0, blockLength);

                // Apply Hann window to each block
                ApplyWindowFunction(block);

                // Perform FFT
                Complex32[] complexBlock = block.Select(x => new Complex32(x, 0)).ToArray();
                Fourier.Forward(complexBlock, FourierOptions.Matlab);

                // Apply frequency shifts with smoothing
                ApplyFrequencyShiftsWithSmoothing(complexBlock, sampleRate);

                // Inverse FFT to return to time domain
                Fourier.Inverse(complexBlock, FourierOptions.Matlab);
                float[] modifiedBlock = complexBlock.Select(x => x.Real).ToArray();

                // Overlap-add method to avoid windowing gaps
                for (int i = 0; i < blockLength; i++)
                {
                    if (start + i < outputSignal.Length)
                        outputSignal[start + i] += modifiedBlock[i];
                }
            }

            // Save the modified audio signal
            SaveToWav(outputSignal, sampleRate, channels, outputPath);
        }
    }

    static float[] ConvertToMono(float[] buffer, int channels)
    {
        if (channels == 1) return buffer;
        
        float[] monoSignal = new float[buffer.Length / channels];
        for (int i = 0; i < monoSignal.Length; i++)
        {
            monoSignal[i] = 0;
            for (int ch = 0; ch < channels; ch++)
            {
                monoSignal[i] += buffer[i * channels + ch];
            }
            monoSignal[i] /= channels;
        }
        return monoSignal;
    }

    static void ApplyWindowFunction(float[] block)
    {
        int n = block.Length;
        for (int i = 0; i < n; i++)
        {
            // Applying Hann window to the block
            block[i] *= (float)(0.5 * (1 - Math.Cos(2 * Math.PI * i / (n - 1))));
        }
    }

    static void ApplyFrequencyShiftsWithSmoothing(Complex32[] spectrum, int sampleRate)
    {
        int totalBins = spectrum.Length;
        double binSize = sampleRate / (double)totalBins;
        
        for (int i = 0; i < totalBins / 2; i++)
        {
            // Frequency shift with smoothing
            SmoothAndShiftFrequency(spectrum, i, 300 , 1000 , -200, binSize, totalBins);
            SmoothAndShiftFrequency(spectrum, i, 1000, 2500, 500, binSize, totalBins);
         //   SmoothAndShiftFrequency(spectrum, i, 2500, 3500, 0, binSize, totalBins);
          //  SmoothAndShiftFrequency(spectrum, i, 3500, 4500, 0, binSize, totalBins);
        }
    }

    static void SmoothAndShiftFrequency(Complex32[] spectrum, int index,double minFreq,double maxFreq, double shift, double binSize, int totalBins)
    {
        int minBins = (int)(minFreq / binSize);
        int maxBins = (int)(maxFreq / binSize);
        int shiftBins = (int)(shift / binSize);
        int targetIndex = index + shiftBins;

        if (shiftBins == 0) return;
        if (index <minBins || index>=maxBins) return;

        if (targetIndex >= 0 && targetIndex < totalBins / 2)
        {
            var tgtIdx1 = targetIndex;
            while (tgtIdx1 > maxBins) tgtIdx1 = tgtIdx1 + minBins - maxBins;
            while (tgtIdx1 < minBins) tgtIdx1 = tgtIdx1 - minBins + maxBins;
            var srcIdx1 = index;
            var tgtIdx2 = totalBins - tgtIdx1 - 1;
            var srcIdx2 = totalBins - srcIdx1 - 1;

            // Apply a simple average-based smoothing
            spectrum[tgtIdx1] = spectrum[srcIdx1];// (spectrum[srcIdx1] + spectrum[tgtIdx1]) / 2;
          //  spectrum[tgtIdx2] = spectrum[srcIdx2];// (spectrum[srcIdx2] + spectrum[tgtIdx2]) / 2;

            // Apply phase correction to maintain harmonic consistency
            spectrum[tgtIdx1] = Complex32.FromPolarCoordinates(spectrum[tgtIdx1].Magnitude, spectrum[srcIdx1].Phase);
          //  spectrum[tgtIdx2] = Complex32.FromPolarCoordinates(spectrum[tgtIdx2].Magnitude, spectrum[srcIdx2].Phase);

            // Zero out the original position for a smooth transition
            spectrum[srcIdx1] = new Complex32(0, 0);
          //  spectrum[srcIdx2] = new Complex32(0, 0);
        }
    }

    static void SaveToWav(float[] buffer, int sampleRate, int channels, string outputPath)
    {
        using (var writer = new WaveFileWriter(outputPath, new WaveFormat(sampleRate, channels)))
        {
            writer.WriteSamples(buffer, 0, buffer.Length);
        }
    }
}
