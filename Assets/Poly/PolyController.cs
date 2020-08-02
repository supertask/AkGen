using UnityEngine;
using UnityEngine.Rendering;
using System.Runtime.InteropServices;
using System;

namespace Akvfx {

public class PolyController: MonoBehaviour
{
    //ボロノイ図 -> ドロネー図
    // https://www.ieice.org/publications/conference-FIT-DVDs/FIT2006/pdf/J/J_036.pdf

    #region Editable attribute

    [SerializeField] DeviceController _device = null;
    [SerializeField] public RenderTexture debugMap;
    [SerializeField] public RenderTexture debugLinesMap;
    [SerializeField] public Material _material = null;

    #endregion


    #region Public accessor properties

    public RenderTexture CellularMap => _cellularMap; //Cellular
    public RenderTexture SeedMap => _seedMap; //Coordinates map of seed point
    public RenderTexture ExistingLineMap => _existingLineMap; //Cellular

    public ComputeShader polyCompute;

    #endregion

    #region Private members
    RenderTexture _cellularMap;
    RenderTexture _seedMap;
    RenderTexture _existingLineMap;
    MaterialPropertyBlock _props;
    public int maxPointNum = 1024;
    public ComputeBuffer pointsBuffer;
    public ComputeBuffer trianglesBuffer;
    public ComputeBuffer triangleCountsBuffer;
    private int[] triangleCounts;

    #endregion

    #region Shader property IDs

    static class ShaderID
    {
        public static int CellularMap    = Shader.PropertyToID("CellularMap");
        public static int SeedMap    = Shader.PropertyToID("SeedMap");
        public static int ExistingLineMap    = Shader.PropertyToID("ExistingLineMap");
        public static int MaxPointNum    = Shader.PropertyToID("MaxPointNum");
        public static int rPoints    = Shader.PropertyToID("rPoints");
        public static int wPoints    = Shader.PropertyToID("wPoints");
        public static int rTriangles    = Shader.PropertyToID("rTriangles");
        public static int wTriangles    = Shader.PropertyToID("wTriangles");
        public static int pointIndex    = Shader.PropertyToID("pointIndex");
    }

    static class KID
    {
        public static int GetPoints;
        public static int ResetCellularNoise;
        public static int ResetExistingLineMap;
        public static int CalcCellularNoise;
        public static int ModifyCellularNoise;
        public static int CalcDelaunayTriangulationLine;
    }

    #endregion

    #region MonoBehaviour implementation

    void Start()
    {
        KID.GetPoints = this.polyCompute.FindKernel("GetPoints");
        KID.ResetCellularNoise = this.polyCompute.FindKernel("ResetCellularNoise");
        KID.ResetExistingLineMap = this.polyCompute.FindKernel("ResetExistingLineMap");
        KID.CalcCellularNoise = this.polyCompute.FindKernel("CalcCellularNoise");
        KID.ModifyCellularNoise = this.polyCompute.FindKernel("ModifyCellularNoise");
        KID.CalcDelaunayTriangulationLine = this.polyCompute.FindKernel("CalcDelaunayTriangulationLine");

        this.pointsBuffer = new ComputeBuffer
            (maxPointNum, Marshal.SizeOf(typeof(Vector2Int)), ComputeBufferType.Append);
        this.pointsBuffer.SetCounterValue(0);

        this.trianglesBuffer = new ComputeBuffer
            (maxPointNum * 2, Marshal.SizeOf(typeof(Vector3Int)), ComputeBufferType.Append);
        this.trianglesBuffer.SetCounterValue(0);

        this.triangleCountsBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);
        this.triangleCounts = new int[]{ 0, 1, 0, 0 };
        this.triangleCountsBuffer.SetData(triangleCounts);

        // Temporary objects for conversion
        var width = ThreadedDriver.ImageWidth;
        var height = ThreadedDriver.ImageHeight;

        this._cellularMap = GetRenderTexture(width, height);
        this._seedMap = GetRenderTexture(width, height);
        this._existingLineMap = GetRenderTexture(this.maxPointNum, this.maxPointNum);
    }

    private RenderTexture GetRenderTexture(int width, int height)
    {
        var renderTexture = new RenderTexture
          (width, height, 0, RenderTextureFormat.ARGBFloat);
        renderTexture.enableRandomWrite = true;
        renderTexture.Create();
        return renderTexture;
    }

    void OnDestroy()
    {
        if (_cellularMap    != null) { Destroy(_cellularMap); }
        if (_seedMap    != null) { Destroy(_seedMap); }
        if (_existingLineMap    != null) { Destroy(_existingLineMap); }
        pointsBuffer?.Dispose();
        trianglesBuffer?.Dispose();
        triangleCountsBuffer?.Dispose();
    }

