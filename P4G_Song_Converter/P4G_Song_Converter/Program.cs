using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace P4G_Song_Converter
{
    class Program
    {
        public struct WaveProps
        {
            public string ChunkID { get; set; }
            public int ChunkSize { get; set; }
            public string Format { get; set; }
            public string Subchunk1ID { get; set; }
            public int Subchunk1Size { get; set; }
            public short AudioFormat { get; set; }
            public short NumChannels { get; set; }
            public int SampleRate { get; set; }
            public int ByteRate { get; set; }
            public short BlockAlign { get; set; }
            public short BitsPerSample { get; set; }
            public short ExtraParamsSize { get; set; }
            public byte[] ExtraParams { get; set; }
            public string Subchunk2ID { get; set; }
            public int Subchunk2Size { get; set; }
            public long DataOffset { get; set; }
        }

        private static string currentDir = String.Empty;
        private static string encoderPath = String.Empty;
        private static string checksumsFolderPath = String.Empty;
        private static bool displayInfo = false;
        private static ChecksumUtils checksum = new ChecksumUtils();
        private static TxthHandler txthHandler = new TxthHandler();

        static int Main(string[] args)
        {
            if (!Init())
            {
                return 1;
            }

            if (args.Length < 2)
            {
                Console.WriteLine("Missing args! Usage: <inputfile>.wav <outputfile>.raw <loop start sample> <loop end sample>");
                return 2;
            }

            string inputFile = Path.GetFullPath(args[0]);
            string outputFile = Path.GetFullPath(args[1]);
            long startloopSample = 0;
            long endloopSample = 0;

            if (args.Length >= 4)
            {
                try
                {
                    // parse samples
                    startloopSample = long.Parse(args[2]);
                    endloopSample = long.Parse(args[3]);

                    if (startloopSample < 0 || endloopSample < 0)
                    {
                        Console.WriteLine("Loop points must be a non-negative integer!");
                        return 2;
                    }
                }
                catch (OverflowException e)
                {
                    Console.WriteLine("A loop point was too large to be parsed! Defaulting to full song loop!");
                }
                catch (Exception e)
                {
                    Console.WriteLine("Problem parsing loop points!");
                    Console.WriteLine(e);
                    return 2;
                }
            }

            Console.WriteLine($"Input: {inputFile}\nOutput: {outputFile}\nLoop Start Sample: {startloopSample}\nLoop End Sample: {endloopSample}");

            bool success = EncodeWave(inputFile, outputFile, startloopSample, endloopSample);

            if (!success)
            {
                Console.WriteLine("Failed to encode wave!");
                return 3;
            }

            Console.WriteLine("Wave to raw+txth success!");
            return 0;
        }

        private static bool Init()
        {
            currentDir = Directory.GetCurrentDirectory();
            encoderPath = $@"{currentDir}\xacttool_0.1\tools\AdpcmEncode.exe";
            checksumsFolderPath = $@"{currentDir}\wave_checksums";

            if (!File.Exists(encoderPath))
            {
                Console.WriteLine($"AdpcmEncode.exe could not be found!\nMissing: {encoderPath}");
                return false;
            }

            try
            {
                Directory.CreateDirectory(checksumsFolderPath);
            }
            catch (Exception e)
            {
                Console.WriteLine("Problem creating checksums directory!");
                return false;
            }

            return true;
        }

        // encode wav to raw + txth
        private static bool EncodeWave(string inputFilePath, string outputFilePath, long startSample, long endSample)
        {
            // check if input file should be re-encoded
            bool waveRequiresEncoding = RequiresEncoding(inputFilePath, outputFilePath);

            // only update txth file if wave doesn't need to be encoded
            if (!waveRequiresEncoding)
            {
                Console.WriteLine("Updating txth file!");
                // store result of txth updated
                bool txthUpdated = txthHandler.UpdateTxthFile($"{outputFilePath}.txth", startSample, endSample);
                return txthUpdated;
            }

            // file path to store temp encoded file (still has header)
            string tempFilePath = $@"{outputFilePath}.temp";

            ProcessStartInfo encodeInfo = new ProcessStartInfo
            {
                FileName = encoderPath,
                Arguments = $@"""{inputFilePath}"" ""{tempFilePath}""",
            };

            // encode file given
            try
            {
                Process process = Process.Start(encodeInfo);
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    Console.WriteLine($"Problem with AdpcmEncode! Exit code: {process.ExitCode}");
                    return false;
                }
            }
            catch (Exception e)
            {
                // problem starting process, exit early
                Console.WriteLine("Problem running AdpcmEncode!");
                Console.WriteLine(e);
                return false;
            }

            // get wave props of input file
            WaveProps inputWaveProps = GetWaveProps(inputFilePath);
            // get num samples from input wave
            int numSamples = GetNumSamples(inputWaveProps);
            if (numSamples <= 0)
            {
                Console.WriteLine("Could not determine number of samples from input wave!");
                return false;
            }

            // get wave props of temp file
            WaveProps outputWaveProps = GetWaveProps(tempFilePath);

            // array to store data chunk bytes
            byte[] outDataChunk = new byte[outputWaveProps.Subchunk2Size];

            try
            {
                // read data chunk into array
                using (FileStream tempfile = File.OpenRead(tempFilePath))
                {
                    tempfile.Seek(outputWaveProps.DataOffset, SeekOrigin.Begin);
                    tempfile.Read(outDataChunk, 0, outDataChunk.Length);
                }
            }
            catch (Exception e)
            {
                // exit early if error reading data chunk
                Console.WriteLine($"Problem reading in data chunk of output!");
                Console.WriteLine(e);
                return false;
            }

            // write txth file
            bool txthSuccess = txthHandler.WriteTxthFile($"{outputFilePath}.txth", outputWaveProps, numSamples, startSample, endSample);
            if (!txthSuccess)
                return false;

            // write raw to file
            try
            {
                // write raw file
                File.WriteAllBytes($"{outputFilePath}", outDataChunk);
                // delete temp file
                File.Delete(tempFilePath);
            }
            catch (Exception e)
            {
                Console.WriteLine("Problem writing raw to file!");
                Console.WriteLine(e);
                return false;
            }

            return true;
        }

        private static WaveProps GetWaveProps(string filePath)
        {
            WaveProps props = new WaveProps();

            try
            {
                // read in wave file header info to props
                using (BinaryReader reader = new BinaryReader(File.OpenRead(filePath)))
                {
                    props.ChunkID = Encoding.ASCII.GetString(reader.ReadBytes(4));
                    props.ChunkSize = reader.ReadInt32();
                    props.Format = Encoding.ASCII.GetString(reader.ReadBytes(4));
                    props.Subchunk1ID = Encoding.ASCII.GetString(reader.ReadBytes(4));
                    props.Subchunk1Size = reader.ReadInt32();
                    props.AudioFormat = reader.ReadInt16();
                    props.NumChannels = reader.ReadInt16();
                    props.SampleRate = reader.ReadInt32();
                    props.ByteRate = reader.ReadInt32();
                    props.BlockAlign = reader.ReadInt16();
                    props.BitsPerSample = reader.ReadInt16();

                    // handle if there are extra params
                    if (props.Subchunk1Size != 16)
                    {
                        props.ExtraParamsSize = reader.ReadInt16();
                        props.ExtraParams = reader.ReadBytes(props.ExtraParamsSize);
                    }
                    else
                    {
                        props.ExtraParamsSize = 0;
                        props.ExtraParams = null;
                    }

                    props.Subchunk2ID = Encoding.ASCII.GetString(reader.ReadBytes(4));
                    props.Subchunk2Size = reader.ReadInt32();
                    props.DataOffset = reader.BaseStream.Position;
                }

                if (displayInfo)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"File: {filePath}");
                    Console.ResetColor();
                    Console.WriteLine($"RIFF: {props.ChunkID}");
                    Console.WriteLine($"ChunkSize: {props.ChunkSize}");
                    Console.WriteLine($"Format: {props.Format}");
                    Console.WriteLine($"Subchunk1ID: {props.Subchunk1ID}");
                    Console.WriteLine($"Subchunk1Size: {props.Subchunk1Size}");
                    Console.WriteLine($"AudioFormat: {props.AudioFormat}");
                    Console.WriteLine($"Channels: {props.NumChannels}");
                    Console.WriteLine($"Sample Rate: {props.SampleRate}");
                    Console.WriteLine($"Byte Rate: {props.ByteRate}");
                    Console.WriteLine($"Block Align: {props.BlockAlign}");
                    if (props.ExtraParamsSize > 0)
                    {
                        Console.WriteLine($"ExtraParamsSize: {props.ExtraParamsSize}");
                        Console.WriteLine($"ExtraParams: {string.Join(",", props.ExtraParams)}");
                        Console.WriteLine($"Samples per Block: {props.ExtraParams[0]}");
                    }
                    Console.WriteLine($"Bits Per Sample: {props.BitsPerSample}");
                    Console.WriteLine($"Subchunk2ID: {props.Subchunk2ID}");
                    Console.WriteLine($"Subchunk2Size: {props.Subchunk2Size}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Problem reading wave file!");
                Console.WriteLine(e);
            }

            return props;
        }

        private static int GetNumSamples(WaveProps wave)
        {
            try
            {
                int totalSamples = wave.Subchunk2Size / wave.NumChannels / (wave.BitsPerSample / 8);
                return totalSamples;
            }
            catch (DivideByZeroException e)
            {
                Console.WriteLine("Can't divide by zero! Re-encoded file?");
                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine("Problem calculating samples!");
                Console.WriteLine(e);
                return 0;
            }
        }

        // check if file needs to be encoded
        private static bool RequiresEncoding(string infile, string outfile)
        {
            try
            {
                string infileChecksum = GetWaveSum(infile);

                // already encoded file doesn't exist
                if (!File.Exists(outfile))
                {
                    Console.WriteLine("Encoded file missing! Re-encoding required!");
                    return true;
                }

                // checks if saved sum matches infile sum
                if (checksum.GetChecksumString(infile).Equals(infileChecksum))
                {
                    Console.WriteLine("Checksum match! Re-encoding not required...");
                    return false;
                }
                else
                {
                    Console.WriteLine("Checksum mismatch! Re-encoding required!");
                    WriteWaveSum(infile);
                    return true;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return true;
            }
        }

        // get a wave files saved checksum, create one if missing
        private static string GetWaveSum(string filePath)
        {
            string waveChecksumFile = $"{Path.GetFileName(filePath)}.music";
            string checksumFilePath = $@"{checksumsFolderPath}\{waveChecksumFile}";

            // check if a checksum file for song exists
            if (!File.Exists(checksumFilePath))
            {
                WriteWaveSum(filePath);
                return null;
            }
            else
            {
                try
                {
                    string savedSum = File.ReadAllText(checksumFilePath);
                    return savedSum;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Problem reading wave file checksum!");
                    Console.WriteLine(e);
                    return null;
                }
            }
        }

        private static void WriteWaveSum(string filePath)
        {
            string waveChecksumFile = $"{Path.GetFileName(filePath)}.music";
            string checksumFilePath = $@"{checksumsFolderPath}\{waveChecksumFile}";

            // write a checksum file for song if missing
            try
            {
                string fileSum = checksum.GetChecksumString(filePath);
                File.WriteAllText(checksumFilePath, fileSum);
                Console.WriteLine($"Saved wave checksum: {Path.GetFileName(filePath)}");
            }
            catch (Exception e)
            {
                Console.WriteLine("Problem writing wave file checksum!");
                Console.WriteLine(e);
            }
        }
    }
}
