using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Toolbox.Library;
using Toolbox.Library.IO;
using SPC;
using DataType = SPC.DataType;
using System.Drawing;
using System.Windows.Forms;
using Toolbox.Library.Forms;
using Toolbox.Library.NodeWrappers;
using Toolbox.Library.Rendering;
using ZeldaLib.ZeldaSceneInfo.Headers.Rooms;
using SPICA.Rendering;

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

        private List<ChunkHeader> Chunks = new List<ChunkHeader>();
        public void Load(System.IO.Stream stream)
        {
            CanSave = false;

            using (var reader = new FileReader(stream))
            {
                int test = reader.ReadInt32();
                bool isLittleEndian = Enum.IsDefined(typeof(DataType), test);
                reader.SetByteOrder(!isLittleEndian);

                reader.Seek(-4, SeekOrigin.Current);

                while (!reader.EndOfStream)
                {
                    ChunkHeader chunk = new ChunkHeader();
                    chunk.Position = reader.Position;
                    chunk.Identifier = (DataType)reader.ReadUInt32();
                    chunk.ToolVersion = reader.ReadUInt32();
                    chunk.CpuRelativeOffsetNextEntry = reader.ReadUInt32();
                    chunk.CpuDataLength = reader.ReadUInt32();
                    chunk.GpuRelativeOffsetNextEntry = reader.ReadUInt32();
                    chunk.GpuDataLength = reader.ReadUInt32();
                    uint unk2 = reader.ReadUInt32();
                    uint unk3 = reader.ReadUInt32();
                    Chunks.Add(chunk);

                    uint resourceid = reader.ReadUInt32();
                    uint tmpOffset = 0;

                    if (chunk.Identifier != DataType.Nothing)
                    {
                        if (unk3 != 0)
                        {
                            if (chunk.Identifier != DataType.SlResourceCollision)
                            {
                                tmpOffset = reader.ReadUInt32();
                            }
                            else
                            {
                                reader.Seek(chunk.Position + 0x40, SeekOrigin.Begin);
                                tmpOffset = 0x30 + reader.ReadUInt32() * 0x14;
                            }
                            if (tmpOffset != 0)
                            {
                                reader.Seek(chunk.Position + 0x20 + tmpOffset, SeekOrigin.Begin);
                                chunk.FileName = reader.ReadString(Syroot.BinaryData.BinaryStringFormat.ZeroTerminated);
                            }
                        }
                        //else
                        //{
                        //    reader.Seek(chunk.Position + 0x20 + 0x18 + 0x1C, SeekOrigin.Begin);
                        //    tmpOffset = reader.ReadUInt32();
                        //    if (tmpOffset != 0)
                        //    {
                        //        reader.Seek(chunk.Position + 0x20 + tmpOffset, SeekOrigin.Begin);
                        //        chunk.FileName = reader.ReadString(Syroot.BinaryData.BinaryStringFormat.ZeroTerminated);
                        //    }
                        //}
                    }

                    reader.Seek(chunk.Position + 0x20, System.IO.SeekOrigin.Begin);

                    switch (chunk.Identifier)
                    {
                        case DataType.SlTexture:
                            if (chunk.CpuRelativeOffsetNextEntry > 0x64)
                            {
                                if (tmpOffset == 0xD4) // determine Wii U format texture
                                {
                                    SWUTexture texture = new SWUTexture();
                                    texture.ImageKey = "texture";
                                    texture.SelectedImageKey = "texture";
                                    reader.SeekBegin(chunk.Position + 72);
                                    texture.ReadChunk(reader);
                                    chunk.ChunkData = texture;
                                    texture.Text = chunk.FileName;
                                    Nodes.Add(texture);
                                }
                                else
                                {
                                    chunk.ChunkData = new TextureFile();
                                }
                                //reader.Seek(chunk.Position + 0x64, System.IO.SeekOrigin.Begin);
                                //chunk.FileName = reader.ReadString(Syroot.BinaryData.BinaryStringFormat.ZeroTerminated);
                            }
                            break;
                        case DataType.Nothing:
                            break;
                        case DataType.SeDefinitionAnimationStreamNode:
                            if (chunk.CpuRelativeOffsetNextEntry > 0xB0)
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
                            chunk.FileName = collisionFile.FileName;
                            chunk.ChunkData = collisionFile;
                            chunk.FileName = chunk.FileName.Replace(':', '/');
                            chunk.FileName = chunk.FileName.Replace('|', '/');
                            collisionFile.Text = chunk.FileName;
                            Nodes.Add(collisionFile);
                            break;
                    }

                    chunk.FileName = chunk.FileName.Replace(':', '/');
                    chunk.FileName = chunk.FileName.Replace('|', '/');

                    chunk.FileName = chunk.FileName.RemoveIllegaleFolderNameCharacters();

                    reader.Seek(chunk.Position + chunk.CpuRelativeOffsetNextEntry, System.IO.SeekOrigin.Begin);
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
                    if (Chunks[i].GpuDataLength != 0 || Chunks[i].FileName != string.Empty || Chunks[i].ChunkData != null)
                    {
                        long pos = reader.Position;

                        var identifer = Chunks[i].Identifier;

                        var fileInfo = new FileInfo();

                        //Get CPU chunk data
                        if (Chunks[i].ChunkData != null)
                        {
                            if (Chunks[i].ChunkData is AnimationFile)
                            {
                                AnimationFile animFile = (AnimationFile)Chunks[i].ChunkData;
                                fileInfo.FileName = animFile.FileName;
                                fileInfo.FileData = animFile.Data;
                            }
                            if (Chunks[i].ChunkData is SkeletonFile)
                            {
                                SkeletonFile skelFile = (SkeletonFile)Chunks[i].ChunkData;
                                fileInfo.FileName = skelFile.FileName;
                                fileInfo.FileData = skelFile.Data;
                            }
                            if (Chunks[i].ChunkData is MaterialFile)
                            {
                                MaterialFile matFile = (MaterialFile)Chunks[i].ChunkData;
                                fileInfo.FileName = matFile.FileName;
                                fileInfo.FileData = matFile.Data;
                            }
                            if (Chunks[i].ChunkData is ModelFile)
                            {
                                ModelFile modelFile = (ModelFile)Chunks[i].ChunkData;
                                fileInfo.FileName = modelFile.FileName;

                                byte[] BufferData = new byte[0];
                                if (Chunks[i].GpuDataLength != 0)
                                    BufferData = reader.ReadBytes((int)Chunks[i].GpuDataLength);

                                fileInfo.FileData = Utils.CombineByteArray(modelFile.Data, modelFile.Data2, modelFile.Data3, BufferData);


                                //Don't advance the stream unless the chunk has a pointer
                                if (Chunks[i].GpuRelativeOffsetNextEntry != 0)
                                    reader.Seek(pos + Chunks[i].GpuRelativeOffsetNextEntry, System.IO.SeekOrigin.Begin);
                            }
                            if (Chunks[i].ChunkData is CollisionFile)
                            {
                                CollisionFile collisionFile = (CollisionFile)Chunks[i].ChunkData;
                                fileInfo.FileName = collisionFile.FileName;
                                fileInfo.FileData = collisionFile.Data;
                            }
                            if (Chunks[i].ChunkData is SWUTexture)
                            {
                                SWUTexture texFile = (SWUTexture)Chunks[i].ChunkData;
                                if (Chunks[i].GpuDataLength != 0)
                                    texFile.ImageData = reader.ReadBytes((int)Chunks[i].GpuDataLength);
                            }
                            if (Chunks[i].ChunkData is TextureFile)
                            {
                                TextureFile texFile = (TextureFile)Chunks[i].ChunkData;

                                if (Chunks[i].GpuDataLength != 0)
                                {
                                    fileInfo.FileData = reader.ReadBytes((int)Chunks[i].GpuDataLength);
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
                                fileInfo.FileName = $"{i} {Chunks[i].CpuDataLength} {identifer.ToString("X")}";

                            if (Chunks[i].GpuDataLength != 0)
                                fileInfo.FileData = reader.ReadBytes((int)Chunks[i].GpuDataLength);
                            else
                                fileInfo.FileData = new byte[0];
                        }

                        //Organise files such as mb into folders - won't be necessary when actual loading is implemented
                        fileInfo.FileName = fileInfo.FileName.Replace(':', '/');
                        fileInfo.FileName = fileInfo.FileName.Replace('|', '/');

                        files.Add(fileInfo);

                        //Don't advance the stream unless the chunk has a pointer
                        if (Chunks[i].GpuRelativeOffsetNextEntry != 0)
                            reader.Seek(pos + Chunks[i].GpuRelativeOffsetNextEntry, System.IO.SeekOrigin.Begin);
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
            public uint ToolVersion;
            public uint CpuRelativeOffsetNextEntry;
            public uint CpuDataLength;
            public uint GpuRelativeOffsetNextEntry;
            public uint GpuDataLength;

            public string FileName = "";
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

                Name = reader.ReadString();
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
