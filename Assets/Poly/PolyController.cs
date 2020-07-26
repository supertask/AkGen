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
    [SerializeField] public RenderTexture cellularMap;

    #endregion


    #region Public accessor properties

    public RenderTexture CellularMap => _cellularMap;

    public ComputeShader polyCompute;

    #endregion

    #region Private members
    RenderTexture _cellularMap;
    public int maxPointNum = 1000;
    public ComputeBuffer pointsBuffer;
    public ComputeBuffer debugBuffer;

    #endregion

    #region Shader property IDs

    static class PolyID
    {
        public static int CellularMap    = Shader.PropertyToID("CellularMap");
        public static int MaxPointNum    = Shader.PropertyToID("MaxPointNum");
        public static int rPoints    = Shader.PropertyToID("rPoints");
        public static int wPoints    = Shader.PropertyToID("wPoints");
        public static int pointIndex    = Shader.PropertyToID("pointIndex");
        public static int debug    = Shader.PropertyToID("debug");
    }

    static class PolyKID
    {
        public static int CalcCelluarNoise;
        public static int GetPoints;
        public static int ResetCelluarNoise;
    }

    #endregion

    #region MonoBehaviour implementation

    void Start()
    {
        PolyKID.GetPoints = this.polyCompute.FindKernel("GetPoints");
        PolyKID.CalcCelluarNoise = this.polyCompute.FindKernel("CalcCelluarNoise");
        PolyKID.ResetCelluarNoise = this.polyCompute.FindKernel("ResetCelluarNoise");

        this.pointsBuffer = new ComputeBuffer
            (maxPointNum, Marshal.SizeOf(typeof(Vector2Int)), ComputeBufferType.Append);
        this.pointsBuffer.SetCounterValue(0);

        this.debugBuffer = new ComputeBuffer
            (maxPointNum, Marshal.SizeOf(typeof(Vector2)));

        // Temporary objects for conversion
        var width = ThreadedDriver.ImageWidth;
        var height = ThreadedDriver.ImageHeight;

        this._cellularMap = new RenderTexture
          (width, height, 0, RenderTextureFormat.ARGBFloat);
        this._cellularMap.enableRandomWrite = true;
        this._cellularMap.Create();
    }

    void OnDestroy()
    {
        if (_cellularMap    != null) {
            _cellularMap.Release();
            Graphics.CopyTexture(this._cellularMap, this.cellularMap);
            Destroy(_cellularMap);
        }
        pointsBuffer?.Dispose();
    }

    unsafe void Update()
    {
        //_compute.SetFloat(ID.MaxDepth, _deviceSettings.maxDepth);
        //_compute.SetBuffer(0, ID.XYTable, _xyTable);

        //var sw = new System.Diagnostics.Stopwatch();
        //sw.Start(); //計測開始

        //Reset
        this.polyCompute.SetTexture(PolyKID.ResetCelluarNoise, PolyID.CellularMap, _cellularMap);
        this.polyCompute.Dispatch(
            PolyKID.ResetCelluarNoise, this._cellularMap.width / 32, this._cellularMap.height / 32, 1);

        //Get mesh points
        this.polyCompute.SetInt(PolyID.MaxPointNum, maxPointNum);
        this.polyCompute.SetInt(PolyID.pointIndex, 0);
        this.polyCompute.SetBuffer(PolyKID.GetPoints, PolyID.wPoints, this.pointsBuffer);
        this.polyCompute.SetTexture(PolyKID.GetPoints, DeviceController.ID.BodyIndexMap, _device.BodyIndexMap);
        this.polyCompute.SetTexture(PolyKID.GetPoints, DeviceController.ID.EdgeMap, _device.EdgeMap);
        this.polyCompute.SetTexture(PolyKID.GetPoints, PolyID.CellularMap, _cellularMap);
        this.polyCompute.Dispatch(
            PolyKID.GetPoints, this._cellularMap.width / 32, this._cellularMap.height / 32, 1);

        this.polyCompute.SetInt(PolyID.MaxPointNum, maxPointNum);
        this.polyCompute.SetBuffer(PolyKID.CalcCelluarNoise, PolyID.debug, this.debugBuffer);
        this.polyCompute.SetBuffer(PolyKID.CalcCelluarNoise, PolyID.rPoints, this.pointsBuffer);
        this.polyCompute.SetTexture(PolyKID.CalcCelluarNoise, DeviceController.ID.BodyIndexMap, _device.BodyIndexMap);
        this.polyCompute.SetTexture(PolyKID.CalcCelluarNoise, PolyID.CellularMap, _cellularMap);
        this.polyCompute.Dispatch(
            PolyKID.CalcCelluarNoise, maxPointNum / 1024, 1, 1);

/*
        this.polyCompute.SetInt(PolyID.MaxPointNum, maxPointNum);
        this.polyCompute.SetBuffer(PolyKID.CalcCelluarNoise, PolyID.debug, this.debugBuffer);
        this.polyCompute.SetBuffer(PolyKID.CalcCelluarNoise, PolyID.rPoints, this.pointsBuffer);
        this.polyCompute.SetTexture(PolyKID.CalcCelluarNoise, PolyID.CellularMap, _cellularMap);
        this.polyCompute.Dispatch(
            PolyKID.CalcCelluarNoise, this._cellularMap.width / 32, this._cellularMap.height / 32, 1);
*/

        this.pointsBuffer.SetCounterValue(0);


        //Debug.Log(sw.Elapsed); //経過時間
        //sw.Stop(); //計測終了

        Graphics.CopyTexture(this._cellularMap, this.cellularMap);
    }

    #endregion
}

}
