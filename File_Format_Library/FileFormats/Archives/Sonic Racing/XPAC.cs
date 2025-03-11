using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Toolbox.Library;
using Toolbox.Library.IO;
using FirstPlugin.FileFormats.Archives.Sonic_Racing;
using System.IO;

namespace FirstPlugin
{
    public class XPAC : IFileFormat, IArchiveFile
    {
        public FileType FileType { get; set; } = FileType.Archive;

        public bool CanSave { get; set; }
        public string[] Description { get; set; } = new string[] { "Sonic & Sega All-Stars Racing Archive" };
        public string[] Extension { get; set; } = new string[] { "*.xpac" };
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public IFileInfo IFileInfo { get; set; }

        public bool Identify(System.IO.Stream stream)
        {
            using (var reader = new Toolbox.Library.IO.FileReader(stream, true))
            {
                return Utils.HasExtension(FileName, ".xpac");
            }
        }

        public Type[] Types
        {
            get
            {
                List<Type> types = new List<Type>();
                return types.ToArray();
            }
        }

        public List<FileInfo> files = new List<FileInfo>();
        public IEnumerable<ArchiveFileInfo> Files => files;

        public void ClearFiles() { files.Clear(); }

        public bool CanAddFiles { get; set; }
        public bool CanRenameFiles { get; set; }
        public bool CanReplaceFiles { get; set; }
        public bool CanDeleteFiles { get; set; }

        private Header header;
        private bool decompressZlib = true;
        private bool sortByStored = true;

        public void Load(System.IO.Stream stream)
        {
            CanSave = false;
            string test = FileName;

            using (var reader = new FileReader(stream))
            {
                header = new Header(reader, FileName);
                long returnpos = 0x18;

                for (uint i = 0; i < header.FileCount; i++)
                {
                    var fileInfo = new FileInfo();

                    reader.Position = returnpos;
                    uint hash = reader.ReadUInt32();
                    uint offset = reader.ReadUInt32();
                    uint sizec = reader.ReadUInt32();
                    uint sizedec = reader.ReadUInt32();
                    uint padding = reader.ReadUInt32();
                    returnpos = reader.Position;

                    fileInfo.FileName = AssignHashName(hash);

                    reader.Position = offset;

                    var data = reader.ReadBytes((int)sizec);
                    byte[] dataDec;

                    bool isZlib = false;
                    try
                    {
                        dataDec = STLibraryCompression.ZLIB.Decompress(data); //decompress if needed
                        isZlib = true;
                    }
                    catch
                    {
                        dataDec = data;
                    }

                    if (isZlib && decompressZlib)
                    {
                        uint filesize = BitConverter.ToUInt32(dataDec.Take(4).ToArray(), 0);
                        dataDec = dataDec.Skip(4).ToArray();
                        data = dataDec;
                    }

                    // this is the best way of determining if the file is a SIF file i can find
                    int isRELO = 0;
                    var RELOcheck = new byte[] { (byte)'R', (byte)'E', (byte)'L', (byte)'O' };
                    for (int j = 0; j < dataDec.Length; j++)
                    {
                        if (dataDec[j] == RELOcheck[isRELO])
                        {
                            if (++isRELO == RELOcheck.Length)
                            {
                                isRELO = j - isRELO + 1;
                                break;
                            }
                        }
                        else
                        {
                            isRELO = 0;
                        }
                    }

                    if (isZlib)
                    {
                        if (decompressZlib)
                        {
                            if (fileInfo.FileName.Contains(".zif"))
                                fileInfo.FileName = fileInfo.FileName.Replace(".zif", ".sif");
                            else if (fileInfo.FileName.Contains(".zig"))
                                fileInfo.FileName = fileInfo.FileName.Replace(".zig", ".sig");
                            else // if hash name isn't found
                            {
                                if (isRELO > 0)
                                    fileInfo.FileName = fileInfo.FileName + ".sif";
                                else
                                    fileInfo.FileName = fileInfo.FileName + ".sig";
                            }

                        }
                        else // if hash name isn't found
                        {
                            if (! (fileInfo.FileName.Contains(".zif")) || (fileInfo.FileName.Contains(".zig")))
                            {
                                if (isRELO > 0)
                                    fileInfo.FileName = fileInfo.FileName + ".zif";
                                else
                                    fileInfo.FileName = fileInfo.FileName + ".zig";
                            }
                        }
                    }

                    fileInfo.FileData = data;
                    fileInfo.Name = offset.ToString();
                    files.Add(fileInfo);
                }
                if (sortByStored)
                    files = files.OrderBy(x => UInt32.Parse(x.Name)).ToList();
            }
        }

