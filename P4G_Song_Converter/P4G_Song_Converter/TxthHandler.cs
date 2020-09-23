using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace P4G_Song_Converter
{
    class TxthHandler
    {
        public bool WriteTxthFile(string outputFilePath, Program.WaveProps wav, long numSamples, long startSample, long endSample)
        {
            // get samples per block from first byte of extra params
            byte samplesPerBlock = wav.ExtraParams[0];

            // build txth
            StringBuilder txthBuilder = new StringBuilder();
            txthBuilder.AppendLine($"num_samples = {numSamples}");
            txthBuilder.AppendLine("codec = MSADPCM");
            txthBuilder.AppendLine($"channels = {wav.NumChannels}");
            txthBuilder.AppendLine($"sample_rate = {wav.SampleRate}");
            txthBuilder.AppendLine($"interleave = {wav.BlockAlign}");
            txthBuilder.AppendLine($"samples_per_block = {wav.ExtraParams[0]}");

            // set txth loop points
            if (startSample == 0 && endSample == 0)
            {
                // no loop points given, set loop points to song length
                txthBuilder.AppendLine("loop_start_sample = 0");
                txthBuilder.AppendLine($"loop_end_sample = {AlignToBlock(numSamples, samplesPerBlock)}");
            }
            else
            {
                long finalStartSample = AlignToBlock(startSample, samplesPerBlock);
                long finalEndSample = AlignToBlock(endSample, samplesPerBlock);

                // verify loop points are valid
                if (!isValidLoop(numSamples, finalStartSample, finalEndSample))
                {
                    Console.WriteLine("Defaulting to full song loop!");
                    finalStartSample = 0;
                    finalEndSample = AlignToBlock(numSamples, samplesPerBlock);
                }

                // add loop points given
                txthBuilder.AppendLine($"loop_start_sample = {finalStartSample}");
                txthBuilder.AppendLine($"loop_end_sample = {finalEndSample}");
            }

            // write txth to file
            try
            {
                File.WriteAllText(outputFilePath, txthBuilder.ToString());
                Console.WriteLine("Created txth file.");
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("Problem writing txth to file!");
                Console.WriteLine(e);
                return false;
            }
        }

        public bool UpdateTxthFile(string outputFilePath, long startSample, long endSample)
        {
            // exit early if original txth is missing (shouldn't happen but oh well)
            if (!File.Exists(outputFilePath))
            {
                Console.WriteLine($"Expected txth file missing! File: {outputFilePath}");
                return false;
            }

            try
            {
                string[] originalTxthFile = File.ReadAllLines(outputFilePath);
                StringBuilder txthBuilder = new StringBuilder();

                long numSamples = long.Parse(Array.Find<string>(originalTxthFile, s => s.StartsWith("num_samples")).Split(" = ")[1]);
                byte samplesPerBlock = byte.Parse(Array.Find<string>(originalTxthFile, s => s.StartsWith("samples_per_block")).Split(" = ")[1]);
                long finalStartSample = AlignToBlock(startSample, samplesPerBlock);
                long finalEndSample = AlignToBlock(endSample, samplesPerBlock);

                // verify loop points are valid
                if (!isValidLoop(numSamples, finalStartSample, finalEndSample))
                {
                    Console.WriteLine("Defaulting to full song loop!");
                    finalStartSample = 0;
                    finalEndSample = AlignToBlock(numSamples, samplesPerBlock);
                }

                foreach (string line in originalTxthFile)
                {
                    if (line.StartsWith("loop_start_sample"))
                        txthBuilder.AppendLine($"loop_start_sample = {finalStartSample}");
                    else if (line.StartsWith("loop_end_sample"))
                        txthBuilder.AppendLine($"loop_end_sample = {finalEndSample}");
                    else
                        txthBuilder.AppendLine(line);
                }

                File.WriteAllText(outputFilePath, txthBuilder.ToString());
            }
            catch (Exception e)
            {
                Console.WriteLine("Problem updating txth file!");
                Console.WriteLine(e);
                return false;
            }

            return true;
        }

        private bool isValidLoop(long totalSamples, long startSample, long endSample)
        {
            if (startSample > totalSamples)
            {
                Console.WriteLine($"Loop start sample exceeds total samples: {startSample} > {totalSamples}");
                return false;
            }
            else if (endSample > totalSamples)
            {
                Console.WriteLine($"Loop end sample exceeds total samples: {endSample} > {totalSamples}");
                return false;
            }
            else
            {
                return true;
            }
        }

        private long AlignToBlock(long sample, byte perBlock)
        {
            // check if sample given aligns
            if (sample % perBlock != 0)
            {
                // align sample to block
                byte adjustment = (byte)(sample % perBlock);
                Console.WriteLine($"Aligning: {sample} to {sample - adjustment} (-{adjustment})");
                return sample - adjustment;
            }
            else
            {
                return sample;
            }
        }
    }
}