    unsafe void Update()
    {
        //_compute.SetFloat(ID.MaxDepth, _deviceSettings.maxDepth);
        //_compute.SetBuffer(0, ID.XYTable, _xyTable);

        //var sw = new System.Diagnostics.Stopwatch();
        //sw.Start(); //計測開始

        //Reset
        this.polyCompute.SetTexture(KID.ResetCellularNoise, ShaderID.CellularMap, _cellularMap);
        this.polyCompute.SetTexture(KID.ResetCellularNoise, ShaderID.SeedMap, _seedMap);
        this.polyCompute.Dispatch(
            KID.ResetCellularNoise, this._cellularMap.width / 32, this._cellularMap.height / 32, 1);

        this.polyCompute.SetTexture(KID.ResetExistingLineMap, ShaderID.ExistingLineMap, _existingLineMap);
        this.polyCompute.Dispatch(
            KID.ResetExistingLineMap, this._existingLineMap.width / 32, this._existingLineMap.height / 32, 1);

        //Get mesh points
        this.polyCompute.SetInt(ShaderID.MaxPointNum, maxPointNum);
        this.polyCompute.SetBuffer(KID.GetPoints, ShaderID.wPoints, this.pointsBuffer);
        this.polyCompute.SetTexture(KID.GetPoints, DeviceController.ID.BodyIndexMap, _device.BodyIndexMap);
        this.polyCompute.SetTexture(KID.GetPoints, DeviceController.ID.EdgeMap, _device.EdgeMap);
        this.polyCompute.SetTexture(KID.GetPoints, ShaderID.CellularMap, _cellularMap);
        this.polyCompute.Dispatch(
            KID.GetPoints, this._cellularMap.width / 32, this._cellularMap.height / 32, 1);

        //Cellular Noise Calculation
        this.polyCompute.SetInt(ShaderID.MaxPointNum, maxPointNum);
        this.polyCompute.SetBuffer(KID.CalcCellularNoise, ShaderID.rPoints, this.pointsBuffer);
        this.polyCompute.SetTexture(KID.CalcCellularNoise, DeviceController.ID.BodyIndexMap, _device.BodyIndexMap);
        this.polyCompute.SetTexture(KID.CalcCellularNoise, ShaderID.CellularMap, _cellularMap);
        this.polyCompute.SetTexture(KID.CalcCellularNoise, ShaderID.SeedMap, _seedMap);
        this.polyCompute.Dispatch(
            KID.CalcCellularNoise, maxPointNum / 512, 1, 1);

        //Cellular Noise Modification
        this.polyCompute.SetTexture(KID.ModifyCellularNoise, ShaderID.CellularMap, _cellularMap);
        this.polyCompute.SetTexture(KID.ModifyCellularNoise, ShaderID.SeedMap, _seedMap);
        this.polyCompute.Dispatch(
            KID.ModifyCellularNoise, this._cellularMap.width / 32, this._cellularMap.height / 32, 1);

        //Calc DelaunayTriangulationLine
        this.polyCompute.SetInt(ShaderID.MaxPointNum, maxPointNum);
        this.polyCompute.SetBuffer(
            KID.CalcDelaunayTriangulationLine, ShaderID.wTriangles, this.trianglesBuffer);
        this.polyCompute.SetTexture(
            KID.CalcDelaunayTriangulationLine,ShaderID.ExistingLineMap, _existingLineMap);
        this.polyCompute.SetTexture(
            KID.CalcDelaunayTriangulationLine,ShaderID.CellularMap, _cellularMap);
        this.polyCompute.SetTexture(
            KID.CalcDelaunayTriangulationLine, ShaderID.SeedMap, _seedMap);
        this.polyCompute.SetTexture(
            KID.CalcDelaunayTriangulationLine, DeviceController.ID.BodyIndexMap, _device.BodyIndexMap);
        this.polyCompute.Dispatch(
            KID.CalcDelaunayTriangulationLine,
            this._cellularMap.width / 32, this._cellularMap.height / 32, 1);

        //Debug.Log(sw.Elapsed); //経過時間
        //sw.Stop(); //計測終了

        //Graphics.CopyTexture(this._seedMap, this.debugMap);
        Graphics.CopyTexture(this._cellularMap, this.debugMap);
        //Graphics.CopyTexture(this._existingLineMap, this.debugLinesMap);

        this.UpdateMaterial();

        this.pointsBuffer.SetCounterValue(0);
        this.trianglesBuffer.SetCounterValue(0);
    }

    void UpdateMaterial() {
        if (_props == null) _props = new MaterialPropertyBlock();

        Debug.Log(DeviceController.ID.PositionMap);
        _props.SetTexture(DeviceController.ID.PositionMap, _device.PositionMap);
        _props.SetBuffer(ShaderID.rTriangles, this.trianglesBuffer);
        _props.SetBuffer(ShaderID.rPoints, this.pointsBuffer);
        _props.SetMatrix("_LocalToWorld", this.transform.localToWorldMatrix);


        this.triangleCounts = new int[] {0,1,0,0};
        this.triangleCountsBuffer.SetData(this.triangleCounts);
        ComputeBuffer.CopyCount(this.pointsBuffer, triangleCountsBuffer, 0);
        this.triangleCountsBuffer.GetData(this.triangleCounts);

        Debug.Log("p: " + this.triangleCounts[0]);

        _props.SetInt("_PointCount", this.triangleCounts[0]);


        this.triangleCounts = new int[] {0,1,0,0};
        this.triangleCountsBuffer.SetData(this.triangleCounts);
        ComputeBuffer.CopyCount(this.trianglesBuffer, triangleCountsBuffer, 0);
        this.triangleCountsBuffer.GetData(this.triangleCounts);

        Debug.Log("t: " + this.triangleCounts[0]);

/*
        Vector3Int[] tri = new Vector3Int[maxPointNum];
        this.trianglesBuffer.GetData(tri);
        for (int i = 0; i < 10; i++) {
            Debug.Log(tri[i]);
        }
        Vector2Int[] pp = new Vector2Int[maxPointNum];
        this.pointsBuffer.GetData(pp);
        for (int i = 0; i < 10; i++) {
            Debug.Log(pp[i]);
        }
*/

        _props.SetInt("_TriangleCount", this.triangleCounts[0]);


        Graphics.DrawProcedural(
            _material,
            new Bounds(this.transform.position, this.transform.lossyScale * 200),
            MeshTopology.Triangles, this.triangleCounts[0] * 3, 1,
            null, _props,
            ShadowCastingMode.TwoSided, true, this.gameObject.layer
        );
    }

    #endregion
}

}
