using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Toolbox.Library;
using Toolbox.Library.IO;
using DataType = SPC.DataType;

namespace FirstPlugin
{
    public class SP2 : TreeNodeFile, IFileFormat, IArchiveFile
    {
        public FileType FileType { get; set; } = FileType.Archive;

        public bool CanSave { get; set; }
        public string[] Description { get; set; } = new string[] { "Team Sonic Racing Archive" };
        public string[] Extension { get; set; } = new string[] { "*.sp2" };
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public IFileInfo IFileInfo { get; set; }

        public bool Identify(System.IO.Stream stream)
        {
            using (var reader = new Toolbox.Library.IO.FileReader(stream, true))
            {
                return Utils.HasExtension(FileName, ".sp2");
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

        private List<ChunkHeader> Assets = new List<ChunkHeader>();

        public bool isLittleEndian;
        public void Load(System.IO.Stream stream)
        {
            CanSave = false;

            using (var reader = new FileReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    ChunkHeader asset = new ChunkHeader();
                    asset.Position = reader.Position;
                    asset.DataType = (DataType)reader.ReadUInt32();
                    asset.ToolVersion = reader.ReadUInt32();
                    asset.CpuRelativeOffsetNextEntry = reader.ReadUInt32();
                    asset.CpuDataLength = reader.ReadUInt32();
                    asset.GpuRelativeOffsetNextEntry = reader.ReadUInt32();
                    asset.GpuDataLength = reader.ReadUInt32();
                    uint unk2 = reader.ReadUInt32();
                    uint unk3 = reader.ReadUInt32();
                    Assets.Add(asset);

                    switch (asset.DataType)
                    {
                        case DataType.SlTexture:
                            if (asset.CpuRelativeOffsetNextEntry > 0x88)
                            {
                                reader.Seek(asset.Position + 0x88, System.IO.SeekOrigin.Begin);
                                asset.FileName = reader.ReadString(Syroot.BinaryData.BinaryStringFormat.ZeroTerminated);
                                asset.ChunkData = new TextureFile();
                            }
                            break;
                        case DataType.Nothing:
                            break;
                        case DataType.SeDefinitionAnimationStreamNode:
                            if (asset.CpuRelativeOffsetNextEntry > 0xB0)
                            {
                                reader.Seek(asset.Position + 0xB0, System.IO.SeekOrigin.Begin);
                                asset.FileName = reader.ReadString(Syroot.BinaryData.BinaryStringFormat.ZeroTerminated);
                            }
                            break;
                        case DataType.SlAnim:
                            AnimationFile animFile = new AnimationFile();
                            animFile.Read(reader);
                            asset.ChunkData = animFile;
                            break;
                        case DataType.SlSkeleton:
                            SkeletonFile skelFile = new SkeletonFile();
                            skelFile.Read(reader);
                            asset.ChunkData = skelFile;
                            break;
                        case DataType.SlModel:
                            ModelFile modelFile = new ModelFile();
                            modelFile.Read(reader);
                            asset.ChunkData = modelFile;
                            break;
                        case DataType.SlMaterial2:
                            MaterialFile matFile = new MaterialFile();
                            matFile.Read(reader);
                            asset.ChunkData = matFile;
                            break;
                        case DataType.SlResourceCollision:
                            CollisionFile collisionFile = new CollisionFile();
                            collisionFile.Read(reader);
                            asset.ChunkData = collisionFile;
                            break;
                    }

                    reader.Seek(asset.Position + asset.CpuRelativeOffsetNextEntry, System.IO.SeekOrigin.Begin);
                }

                ReadGPUFile(FilePath);
            }
            TreeHelper.CreateFileDirectory(this);
        }

        private void ReadGPUFile(string FileName)
        {
            string path = FileName.Replace("cpu", "gpu");
            if (!System.IO.File.Exists(path))
                return;

            int offset = 0;
            //Read the data based on CPU chunk info
            using (var reader = new FileReader(path))
            {
                for (int i = 0; i < Assets.Count; i++)
                {
                    if (Assets[i].GpuDataLength != 0 || Assets[i].FileName != string.Empty || Assets[i].ChunkData != null)
                    {
                        long pos = reader.Position;

                        var identifer = Assets[i].DataType;

                        var fileInfo = new FileInfo();

                        //Get CPU chunk data
                        switch (Assets[i].DataType)
                        {
                            case DataType.SlAnim:
                                AnimationFile animFile = (AnimationFile)Assets[i].ChunkData;
                                fileInfo.FileName = animFile.FileName;
                                fileInfo.FileData = animFile.Data;
                                break;
                            case DataType.SlSkeleton:
                                SkeletonFile skelFile = (SkeletonFile)Assets[i].ChunkData;
                                fileInfo.FileName = skelFile.FileName;
                                fileInfo.FileData = skelFile.Data;
                                break;
                            case DataType.SlMaterial2:
                                MaterialFile matFile = (MaterialFile)Assets[i].ChunkData;
                                fileInfo.FileName = matFile.FileName;
                                fileInfo.FileData = matFile.Data;
                                break;
                            case DataType.SlModel:
                                ModelFile modelFile = (ModelFile)Assets[i].ChunkData;
                                fileInfo.FileName = modelFile.FileName;

                                byte[] BufferData = new byte[0];
                                if (Assets[i].GpuDataLength != 0)
                                    BufferData = reader.ReadBytes((int)Assets[i].GpuDataLength);

                                fileInfo.FileData = Utils.CombineByteArray(modelFile.Data, modelFile.Data2, modelFile.Data3, BufferData);

                                //Don't advance the stream unless the chunk has a pointer
                                if (Assets[i].GpuRelativeOffsetNextEntry != 0)
                                    reader.Seek(pos + Assets[i].GpuRelativeOffsetNextEntry, System.IO.SeekOrigin.Begin);
                                break;
                            case DataType.SlResourceCollision:
                                CollisionFile collisionFile = (CollisionFile)Assets[i].ChunkData;
                                fileInfo.FileName = collisionFile.FileName;
                                fileInfo.FileData = collisionFile.Data;
                                break;
                            case DataType.SlTexture:
                                if (Assets[i].GpuDataLength != 0)
                                {
                                    fileInfo.FileData = reader.ReadBytes((int)Assets[i].GpuDataLength);
                                    try
                                    {

                                        var texture = new DDS(fileInfo.FileData);
                                        texture.WiiUSwizzle = false;
                                        texture.ImageKey = "texture";
                                        texture.SelectedImageKey = "texture";
                                        texture.Text = Assets[i].FileName;
                                        Nodes.Add(texture);
                                    }
                                    catch
                                    {
                                        fileInfo.FileName = Assets[i].FileName;
                                    }
                                }
                                break;
                            default:
                                if (Assets[i].FileName != string.Empty)
                                    fileInfo.FileName = $"{Assets[i].FileName}";
                                else
                                    fileInfo.FileName = $"{i} {Assets[i].CpuDataLength} {identifer.ToString("X")}";

                                if (Assets[i].GpuDataLength != 0)
                                    fileInfo.FileData = reader.ReadBytes((int)Assets[i].GpuDataLength);
                                else
                                    fileInfo.FileData = new byte[0];
                            break;
                        }

                        //Organise files such as mb into folders - won't be necessary when actual loading is implemented
                        fileInfo.FileName = fileInfo.FileName.Replace(':', '/');
                        fileInfo.FileName = fileInfo.FileName.Replace('|', '/');

                        files.Add(fileInfo);

                        //Don't advance the stream unless the chunk has a pointer
                        if (Assets[i].GpuRelativeOffsetNextEntry != 0)
                            reader.Seek(pos + Assets[i].GpuRelativeOffsetNextEntry, System.IO.SeekOrigin.Begin);
                    }
                }
            }
        }

        public void Unload()
        {

        }

        public interface IChunkData { }

        public class ChunkHeader
        {
            public IChunkData ChunkData;

            public DataType DataType;
            public long Position;
            public uint ToolVersion;
            public uint CpuRelativeOffsetNextEntry;
            public uint CpuDataLength;
            public uint GpuRelativeOffsetNextEntry;
            public uint GpuDataLength;

            public string FileName = "";
        }

        //Info in CPU file about the model
        //Note the GPU file chunk linked from this contains the buffers
        public class ModelFile : IChunkData
        {
            public string FileName = "";
            public string FileName2 = ""; //Yeah there's another file for some reason

            public byte[] Data;
            public byte[] Data2;
            public byte[] Data3;

            bool PadName = true;

            public void Read(FileReader reader)
            {
                long pos = reader.Position;

                uint unk3 = reader.ReadUInt32();
                uint unk4 = reader.ReadUInt32(); //Set to 1
                uint SectionSize = reader.ReadUInt32(); //At the end, the file name
                uint Padding = reader.ReadUInt32();
                uint Section2Offset = reader.ReadUInt32();

                reader.Seek(pos, System.IO.SeekOrigin.Begin);
                //Model FILE
                Data = reader.ReadBytes((int)SectionSize);

                FileName = reader.ReadString(Syroot.BinaryData.BinaryStringFormat.ZeroTerminated);

                if (PadName)
                {
                    reader.Seek(pos, System.IO.SeekOrigin.Begin);
                    Data = reader.ReadBytes((int)Section2Offset);
                }

                //Section 2
                reader.Seek(pos+Section2Offset, System.IO.SeekOrigin.Begin);

                uint unk5 = reader.ReadUInt32();
                uint unk6 = reader.ReadUInt32(); //Set to 2
                uint SectionSize2 = reader.ReadUInt32();

                reader.Seek(pos + Section2Offset + 96, System.IO.SeekOrigin.Begin);
                uint Section2OffsetDupe = reader.ReadUInt32(); //idk
                uint unk7 = reader.ReadUInt32();
                uint Section3Offset = reader.ReadUInt32();
                uint unk8 = reader.ReadUInt32();
                uint Section35Offset = reader.ReadUInt32(); //i guess section3 is split?
                uint unk9 = reader.ReadUInt32();
                uint BufferOffset = reader.ReadUInt32();

                reader.Seek(pos+Section2Offset, System.IO.SeekOrigin.Begin);

                Data2 = reader.ReadBytes((int)SectionSize2 - (int)Section2Offset);

                FileName2 = reader.ReadString(Syroot.BinaryData.BinaryStringFormat.ZeroTerminated);

                if (PadName)
                {
                    reader.Seek(pos + Section2Offset, System.IO.SeekOrigin.Begin);
                    Data2 = reader.ReadBytes((int)Section3Offset - (int)Section2Offset);
                }

                //Section 3
                reader.Seek(pos + Section3Offset, System.IO.SeekOrigin.Begin);
                Data3 = reader.ReadBytes((int)BufferOffset - (int)Section3Offset);

            }
        }

        public class MaterialFile : IChunkData
        {
            public string FileName = "";
            public byte[] Data;

            public void Read(FileReader reader)
            {
                long pos = reader.Position;

                uint unk3 = reader.ReadUInt32();
                uint unk4 = reader.ReadUInt32(); //Set to 1
                uint SectionSize = reader.ReadUInt32(); //At the end, the file name
                uint Padding = reader.ReadUInt32();

                reader.Seek(pos, System.IO.SeekOrigin.Begin);
                //Material FILE
                Data = reader.ReadBytes((int)SectionSize);

                FileName = reader.ReadString(Syroot.BinaryData.BinaryStringFormat.ZeroTerminated);
            }
        }

        public class SkeletonFile : IChunkData
        {
            public string FileName = "";
            public byte[] Data;

            public void Read(FileReader reader)
            {
                long pos = reader.Position;

                uint unk3 = reader.ReadUInt32();
                uint unk4 = reader.ReadUInt32(); //Set to 1
                uint SectionSize = reader.ReadUInt32(); //At the end, the file name
                uint Padding = reader.ReadUInt32();

                reader.Seek(pos, System.IO.SeekOrigin.Begin);
                //SKEL FILE
                Data = reader.ReadBytes((int)SectionSize);

                FileName = reader.ReadString(Syroot.BinaryData.BinaryStringFormat.ZeroTerminated);
            }
        }

        public class AnimationFile : IChunkData
        {
            public string FileName = "";
            public byte[] Data;

            public void Read(FileReader reader)
            {
                long pos = reader.Position;

                uint Hash = reader.ReadUInt32(); //Maybe a hash? Idk
                uint unk4 = reader.ReadUInt32(); //Set to 1
                uint SectionSize = reader.ReadUInt32(); //At the end, the file name
                uint Padding = reader.ReadUInt32();

                reader.Seek(pos, System.IO.SeekOrigin.Begin);
                //ANIM FILE
                Data = reader.ReadBytes((int)SectionSize);

                FileName = reader.ReadString(Syroot.BinaryData.BinaryStringFormat.ZeroTerminated);
            }
        }

        public class TextureFile : IChunkData
        {

        }

        public class TextureInfo : IChunkData
        {

        }

        public class CollisionFile : IChunkData
        {
            public string FileName = "";
            public byte[] Data;

            public void Read(FileReader reader)
            {
                long pos = reader.Position;

                uint unk3 = reader.ReadUInt32();
                uint unk4 = reader.ReadUInt32(); //Set to 1
                uint SectionSize = reader.ReadUInt32(); //At the end, the file name
                uint Padding = reader.ReadUInt32();

                reader.Seek(pos, System.IO.SeekOrigin.Begin);
                //Material FILE
                Data = reader.ReadBytes((int)SectionSize);

                FileName = reader.ReadString(Syroot.BinaryData.BinaryStringFormat.ZeroTerminated);
            }
        }

        public class CollisionMaterialFile : IChunkData
        {
            //Todo
            public string FileName = "";
            public byte[] Data;

            public void Read(FileReader reader)
            {
                long pos = reader.Position;

                uint unk3 = reader.ReadUInt32();
                uint unk4 = reader.ReadUInt32(); //Set to 1
                uint SectionSize = reader.ReadUInt32(); //At the end, the file name
                uint Padding = reader.ReadUInt32();
                uint FileSize = reader.ReadUInt32(); //Duplicate of SectionSize?

                reader.Seek(pos, System.IO.SeekOrigin.Begin);
                //Material FILE
                Data = reader.ReadBytes((int)SectionSize);

                FileName = reader.ReadString(Syroot.BinaryData.BinaryStringFormat.ZeroTerminated);
            }
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
