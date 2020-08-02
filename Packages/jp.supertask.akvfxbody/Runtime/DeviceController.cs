using UnityEngine;

namespace Akvfx {

public class DeviceController : MonoBehaviour
{
    //ボロノイ図 -> ドロネー図
    // https://www.ieice.org/publications/conference-FIT-DVDs/FIT2006/pdf/J/J_036.pdf

    #region Editable attribute

    [SerializeField, HideInInspector] RenderTexture colorMap;
    [SerializeField, HideInInspector] RenderTexture positionMap;
    [SerializeField, HideInInspector] RenderTexture bodyIndexMap;
    [SerializeField, HideInInspector] RenderTexture depthMap;
    [SerializeField, HideInInspector] RenderTexture edgeMap;

    [SerializeField] DeviceSettings _deviceSettings = null;

    public DeviceSettings DeviceSettings
      { get => _deviceSettings; set => SetDeviceSettings(value); }

    #endregion

    #region Asset reference

    [SerializeField] ComputeShader _compute = null;

    #endregion

    #region Public accessor properties

    public RenderTexture ColorMap => _colorMap;
    public RenderTexture PositionMap => _positionMap;
    public RenderTexture BodyIndexMap => _bodyIndexMap;
    public RenderTexture DepthMap => _depthMap;
    public RenderTexture EdgeMap => _edgeMap;


    #endregion

    #region Private members

    ThreadedDriver _driver;
    ComputeBuffer _xyTable;
    ComputeBuffer _colorBuffer;
    ComputeBuffer _depthBuffer;
    ComputeBuffer _bodyIndexBuffer;
    RenderTexture _colorMap;
    RenderTexture _positionMap;
    RenderTexture _bodyIndexMap;
    RenderTexture _depthMap;
    RenderTexture _edgeMap;

    void SetDeviceSettings(DeviceSettings settings)
    {
        _deviceSettings = settings;
        if (_driver != null) _driver.Settings = settings;
    }

    #endregion

    #region Shader property IDs

    public static class ID
    {
        public static int ColorBuffer = Shader.PropertyToID("ColorBuffer");
        public static int DepthBuffer = Shader.PropertyToID("DepthBuffer");
        public static int BodyIndexBuffer = Shader.PropertyToID("BodyIndexBuffer");
        public static int XYTable     = Shader.PropertyToID("XYTable");
        public static int MaxDepth    = Shader.PropertyToID("MaxDepth");
        public static int ColorMap    = Shader.PropertyToID("ColorMap");
        public static int PositionMap = Shader.PropertyToID("PositionMap");
        public static int BodyIndexMap = Shader.PropertyToID("BodyIndexMap");
        public static int DepthMap = Shader.PropertyToID("DepthMap");
        public static int EdgeMap = Shader.PropertyToID("EdgeMap");
        public static int EdgeSensitivity = Shader.PropertyToID("EdgeSensitivity");
    }

    public static class KID
    {
        public static int Unproject;
        public static int BakeEdges;
    }

    #endregion

    #region MonoBehaviour implementation

    public void Start()
    {
        // Start capturing via the threaded driver.
        _driver = new ThreadedDriver(_deviceSettings);

        KID.Unproject = this._compute.FindKernel("Unproject");
        KID.BakeEdges = this._compute.FindKernel("BakeEdges");

        // Temporary objects for conversion
        var width = ThreadedDriver.ImageWidth;
        var height = ThreadedDriver.ImageHeight;

        _colorBuffer = new ComputeBuffer(width * height, 4);
        _depthBuffer = new ComputeBuffer(width * height / 2, 4);
        _bodyIndexBuffer = new ComputeBuffer(width * height / 4, 4);

        _colorMap = new RenderTexture
          (width, height, 0, RenderTextureFormat.Default);
        _colorMap.enableRandomWrite = true;
        _colorMap.Create();

        _positionMap = new RenderTexture
          (width, height, 0, RenderTextureFormat.ARGBFloat);
        _positionMap.enableRandomWrite = true;
        _positionMap.Create();

        _bodyIndexMap = new RenderTexture
          (width, height, 0, RenderTextureFormat.ARGBFloat);
        _bodyIndexMap.enableRandomWrite = true;
        _bodyIndexMap.Create();

        _depthMap = new RenderTexture
          (width, height, 0, RenderTextureFormat.ARGBFloat);
        _depthMap.enableRandomWrite = true;
        _depthMap.Create();

        _edgeMap = new RenderTexture
          (width, height, 0, RenderTextureFormat.ARGBFloat);
        _edgeMap.enableRandomWrite = true;
        _edgeMap.Create();
    }

