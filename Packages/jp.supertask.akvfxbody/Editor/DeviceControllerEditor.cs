using UnityEngine;
using UnityEditor;

namespace Akvfx {

[CanEditMultipleObjects]
[CustomEditor(typeof(DeviceController))]
sealed class DeviceControllerEditor : Editor
{
    SerializedProperty _deviceSettings;
    SerializedProperty bodyIndexMap;
    SerializedProperty colorMap;
    SerializedProperty positionMap;

    void OnEnable()
    {
      _deviceSettings = serializedObject.FindProperty("_deviceSettings");
      bodyIndexMap = serializedObject.FindProperty("bodyIndexMap");
      colorMap = serializedObject.FindProperty("colorMap");
      positionMap = serializedObject.FindProperty("positionMap");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(_deviceSettings);
        EditorGUILayout.PropertyField(colorMap);
        EditorGUILayout.PropertyField(positionMap);
        EditorGUILayout.PropertyField(bodyIndexMap);
        serializedObject.ApplyModifiedProperties();
    }
}

}
