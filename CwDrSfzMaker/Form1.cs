using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.IO;
using Microsoft.Win32;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CwDrSfzMaker
{

    public partial class Form1 : Form
    {

        public Form1()
        {
            InitializeComponent();

            //Get the content location from the registry.
            RegistryKey cakewalkKey;
            try
            {
                cakewalkKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            }
            catch
            {
                cakewalkKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            }

            string sUserSfzPath = cakewalkKey.OpenSubKey(@"Software\Cakewalk Music Software").GetValue("ContentDir").ToString();

            if (sUserSfzPath == "")
            {
                sUserSfzPath = @"C:\Cakewalk Content";
            }

            sUserSfzPath += @"\Drum Replacer\Drums\User";

            //Fill up combo box with "Type" suggestions
            typeComboBox.Items.Add("Kicks");
            typeComboBox.Items.Add("Snares");
            typeComboBox.Items.Add("Toms");
            //And add any already-existing user folders
            if (Directory.Exists(sUserSfzPath))
            { 
                foreach (string dir in Directory.GetDirectories(sUserSfzPath, "*"))
                {
                    string sCustomName = dir.Split(Path.DirectorySeparatorChar).Last();
                    if (sCustomName != "Kicks" && sCustomName != "Snares" && sCustomName != "Toms")
                        typeComboBox.Items.Add(sCustomName);
                }
            }
        }

        private void CropWavFile(string inputFilePath, string outputFilePath, TimeSpan start, TimeSpan end)
        {
            var stream = new FileStream(inputFilePath, FileMode.Open);
            var newStream = new FileStream(outputFilePath, FileMode.OpenOrCreate);
            var isFloatingPoint = false;
            var sampleRate = 0;
            var bitDepth = 0;
            var channelCount = 0;
            var headerSize = 0;

            // Get meta info
            ReadMetaData(stream, out isFloatingPoint, out channelCount, out sampleRate, out bitDepth, out headerSize);

            // Calculate where we need to start and stop reading.
            var startIndex = (int)(start.TotalSeconds * sampleRate * (bitDepth / 8) * channelCount);
            var endIndex = (int)(end.TotalSeconds * sampleRate * (bitDepth / 8) * channelCount);
            var bytesCount = endIndex - startIndex;
            var newBytes = new byte[bytesCount];

            // Read audio data.
            stream.Position = startIndex + headerSize;
            stream.Read(newBytes, 0, bytesCount);

            // Write the wav header and our newly extracted audio to the new wav file.
            WriteMetaData(newStream, isFloatingPoint, (ushort)channelCount, (ushort)bitDepth, sampleRate, newBytes.Length / (bitDepth / 8));
            newStream.Write(newBytes, 0, newBytes.Length);

            stream.Dispose();
            newStream.Dispose();
        }

        private void WriteMetaData(FileStream stream, bool isFloatingPoint, ushort channels, ushort bitDepth, int sampleRate, int totalSampleCount)
        {
            stream.Position = 0;

            // RIFF header.
            // Chunk ID.
            stream.Write(Encoding.ASCII.GetBytes("RIFF"), 0, 4);

            // Chunk size.
            stream.Write(BitConverter.GetBytes(((bitDepth / 8) * totalSampleCount) + 36), 0, 4);

            // Format.
            stream.Write(Encoding.ASCII.GetBytes("WAVE"), 0, 4);

            // Sub-chunk 1.
            // Sub-chunk 1 ID.
            stream.Write(Encoding.ASCII.GetBytes("fmt "), 0, 4);

            // Sub-chunk 1 size.
            stream.Write(BitConverter.GetBytes(16), 0, 4);

            // Audio format (floating point (3) or PCM (1)). Any other format indicates compression.
            stream.Write(BitConverter.GetBytes((ushort)(isFloatingPoint ? 3 : 1)), 0, 2);

            // Channels.
            stream.Write(BitConverter.GetBytes(channels), 0, 2);

            // Sample rate.
            stream.Write(BitConverter.GetBytes(sampleRate), 0, 4);

            // Bytes rate.
            stream.Write(BitConverter.GetBytes(sampleRate * channels * (bitDepth / 8)), 0, 4);

            // Block align.
            stream.Write(BitConverter.GetBytes((ushort)channels * (bitDepth / 8)), 0, 2);

            // Bits per sample.
            stream.Write(BitConverter.GetBytes(bitDepth), 0, 2);

            // Sub-chunk 2.
            // Sub-chunk 2 ID.
            stream.Write(Encoding.ASCII.GetBytes("data"), 0, 4);

            // Sub-chunk 2 size.
            stream.Write(BitConverter.GetBytes((bitDepth / 8) * totalSampleCount), 0, 4);
        }

        private void ReadMetaData(FileStream stream, out bool isFloatinfPoint, out int channelCount, out int sampleRate, out int bitDepth, out int headerSize)
        {
            var headerBytes = new byte[200];

            // Read header bytes.
            stream.Position = 0;
            stream.Read(headerBytes, 0, 200);

            headerSize = new string(Encoding.ASCII.GetChars(headerBytes)).IndexOf("data") + 8;
            isFloatinfPoint = BitConverter.ToUInt16(new byte[] { headerBytes[20], headerBytes[21] }, 0) == 3 ? true : false;
            channelCount = BitConverter.ToUInt16(new byte[] { headerBytes[22], headerBytes[23] }, 0);
            sampleRate = (int)BitConverter.ToUInt32(new byte[] { headerBytes[24], headerBytes[25], headerBytes[26], headerBytes[27] }, 0);
            bitDepth = BitConverter.ToUInt16(new byte[] { headerBytes[34], headerBytes[35] }, 0);
        }

        //User clicked the "Make It" button
        private void button1_Click(object sender, EventArgs e)
        {
            //Set up variable
            String sName = nameTextBox.Text.Trim();
            String sType = typeComboBox.Text.Trim();
            String sFile = inTextBox.Text.Trim();
            String sLhFile = lhTextBox.Text.Trim();
            String sNoteNum = "35";
            int lovel;
            int hivel;

            //Make sure we have what we need to proceed.
            if (sName == "" || sType == "" || sFile == "" || File.Exists(sFile) == false)
            {
                MessageBox.Show("Something's Missing.");
            }
            else
            {
                //Disable the button and update text to "working."
                button1.Enabled = false;
                button1.Text = "Working...";
                //Get the content location from the registry.
                RegistryKey cakewalkKey;
                try
                {
                    cakewalkKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                }
                catch
                {
                    cakewalkKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
                }

                string sContentPath = cakewalkKey.OpenSubKey(@"Software\Cakewalk Music Software").GetValue("ContentDir").ToString();

                if (sContentPath == "")
                {
                    sContentPath = @"C:\Cakewalk Content";
                }
                sContentPath += @"\Drum Replacer";

                //Set a note number based on the "Type" specified by the user
                switch (sType)
                {
                    case "Kicks":
                        {
                            sNoteNum = "35";
                            break;
                        }
                    case "Snares":
                        {
                            sNoteNum = "38";
                            break;
                        }
                    case "Toms":
                        {
                            sNoteNum = "41";
                            break;
                        }
                    default:
                        {
                            sNoteNum = "37";
                            break;
                        }
                }

                //Make the folders we need
                Directory.CreateDirectory(sContentPath + @"\Sampledata\User\" + sType + @"\" + sName);
                Directory.CreateDirectory(sContentPath + @"\Drums\User\" + sType);
                if (sLhFile != "")
                    Directory.CreateDirectory(sContentPath + @"\Sampledata\User\" + sType + @"\" + sName + @"\AltHand");

                //Split out each hit from the input wave file
                for (int i = 1; i < 33; i++)
                {
                    TimeSpan nStart = TimeSpan.FromMilliseconds((i * 2000) + 16);
                    TimeSpan nEnd = nStart + TimeSpan.FromMilliseconds(1900);
                    String sOutput = sContentPath + @"\Sampledata\User\" + sType + @"\" + sName + @"\" + sName + "_" + (i).ToString() + ".wav";
                    CropWavFile(sFile, sOutput, nStart, nEnd);
                }
                //Do the same for the second wave, if it exists
                if (sLhFile != "")
                {
                    for (int i = 1; i < 33; i++)
                    {
                        TimeSpan nStart = TimeSpan.FromMilliseconds((i * 2000) + 16);
                        TimeSpan nEnd = nStart + TimeSpan.FromMilliseconds(1900);
                        String sOutput = sContentPath + @"\Sampledata\User\" + sType + @"\" + sName + @"\AltHand\" + sName + "_" + (i).ToString() + ".wav";
                        CropWavFile(sLhFile, sOutput, nStart, nEnd);
                    }
                }

                //Make the sfz file
                using (TextWriter sfzWriter = File.CreateText(sContentPath + @"\Drums\User\" + sType + @"\" + sName + ".sfz"))
                {
                    //Static sfz header stuff
                    sfzWriter.WriteLine("//  SFZ Definition File");
                    sfzWriter.WriteLine("//  Generated by the Drum Replacer Sampler Tool Thing");
                    sfzWriter.WriteLine("//  On " + DateTime.Now);
                    sfzWriter.WriteLine("");
                    sfzWriter.WriteLine("<group>");
                    sfzWriter.WriteLine("lokey=0");
                    sfzWriter.WriteLine("hikey=127");
                    sfzWriter.WriteLine("group=" + sNoteNum);
                    //Maybe make polyphony variable?
                    //There are cases where 1 sucks, like a tom
                    //But more than 1 sucks for hihat/cymbals
                    sfzWriter.WriteLine("polyphony=3");
                    sfzWriter.WriteLine("loop_mode=one_shot");
                    sfzWriter.WriteLine("");

                    //Do the Loudest sample first for best sample waveform display
                    //Right in a <region> for each extracted hit
                    for (int i = 31; i >= 0; i--)
                    {
                        sfzWriter.WriteLine("<region>");
                        sfzWriter.WriteLine(@"sample=..\..\..\Sampledata\User\" + sType + @"\" + sName + @"\" + sName + "_" + (i + 1).ToString() + ".wav");
                        lovel = (i * 3) + i;
                        hivel = lovel + 3;
                        sfzWriter.WriteLine("lovel=" + lovel.ToString());
                        sfzWriter.WriteLine("hivel=" + hivel.ToString());
                        //Add randomness if there are 2 input files.
                        if (sLhFile != "")
                        {
                            sfzWriter.WriteLine("lorand=0.0");
                            sfzWriter.WriteLine("hirand=0.5");
                        }
                        sfzWriter.WriteLine("");
                    }
                    //Repeat for second input wave file, if it exists
                    if (sLhFile != "")
                    {
                        for (int i = 31; i >= 0; i--)
                        {
                            sfzWriter.WriteLine("<region>");
                            sfzWriter.WriteLine(@"sample=..\..\..\Sampledata\User\" + sType + @"\" + sName + @"\AltHand\" + sName + "_" + (i + 1).ToString() + ".wav");
                            lovel = (i * 3) + i;
                            hivel = lovel + 3;
                            sfzWriter.WriteLine("lovel=" + lovel.ToString());
                            sfzWriter.WriteLine("hivel=" + hivel.ToString());
                            sfzWriter.WriteLine("lorand=0.5");
                            sfzWriter.WriteLine("hirand=1.0");
                            sfzWriter.WriteLine("");
                        }
                    }
                }
                //All done
                MessageBox.Show("Success!");
                button1.Enabled = true;
                button1.Text = "Make It";
            }
        }

        //Browse for input wave file 1
        private void button2_Click(object sender, EventArgs e)
        {
            DialogResult result = openFileDialog1.ShowDialog();
            if (result == DialogResult.OK)
                inTextBox.Text = openFileDialog1.FileName;
        }

        //Browse for input wave file 2
        private void button3_Click(object sender, EventArgs e)
        {
            DialogResult result = openFileDialog1.ShowDialog();
            if (result == DialogResult.OK)
                lhTextBox.Text = openFileDialog1.FileName;
        }
    }
}