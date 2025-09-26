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

        private List<ChunkHeader> Chunks = new List<ChunkHeader>();
        public void Load(System.IO.Stream stream)
        {
            CanSave = false;

            using (var reader = new FileReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    ChunkHeader chunk = new ChunkHeader();
                    chunk.Position = reader.Position;
                    chunk.Identifier = (DataType)reader.ReadUInt32();
                    uint unk = reader.ReadUInt32();
                    chunk.ChunkSize = reader.ReadUInt32();
                    chunk.ChunkId = reader.ReadUInt32();
                    chunk.NextFilePtr = reader.ReadUInt32();
                    chunk.FileSize = reader.ReadUInt32();
                    uint unk2 = reader.ReadUInt32();
                    uint unk3 = reader.ReadUInt32();
                    Chunks.Add(chunk);

                    switch (chunk.Identifier)
                    {
                        case DataType.SlTexture:
                            if (chunk.ChunkSize > 0x88)
                            {
                                reader.Seek(chunk.Position + 0x88, System.IO.SeekOrigin.Begin);
                                chunk.FileName = reader.ReadString(Syroot.BinaryData.BinaryStringFormat.ZeroTerminated);
                                chunk.ChunkData = new TextureFile();
                            }
                            break;
                        case DataType.Nothing:
                            break;
                        case DataType.SeDefinitionAnimationStreamNode:
                            if (chunk.ChunkSize > 0xB0)
                            {
                                reader.Seek(chunk.Position + 0xB0, System.IO.SeekOrigin.Begin);
                                chunk.FileName = reader.ReadString(Syroot.BinaryData.BinaryStringFormat.ZeroTerminated);
                            }
                            break;
                        case DataType.SlAnim:
                            AnimationFile animFile = new AnimationFile();
                            animFile.Read(reader);
                            chunk.ChunkData = animFile;
                            break;
                        case DataType.SlSkeleton:
                            SkeletonFile skelFile = new SkeletonFile();
                            skelFile.Read(reader);
                            chunk.ChunkData = skelFile;
                            break;
                        case DataType.SlModel:
                            ModelFile modelFile = new ModelFile();
                            modelFile.Read(reader);
                            chunk.ChunkData = modelFile;
                            break;
                        case DataType.SlMaterial2:
                            MaterialFile matFile = new MaterialFile();
                            matFile.Read(reader);
                            chunk.ChunkData = matFile;
                            break;
                        case DataType.SlResourceCollision:
                            CollisionFile collisionFile = new CollisionFile();
                            collisionFile.Read(reader);
                            chunk.ChunkData = collisionFile;
                            break;
                    }

                    reader.Seek(chunk.Position + chunk.ChunkSize, System.IO.SeekOrigin.Begin);
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
                for (int i = 0; i < Chunks.Count; i++)
                {
                    if (Chunks[i].FileSize != 0 || Chunks[i].FileName != string.Empty || Chunks[i].ChunkData != null)
                    {
                        long pos = reader.Position;

                        var identifer = Chunks[i].Identifier;

                        var fileInfo = new FileInfo();

                        //Get CPU chunk data
                        if (Chunks[i].ChunkData != null)
                        {
                            if ( Chunks[i].ChunkData is AnimationFile)
                            {
                                AnimationFile animFile = (AnimationFile)Chunks[i].ChunkData;
                                fileInfo.FileName = animFile.FileName;
                                fileInfo.FileData = animFile.Data;
                            }
                            if (Chunks[i].ChunkData is SkeletonFile)
                            {
                                SkeletonFile animFile = (SkeletonFile)Chunks[i].ChunkData;
                                fileInfo.FileName = animFile.FileName;
                                fileInfo.FileData = animFile.Data;
                            }
                            if (Chunks[i].ChunkData is MaterialFile)
                            {
                                MaterialFile animFile = (MaterialFile)Chunks[i].ChunkData;
                                fileInfo.FileName = animFile.FileName;
                                fileInfo.FileData = animFile.Data;
                            }
                            if (Chunks[i].ChunkData is ModelFile)
                            {
                                ModelFile modelFile = (ModelFile)Chunks[i].ChunkData;
                                fileInfo.FileName = modelFile.FileName;

                                byte[] BufferData = new byte[0];
                                if (Chunks[i].FileSize != 0)
                                    BufferData = reader.ReadBytes((int)Chunks[i].FileSize);

                                fileInfo.FileData = Utils.CombineByteArray(modelFile.Data, modelFile.Data2, modelFile.Data3, BufferData);


                                //Don't advance the stream unless the chunk has a pointer
                                if (Chunks[i].NextFilePtr != 0)
                                    reader.Seek(pos + Chunks[i].NextFilePtr, System.IO.SeekOrigin.Begin);
                            }
                            if (Chunks[i].ChunkData is CollisionFile)
                            {
                                CollisionFile animFile = (CollisionFile)Chunks[i].ChunkData;
                                fileInfo.FileName = animFile.FileName;
                                fileInfo.FileData = animFile.Data;
                            }
                            if (identifer == DataType.SlTexture)
                            {
                                if (Chunks[i].FileSize != 0)
                                {
                                    fileInfo.FileData = reader.ReadBytes((int)Chunks[i].FileSize);
                                    try
                                    {

                                        var texture = new DDS(fileInfo.FileData);
                                        texture.WiiUSwizzle = false;
                                        texture.ImageKey = "texture";
                                        texture.SelectedImageKey = "texture";
                                        texture.Text = Chunks[i].FileName;
                                        Nodes.Add(texture);
                                    }
                                    catch
                                    {
                                        fileInfo.FileName = Chunks[i].FileName;
                                    }
                                }
                            }
                        }
                        else //Else get the data from GPU
                        {
                            if (Chunks[i].FileName != string.Empty)
                                fileInfo.FileName = $"{Chunks[i].FileName}";
                            else
                                fileInfo.FileName = $"{i} {Chunks[i].ChunkId} {identifer.ToString("X")}";

                            if (Chunks[i].FileSize != 0)
                                fileInfo.FileData = reader.ReadBytes((int)Chunks[i].FileSize);
                            else
                                fileInfo.FileData = new byte[0];
                        }

                        //Organise files such as mb into folders - won't be necessary when actual loading is implemented
                        fileInfo.FileName = fileInfo.FileName.Replace(':', '/');
                        fileInfo.FileName = fileInfo.FileName.Replace('|', '/');

                        files.Add(fileInfo);

                        //Don't advance the stream unless the chunk has a pointer
                        if (Chunks[i].NextFilePtr != 0)
                            reader.Seek(pos + Chunks[i].NextFilePtr, System.IO.SeekOrigin.Begin);
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

            public DataType Identifier;
            public long Position;
            public uint ChunkSize;
            public uint ChunkId;
            public uint NextFilePtr;
            public uint FileSize;

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
