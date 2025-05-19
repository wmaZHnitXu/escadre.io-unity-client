// File: Scripts/Client/Utils/ReadOnlyAttribute.cs (or a shared Utils location)
using UnityEngine;

/// <summary>
/// Custom attribute to mark a field as read-only in the Unity Inspector.
/// Requires a custom PropertyDrawer (ReadOnlyDrawer) to take effect.
/// </summary>
public class ReadOnlyAttribute : PropertyAttribute { }

#if UNITY_EDITOR
namespace UnityEditor.CustomAttributes
{
    [CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public class ReadOnlyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            bool oldEnabled = GUI.enabled;
            GUI.enabled = false;
            EditorGUI.PropertyField(position, property, label, true); // Pass true for includeChildren
            GUI.enabled = oldEnabled;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // Ensure the height calculation includes children for complex types (like Vector3)
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
    }
}
#endif