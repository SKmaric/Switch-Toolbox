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
                Read(reader);
            }
        }

        public void Read(FileReader reader)
        {
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
                uint Hash = reader.ReadUInt32();
                uint NameOffset = reader.ReadUInt32() + (uint)pos;
                uint ForestOffset = reader.ReadUInt32() + (uint)pos;
                uint Padding = reader.ReadUInt32();
                nextEntryPos = reader.Position;

                //Forest Name
                reader.Seek(NameOffset, System.IO.SeekOrigin.Begin);
                string Name = reader.ReadString(Syroot.BinaryData.BinaryStringFormat.ZeroTerminated);

                //Forest Index
                reader.Seek(ForestOffset, System.IO.SeekOrigin.Begin);
                uint ContentType0Count = reader.ReadUInt32();
                uint ContentType0Offset = reader.ReadUInt32() + ForestOffset;
                uint TextureCount = reader.ReadUInt32();
                uint TextureOffset = reader.ReadUInt32() + ForestOffset;
                uint ContentType2Count = reader.ReadUInt32();
                uint ContentType2Offset = reader.ReadUInt32() + ForestOffset;
                uint ContentType3Count = reader.ReadUInt32();
                uint ContentType3Offset = reader.ReadUInt32() + ForestOffset;
                Padding = reader.ReadUInt32();

                //ContentType0
                reader.Seek(ContentType0Offset, System.IO.SeekOrigin.Begin);
                for (int j = 0; j < ContentType0Count; j++)
                {
                    uint ItemOffset = reader.ReadUInt32() + ForestOffset;
                    long nextItemPos = reader.Position;

                    reader.Seek(ItemOffset, System.IO.SeekOrigin.Begin);
                    Padding = reader.ReadUInt32();
                    uint ItemHash = reader.ReadUInt32();
                    uint NodeCount = reader.ReadUInt32();
                    uint NodesOffset = reader.ReadUInt32() + ForestOffset;
                    uint NodeTranslationsOffset = reader.ReadUInt32() + ForestOffset;
                    uint NodeRotationsOffset = reader.ReadUInt32() + ForestOffset;
                    uint NodeScalesOffset = reader.ReadUInt32() + ForestOffset;
                    uint Field1C = reader.ReadUInt32();
                    uint ContentType9Count = reader.ReadUInt32();
                    uint ContentType9Offset = reader.ReadUInt32() + ForestOffset;
                    uint Field28 = reader.ReadUInt32();
                    uint Field2COffset = reader.ReadUInt32() + ForestOffset;
                    uint CameraCount = reader.ReadUInt32();
                    uint CamerasOffset = reader.ReadUInt32() + ForestOffset;
                    uint Field38 = reader.ReadUInt32();
                    uint Field3COffset = reader.ReadUInt32() + ForestOffset;
                    uint Field40 = reader.ReadUInt32();
                    uint Field44Offset = reader.ReadUInt32() + ForestOffset;
                    uint Field48Offset = reader.ReadUInt32() + ForestOffset;
                    uint AnimationCount = reader.ReadUInt32();
                    uint AnimationsOffset = reader.ReadUInt32() + ForestOffset;
                    uint Field54 = reader.ReadUInt32();
                    uint Field58Offset = reader.ReadUInt32() + ForestOffset;
                    uint Field5C = reader.ReadUInt32();
                    uint Field60Offset = reader.ReadUInt32() + ForestOffset;

                    reader.Seek(NodesOffset, System.IO.SeekOrigin.Begin);
                    for (int k = 0; k < NodeCount; k++)
                    {

                    }

                    reader.Seek(nextItemPos, System.IO.SeekOrigin.Begin);
                }

            }

            //Load vertices
            for (int v = 0; v < 100; v++)
            {
                Vertex vert = new Vertex();
                vert.pos = new Vector3(1, 5, 1);
                vert.nrm = new Vector3(1, 0, 1);
                vert.uv0 = new Vector2(1, 0);
                renderedMesh.vertices.Add(vert);
            }

            ushort[] Indices = new ushort[] { 0, 1, 2, 3, 4, 5, 6 };

            //Faces are stored in polygon groups allowing to specifically map materials to certain groups
            renderedMesh.PolygonGroups = new List<STGenericPolygonGroup>();
            var polygonGroup = new STGenericPolygonGroup();

            //for (int f = 0; f < Indices.Length; f++)
            //{
            //    polygonGroup.faces.AddRange(new int[3]
            //    {
            //               Indices[f++],
            //               Indices[f++],
            //               Indices[f]
            //    });
            //}

            renderedMesh.PolygonGroups.Add(polygonGroup);
            renderedMesh.Text = $"Mesh 0";
            Nodes.Add(renderedMesh);
            Renderer.Meshes.Add(renderedMesh);
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