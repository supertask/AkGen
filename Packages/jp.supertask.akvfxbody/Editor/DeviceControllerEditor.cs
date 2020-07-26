using UnityEngine;
using UnityEditor;

namespace Akvfx {

[CanEditMultipleObjects]
[CustomEditor(typeof(DeviceController))]
sealed class DeviceControllerEditor : Editor
{
    SerializedProperty _deviceSettings;
    SerializedProperty colorMap;
    SerializedProperty positionMap;
    SerializedProperty bodyIndexMap;
    SerializedProperty depthMap;
    SerializedProperty edgeMap;

    void OnEnable()
    {
      _deviceSettings = serializedObject.FindProperty("_deviceSettings");
      colorMap = serializedObject.FindProperty("colorMap");
      positionMap = serializedObject.FindProperty("positionMap");
      bodyIndexMap = serializedObject.FindProperty("bodyIndexMap");
      depthMap = serializedObject.FindProperty("depthMap");
      edgeMap = serializedObject.FindProperty("edgeMap");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(_deviceSettings);
        EditorGUILayout.PropertyField(colorMap);
        EditorGUILayout.PropertyField(positionMap);
        EditorGUILayout.PropertyField(bodyIndexMap);
        EditorGUILayout.PropertyField(depthMap);
        EditorGUILayout.PropertyField(edgeMap);
        serializedObject.ApplyModifiedProperties();
    }
}

}
