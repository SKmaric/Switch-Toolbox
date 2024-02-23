using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Toolbox.Library;
using Toolbox.Library.IO;

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

                    fileInfo.FileName = hash.ToString("X8");
                    fileInfo.FileName = AssignHashName(fileInfo.FileName);

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
                            if (isRELO > 0)
                                fileInfo.FileName = fileInfo.FileName + ".sif";
                            else
                                fileInfo.FileName = fileInfo.FileName + ".sig";
                        }
                        else
                        {
                            if (isRELO > 0)
                                fileInfo.FileName = fileInfo.FileName + ".zif";
                            else
                                fileInfo.FileName = fileInfo.FileName + ".zig";
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

        public string AssignHashName(string fileHash)
        {
            var NameLookupDictionary = new Dictionary<string, string>()
            {
                //Base.xpac (PC)
                {"70FE460F", "AchievementsList.dat"},
                {"7399063B", "AllProfiles.dat"},
                {"4059BC33", "AllGadgets"},
                {"5B16F914", "AllGadgets"},
                {"C8F6151F", "DebugLights"},
                {"77CC3C42", "DebugLights"},
                {"332A499B", "EffectsParams.dat"},
                {"BBE936E5", "GadgetWeighting.dat"},
                {"6596250A", "ItemProfiles.dat"},
                {"53310504", "Machine"},
                {"B0784A37", "Machine"},
                {"ED1F1715", "MainMenu"},
                {"A897802E", "MainMenu"},
                {"4FC32D97", "MiscParams.dat"},
                {"610DE409", "MissionParams.dat"},
                {"1F5B7E65", "MissionPyramid.dat"},
                {"1EF5A009", "Podium"},
                {"025BC99A", "Podium"},
                {"FECF5CFC", "RacerParams.dat"},
                {"3AA90029", "RadialBlurParams.dat"},
                {"46713920", "RumbleParams.dat"},
                {"7A55BD39", "SegaMiles.dat"},
                {"AA924689", "Shopping.dat"},
                {"A984F43F", "surfacetypes.def"},
                {"98F6FCDF", "Test"},
                {"0238A928", "Test"},
                {"A80D0E83", "TrackEvents.dat"},
                {"5514CDEE", "TrackParams.dat"},

                //Tracks.xpac (PC)
                {"E62ED65F", "BillyHatcher_Easy"},
                {"690F1CB8", "BillyHatcher_Easy"},
                {"D6B5742A", "BillyHatcher_Easy_4p"},
                {"E17AD10D", "BillyHatcher_Easy_4p"},
                {"BABE53E0", "BillyHatcher_Easy_PCRT_SH_Data"},
                {"1414C4BB", "BillyHatcher_Easy_PCRT_SH_Geom"},
                {"3DDBCF20", "BillyHatcher_Hard"},
                {"C0BC1579", "BillyHatcher_Hard"},
                {"2E626CEB", "BillyHatcher_Hard_4p"},
                {"3927C9CE", "BillyHatcher_Hard_4p"},
                {"BE610C7A", "BillyHatcher_Medium"},
                {"0AACD09B", "BillyHatcher_Medium"},
                {"6E68B99D", "BillyHatcher_Medium_4p"},
                {"76A86698", "BillyHatcher_Medium_4p"},
                {"10785C5E", "CasinoPark_Easy"},
                {"00FEFA29", "CasinoPark_Easy_4p"},
                {"A6BDF554", "CasinoPark_Hard"},
                {"CE2EE087", "CasinoPark_Hard_4p"},
                {"9358175B", "CasinoPark_Medium"},
                {"BAC9028E", "CasinoPark_Medium_4p"},
                {"8CE05F92", "FinalFortress_Easy"},
                {"A1C31E73", "FinalFortress_Easy_4p"},
                {"6A63AA55", "FinalFortress_Hard"},
                {"7F466936", "FinalFortress_Hard_4p"},
                {"2E920F63", "FinalFortress_Medium"},
                {"427FA84C", "FinalFortress_Medium_4p"},
                {"1BC01720", "HOTD_Arena"},
                {"15048861", "HOTD_Arena_4p"},
                {"F8FD243C", "HouseOfTheDead_Easy"},
                {"A904D15F", "HouseOfTheDead_Easy_4p"},
                {"532C6605", "HouseOfTheDead_Hard"},
                {"03341328", "HouseOfTheDead_Hard_4p"},
                {"B6EA1C2F", "HouseOfTheDead_Medium"},
                {"E97F5B6A", "HouseOfTheDead_Medium_4p"},
                {"007E0154", "JetSetRadio_Easy"},
                {"2F465C6D", "JetSetRadio_Easy_4p"},
                {"ED9E97BF", "JetSetRadio_Hard"},
                {"1C66F2D8", "JetSetRadio_Hard_4p"},
                {"080151DD", "JetSetRadio_Medium"},
                {"1CE410BE", "JetSetRadio_Medium_4p"},
                {"EE947CD8", "JSR_Traffic"},
                {"F6DD8DB9", "MonkeyBall_Arena"},
                {"25A5E8D2", "MonkeyBall_Arena_4p"},
                {"94AAD2E5", "RouletteTest"},
                {"869BF2E4", "Samba_Easy"},
                {"7FE06425", "Samba_Easy_4p"},
                {"4A0960C7", "Samba_Hard"},
                {"434DD208", "Samba_Hard_4p"},
                {"DF09CF15", "Samba_Medium"},
                {"8C15CD5E", "Samba_Medium_4p"},
                {"9BFE2CE8", "SeasideHill_Arena"},
                {"8C84CAB3", "SeasideHill_Arena_4p"},
                {"C67B2F09", "SeasideHill_Easy"},
                {"C19E1DFC", "SeasideHill_Easy"},
                {"F5438A22", "SeasideHill_Easy_4p"},
                {"418F4E43", "SeasideHill_Easy_4p"},
                {"C81A91B4", "SeasideHill_Easy_PCRT_SH_Data"},
                {"D09A407D", "SeasideHill_Easy_PCRT_SH_Geom"},
                {"B39BC574", "SeasideHill_Hard"},
                {"AEBEB467", "SeasideHill_Hard"},
                {"09A915C5", "SeasideHill_Hard_Unused"},
                {"55F4D9E6", "SeasideHill_Hard_Unused"},
                {"E264208D", "SeasideHill_Hard_4p"},
                {"2EAFE4AE", "SeasideHill_Hard_4p"},
                {"B53B281F", "SeasideHill_Hard_PCRT_SH_Data"},
                {"BDBAD6E8", "SeasideHill_Hard_PCRT_SH_Geom"},
                {"CDFE7F92", "SeasideHill_Medium"},
                {"C6C27F1D", "SeasideHill_Medium"},
                {"E2E13E73", "SeasideHill_Medium_4p"},
                {"65DFC69C", "SeasideHill_Medium_4p"},
                {"936BB895", "SeasideHill_Medium_PCRT_SH_Data"},
                {"4AA778A6", "SeasideHill_Medium_PCRT_SH_Geom"},
                {"6A29DC9E", "SMB_Easy"},
                {"F80F0F17", "SMB_Easy_4p"},
                {"2A44A529", "SMB_Hard"},
                {"B829D7A2", "SMB_Hard_4p"},
                {"2D0D2E87", "SMB_Medium"},
                {"26519FC8", "SMB_Medium_4p"},
                {"9C41691A", "ViewerEnvironment"},
                {"1F21AF73", "ViewerEnvironment"},
                {"19A5C0DF", "FX/Snowflake"},
                {"CB33CCE2", "FX/Snowflake"},

                //packfile.xpac (X360)
                {"BD5709AB", "SeasideHill_Easy"},
                {"C81C668E", "SeasideHill_Easy"},
                {"D144A294", "SeasideHill_Easy_4p"},
                {"09DA2705", "SeasideHill_Easy_4p"},
                {"4419D3C6", "SeasideHill_Easy_PCRT_SH_Data"},
                {"5878075F", "SeasideHill_Easy_PCRT_SH_Geom"},
                {"74222248", "SHE.axml"},
                {"67512855", "SHE.xml"}
            };

            if (NameLookupDictionary.ContainsKey(fileHash))
                return NameLookupDictionary[fileHash];
            else
                return fileHash;
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
