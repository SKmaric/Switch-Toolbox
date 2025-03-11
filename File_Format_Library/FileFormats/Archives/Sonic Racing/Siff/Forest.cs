using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Toolbox.Library;
using Toolbox.Library.IO;
using Toolbox.Library.Rendering;
using Toolbox.Library.Forms;
using OpenTK;
using GL_EditorFramework.GL_Core;
using OpenTK.Graphics.OpenGL;
using OdysseyEditor;
using static FirstPlugin.GCDisk;

namespace Siff
{
    public class Forest : TreeNodeFile, IContextMenuNode, IFileFormat
    {
        public FileType FileType { get; set; } = FileType.Model;

        public bool CanSave { get; set; }
        public string[] Description { get; set; } = new string[] { "All Stars Racing Model" };
        public string[] Extension { get; set; } = new string[] { "*.fore" };
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public IFileInfo IFileInfo { get; set; }

        public bool Identify(System.IO.Stream stream)
        {
            using (var reader = new Toolbox.Library.IO.FileReader(stream, true))
            {
                return Utils.GetExtension(FileName) == ".fore";
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

        public ToolStripItem[] GetContextMenuItems()
        {
            return new ToolStripItem[]
            {
                //new ToolStripMenuItem("Save", null, Save, Keys.Control | Keys.S),
                new ToolStripMenuItem("Export", null, Export, Keys.Control | Keys.E)
                //new ToolStripMenuItem("Replace", null, Replace, Keys.Control | Keys.R),
            };
        }

        //Check for the viewport in the object editor
        //This is attached to it to load multiple file formats within the object editor to the viewer
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
            //Make sure opengl is enabled
            if (Runtime.UseOpenGL)
            {
                //Open the viewport
                if (viewport == null)
                {
                    viewport = new Viewport(ObjectEditor.GetDrawableContainers());
                    viewport.Dock = DockStyle.Fill;
                }

                //Make sure to load the drawables only once so set it to true!
                if (!DrawablesLoaded)
                {
                    ObjectEditor.AddContainer(DrawableContainer);
                    DrawablesLoaded = true;
                }

                //Reload which drawable to display
                viewport.ReloadDrawables(DrawableContainer);
                LibraryGUI.LoadEditor(viewport);

                viewport.Text = Text;
            }
        }

        public FORE_Renderer Renderer;

        public DrawableContainer DrawableContainer = new DrawableContainer();

        public void Load(System.IO.Stream stream)
        {
            CanSave = false;
            using (var reader = new FileReader(stream))
            {
                Read(reader, new FileReader(new byte[0]));
            }
        }

        public void Read(FileReader reader, FileReader GPUReader)
        {
            bool GPUFileLoaded = (GPUReader.Length > 0);

            GPUReader.Seek(0x3398, System.IO.SeekOrigin.Begin);

            //Set renderer
            //Load it to a drawables list
            Renderer = new FORE_Renderer();
            DrawableContainer.Name = FileName;
            DrawableContainer.Drawables.Add(Renderer);

            //Lets make a new skeleton too
            STSkeleton skeleton = new STSkeleton();

            //Note the bone class will be rewritten soon to be a bit better
            STBone bone = new STBone(skeleton); //Add the skeleton as a paramater
            bone.RotationType = STBone.BoneRotationType.Euler;
            bone.Position = new Vector3(5, 0, 0);
            bone.Scale = new Vector3(1, 1, 1);
            bone.Rotation = new Quaternion(0, 0.5f, 0, 1);
            bone.parentIndex = -1;
            skeleton.bones.Add(bone);

            //Update the skeleton bone matrices
            skeleton.reset();
            skeleton.update();

            //Create a renderable object for our mesh
            var renderedMesh = new GenericRenderedObject();
            renderedMesh.ImageKey = "mesh";
            renderedMesh.SelectedImageKey = "mesh";
            renderedMesh.Checked = true;

            reader.ByteOrder = Syroot.BinaryData.ByteOrder.LittleEndian;
            // Read Header
            long pos = reader.Position;
            uint ForestCount = reader.ReadUInt32();
            long nextEntryPos = reader.Position;

            for (int i = 0; i < ForestCount; i++)
            {
                //Forest Header
                reader.Seek(nextEntryPos, System.IO.SeekOrigin.Begin);
                uint m_iName = reader.ReadUInt32();
                uint m_pName = reader.ReadUInt32();
                uint m_pForest = reader.ReadUInt32();
                uint m_iGpuData = reader.ReadUInt32();
                nextEntryPos = reader.Position;

                //Forest Name
                reader.Seek(m_pName + pos, System.IO.SeekOrigin.Begin);
                string ForestName = reader.ReadString(Syroot.BinaryData.BinaryStringFormat.ZeroTerminated);

                //Forest Index
                reader.Seek(m_pForest + pos, System.IO.SeekOrigin.Begin);
                uint m_uNumTrees = reader.ReadUInt32();
                uint m_ppTreesOffset = reader.ReadUInt32();
                uint m_uNumTextureResources = reader.ReadUInt32();
                uint m_ppTextureResourcesOffset = reader.ReadUInt32();
                uint m_uNumGroups = reader.ReadUInt32();
                uint m_pGroupsOffset = reader.ReadUInt32();
                uint m_uNumTextures = reader.ReadUInt32();
                uint m_ppTexturesOffset = reader.ReadUInt32();
                uint m_pBlindData = reader.ReadUInt32();

                //m_ppTrees
                reader.Seek(m_ppTreesOffset + m_pForest + pos, System.IO.SeekOrigin.Begin);
                for (int j = 0; j < m_uNumTrees; j++)
                {
                    long localpos = reader.Position;

                    uint ItemOffset = reader.ReadUInt32();
                    long nextItemPos = reader.Position;

                    reader.Seek(ItemOffset + m_pForest + pos, System.IO.SeekOrigin.Begin);
                    uint m_pBlindData2 = reader.ReadUInt32();
                    uint m_uHashValue = reader.ReadUInt32();
                    uint m_uNumBranches = reader.ReadUInt32();
                    uint m_ppBranchesOffset = reader.ReadUInt32();
                    uint m_pTranslationsOffset = reader.ReadUInt32();
                    uint m_pRotationsOffset = reader.ReadUInt32();
                    uint m_pScalesOffset = reader.ReadUInt32();
                    uint m_NumTextureMatrices = reader.ReadUInt32();
                    uint m_uNumCollisionMeshes = reader.ReadUInt32();
                    uint m_ppCollisionMeshesOffset = reader.ReadUInt32();
                    uint m_uNumLights = reader.ReadUInt32();
                    uint m_ppLightsOffset = reader.ReadUInt32();
                    uint m_uNumCameras = reader.ReadUInt32();
                    uint m_ppCamerasOffset = reader.ReadUInt32();
                    uint m_uNumEmitters = reader.ReadUInt32();
                    uint m_uNumEmittersOffset = reader.ReadUInt32();
                    uint m_uNumCurves = reader.ReadUInt32();
                    uint m_ppCurvesOffset = reader.ReadUInt32();
                    uint m_pDefaultTextureTransOffset = reader.ReadUInt32();
                    uint m_uNumAnimations = reader.ReadUInt32();
                    uint m_pAnimationEntrysOffset = reader.ReadUInt32();
                    uint m_uNoFloatStreams = reader.ReadUInt32();
                    uint m_pDefaultAnimFloatsOffset = reader.ReadUInt32();
                    uint m_uNoSreamOverideIndexes = reader.ReadUInt32();
                    uint Field60Offset = reader.ReadUInt32();

                    reader.Seek(m_ppBranchesOffset, System.IO.SeekOrigin.Begin);
                    for (int k = 0; k < m_uNumBranches; k++)
                    {

                    }

                    reader.Seek(nextItemPos, System.IO.SeekOrigin.Begin);
                }

                //m_ppTrees
                reader.Seek(m_ppTextureResourcesOffset + m_pForest + pos, System.IO.SeekOrigin.Begin);
                for (int j = 0; j < m_uNumTextureResources; j++)
                {
                    uint ItemOffset = reader.ReadUInt32();
                    long nextItemPos = reader.Position;

                    reader.Seek(ItemOffset + m_pForest + pos, System.IO.SeekOrigin.Begin);
                    uint m_pNameOffset = reader.ReadUInt32();
                    uint _04 = reader.ReadUInt32();
                    uint m_pImageDataOffset = reader.ReadUInt32(); // Implement image loading
                    uint _0C = reader.ReadUInt32();

                    reader.Seek(m_pNameOffset + m_pForest + pos, System.IO.SeekOrigin.Begin);
                    string TextureName = reader.ReadString(Syroot.BinaryData.BinaryStringFormat.ZeroTerminated);

                    if (GPUFileLoaded)
                    {
                        GPUReader.Seek(m_pImageDataOffset + m_iGpuData, System.IO.SeekOrigin.Begin);
                        string Magic = GPUReader.ReadString(4);
                        if (Magic == "DDS ")
                        {
                            var header = new DDS.Header();

                            header.size = GPUReader.ReadUInt32();
                            header.flags = GPUReader.ReadUInt32();
                            header.height = GPUReader.ReadUInt32();
                            header.width = GPUReader.ReadUInt32();
                            header.pitchOrLinearSize = GPUReader.ReadUInt32();
                            header.depth = GPUReader.ReadUInt32();
                            header.mipmapCount = GPUReader.ReadUInt32();
                            header.reserved1 = new uint[11];
                            for (int k = 0; k < 11; ++k)
                                header.reserved1[k] = GPUReader.ReadUInt32();

                            header.ddspf.size = GPUReader.ReadUInt32();
                            header.ddspf.flags = GPUReader.ReadUInt32();
                            header.ddspf.fourCC = GPUReader.ReadUInt32();
                            header.ddspf.RGBBitCount = GPUReader.ReadUInt32();
                            header.ddspf.RBitMask = GPUReader.ReadUInt32();
                            header.ddspf.GBitMask = GPUReader.ReadUInt32();
                            header.ddspf.BBitMask = GPUReader.ReadUInt32();
                            header.ddspf.ABitMask = GPUReader.ReadUInt32();

                            header.caps = GPUReader.ReadUInt32();
                            header.caps2 = GPUReader.ReadUInt32();
                            header.caps3 = GPUReader.ReadUInt32();
                            header.caps4 = GPUReader.ReadUInt32();
                            header.reserved2 = GPUReader.ReadUInt32();

                            uint ddsfilesize = calculateDDSDataSize(header);

                            GPUReader.Seek(m_pImageDataOffset + m_iGpuData, System.IO.SeekOrigin.Begin);

                            var texture = new DDS(GPUReader.ReadBytes((int)ddsfilesize + 0x80));
                            texture.WiiUSwizzle = false;
                            texture.ImageKey = "texture";
                            texture.SelectedImageKey = "texture";
                            texture.Text = TextureName;
                            Nodes.Add(texture);
                        }
                    }

                    GPUReader.Seek(0, System.IO.SeekOrigin.Begin);

                    reader.Seek(nextItemPos, System.IO.SeekOrigin.Begin);
                }

                //m_ppTrees
                reader.Seek(m_pGroupsOffset + m_pForest + pos, System.IO.SeekOrigin.Begin);
                for (int j = 0; j < m_uNumGroups; j++)
                {
                    uint hash = reader.ReadUInt32();
                    uint m_uNumTreeHashes = reader.ReadUInt32();
                    uint m_pTreeHashesOffset = reader.ReadUInt32();

                    long nextItemPos = reader.Position;

                    uint[] m_pTreeHashes = new uint[m_uNumTreeHashes];

                    reader.Seek(m_pTreeHashesOffset + m_pForest + pos, System.IO.SeekOrigin.Begin);
                    for (int k = 0; k < m_uNumTreeHashes; k++)
                        m_pTreeHashes[k] = reader.ReadUInt32();

                    reader.Seek(nextItemPos, System.IO.SeekOrigin.Begin);
                }

            }

            ////Load vertices
            //for (int v = 0; v < 100; v++)
            //{
            //    Vertex vert = new Vertex();
            //    vert.pos = new Vector3(1, 5, 1);
            //    vert.nrm = new Vector3(1, 0, 1);
            //    vert.uv0 = new Vector2(1, 0);
            //    renderedMesh.vertices.Add(vert);
            //}

            //ushort[] Indices = new ushort[] { 0, 1, 2, 3, 4, 5, 6 };

            ////Faces are stored in polygon groups allowing to specifically map materials to certain groups
            //renderedMesh.PolygonGroups = new List<STGenericPolygonGroup>();
            //var polygonGroup = new STGenericPolygonGroup();

            ////for (int f = 0; f < Indices.Length; f++)
            ////{
            ////    polygonGroup.faces.AddRange(new int[3]
            ////    {
            ////               Indices[f++],
            ////               Indices[f++],
            ////               Indices[f]
            ////    });
            ////}

            //renderedMesh.PolygonGroups.Add(polygonGroup);
            //renderedMesh.Text = $"Mesh 0";
            //Nodes.Add(renderedMesh);
            //Renderer.Meshes.Add(renderedMesh);
        }

        private uint calculateDDSDataSize(DDS.Header header)
        {
            uint totalSize = 0;
            bool isCompressed = (header.ddspf.flags & 0x00000004) != 0; // Check if texture is compressed
            uint bytesPerPixel = isCompressed ? (uint)(header.ddspf.fourCC == 0x31545844 ? 8 : 16) : 4;

            uint width = header.width;
            uint height = header.height;
            uint level = 0;
            for (level = 0; level < header.mipmapCount; ++level)
            {
                totalSize += calculateMipmapSize(width, height, bytesPerPixel, isCompressed);

                if (width > 1) width /= 2;
                if (height > 1) height /= 2;
            }
            return totalSize;
        }

        uint calculateMipmapSize(uint width, uint height, uint bytesPerPixel, bool isCompressed)
        {
            if (isCompressed)
            {
                // For compressed formats (e.g., DXT1, DXT3, DXT5), the size is calculated based on blocks of 4x4 pixels
                uint blockCount = ((width + 3) / 4) * ((height + 3) / 4);
                return blockCount * bytesPerPixel; // bytesPerPixel is block size for compressed formats
            }
            else
            {
                // For uncompressed formats
                return width * height * bytesPerPixel;
            }
        }

        private void Export(object sender, EventArgs args)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Supported Formats|*.dae;";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                ExportModelSettings settings = new ExportModelSettings();
                if (settings.ShowDialog() == DialogResult.OK)
                {
                    List<STGenericMaterial> Materials = new List<STGenericMaterial>();
                    foreach (STGenericPolygonGroup poly in ((GenericModelRenderer)DrawableContainer.Drawables[1]).Meshes[0].PolygonGroups)
                        Materials.Add(poly.Material);

                    var model = new STGenericModel();
                    model.Materials = Materials;
                    model.Objects = ((GenericModelRenderer)DrawableContainer.Drawables[1]).Meshes;

                    DAE.Export(sfd.FileName, settings.Settings, model, new List<STGenericTexture>(), ((STSkeleton)DrawableContainer.Drawables[0]));
                }
            }
        }
        public void Unload()
        {

        }

