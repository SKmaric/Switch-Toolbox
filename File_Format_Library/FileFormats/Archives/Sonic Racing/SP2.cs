using System;
using System.Collections.Generic;
using System.IO;
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

        public bool isLittleEndian;

        private List<SPCAsset> Assets = new List<SPCAsset>();

        private HashSet<DataType> usedDataTypes = new HashSet<DataType>();

        public void Load(System.IO.Stream stream)
        {
            CanSave = false;

            using (var b = new FileReader(stream))
            {
                int test = b.ReadInt32();
                isLittleEndian = Enum.IsDefined(typeof(DataType), test);
                b.SetByteOrder(!isLittleEndian);

                int cpuOffset = 0;
                int gpuOffset = 0;
                while (cpuOffset < b.BaseStream.Length)
                {
                    b.BaseStream.Seek(cpuOffset, SeekOrigin.Begin);
                    SPCAsset entry = new SPCAsset
                    {
                        assetNumber = Assets.Count,
                        dataType = (DataType)b.ReadInt32(),
                        toolVersion = b.ReadInt32(),
                        cpuOffsetDataHeader = cpuOffset,
                        cpuOffsetData = cpuOffset + 0x20,
                        cpuRelativeOffsetNextEntry = b.ReadInt32(),
                        cpuDataLength = b.ReadInt32(),
                        gpuOffsetData = gpuOffset,
                        gpuRelativeOffsetNextEntry = b.ReadInt32(),
                        gpuDataLength = b.ReadInt32(),
                        unknown = b.ReadInt32(),
                        name = "[No name found]"
                    };

                    int tmpOffset;
                    uint id;
                    if (b.ReadUInt32() != 0) // Entry type
                    {
                        SPCResource resource = new SPCResource(entry);
                        entry = resource;

                        resource.id = b.ReadUInt32();
                        //FindOrCreateAssetList(resourceDictionary, resource.id).Add(resource);

                        b.BaseStream.Seek(0x04, SeekOrigin.Current);

                        tmpOffset = b.ReadInt32();

                        if (tmpOffset != 0)
                        {
                            b.BaseStream.Seek(entry.cpuOffsetData + tmpOffset, SeekOrigin.Begin);
                            resource.name = b.ReadString(Syroot.BinaryData.BinaryStringFormat.ZeroTerminated);
                        }

                        switch (resource.dataType)
                        {
                            case DataType.SlMaterial2:
                                b.BaseStream.Seek(entry.cpuOffsetData + 0xC, SeekOrigin.Begin);
                                // shader
                                id = b.ReadUInt32();
                                resource.references.Add(new SPCResource() { id = id, dataType = DataType.SlShader });
                                // textures
                                for (int j = 0; j < 9; j++)
                                {
                                    if (j == 1)
                                    {
                                        continue;
                                    }
                                    b.BaseStream.Seek(resource.cpuOffsetData + 0x24 + j * 4, SeekOrigin.Begin);
                                    int offset1 = b.ReadInt32();
                                    if (offset1 == 0)
                                    {
                                        continue;
                                    }
                                    b.BaseStream.Seek(resource.cpuOffsetData + offset1 + 0xC, SeekOrigin.Begin);
                                    id = b.ReadUInt32();
                                    resource.references.Add(new SPCResource() { id = id, dataType = DataType.SlTexture });
                                }
                                // cbdesc
                                for (int j = 0; j < 9; j++)
                                {
                                    b.BaseStream.Seek(resource.cpuOffsetData + 0x50 + j * 4, SeekOrigin.Begin);
                                    int offset1 = b.ReadInt32();
                                    if (offset1 == 0)
                                    {
                                        continue;
                                    }
                                    b.BaseStream.Seek(resource.cpuOffsetData + offset1 + 0xC, SeekOrigin.Begin);
                                    id = b.ReadUInt32();
                                    resource.references.Add(new SPCResource() { id = id, dataType = DataType.SlConstantBufferDesc });
                                }
                                break;
                            case DataType.SlAnim:
                                // SlSkeleton
                                b.BaseStream.Seek(resource.cpuOffsetData + 0x10, SeekOrigin.Begin);
                                id = b.ReadUInt32();
                                resource.references.Add(new SPCResource() { id = id, dataType = DataType.SlSkeleton });
                                break;
                            case DataType.SlModel:
                                // SlSkeleton
                                b.BaseStream.Seek(resource.cpuOffsetData + 0xC, SeekOrigin.Begin);
                                int offset2 = b.ReadInt32();
                                b.BaseStream.Seek(resource.cpuOffsetData + offset2 + 0xC, SeekOrigin.Begin);
                                id = b.ReadUInt32();
                                if (id != 0)
                                {
                                    resource.references.Add(new SPCResource() { id = id, dataType = DataType.SlSkeleton });
                                }
                                // SlMaterial
                                b.BaseStream.Seek(resource.cpuOffsetData + 0x40, SeekOrigin.Begin);
                                int materialCount = b.ReadInt32();
                                b.BaseStream.Seek(resource.cpuOffsetData + 0x60, SeekOrigin.Begin);
                                //for (int i = 0; i < materialCount; i++)
                                //{
                                //    id = b.ReadUInt32();
                                //    resource.references.Add(new SPCResource() { id = id, dataType = DataType.SlMaterial2 });
                                //}
                                break;
                        }
                    }
                    else
                    {
                        SPCNode node = new SPCNode(entry);
                        entry = node;

                        b.BaseStream.Seek(entry.cpuOffsetData + 0x14, SeekOrigin.Begin);
                        node.id = b.ReadUInt32();
                        //FindOrCreateAssetList(nodeDictionary, node.id).Add(node);

                        b.BaseStream.Seek(entry.cpuOffsetData + 0x1C, SeekOrigin.Begin);
                        tmpOffset = b.ReadInt32();
                        if (tmpOffset != 0)
                        {
                            b.BaseStream.Seek(entry.cpuOffsetData + tmpOffset, SeekOrigin.Begin);
                            node.name = b.ReadString(Syroot.BinaryData.BinaryStringFormat.ZeroTerminated);
                        }
                        b.BaseStream.Seek(entry.cpuOffsetData + 0x24, SeekOrigin.Begin);
                        tmpOffset = b.ReadInt32();
                        if (tmpOffset != 0)
                        {
                            b.BaseStream.Seek(entry.cpuOffsetData + tmpOffset, SeekOrigin.Begin);
                            node.shortName = b.ReadString(Syroot.BinaryData.BinaryStringFormat.ZeroTerminated);
                        }
                        b.BaseStream.Seek(entry.cpuOffsetData + 0x40, SeekOrigin.Begin);
                        tmpOffset = b.ReadInt32();
                        if (tmpOffset != 0)
                        {
                            b.BaseStream.Seek(entry.cpuOffsetData + tmpOffset, SeekOrigin.Begin);
                            node.parent.Add(new SPCNode { id = b.ReadUInt32() });
                        }
                        b.BaseStream.Seek(entry.cpuOffsetData + 0x68, SeekOrigin.Begin);
                        tmpOffset = b.ReadInt32();
                        if (tmpOffset != 0)
                        {
                            b.BaseStream.Seek(entry.cpuOffsetData + tmpOffset, SeekOrigin.Begin);
                            node.definition.Add(new SPCNode { id = b.ReadUInt32() });
                        }
                        switch (node.dataType)
                        {
                            case DataType.Water13DefNode:
                                // Water13Simulation
                                b.BaseStream.Seek(entry.cpuOffsetData + 0xD0, SeekOrigin.Begin);
                                id = b.ReadUInt32();
                                node.references.Add(new SPCResource() { id = id, dataType = DataType.Water13Simulation });
                                // Water13Renderable
                                id = b.ReadUInt32();
                                node.references.Add(new SPCResource() { id = id, dataType = DataType.Water13Renderable });
                                break;
                            case DataType.Water13InstanceNode:
                                // Water13SurfaceWavesDefNode
                                b.BaseStream.Seek(entry.cpuOffsetData + 0x1D0, SeekOrigin.Begin);
                                id = b.ReadUInt32();
                                node.references.Add(new SPCNode() { id = id, dataType = DataType.Water13SurfaceWavesDefNode });
                                // WaterShader4DefinitionNode
                                id = b.ReadUInt32();
                                node.references.Add(new SPCNode() { id = id, dataType = DataType.WaterShader4DefinitionNode });
                                break;
                            case DataType.SeDefinitionParticleEmitterNode:
                                // SeDefinitionParticleStyleNode
                                b.BaseStream.Seek(entry.cpuOffsetData + 0x198, SeekOrigin.Begin);
                                id = b.ReadUInt32();
                                node.references.Add(new SPCNode() { id = id, dataType = DataType.SeDefinitionParticleStyleNode });
                                break;
                            case DataType.SeDefinitionParticleStyleNode:
                                // SeDefinitionTextureNode
                                b.BaseStream.Seek(entry.cpuOffsetData + 0x1D0, SeekOrigin.Begin);
                                id = b.ReadUInt32();
                                node.references.Add(new SPCNode() { id = id, dataType = DataType.SeDefinitionTextureNode });
                                break;
                            case DataType.CameoObjectInstanceNode:
                                // SeInstanceSplineNode
                                b.BaseStream.Seek(entry.cpuOffsetData + 0x1A4, SeekOrigin.Begin);
                                id = b.ReadUInt32();
                                if (id != 0)
                                {
                                    node.references.Add(new SPCNode() { id = id, dataType = DataType.SeInstanceSplineNode });
                                }
                                break;
                        }
                    }

                    cpuOffset += entry.cpuRelativeOffsetNextEntry;
                    b.BaseStream.Seek(cpuOffset + 8, SeekOrigin.Begin);
                    entry.cpuOffsetPointersHeader = cpuOffset;
                    entry.cpuOffsetPointers = cpuOffset + 0x20;
                    entry.cpuRelativeOffsetNextEntry += b.ReadInt32();
                    entry.cpuPointersLength = b.ReadInt32();

                    Assets.Add(entry);
                    usedDataTypes.Add(entry.dataType);

                    cpuOffset = entry.cpuOffsetDataHeader + entry.cpuRelativeOffsetNextEntry;
                    gpuOffset = entry.gpuOffsetData + entry.gpuRelativeOffsetNextEntry;
                }
                //ReadGPUFile(FilePath);
                GetData(FilePath);
            }
            TreeHelper.CreateFileDirectory(this);
        }

        private void GetData(string cpuPath)
        {
            string gpuPath = cpuPath.Replace("cpu", "gpu");

            using (var cpuReader = new FileReader(cpuPath))
            {
                foreach (var entry in Assets)
                {
                    var fileInfo = new FileInfo();
                    fileInfo.FileName = entry.name;

                    entry.msCpuData = new MemoryStream();

                    byte[] cpuData;

                    cpuReader.BaseStream.Seek(entry.cpuOffsetData, SeekOrigin.Begin);
                    cpuData = cpuReader.ReadBytes(entry.cpuDataLength);
                    //cpuReader.BaseStream.CopyTo(entry.msCpuData, entry.cpuDataLength);

                    //fileInfo.FileData = entry.msCpuData.ToBytes();

                    fileInfo.FileData = cpuData;

                    if (System.IO.File.Exists(gpuPath))
                    {
                        using (var gpuReader = new FileReader(gpuPath))
                        {
                            entry.msGpuData = new MemoryStream();
                            byte[] gpuData;

                            gpuReader.BaseStream.Seek(entry.gpuOffsetData, SeekOrigin.Begin);
                            gpuData = gpuReader.ReadBytes(entry.gpuDataLength);
                            //gpuReader.BaseStream.CopyTo(entry.msGpuData, entry.gpuDataLength);

                            //fileInfo.FileData = Utils.CombineByteArray(fileInfo.FileData, entry.msGpuData.ToBytes());
                            fileInfo.FileData = Utils.CombineByteArray(fileInfo.FileData, gpuData);
                        }
                    }
                    //Organise files such as mb into folders - won't be necessary when actual loading is implemented
                    fileInfo.FileName = fileInfo.FileName.Replace(":", ":/");
                    fileInfo.FileName = fileInfo.FileName.Replace("|", "|/");

                    files.Add(fileInfo);
                }
            }
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
                    if (Assets[i].gpuDataLength != 0 || Assets[i].name != string.Empty || Assets[i].ChunkData != null)
                    {
                        long pos = reader.Position;

                        var identifer = Assets[i].dataType;

                        var fileInfo = new FileInfo();

                        //Get CPU chunk data
                        switch (Assets[i].dataType)
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
                                if (Assets[i].gpuDataLength != 0)
                                    BufferData = reader.ReadBytes((int)Assets[i].gpuDataLength);

                                fileInfo.FileData = Utils.CombineByteArray(modelFile.Data, modelFile.Data2, modelFile.Data3, BufferData);

                                //Don't advance the stream unless the chunk has a pointer
                                if (Assets[i].gpuRelativeOffsetNextEntry != 0)
                                    reader.Seek(pos + Assets[i].gpuRelativeOffsetNextEntry, System.IO.SeekOrigin.Begin);
                                break;
                            case DataType.SlResourceCollision:
                                CollisionFile collisionFile = (CollisionFile)Assets[i].ChunkData;
                                fileInfo.FileName = collisionFile.FileName;
                                fileInfo.FileData = collisionFile.Data;
                                break;
                            case DataType.SlTexture:
                                if (Assets[i].gpuDataLength != 0)
                                {
                                    fileInfo.FileData = reader.ReadBytes((int)Assets[i].gpuDataLength);
                                    fileInfo.FileName = Assets[i].name;
                                    try
                                    {

                                        var texture = new DDS(fileInfo.FileData);
                                        texture.WiiUSwizzle = false;
                                        texture.ImageKey = "texture";
                                        texture.SelectedImageKey = "texture";
                                        texture.Text = Assets[i].name;
                                        //Nodes.Add(texture);
                                    }
                                    catch
                                    {
                                        fileInfo.FileName = Assets[i].name;
                                    }
                                }
                                break;
                            default:
                                if (Assets[i].name != string.Empty)
                                    fileInfo.FileName = $"{Assets[i].name}";
                                else
                                    fileInfo.FileName = $"{i} {Assets[i].cpuDataLength} {identifer.ToString("X")}";

                                if (Assets[i].gpuDataLength != 0)
                                    fileInfo.FileData = reader.ReadBytes((int)Assets[i].gpuDataLength);
                                else
                                    fileInfo.FileData = new byte[0];
                            break;
                        }

                        //Organise files such as mb into folders - won't be necessary when actual loading is implemented
                        fileInfo.FileName = fileInfo.FileName.Replace(':', '/');
                        fileInfo.FileName = fileInfo.FileName.Replace('|', '/');

                        files.Add(fileInfo);

                        //Don't advance the stream unless the chunk has a pointer
                        if (Assets[i].gpuRelativeOffsetNextEntry != 0)
                            reader.Seek(pos + Assets[i].gpuRelativeOffsetNextEntry, System.IO.SeekOrigin.Begin);
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
