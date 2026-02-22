using Collada141;
using SPC;
using SPICA.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Toolbox.Library;
using Toolbox.Library.Forms;
using Toolbox.Library.IO;
using Toolbox.Library.NodeWrappers;
using Toolbox.Library.Rendering;
using DataType = SPC.DataType;

namespace FirstPlugin
{
    public class SPC : TreeNodeFile, IFileFormat, IArchiveFile
    {
        public FileType FileType { get; set; } = FileType.Archive;

        public bool CanSave { get; set; }
        public string[] Description { get; set; } = new string[] { "Sonic and All Stars Racing Transformed Archive" };
        public string[] Extension { get; set; } = new string[] { "*.spc", "*.swu" };
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public IFileInfo IFileInfo { get; set; }

        public bool Identify(System.IO.Stream stream)
        {
            using (var reader = new Toolbox.Library.IO.FileReader(stream, true))
            {
                return (Utils.HasExtension(FileName, ".spc") | (Utils.HasExtension(FileName, ".swu")));
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

                        if (resource.dataType == DataType.SlResourceCollision)
                            b.BaseStream.Seek(0x10, SeekOrigin.Current);

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
                                for (int i = 0; i < materialCount; i++)
                                {
                                    id = b.ReadUInt32();
                                    resource.references.Add(new SPCResource() { id = id, dataType = DataType.SlMaterial2 });
                                }
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
                cpuReader.SetByteOrder(!isLittleEndian);
                foreach (var entry in Assets)
                {
                    bool nodeCreated = false;

                    var fileInfo = new FileInfo();
                    fileInfo.FileName = entry.name;

                    entry.msCpuData = new MemoryStream();

                    byte[] cpuData;

                    cpuReader.BaseStream.Seek(entry.cpuOffsetData, SeekOrigin.Begin);
                    cpuData = cpuReader.ReadBytes(entry.cpuDataLength);
                    //cpuReader.BaseStream.CopyTo(entry.msCpuData, entry.cpuDataLength);

                    //fileInfo.FileData = entry.msCpuData.ToBytes();

                    fileInfo.FileData = cpuData;

                    switch (entry.dataType)
                    {
                        // Non GPU types first
                        case DataType.SlResourceCollision:
                            break;
                        default:
                            // GPU types
                            if (System.IO.File.Exists(gpuPath))
                            {
                                using (var gpuReader = new FileReader(gpuPath))
                                {
                                    gpuReader.SetByteOrder(!isLittleEndian);
                                    byte[] gpuData;
                                    gpuReader.BaseStream.Seek(entry.gpuOffsetData, SeekOrigin.Begin);
                                    gpuData = gpuReader.ReadBytes(entry.gpuDataLength);

                                    switch (entry.dataType)
                                    {
                                        case DataType.SlTexture:
                                            // Determine Wii U format
                                            cpuReader.BaseStream.Seek(entry.cpuOffsetData + 0x04, SeekOrigin.Begin);
                                            var test = cpuReader.ReadUInt32();
                                            if (test == 0xD4)
                                            {
                                                try
                                                {
                                                    var texture = new SWUTexture();
                                                    texture.ImageKey = "texture";
                                                    texture.SelectedImageKey = "texture";
                                                    cpuReader.Seek(entry.cpuOffsetData + 0x28, SeekOrigin.Begin);
                                                    texture.ReadChunk(cpuReader);
                                                    texture.Text = entry.name;
                                                    texture.ImageData = gpuData;
                                                    Nodes.Add(texture);
                                                    nodeCreated = true;
                                                }
                                                catch
                                                {
                                                    fileInfo.FileData = gpuData;
                                                }
                                            }
                                            else
                                            {
                                                try
                                                {
                                                    var texture = new DDS(gpuData);
                                                    texture.WiiUSwizzle = false;
                                                    texture.ImageKey = "texture";
                                                    texture.SelectedImageKey = "texture";
                                                    texture.Text = entry.name;
                                                    Nodes.Add(texture);
                                                    nodeCreated = true;
                                                }
                                                catch
                                                {
                                                    fileInfo.FileData = gpuData;
                                                }
                                            }
                                            break;
                                        default:
                                            entry.msGpuData = new MemoryStream();
                                            
                                            //gpuReader.BaseStream.CopyTo(entry.msGpuData, entry.gpuDataLength);

                                            //fileInfo.FileData = Utils.CombineByteArray(fileInfo.FileData, entry.msGpuData.ToBytes());
                                            fileInfo.FileData = Utils.CombineByteArray(fileInfo.FileData, gpuData);
                                            break;
                                    }
                                }
                            }
                            else
                            {
                                // warning GPU file not found
                            }
                            break;
                    }

                    
                    //Organise files such as mb into folders - won't be necessary when actual loading is implemented
                    fileInfo.FileName = fileInfo.FileName.Replace(":", ":/");
                    fileInfo.FileName = fileInfo.FileName.Replace("|", "|/");

                    if (!nodeCreated)
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
                                    try
                                    {

                                        var texture = new DDS(fileInfo.FileData);
                                        texture.WiiUSwizzle = false;
                                        texture.ImageKey = "texture";
                                        texture.SelectedImageKey = "texture";
                                        texture.Text = Assets[i].name;
                                        Nodes.Add(texture);
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


        //Info in CPU file about the model
        //Note the GPU file chunk linked from this contains the buffers
        public class ModelFile : STGenericWrapper, IChunkData
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
                uint SectionSize = reader.ReadUInt32(); //At the end, the file name
                uint unk4 = reader.ReadUInt32(); //Set to 1
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
                reader.Seek(pos + Section2Offset, System.IO.SeekOrigin.Begin);

                uint unk5 = reader.ReadUInt32();
                uint SectionSize2 = reader.ReadUInt32();
                uint unk6 = reader.ReadUInt32(); //Set to 2

                reader.Seek(pos + Section2Offset + 68, System.IO.SeekOrigin.Begin);
                uint Section2OffsetDupe = reader.ReadUInt32(); //idk
                uint unk7 = reader.ReadUInt32();
                uint Section3Offset = reader.ReadUInt32();
                uint unk8 = reader.ReadUInt32();
                uint Section35Offset = reader.ReadUInt32(); //i guess section3 is split?
                uint BufferOffset = reader.ReadUInt32();

                reader.Seek(pos + Section2Offset, System.IO.SeekOrigin.Begin);

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

        public class MaterialFile : STGenericWrapper, IChunkData
        {
            public string FileName = "";
            public byte[] Data;

            public void Read(FileReader reader)
            {
                long pos = reader.Position;

                uint unk3 = reader.ReadUInt32();
                uint SectionSize = reader.ReadUInt32(); //At the end, the file name
                uint unk4 = reader.ReadUInt32(); //Set to 1
                uint unk5 = reader.ReadUInt32();

                reader.Seek(pos, System.IO.SeekOrigin.Begin);
                //Material FILE
                Data = reader.ReadBytes((int)SectionSize);

                FileName = reader.ReadString(Syroot.BinaryData.BinaryStringFormat.ZeroTerminated);
            }
        }

        public class SkeletonFile : STGenericWrapper, IChunkData
        {
            public string FileName = "";
            public byte[] Data;

            public void Read(FileReader reader)
            {
                long pos = reader.Position;

                uint unk3 = reader.ReadUInt32();
                uint SectionSize = reader.ReadUInt32(); //At the end, the file name
                uint unk4 = reader.ReadUInt32(); //Set to 1
                uint unk5 = reader.ReadUInt32();

                reader.Seek(pos, System.IO.SeekOrigin.Begin);
                //SKEL FILE
                Data = reader.ReadBytes((int)SectionSize);

                FileName = reader.ReadString(Syroot.BinaryData.BinaryStringFormat.ZeroTerminated);
            }
        }

        public class AnimationFile : STGenericWrapper, IChunkData
        {
            public string FileName = "";
            public byte[] Data;

            public void Read(FileReader reader)
            {
                long pos = reader.Position;

                uint Hash = reader.ReadUInt32(); //Maybe a hash? Idk
                uint SectionSize = reader.ReadUInt32(); //At the end, the file name
                uint unk4 = reader.ReadUInt32(); //Set to 1
                uint unk5 = reader.ReadUInt32();

                reader.Seek(pos, System.IO.SeekOrigin.Begin);
                //ANIM FILE
                Data = reader.ReadBytes((int)SectionSize);

                FileName = reader.ReadString(Syroot.BinaryData.BinaryStringFormat.ZeroTerminated);
            }
        }

        public class TextureFile : DDS, IChunkData
        {

        }

        public class SWUTexture : STGenericTexture, IChunkData
        {
            public byte[] ImageData;

            public FileType FileType { get; set; } = FileType.Image;

            public override bool CanEdit { get; set; } = false;

            public override TEX_FORMAT[] SupportedFormats
            {
                get
                {
                    return new TEX_FORMAT[] {
                    TEX_FORMAT.R8G8B8A8_UNORM,
                };
                }
            }

            public override void OnClick(TreeView treeview)
            {
                ImageEditorBase editor = (ImageEditorBase)LibraryGUI.GetActiveContent(typeof(ImageEditorBase));
                if (editor == null)
                {
                    editor = new ImageEditorBase();
                    editor.Dock = DockStyle.Fill;
                    LibraryGUI.LoadEditor(editor);
                }

                if (GX2Surface != null)
                {
                    var tex = Bfres.Structs.FTEX.FromGx2Surface(GX2Surface, Text);
                    editor.LoadProperties(tex);
                }

                editor.Text = Text;
                editor.LoadImage(this);
            }

            GX2.GX2Surface GX2Surface;

            public void ReadChunk(FileReader reader)
            {
                reader.SetByteOrder(true);

                Console.WriteLine("TEX pos " + reader.Position);
                GX2Surface = new GX2.GX2Surface();
                GX2Surface.dim = reader.ReadUInt32();
                GX2Surface.width = reader.ReadUInt32();
                GX2Surface.height = reader.ReadUInt32();
                GX2Surface.depth = reader.ReadUInt32();
                GX2Surface.numMips = reader.ReadUInt32();
                GX2Surface.format = reader.ReadUInt32();
                GX2Surface.aa = reader.ReadUInt32();
                GX2Surface.use = reader.ReadUInt32();
                GX2Surface.imageSize = reader.ReadUInt32();
                GX2Surface.imagePtr = reader.ReadUInt32();
                GX2Surface.mipSize = reader.ReadUInt32();
                GX2Surface.mipPtr = reader.ReadUInt32();
                GX2Surface.tileMode = reader.ReadUInt32();
                GX2Surface.swizzle = reader.ReadUInt32();
                GX2Surface.alignment = reader.ReadUInt32();
                GX2Surface.pitch = reader.ReadUInt32();
                GX2Surface.mipOffset = reader.ReadUInt32s(13);
                GX2Surface.firstMip = reader.ReadUInt32();
                GX2Surface.imageCount = reader.ReadUInt32();
                GX2Surface.firstSlice = reader.ReadUInt32();
                GX2Surface.numSlices = reader.ReadUInt32();
                GX2Surface.compSel = reader.ReadBytes(4);
                GX2Surface.texRegs = reader.ReadUInt32s(4);

                RedChannel = GX2ChanneToGeneric((Syroot.NintenTools.Bfres.GX2.GX2CompSel)GX2Surface.compSel[0]);
                GreenChannel = GX2ChanneToGeneric((Syroot.NintenTools.Bfres.GX2.GX2CompSel)GX2Surface.compSel[1]);
                BlueChannel = GX2ChanneToGeneric((Syroot.NintenTools.Bfres.GX2.GX2CompSel)GX2Surface.compSel[2]);
                AlphaChannel = GX2ChanneToGeneric((Syroot.NintenTools.Bfres.GX2.GX2CompSel)GX2Surface.compSel[3]);

                if (GX2Surface.numMips > 13)
                    return;

                Width = GX2Surface.width;
                Height = GX2Surface.height;
                MipCount = GX2Surface.numMips;
                ArrayCount = GX2Surface.numArray;
                Format = Bfres.Structs.FTEX.ConvertFromGx2Format((Syroot.NintenTools.Bfres.GX2.GX2SurfaceFormat)GX2Surface.format);
            }

            private STChannelType GX2ChanneToGeneric(Syroot.NintenTools.Bfres.GX2.GX2CompSel comp)
            {
                if (comp == Syroot.NintenTools.Bfres.GX2.GX2CompSel.ChannelR) return STChannelType.Red;
                else if (comp == Syroot.NintenTools.Bfres.GX2.GX2CompSel.ChannelG) return STChannelType.Green;
                else if (comp == Syroot.NintenTools.Bfres.GX2.GX2CompSel.ChannelB) return STChannelType.Blue;
                else if (comp == Syroot.NintenTools.Bfres.GX2.GX2CompSel.ChannelA) return STChannelType.Alpha;
                else if (comp == Syroot.NintenTools.Bfres.GX2.GX2CompSel.Always0) return STChannelType.Zero;
                else return STChannelType.One;
            }

            public override void SetImageData(Bitmap bitmap, int ArrayLevel)
            {
                throw new NotImplementedException("Cannot set image data! Operation not implemented!");
            }

            public override byte[] GetImageData(int ArrayLevel = 0, int MipLevel = 0, int DepthLevel = 0)
            {
                if (GX2Surface != null)
                {
                    GX2Surface.data = ImageData;
                    GX2Surface.mipData = ImageData;

                    return GX2.Decode(GX2Surface, ArrayLevel, MipLevel);
                }
                else
                {
                    return ImageData;
                }
            }
        }

        public class TextureInfo : IChunkData
        {

        }

        public class CollisionFile : TreeNodeFile, IChunkData
        {
            public string FileName = "";
            public byte[] Data;

            //public List<CollisionMesh> Meshes = new List<CollisionMesh>();
            public List<CollisionMaterial> Materials = new List<CollisionMaterial>();
            public STSkeleton Skeleton { get; set; }
            public GenericModelRenderer Renderer;

            public DrawableContainer DrawableContainer = new DrawableContainer();

            public void Read(FileReader reader)
            {
                Skeleton = new STSkeleton();
                Renderer = new GenericModelRenderer();
                DrawableContainer.Drawables.Add(Skeleton);
                DrawableContainer.Drawables.Add(Renderer);

                this.ImageKey = "mesh";
                this.SelectedImageKey = "mesh";
                this.Checked = true;

                long pos = reader.Position;

                uint field00 = reader.ReadUInt32();
                uint field04 = reader.ReadUInt32(); 
                uint field08 = reader.ReadUInt32(); //Set to 1
                uint field0c = reader.ReadUInt32(); //Set to 3
                uint field10 = reader.ReadUInt32(); //dupe of field00?
                uint SectionSize = reader.ReadUInt32(); //At the end, the file name
                uint field18 = reader.ReadUInt32();
                uint KeyOffsetsPos = reader.ReadUInt32();

                reader.Seek(pos + KeyOffsetsPos, System.IO.SeekOrigin.Begin);

                uint MaterialCount = reader.ReadUInt32();
                uint MaterialsOffset = reader.ReadUInt32();
                uint MeshCount = reader.ReadUInt32();
                uint MeshesOffset = reader.ReadUInt32();

                reader.Seek(pos + MaterialsOffset, System.IO.SeekOrigin.Begin);

                for (int i = 0; i < MaterialCount; i++)
                {
                    CollisionMaterial material = new CollisionMaterial();
                    material.Read(reader, pos);
                    Materials.Add(material);
                }

                reader.Seek(pos + MeshesOffset, System.IO.SeekOrigin.Begin);

                for (int i = 0; i < MeshCount; i++)
                {
                    CollisionMesh mesh = new CollisionMesh();
                    mesh.Read(reader, pos);
                    //Meshes.Add(mesh);
                    Renderer.Meshes.Add(mesh);
                    Nodes.Add(mesh);
                }

                // File Name + Data
                reader.Seek(pos, System.IO.SeekOrigin.Begin);
                Data = reader.ReadBytes((int)SectionSize);
                FileName = reader.ReadString(Syroot.BinaryData.BinaryStringFormat.ZeroTerminated);
                Text = FileName;
                DrawableContainer.Name = FileName;
            }

            Viewport viewport
            {
                get
                {
                    var editor = LibraryGUI.GetObjectEditor();
                    return editor.GetViewport();
                }
                set
                {
                    var editor = LibraryGUI.GetObjectEditor();
                    editor.LoadViewport(value);
                }
            }
            bool DrawablesLoaded = false;
            public override void OnClick(TreeView treeView)
            {
                if (Runtime.UseOpenGL)
                {
                    if (viewport == null)
                    {
                        viewport = new Viewport(ObjectEditor.GetDrawableContainers());
                        viewport.Dock = DockStyle.Fill;
                    }

                    if (!DrawablesLoaded)
                    {
                        ObjectEditor.AddContainer(DrawableContainer);
                        DrawablesLoaded = true;
                    }

                    viewport.ReloadDrawables(DrawableContainer);
                    LibraryGUI.LoadEditor(viewport);

                    viewport.Text = Text;
                }
            }
        }

        public class CollisionMaterial : STGenericMaterial, IChunkData
        {
            public void Read(FileReader reader, long pos = 0)
            {
                long offsetReturn = reader.Position;

                uint matInfoOffset = reader.ReadUInt32();

                reader.Seek(pos + matInfoOffset, System.IO.SeekOrigin.Begin);

                uint Field00 = reader.ReadUInt32();
                uint Field04 = reader.ReadUInt32();
                uint Field08 = reader.ReadUInt32();
                uint NameOffset = reader.ReadUInt32();

                reader.Seek(pos + NameOffset, System.IO.SeekOrigin.Begin);

                Name = reader.ReadString(Syroot.BinaryData.BinaryStringFormat.ZeroTerminated);
                Text = Name;

                reader.Seek(offsetReturn + 4, System.IO.SeekOrigin.Begin);
            }
        }

        public class CollisionMesh : GenericRenderedObject, IChunkData
        {
            public void Read(FileReader reader, long pos = 0)
            {
                long offsetReturn = reader.Position;

                uint meshInfoOffset = reader.ReadUInt32();

                reader.Seek(pos + meshInfoOffset, System.IO.SeekOrigin.Begin);

                uint Field00 = reader.ReadUInt32();
                uint VectorCount = reader.ReadUInt32();
                uint Field08 = reader.ReadUInt32();
                uint VectorOffset = reader.ReadUInt32();
                uint FaceCount = reader.ReadUInt32();
                uint FaceInfoOffset = reader.ReadUInt32();

                reader.Seek(pos + FaceInfoOffset, System.IO.SeekOrigin.Begin);

                for (int i = 0; i < FaceCount; i++)
                {
                    uint unk0 = reader.ReadUInt32();
                    uint FaceOffset = reader.ReadUInt32();
                    uint unk1 = reader.ReadUInt32();

                    long nextInfoOffset = reader.Position;

                    reader.Seek(pos + FaceOffset, System.IO.SeekOrigin.Begin);

                    Vertex vertex1 = new Vertex();
                    vertex1.pos = reader.ReadVec3();
                    vertices.Add(vertex1);

                    Vertex vertex2 = new Vertex();
                    vertex2.pos = reader.ReadVec3();
                    vertices.Add(vertex2);

                    Vertex vertex3 = new Vertex();
                    vertex3.pos = reader.ReadVec3();
                    vertices.Add(vertex3);

                    uint unk2 = reader.ReadUInt32();

                    faces.Add(i * 3);
                    faces.Add(i * 3 + 1);
                    faces.Add(i * 3 + 2);

                    reader.Seek(nextInfoOffset, System.IO.SeekOrigin.Begin);

                }
                Text = "mesh";

                reader.Seek(offsetReturn + 4, System.IO.SeekOrigin.Begin);
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
