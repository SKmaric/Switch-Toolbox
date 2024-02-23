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
        public COLI_Renderer Renderer;

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
            Renderer = new COLI_Renderer();
            DrawableContainer.Drawables.Add(Skeleton);
            DrawableContainer.Drawables.Add(Renderer);

            GenericRenderedObject ColiMesh = new GenericRenderedObject();
            ColiMesh.ImageKey = "mesh";
            ColiMesh.SelectedImageKey = "mesh";
            ColiMesh.Checked = true;
            ColiMesh.Text = "track.CollisionData";

            long pos = reader.Position;

            // endian check
            reader.ByteOrder = Syroot.BinaryData.ByteOrder.BigEndian;
            uint Hash = reader.ReadUInt32();
            if (Hash == 0x64F8FC38)
                reader.ByteOrder = Syroot.BinaryData.ByteOrder.LittleEndian;

            uint Unknown = reader.ReadUInt32();

            // Read header
            uint VertexCount = reader.ReadUInt32();
            uint FaceCount = reader.ReadUInt32();
            uint Count3 = reader.ReadUInt32();
            uint Count4 = reader.ReadUInt32();

            MaxPosition = reader.ReadVec3();
            MinPosition = reader.ReadVec3();

            uint VertexPositionOffset = reader.ReadUInt32();
            uint FaceOffset = reader.ReadUInt32();
            uint Offset3 = reader.ReadUInt32();
            uint Offset4 = reader.ReadUInt32();
            uint Offset5 = reader.ReadUInt32();
            uint FileNameOffset = reader.ReadUInt32();

            //Load vertices
            reader.SeekBegin(pos + VertexPositionOffset);
            for (int i = 0; i < VertexCount; i++)
            {
                Vertex vertex = new Vertex();
                vertex.pos = reader.ReadVec3();
                ColiMesh.vertices.Add(vertex);
                
                float UnknownV = reader.ReadSingle();
            }

            // Load faces
            List<ushort> Faces = new List<ushort>();
            List<List<int>> FaceMats = new List<List<int>>();
            List<uint> MatNames = new List<uint>();
            //Dictionary<uint, uint> MatNames = new Dictionary<uint, uint>();

            int curMatID = 0;
            reader.SeekBegin(pos + FaceOffset);
            for (int i = 0; i < FaceCount; i++)
            {
                Faces.Add(reader.ReadUInt16());
                Faces.Add(reader.ReadUInt16());
                Faces.Add(reader.ReadUInt16());
                byte Unknown1 = reader.ReadByte();
                byte Unknown2 = reader.ReadByte();
                uint MatHash = reader.ReadUInt32();

                if (!MatNames.Contains(MatHash))
                {
                    MatNames.Add(MatHash);
                    FaceMats.Add(new List<int>());
                    curMatID += 1;
                }
                FaceMats[MatNames.IndexOf(MatHash)].Add(i);
            }

            for (int i = 0; i < MatNames.Count; i++)
            {
                var submsh = new STGenericPolygonGroup();
                submsh.PrimativeType = STPrimitiveType.Triangles;
                submsh.Material = new STGenericMaterial();
                submsh.Material.Text = AssignHashName(MatNames[i].ToString("X8"));
                var submeshfaces = new List<int>();

                for (int j = 0; j < FaceMats[i].Count; j++)
                {
                    submeshfaces.Add(Faces[(3 * FaceMats[i][j])]);
                    submeshfaces.Add(Faces[(3 * FaceMats[i][j])+1]);
                    submeshfaces.Add(Faces[(3 * FaceMats[i][j])+2]);
                }

                submsh.faces = submeshfaces;
                ColiMesh.PolygonGroups.Add(submsh);
            }

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

        public class COLI_Renderer : GenericModelRenderer
        {

            public override void OnRender(GLControl control)
            {
                //Here we can add things on each frame rendered
            }

            //Render data to display by per material and per mesh
            public override void SetRenderData(STGenericMaterial mat, ShaderProgram shader, STGenericObject m)
            {
            }
        }

        public string AssignHashName(string typeHash)
        {
            var NameLookupDictionary = new Dictionary<string, string>()
            { //WIP
                {"B6496F4C", "track_asphalt"},
                {"A15592BF", "track_asphalt_semi"},
                //{"", "track_castle_cobbles"},
                //{"", "track_hard_snow"},
                //{"", "track_deep_snow"},
                //{"", "track_dual_phase_ice"},
                //{"", "track_stone"},
                //{"", "track_stone_water_beach"},
                //{"", "offtrack_stone_water"},
                //{"", "track_stone_crumbling"},
                {"678E49B6", "track_sand"},
                //{"", "track_wet_sand"},
                {"13E77BAE", "track_wooden_boards"},
                {"D4C68E4F", "track_rickety_wooden_boards"},
                //{"", "track_ropebridge"},
                //{"", "track_steelwirebridge"},
                //{"", "track_BillyMountainRock"},
                //{"", "track_solidifiedlava"},
                {"9F0AE461", "track_grass"},
                //{"", "track_grass_no_skid"},
                //{"", "track_grass_hotd"},
                {"C1276C22", "track_grasschevron"},
                //{"", "track_long_grass"},
                //{"", "offtrack_vegetation"},
                //{"", "offtrack_lava"},
                //{"", "track_bluemonkeystone"},
                //{"", "track_brownmonkeystone"},
                //{"", "track_sambarooftop1"},
                //{"", "track_sambarooftop2"},
                //{"", "track_technometal"},
                //{"", "track_technometalmesh"},
                //{"", "track_technometalrunoff"},
                //{"", "track_mud"},
                //{"", "track_SMB_HarshSlow"},
                //{"", "track_metal"},
                //{"", "offtrack_metal"},
                //{"", "track_canvas"},
                //{"", "track_packed_dirt"},
                //{"", "track_puddledirt"},
                //{"", "offtrack_loose_dirt"},
                //{"", "track_plastic"},
                //{"", "track_casinosmooth"},
                //{"", "offtrack_casinobumpy"},
                //{"", "track_woodsmooth"},
                //{"", "track_tilessmooth"},
                //{"", "track_tileswater"},
                //{"", "track_carpet"},
                //{"", "track_carpetred"},
                //{"", "track_carpetblue"},
                //{"", "track_felt"},
                //{"", "offtrack_carpet"},
                //{"", "track_glass"},
                //{"", "track_rubber"},
                //{"", "track_roofslate"},
                //{"", "offtrack_leafgutter"},
                //{"", "track_reflective"},
                //{"", "offtrack_asphalt"},
                //{"", "offtrack_pavement"},
                //{"", "offtrack_grass"},
                //{"", "offtrack_long_grass"},
                //{"", "offtrack_sand"},
                //{"", "offtrack_sand_water"},
                //{"", "offtrack_deep_snow"},
                //{"", "barrier_bouncy"},
                //{"", "barrier"},
                //{"", "barrier_woodsplinters"},
                //{"", "barrier_woodsplintersred"},
                //{"", "barrier_rock"},
                //{"", "barrier_stone"},
                //{"", "barrier_stonegreen"},
                //{"", "barrier_stonebeige"},
                //{"", "barrier_greyconcrete"},
                //{"", "barrier_whitechips"},
                //{"", "barrier_sandstone"},
                //{"", "barrier_egyptstone"},
                //{"", "barrier_castlebrick"},
                //{"", "barrier_Lava"},
                //{"", "barrier_volcano"},
                //{"", "barrier_ice"},
                //{"", "barrier_snow"},
                //{"", "barrier_pinball"},
                //{"", "barrier_metal"},
                //{"", "barrier_shinymetalrail"},
                //{"", "barrier_wirefence"},
                //{"", "barrier_hedgerow"},
                //{"", "barrier_metalgate"},
                //{"", "barrier_low_bounce"},
                //{"", "asphalt"},
                //{"", "grass"},
                //{"", "sand"},
                //{"", "rock"},
                //{"", "watersplash"},
                //{"", "invisible"},
                {"30D13D2D", "reset"},
                //{"", "track_jumps"}
            };

            if (NameLookupDictionary.ContainsKey(typeHash))
                return NameLookupDictionary[typeHash];
            else
                return typeHash;
        }
    }
}