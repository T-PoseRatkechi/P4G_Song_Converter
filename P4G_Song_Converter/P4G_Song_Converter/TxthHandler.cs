using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace P4G_Song_Converter
{
    class TxthHandler
    {
        public void WriteTxthFile(string outputFilePath, Program.WaveProps wav, long numSamples, long startSample, long endSample)
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

            // set txth loop points
            if (startSample == 0 && endSample == 0)
            {
                // no loop points given, set loop points to song length
                txthBuilder.AppendLine("loop_start_sample = 0");
                txthBuilder.AppendLine($"loop_end_sample = {AlignToBlock(numSamples, samplesPerBlock)}");
            }
            else
            {
                // add loop points given
                txthBuilder.AppendLine($"loop_start_sample = {AlignToBlock(startSample, samplesPerBlock)}");
                txthBuilder.AppendLine($"loop_end_sample = {AlignToBlock(endSample, samplesPerBlock)}");
            }

            // write txth to file
            try
            {
                File.WriteAllText(outputFilePath, txthBuilder.ToString());
            }
            catch (Exception e)
            {
                Console.WriteLine("Problem writing txth to file!");
                Console.WriteLine(e);
                return;
            }
        }

        public bool UpdateTxthFile(string outputFilePath, long loopStart, long loopEnd)
        {
            // exit early if original txth is missing (shouldn't happen but oh well)
            if (!File.Exists(outputFilePath))
            {
                Console.WriteLine($"TXTH file missing! File: {outputFilePath}");
                return false;
            }

            try
            {
                string[] originalTxthFile = File.ReadAllLines(outputFilePath);
                StringBuilder txthBuilder = new StringBuilder();

                foreach (string line in originalTxthFile)
                {
                    if (line.StartsWith("loop_start_sample"))
                        txthBuilder.AppendLine($"loop_start_sample = {loopStart}");
                    else if (line.StartsWith("loop_end_sample"))
                        txthBuilder.AppendLine($"loop_end_sample = {loopEnd}");
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
