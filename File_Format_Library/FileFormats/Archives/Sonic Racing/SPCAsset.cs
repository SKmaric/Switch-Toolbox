using Collada141;
using CSCore.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using DataType = SPC.DataType;

namespace FirstPlugin
{
    // Adapted from ASRT_CpuGpuTool
    // https://github.com/Tyaap/ASRT_CpuGpuTool

    public class SPCAsset
    {
        public IChunkData ChunkData;
        // todo: more subclasses of asset
        public MemoryStream msCpuData; // todo: individual memory streams for each asset
        public MemoryStream msGpuData; // todo: individual memory streams for each asset
        public int assetNumber = -1;
        public DataType dataType;
        public int toolVersion;
        public int cpuOffsetDataHeader;
        public int cpuOffsetData;
        public int cpuDataLength;
        public int cpuOffsetPointersHeader;
        public int cpuOffsetPointers;
        public int cpuPointersLength;
        public int cpuRelativeOffsetNextEntry;
        public int gpuOffsetData;
        public int gpuDataLength;
        public int gpuRelativeOffsetNextEntry;
        public int unknown;
        public string name;
        public uint id;
        public List<SPCAsset> references = new List<SPCAsset>();
        public List<SPCAsset> referees = new List<SPCAsset>();

        public override int GetHashCode()
        {
            return (int)id;
        }

        public override bool Equals(object obj)
        {
            SPCAsset entry = obj as SPCAsset;
            return entry != null && entry.id == id;
        }
    }

    public class SPCNode : SPCAsset
    {
        public SPCNode() { }
        public SPCNode(SPCAsset entry)
        {
            assetNumber = entry.assetNumber;
            dataType = entry.dataType;
            toolVersion = entry.toolVersion;
            cpuOffsetDataHeader = entry.cpuOffsetDataHeader;
            cpuOffsetData = entry.cpuOffsetData;
            cpuDataLength = entry.cpuDataLength;
            cpuOffsetPointersHeader = entry.cpuOffsetPointersHeader;
            cpuOffsetPointers = entry.cpuOffsetPointers;
            cpuPointersLength = entry.cpuPointersLength;
            cpuRelativeOffsetNextEntry = entry.cpuRelativeOffsetNextEntry;
            gpuOffsetData = entry.gpuOffsetData;
            gpuDataLength = entry.gpuDataLength;
            gpuRelativeOffsetNextEntry = entry.gpuRelativeOffsetNextEntry;
            unknown = entry.unknown;
            name = entry.name;
            id = entry.id;
        }

        public string shortName;
        public List<SPCNode> definition = new List<SPCNode>();
        public List<SPCNode> parent = new List<SPCNode>();
        public List<SPCNode> daughters = new List<SPCNode>();
        public List<SPCNode> instances = new List<SPCNode>();
    }

    public class SPCResource : SPCAsset
    {
        public SPCResource() { }
        public SPCResource(SPCAsset entry)
        {
            assetNumber = entry.assetNumber;
            dataType = entry.dataType;
            toolVersion = entry.toolVersion;
            cpuOffsetDataHeader = entry.cpuOffsetDataHeader;
            cpuOffsetData = entry.cpuOffsetData;
            cpuDataLength = entry.cpuDataLength;
            cpuOffsetPointersHeader = entry.cpuOffsetPointersHeader;
            cpuOffsetPointers = entry.cpuOffsetPointers;
            cpuPointersLength = entry.cpuPointersLength;
            cpuRelativeOffsetNextEntry = entry.cpuRelativeOffsetNextEntry;
            gpuOffsetData = entry.gpuOffsetData;
            gpuDataLength = entry.gpuDataLength;
            gpuRelativeOffsetNextEntry = entry.gpuRelativeOffsetNextEntry;
            unknown = entry.unknown;
            name = entry.name;
            id = entry.id;
        }
    }
    public interface IChunkData { }
}
