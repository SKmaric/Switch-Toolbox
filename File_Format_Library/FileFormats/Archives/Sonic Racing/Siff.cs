using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Toolbox.Library;
using Toolbox.Library.IO;
using Toolbox.Library.Rendering;
using Toolbox.Library.Forms;
using FirstPlugin.NodeWrappers;
using OpenTK;
using Siff;

namespace FirstPlugin
{
    public class Siff : SiffWrapper, IFileFormat, IArchiveFile
    {
        public FileType FileType { get; set; } = FileType.Resource;

        public bool CanSave { get; set; }
        public string[] Description { get; set; } = new string[] { "Sonic & Sega All-Stars Racing SIF Archive" };
        public string[] Extension { get; set; } = new string[] { "*.sif", "*.zif" };
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public IFileInfo IFileInfo { get; set; }

        public bool Identify(System.IO.Stream stream)
        {
            using (var readerTemp = new Toolbox.Library.IO.FileReader(stream, true))
            {
                var reader = readerTemp;

                if (Utils.HasExtension(FileName, ".zif"))
                {
                    var data = reader.ReadBytes((int)stream.Length);
                    //data = STLibraryCompression.ZLIB.Decompress(data);
                    data = data.Skip(4).ToArray();
                    reader = new FileReader(data);
                }

                return reader.CheckSignature(4, "OBJS") ||
                       reader.CheckSignature(4, "FONT") ||
                       reader.CheckSignature(4, "TEXT") ||
                       reader.CheckSignature(4, "PTEX") ||
                       reader.CheckSignature(4, "COLI") ||
                       reader.CheckSignature(4, "TRAK") ||
                       reader.CheckSignature(4, "PVD4") ||
                       reader.CheckSignature(4, "BDAT") ||
                       reader.CheckSignature(4, "LOGC") ||
                       reader.CheckSignature(4, "TRAL") ||
                       reader.CheckSignature(4, "LF 1") ||
                       reader.CheckSignature(4, "LF 2") ||
                       reader.CheckSignature(4, "SH01") ||
                       reader.CheckSignature(4, "VEG4") ||
                       reader.CheckSignature(4, "FORE");
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
        GenericModelRenderer DrawableRenderer { get; set; }

        private List<ChunkHeader> Chunks = new List<ChunkHeader>();

        public DrawableContainer DrawableContainer = new DrawableContainer();

        public void Load(System.IO.Stream stream)
        {
            CanSave = false;

            DrawableContainer.Name = FileName;

            DrawableRenderer = new GenericModelRenderer();
            DrawableContainer.Drawables.Add(DrawableRenderer);

            using (var readerTemp = new FileReader(stream))
            {
                var reader = readerTemp;

                if (Utils.HasExtension(FileName, ".zif"))
                {
                    var data = reader.ReadBytes((int)stream.Length);
                    //data = STLibraryCompression.ZLIB.Decompress(data);
                    data = data.Skip(4).ToArray();
                    reader = new FileReader(data);
                }

                reader.ByteOrder = Syroot.BinaryData.ByteOrder.BigEndian; // X360
                uint fileID = 0;

                while (!reader.EndOfStream)
                {
                    ChunkHeader chunk = new ChunkHeader();
                    chunk.ReadHeader(reader);
                    Chunks.Add(chunk);

                    var fileInfo = new FileInfo();

                    switch (chunk.Identifier)
                    {

                        //case "OBJS":
                        //    OBJS objsFile = new OBJS();
                        //    objsFile.Read(reader);
                        //    break;
                        case "FONT":
                        //    Font fontFile = new Font();
                        //    fontFile.Read(reader);
                            fileInfo.FileName = fileID + "." + "Font";
                            fileInfo.FileData = reader.ReadBytes((int)chunk.DataSize);
                            break;
                        case "TEXT":
                        //    TEXT textFile = new TEXT();
                        //    textFile.Read(reader);
                            fileInfo.FileName = fileID + "." + "Text";
                            fileInfo.FileData = reader.ReadBytes((int)chunk.DataSize);
                            break;
                        //case "PTEX":
                        //    PTEX ptexFile = new PTEX();
                        //    ptexFile.Read(reader);
                        //    break;
                        case "COLI":
                        //    COLI coliFile = new COLI();
                        //    coliFile.Read(reader);
                            fileInfo.FileName = fileID + "." + "CollisionData";
                            fileInfo.FileData = reader.ReadBytes((int)chunk.DataSize);
                            break;
                        //case "TRAK":
                        //    TRAK trakFile = new TRAK();
                        //    trakFile.Read(reader);
                        //    break;
                        //case "PVD4":
                        //    PVD4 pvd4File = new PVD4();
                        //    pvd4File.Read(reader);
                        //    break;
                        //case "BDAT":
                        //    BDAT bdatFile = new BDAT();
                        //    bdatFile.Read(reader);
                        //    break;
                        case "LOGC":
                        //    LOGC logcFile = new LOGC();
                        //    logcFile.Read(reader);
                            fileInfo.FileName = fileID + "." + "LogicData";
                            fileInfo.FileData = reader.ReadBytes((int)chunk.DataSize);
                            break;
                        case "TRAL":
                        //    TRAL tralFile = new TRAL();
                        //    tralFile.Read(reader);
                            fileInfo.FileName = fileID + "." + "TrailData";
                            fileInfo.FileData = reader.ReadBytes((int)chunk.DataSize);
                            break;
                        //case "LF 1":
                        //    LF1 lf1File = new LF1();
                        //    lf1File.Read(reader);
                        //    break;
                        //case "LF 2":
                        //    LF2 lf2File = new LF2();
                        //    lf2File.Read(reader);
                        //    break;
                        //case "SH01":
                        //    SH01 sh01File = new SH01();
                        //    sh01File.Read(reader);
                        //    break;
                        //case "VEG4":
                        //    VEG4 veg4File = new VEG4();
                        //    veg4File.Read(reader);
                        //    break;
                        case "FORE":
                            //Forest forestFile = new Forest();
                            //forestFile.Read(reader, chunk.DataSize);
                            fileInfo.FileName = fileID + "." + "Forest";
                            fileInfo.FileData = reader.ReadBytes((int)chunk.DataSize);
                            break;
                        default:
                            fileInfo.FileName = fileID + "." + chunk.Identifier;
                            fileInfo.FileData = reader.ReadBytes((int)chunk.DataSize);
                            break;
                    }
                    files.Add(fileInfo);

                    chunk.ReadFooter(reader);

                    reader.Seek(chunk.Position + chunk.FooterOffset + chunk.NextFileOffset, System.IO.SeekOrigin.Begin);
                    fileID++;
                }
            }
        }



        public interface IChunkData { }

        public class ChunkHeader
        {
            public IChunkData ChunkData;

            //Header
            public long Position;
            public string Identifier;
            public uint FooterOffset;
            public uint DataSize;

            //Footer
            public string FooterMagic;
            public uint NextFileOffset;
            public uint FooterSize;

            public string FileName = "";

            public void ReadHeader(FileReader reader)
            {
                Position = reader.Position;
                Identifier = Encoding.ASCII.GetString(reader.ReadBytes(4)).ToUpperInvariant();
                FooterOffset = reader.ReadUInt32(); //At the end, the file name
                DataSize = reader.ReadUInt32();
                uint Unknown = reader.ReadUInt32();
            }

            public void ReadFooter(FileReader reader)
            {
                reader.Seek(Position + FooterOffset, System.IO.SeekOrigin.Begin);
                string FooterMagic = Encoding.ASCII.GetString(reader.ReadBytes(4)).ToUpperInvariant();
                NextFileOffset = reader.ReadUInt32(); //At the end, the file name
                FooterSize = reader.ReadUInt32();
            }
        }

        public class TRAK : IChunkData // Track data (checkpoints etc)
        {
            public string FileName = "";
            public byte[] Data;

            public void Read(FileReader reader)
            {
                long pos = reader.Position;

                //reader.Seek(pos, System.IO.SeekOrigin.Begin);
                //
                //Data = reader.ReadBytes((int)FileSize);

                //FileName = reader.ReadString(Syroot.BinaryData.BinaryStringFormat.ZeroTerminated);
            }
        }

        public class SH01 : IChunkData
        {

        }

        public void Unload()
        {

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