        public void Save(System.IO.Stream stream)
        {
        }

        public class MaterialTextureMap : STGenericMatTexture
        {
            //The index of a texture
            //Some formats will map them by index, some by name, some by a hash, it's up to how the user handles it
            public int TextureIndex { get; set; }
        }

        public class FORE_Renderer : GenericModelRenderer
        {
            //A list of textures to display on the model
            public List<STGenericTexture> TextureList = new List<STGenericTexture>();

            public override void OnRender(GLControl control)
            {
                //Here we can add things on each frame rendered
            }

            //Render data to display by per material and per mesh
            public override void SetRenderData(STGenericMaterial mat, ShaderProgram shader, STGenericObject m)
            {
            }

            //Custom bind texture method
            public override int BindTexture(STGenericMatTexture tex, ShaderProgram shader)
            {
                //By default we bind to the default texture to use
                //This will be used if no texture is found
                GL.ActiveTexture(TextureUnit.Texture0 + tex.textureUnit + 1);
                GL.BindTexture(TextureTarget.Texture2D, RenderTools.defaultTex.RenderableTex.TexID);

                string activeTex = tex.Name;

                //We want to cast our custom texture map class to get any custom properties we may need
                //If you don't need any custom way of mapping, you can just stick with the generic one
                var matTexture = (MaterialTextureMap)tex;

                //Go through our texture maps in the material and see if the index matches
                foreach (var texture in TextureList)
                {
                    if (TextureList.IndexOf(texture) == matTexture.TextureIndex)
                    {
                        BindGLTexture(tex, shader, TextureList[matTexture.TextureIndex]);
                        return tex.textureUnit + 1;
                    }

                    //You can also check if the names match
                    if (texture.Text == tex.Name)
                    {
                        BindGLTexture(tex, shader, TextureList[matTexture.TextureIndex]);
                        return tex.textureUnit + 1;
                    }
                }

                //Return our texture uint id. 
                return tex.textureUnit + 1;
            }
        }
    }
}