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
using OpenTK;
using GL_EditorFramework.GL_Core;
using OpenTK.Graphics.OpenGL;
using System.IO;
using FirstPlugin.FileFormats.Archives.Sonic_Racing;

namespace Siff
{
    public class CollisionData : TreeNodeFile, IContextMenuNode, IFileFormat
    {
        public FileType FileType { get; set; } = FileType.Collision;

        public bool CanSave { get; set; }
        public string[] Description { get; set; } = new string[] { "All Stars Racing Collision" };
        public string[] Extension { get; set; } = new string[] { "*.coli" };
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public IFileInfo IFileInfo { get; set; }

        public bool Identify(System.IO.Stream stream)
        {
            using (var reader = new Toolbox.Library.IO.FileReader(stream, true))
            {
                uint sigcheck = reader.ReadUInt32();
                return (sigcheck == 0x64F8FC38 || sigcheck == 0x38FCF864) && Utils.GetExtension(FileName) == ".coli";
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

        public STSkeleton Skeleton { get; set; }
        public GenericModelRenderer Renderer;

        public DrawableContainer DrawableContainer = new DrawableContainer();

        public Vector3 MaxPosition = new Vector3(0);
        public Vector3 MinPosition = new Vector3(0);

        public void Load(System.IO.Stream stream)
        {
            CanSave = false;

            DrawableContainer.Name = FileName;

            using (var reader = new FileReader(stream))
            {
                Read(reader);
            }
        }

        public void Read(FileReader reader)
        {
            Skeleton = new STSkeleton();
            Renderer = new GenericModelRenderer();
            DrawableContainer.Drawables.Add(Skeleton);
            DrawableContainer.Drawables.Add(Renderer);

            this.ImageKey = "mesh";
            this.SelectedImageKey = "mesh";
            this.Checked = true;

            GenericRenderedObject ColiMesh = new GenericRenderedObject();
            ColiMesh.ImageKey = "mesh";
            ColiMesh.SelectedImageKey = "mesh";
            ColiMesh.Checked = true;

            long pos = reader.Position;

            // Read header
            uint m_nameHash = reader.ReadUInt32();
            this.Text = SiffHashes.sumohash_t_ToString(m_nameHash);

            uint m_collisionVersion = reader.ReadUInt32();
            uint m_numVertices = reader.ReadUInt32();
            uint m_numTriangles = reader.ReadUInt32();
            uint m_numOctreeNodes = reader.ReadUInt32();
            uint m_numOctreeTriangleIndices = reader.ReadUInt32();
            MinPosition = reader.ReadVec3();
            MaxPosition = reader.ReadVec3();


            uint m_pVerticesOffset = reader.ReadUInt32();
            uint m_pTrianglesOffset = reader.ReadUInt32();
            uint m_pTriangleNormalsOffset = reader.ReadUInt32();
            uint m_pOctreeNodesOffset = reader.ReadUInt32();
            uint m_pOctreeTriangleIndicesOffset = reader.ReadUInt32();

            uint FileNameOffset = reader.ReadUInt32();

            //Load vertices
            reader.SeekBegin(pos + m_pVerticesOffset);
            for (int i = 0; i < m_numVertices; i++)
            {
                Vertex vertex = new Vertex();
                vertex.pos = reader.ReadVec3();
                ColiMesh.vertices.Add(vertex);
                
                float UnknownW = reader.ReadSingle();
            }

            // Load faces
            List<ushort> Faces = new List<ushort>();
            List<List<int>> FaceMats = new List<List<int>>();
            List<string> MatNames = new List<string>();
            //Dictionary<uint, uint> MatNames = new Dictionary<uint, uint>();

            //int curMatID = 0;
            reader.SeekBegin(pos + m_pTrianglesOffset);
            for (int i = 0; i < m_numTriangles; i++)
            {
                Faces.Add(reader.ReadUInt16());
                Faces.Add(reader.ReadUInt16());
                Faces.Add(reader.ReadUInt16());
                byte Unknown1 = reader.ReadByte();
                byte Unknown2 = reader.ReadByte();
                uint surfaceTypeHash = reader.ReadUInt32();

                string surfaceType = SiffHashes.sumohash_t_ToString(surfaceTypeHash);

                if (!MatNames.Contains(surfaceType))
                {
                    MatNames.Add(surfaceType);
                    FaceMats.Add(new List<int>());
                    //curMatID += 1;
                }
                FaceMats[MatNames.IndexOf(surfaceType)].Add(i);
            }

            for (int i = 0; i < FaceMats.Count; i++)
            {
                var submsh = new STGenericPolygonGroup();
                submsh.PrimativeType = STPrimitiveType.Triangles;
                submsh.Material = new STGenericMaterial();
                submsh.Material.Text = MatNames[i];
                var submeshfaces = new List<int>();

                for (int j = 0; j < FaceMats[i].Count; j++)
                {
                    submeshfaces.Add(Faces[(3 * FaceMats[i][j])]);
                    submeshfaces.Add(Faces[(3 * FaceMats[i][j]) + 1]);
                    submeshfaces.Add(Faces[(3 * FaceMats[i][j]) + 2]);
                }

                submsh.faces = submeshfaces;
                ColiMesh.PolygonGroups.Add(submsh);
            }

            reader.SeekBegin(pos + FileNameOffset);
            FileName = reader.ReadString(Syroot.BinaryData.BinaryStringFormat.ZeroTerminated);
            DrawableContainer.Name = FileName;

            ColiMesh.Text = "mesh";

            Renderer.Meshes.Add(ColiMesh);
            Nodes.Add(ColiMesh);
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
    }
}