    public void OnDestroy()
    {
        if (_colorMap    != null) Destroy(_colorMap);
        if (_positionMap != null) Destroy(_positionMap);
        if (_bodyIndexMap != null) Destroy(_bodyIndexMap);
        if (_depthMap != null) Destroy(_depthMap);
        if (_edgeMap != null) Destroy(_edgeMap);

        _colorBuffer?.Dispose();
        _depthBuffer?.Dispose();
        _bodyIndexBuffer?.Dispose();

        _xyTable?.Dispose();
        _driver?.Dispose();
    }

    unsafe public void Update()
    {
        // Try initializing XY table if it's not ready.
        if (_xyTable == null)
        {
            var data = _driver.XYTable;
            if (data.IsEmpty) return; // Table is not ready.

            // Allocate and initialize the XY table.
            _xyTable = new ComputeBuffer(data.Length, sizeof(float));
            _xyTable.SetData(data);
        }

        // Try retrieving the last frame.
        var (color, depth, bodyIndex) = _driver.LockLastFrame();
        if (color.IsEmpty || depth.IsEmpty || bodyIndex.IsEmpty) return;

        // Load the frame data into the compute buffers.
        _colorBuffer.SetData(color.Span); //4 byte * width * height
        _depthBuffer.SetData(depth.Span); //2 byte * width * height
        _bodyIndexBuffer.SetData(bodyIndex.Span); //1 byte * width * height
        _driver.UpdateSkelton();

        // We don't need the last frame any more.
        _driver.ReleaseLastFrame();

        // Invoke the unprojection compute shader.
        _compute.SetFloat(ID.MaxDepth, _deviceSettings.maxDepth);
        _compute.SetBuffer(KID.Unproject, ID.ColorBuffer, _colorBuffer);
        _compute.SetBuffer(KID.Unproject, ID.DepthBuffer, _depthBuffer);
        _compute.SetBuffer(KID.Unproject, ID.BodyIndexBuffer, _bodyIndexBuffer);
        _compute.SetBuffer(KID.Unproject, ID.XYTable, _xyTable);
        _compute.SetTexture(KID.Unproject, ID.ColorMap, _colorMap);
        _compute.SetTexture(KID.Unproject, ID.PositionMap, _positionMap);
        _compute.SetTexture(KID.Unproject, ID.BodyIndexMap, _bodyIndexMap);
        _compute.SetTexture(KID.Unproject, ID.DepthMap, _depthMap);
        _compute.Dispatch(KID.Unproject, _colorMap.width / 8, _colorMap.height / 8, 1);

        _compute.SetFloat(ID.EdgeSensitivity, _deviceSettings.edgeSensitivity);
        _compute.SetTexture(KID.BakeEdges, ID.DepthMap, _depthMap);
        _compute.SetTexture(KID.BakeEdges, ID.EdgeMap, _edgeMap);
        _compute.Dispatch(KID.BakeEdges, _colorMap.width / 8, _colorMap.height / 8, 1);

        //Debug
        Graphics.CopyTexture(this._colorMap, this.colorMap);
        Graphics.CopyTexture(this._positionMap, this.positionMap);
        Graphics.CopyTexture(this._bodyIndexMap, this.bodyIndexMap);
        Graphics.CopyTexture(this._depthMap, this.depthMap);
        Graphics.CopyTexture(this._edgeMap, this.edgeMap);
    }

    #endregion
}

}
