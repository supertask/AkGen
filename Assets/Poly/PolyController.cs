using UnityEngine;
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
    public int maxPointNum = 1000;
    public ComputeBuffer pointsBuffer;
    public ComputeBuffer linesBuffer;

    #endregion

    #region Shader property IDs

    static class PolyID
    {
        public static int CellularMap    = Shader.PropertyToID("CellularMap");
        public static int SeedMap    = Shader.PropertyToID("SeedMap");
        public static int ExistingLineMap    = Shader.PropertyToID("ExistingLineMap");
        public static int MaxPointNum    = Shader.PropertyToID("MaxPointNum");
        public static int rPoints    = Shader.PropertyToID("rPoints");
        public static int wPoints    = Shader.PropertyToID("wPoints");
        public static int wLines    = Shader.PropertyToID("wLines");
        public static int pointIndex    = Shader.PropertyToID("pointIndex");
    }

    static class PolyKID
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
        PolyKID.GetPoints = this.polyCompute.FindKernel("GetPoints");
        PolyKID.ResetCellularNoise = this.polyCompute.FindKernel("ResetCellularNoise");
        PolyKID.ResetExistingLineMap = this.polyCompute.FindKernel("ResetExistingLineMap");
        PolyKID.CalcCellularNoise = this.polyCompute.FindKernel("CalcCellularNoise");
        PolyKID.ModifyCellularNoise = this.polyCompute.FindKernel("ModifyCellularNoise");
        PolyKID.CalcDelaunayTriangulationLine = this.polyCompute.FindKernel("CalcDelaunayTriangulationLine");

        this.pointsBuffer = new ComputeBuffer
            (maxPointNum, Marshal.SizeOf(typeof(Vector2Int)), ComputeBufferType.Append);
        this.pointsBuffer.SetCounterValue(0);

        this.linesBuffer = new ComputeBuffer
            (maxPointNum * 10, Marshal.SizeOf(typeof(Vector4)));
        this.linesBuffer.SetCounterValue(0);

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
        linesBuffer?.Dispose();
    }

    unsafe void Update()
    {
        //_compute.SetFloat(ID.MaxDepth, _deviceSettings.maxDepth);
        //_compute.SetBuffer(0, ID.XYTable, _xyTable);

        //var sw = new System.Diagnostics.Stopwatch();
        //sw.Start(); //計測開始

        //Reset
        this.polyCompute.SetTexture(PolyKID.ResetCellularNoise, PolyID.CellularMap, _cellularMap);
        this.polyCompute.SetTexture(PolyKID.ResetCellularNoise, PolyID.SeedMap, _seedMap);
        this.polyCompute.Dispatch(
            PolyKID.ResetCellularNoise, this._cellularMap.width / 32, this._cellularMap.height / 32, 1);

        this.polyCompute.SetTexture(PolyKID.ResetExistingLineMap, PolyID.ExistingLineMap, _existingLineMap);
        this.polyCompute.Dispatch(
            PolyKID.ResetExistingLineMap, this._existingLineMap.width / 32, this._existingLineMap.height / 32, 1);

        //Get mesh points
        this.polyCompute.SetInt(PolyID.MaxPointNum, maxPointNum);
        this.polyCompute.SetBuffer(PolyKID.GetPoints, PolyID.wPoints, this.pointsBuffer);
        this.polyCompute.SetTexture(PolyKID.GetPoints, DeviceController.ID.BodyIndexMap, _device.BodyIndexMap);
        this.polyCompute.SetTexture(PolyKID.GetPoints, DeviceController.ID.EdgeMap, _device.EdgeMap);
        this.polyCompute.SetTexture(PolyKID.GetPoints, PolyID.CellularMap, _cellularMap);
        this.polyCompute.Dispatch(
            PolyKID.GetPoints, this._cellularMap.width / 32, this._cellularMap.height / 32, 1);

        //Cellular Noise Calculation
        this.polyCompute.SetInt(PolyID.MaxPointNum, maxPointNum);
        this.polyCompute.SetBuffer(PolyKID.CalcCellularNoise, PolyID.rPoints, this.pointsBuffer);
        this.polyCompute.SetTexture(PolyKID.CalcCellularNoise, DeviceController.ID.BodyIndexMap, _device.BodyIndexMap);
        this.polyCompute.SetTexture(PolyKID.CalcCellularNoise, PolyID.CellularMap, _cellularMap);
        this.polyCompute.SetTexture(PolyKID.CalcCellularNoise, PolyID.SeedMap, _seedMap);
        this.polyCompute.Dispatch(
            PolyKID.CalcCellularNoise, maxPointNum / 1024, 1, 1);

        //Cellular Noise Modification
        this.polyCompute.SetTexture(PolyKID.ModifyCellularNoise, PolyID.CellularMap, _cellularMap);
        this.polyCompute.SetTexture(PolyKID.ModifyCellularNoise, PolyID.SeedMap, _seedMap);
        this.polyCompute.Dispatch(
            PolyKID.ModifyCellularNoise, this._cellularMap.width / 32, this._cellularMap.height / 32, 1);

        //Calc DelaunayTriangulationLine
        this.polyCompute.SetBuffer(
            PolyKID.CalcDelaunayTriangulationLine, PolyID.wLines, this.linesBuffer);
        this.polyCompute.SetTexture(
            PolyKID.CalcDelaunayTriangulationLine,PolyID.ExistingLineMap, _existingLineMap);
        this.polyCompute.SetTexture(
            PolyKID.CalcDelaunayTriangulationLine,PolyID.CellularMap, _cellularMap);
        this.polyCompute.SetTexture(
            PolyKID.CalcDelaunayTriangulationLine, PolyID.SeedMap, _seedMap);
        this.polyCompute.Dispatch(
            PolyKID.CalcDelaunayTriangulationLine,
            this._cellularMap.width / 32, this._cellularMap.height / 32, 1);

        this.pointsBuffer.SetCounterValue(0);
        this.linesBuffer.SetCounterValue(0);


        //Debug.Log(sw.Elapsed); //経過時間
        //sw.Stop(); //計測終了

        //Graphics.CopyTexture(this._seedMap, this.debugMap);
        //Graphics.CopyTexture(this._cellularMap, this.debugMap);
        Graphics.CopyTexture(this._existingLineMap, this.debugLinesMap);
    }

    #endregion
}

}