        public class Header
        {
            public long TableSize;
            public uint FileCount;

            public Header(FileReader reader, string FileName)
            {
                reader.ByteOrder = Syroot.BinaryData.ByteOrder.LittleEndian;
                // There isn't any sort of header magic to check against so just using filename instead
                if (FileName == "packfile.xpac") // X360
                    reader.ByteOrder = Syroot.BinaryData.ByteOrder.BigEndian;
                    
                reader.Seek(0x08, System.IO.SeekOrigin.Begin);

                TableSize = reader.ReadUInt32();
                FileCount = reader.ReadUInt32();
            }
        }


        public void Unload()
        {

        }

        public string AssignHashName(uint fileHash)
        {
            string name = XPACHashes.xpachash_t_ToString(fileHash);

            name = name.Replace(".\\", "");
            name = name.Replace("\\", "/");

            return name;

            //var NameLookupDictionary = new Dictionary<string, string>()
            //{

            //    {"7EFC3B8B", "Resource/TSOData/7EFC3B8B"},
            //    {"0090AE05", "Resource/TSOData/0090AE05"},
            //    {"9D853559", "Resource/TSOData/9D853559"},
            //    {"59C8DA80", "Resource/TSOData/GroupNames"},
            //    {"FFE6BEC5", "Resource/TSOData/ItemDefaults"},

            //    {"5875B07C", "Resource/Racers/Avatar"},
            //    {"8A3B39B7", "Resource/Racers/Avatar"},
            //    {"3C5249FB", "Resource/Racers/Banjo"},
            //    {"B3E850E4", "Resource/Racers/Banjo"},
            //    {"C03C389F", "Resource/Racers/Zobio"},
            //    {"71CA44A2", "Resource/Racers/Zobio"},

            //    {"15048861" , "Resource/Tracks/HOTD_Arena_4p"},
            //    {"F0B4ADEA" , "Resource/Tracks/HOTD_Arena_4p"},
            //    {"0AC1CFB4" , "Resource/Tracks/HOTD_Arena_PCRT_SH_Data"},
            //    {"25A5E8D2" , "Resource/Tracks/MonkeyBall_Arena_4p"},
            //    {"71F1ACF3" , "Resource/Tracks/MonkeyBall_Arena_4p"},
            //    {"00FC9F2D" , "Resource/Tracks/MonkeyBall_Arena_PCRT_SH_Data"},
            //    {"94AAD2E5" , "Resource/Tracks/Particle_TestTrack"},
            //    {"4638DEE8" , "Resource/Tracks/Particle_TestTrack"},
            //    {"09A915C5" , "Resource/Tracks/SeasideHill_Hard_Unused"},
            //    {"55F4D9E6" , "Resource/Tracks/SeasideHill_Hard_Unused"},

            //    //packfile.xpac (X360)
            //    {"BD5709AB", "SeasideHill_Easy"},
            //    {"C81C668E", "SeasideHill_Easy"},
            //    {"D144A294", "SeasideHill_Easy_4p"},
            //    {"09DA2705", "SeasideHill_Easy_4p"},
            //    {"4419D3C6", "SeasideHill_Easy_PCRT_SH_Data"},
            //    {"5878075F", "SeasideHill_Easy_PCRT_SH_Geom"},
            //    {"74222248", "SHE.axml"},
            //    {"67512855", "SHE.xml"},
            //    {"887E8163", "DLC0/AI/AI_DeathEgg.txt"}
            //};

            //if (NameLookupDictionary.ContainsKey(fileHash))
            //    return NameLookupDictionary[fileHash];
            //else
            //    return fileHash;
        }

        public void Save(System.IO.Stream stream)
        {
        }

        public bool AddFile(ArchiveFileInfo archiveFileInfo)
        {
            return false;
        }

        public bool DeleteFile(ArchiveFileInfo archiveFileInfo)
        {
            return false;
        }

        public class FileInfo : ArchiveFileInfo
        {

        }
    }
}
