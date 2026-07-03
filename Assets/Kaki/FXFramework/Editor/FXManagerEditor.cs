using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Events;

namespace Kaki
{
    // FXPlayer 的自定义 Inspector：
    // 1) 分组显示：每个 Group 绑定一个目标对象
    // 2) 事件下拉：自动列出事件源上的 C# event 和 UnityEvent
    // 3) 方法下拉：只显示目标对象上带 [TriggerAction] 的方法
    [CustomEditor(typeof(FXPlayer))]
    public class FXManagerEditor : Editor
    {
        private SerializedProperty groupsProp;
        private ReorderableList groupsList;
        private readonly Dictionary<string, ReorderableList> bindingsLists = new Dictionary<string, ReorderableList>();

        private void OnEnable()
        {
            if (!IsTargetValid())
            {
                return;
            }

            InitializeLists();
        }

        public override void OnInspectorGUI()
        {
            if (!IsTargetValid())
            {
                return;
            }

            if (groupsProp == null || groupsList == null)
            {
                InitializeLists();
            }

            serializedObject.Update();
            groupsList.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
        }

        private void InitializeLists()
        {
            groupsProp = serializedObject.FindProperty("groups");
            groupsList = new ReorderableList(serializedObject, groupsProp, true, true, true, true);
            groupsList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "FX Groups");
            groupsList.elementHeightCallback = GetGroupHeight;
            groupsList.drawElementCallback = DrawGroupElement;
        }

        private bool IsTargetValid()
        {
            if (targets == null || targets.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null)
                {
                    return false;
                }
            }

            return true;
        }

        private float GetGroupHeight(int index)
        {
            var groupProp = groupsProp.GetArrayElementAtIndex(index);
            var list = GetBindingsList(groupProp);
            float line = EditorGUIUtility.singleLineHeight + 4f;
            return line * 1 + list.GetHeight() + 6f;
        }

        private void DrawGroupElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var groupProp = groupsProp.GetArrayElementAtIndex(index);
            var objectProp = groupProp.FindPropertyRelative("object");

            float lineHeight = EditorGUIUtility.singleLineHeight;
            rect.y += 2f;

            var line = new Rect(rect.x, rect.y, rect.width, lineHeight);
            EditorGUI.BeginChangeCheck();
            var newTarget = EditorGUI.ObjectField(line, "Object", objectProp.objectReferenceValue, typeof(UnityEngine.Object), true);
            if (newTarget != null && !(newTarget is GameObject) && !(newTarget is Component))
            {
                newTarget = null;
            }

            if (EditorGUI.EndChangeCheck())
            {
                objectProp.objectReferenceValue = newTarget;
                ClearMethodSelection(groupProp);
            }

            line.y += lineHeight + 4f;
            var listRect = new Rect(rect.x, line.y, rect.width, rect.height - (lineHeight + 4f));
            var list = GetBindingsList(groupProp);
            list.DoList(listRect);
        }

        private ReorderableList GetBindingsList(SerializedProperty groupProp)
        {
            var bindingsProp = groupProp.FindPropertyRelative("fxTriggerMethods");
            var key = bindingsProp.propertyPath;
            if (bindingsLists.TryGetValue(key, out var list))
            {
                return list;
            }

            list = new ReorderableList(serializedObject, bindingsProp, true, true, true, true);
            list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Trigger Methods");
            list.elementHeightCallback = index => GetBindingHeight(groupProp, index);
            list.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                var element = bindingsProp.GetArrayElementAtIndex(index);
                DrawBindingElement(rect, groupProp, element);
            };

            bindingsLists[key] = list;
            return list;
        }

        private float GetBindingHeight(SerializedProperty groupProp, int index)
        {
            var bindingsProp = groupProp.FindPropertyRelative("fxTriggerMethods");
            var bindingProp = bindingsProp.GetArrayElementAtIndex(index);
            int lines = 4; // Name + Method + EventSource + Event

            var method = GetMethodInfo(groupProp, bindingProp);
            if (method != null)
            {
                lines += method.GetParameters().Length;
            }

            return (EditorGUIUtility.singleLineHeight + 4f) * lines + 4f;
        }

        private void DrawBindingElement(Rect rect, SerializedProperty groupProp, SerializedProperty bindingProp)
        {
            var nameProp = bindingProp.FindPropertyRelative("name");
            var methodNameProp = bindingProp.FindPropertyRelative("methodName");
            var methodSignatureProp = bindingProp.FindPropertyRelative("methodSignature");
            var argsProp = bindingProp.FindPropertyRelative("arguments");
            var eventSourceProp = bindingProp.FindPropertyRelative("eventSource");
            var eventNameProp = bindingProp.FindPropertyRelative("eventName");
            var eventComponentProp = bindingProp.FindPropertyRelative("eventComponent");
            var methodComponentProp = bindingProp.FindPropertyRelative("methodComponent");

            float lineHeight = EditorGUIUtility.singleLineHeight;
            rect.y += 2f;

            var line = new Rect(rect.x, rect.y, rect.width, lineHeight);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUI.PropertyField(line, nameProp, new GUIContent("Name"));
            EditorGUI.EndDisabledGroup();

            line.y += lineHeight + 4f;
            DrawMethodPopup(line, groupProp, methodNameProp, methodSignatureProp, methodComponentProp, argsProp, nameProp);

            line.y += lineHeight + 4f;
            DrawEventSourceField(line, eventSourceProp, eventNameProp, eventComponentProp);

            line.y += lineHeight + 4f;
            DrawEventPopup(line, eventSourceProp, eventNameProp, eventComponentProp);

            var method = GetMethodInfo(groupProp, bindingProp);
            if (method == null)
            {
                return;
            }

            var parameters = method.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
            {
                line.y += lineHeight + 4f;
                if (i >= argsProp.arraySize)
                {
                    continue;
                }

                var argProp = argsProp.GetArrayElementAtIndex(i);
                DrawArgumentField(line, parameters[i], argProp);
            }
        }

        private static void DrawEventSourceField(Rect rect, SerializedProperty sourceProp, SerializedProperty eventNameProp, SerializedProperty eventComponentProp)
        {
            EditorGUI.BeginChangeCheck();
            var newSource = EditorGUI.ObjectField(rect, "Event Source", sourceProp.objectReferenceValue, typeof(UnityEngine.Object), true);
            if (newSource != null && !(newSource is GameObject) && !(newSource is Component))
            {
                newSource = null;
            }

            if (EditorGUI.EndChangeCheck())
            {
                sourceProp.objectReferenceValue = newSource;
                eventNameProp.stringValue = string.Empty;
                eventComponentProp.objectReferenceValue = null;
            }
        }

        private void DrawEventPopup(Rect rect, SerializedProperty sourceProp, SerializedProperty eventNameProp, SerializedProperty eventComponentProp)
        {
            var sourceObj = sourceProp.objectReferenceValue;
            if (sourceObj == null)
            {
                EditorGUI.PropertyField(rect, eventNameProp, new GUIContent("Event"));
                return;
            }

            var candidates = GetEventCandidates(sourceObj);
            if (candidates.Count == 0)
            {
                EditorGUI.PropertyField(rect, eventNameProp, new GUIContent("Event"));
                return;
            }

            int selectedIndex = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Name == eventNameProp.stringValue &&
                    (eventComponentProp.objectReferenceValue == null || candidates[i].Source == eventComponentProp.objectReferenceValue))
                {
                    selectedIndex = i;
                    break;
                }
            }

            var display = new string[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                display[i] = candidates[i].Display;
            }

            int newIndex = EditorGUI.Popup(rect, "Event", selectedIndex, display);
            eventNameProp.stringValue = candidates[newIndex].Name;
            eventComponentProp.objectReferenceValue = candidates[newIndex].Source;
        }

        private void DrawMethodPopup(Rect rect, SerializedProperty groupProp, SerializedProperty methodNameProp, SerializedProperty methodSignatureProp, SerializedProperty methodComponentProp, SerializedProperty argsProp, SerializedProperty nameProp)
        {
            var objectProp = groupProp.FindPropertyRelative("object");
            var targetObj = objectProp.objectReferenceValue;
            if (targetObj == null)
            {
                EditorGUI.PropertyField(rect, methodNameProp, new GUIContent("Method"));
                return;
            }

            var candidates = GetMethodCandidates(targetObj);
            if (candidates.Count == 0)
            {
                EditorGUI.PropertyField(rect, methodNameProp, new GUIContent("Method"));
                return;
            }

            int selectedIndex = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Signature == methodSignatureProp.stringValue &&
                    (methodComponentProp.objectReferenceValue == null || candidates[i].Source == methodComponentProp.objectReferenceValue))
                {
                    selectedIndex = i;
                    break;
                }
            }

            var display = new string[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
            {
                display[i] = candidates[i].Display;
            }

            int newIndex = EditorGUI.Popup(rect, "Method", selectedIndex, display);
            var selected = candidates[newIndex];
            var prevSignature = methodSignatureProp.stringValue;

            methodNameProp.stringValue = selected.Name;
            methodSignatureProp.stringValue = selected.Signature;
            methodComponentProp.objectReferenceValue = selected.Source;
            if (nameProp != null)
            {
                nameProp.stringValue = selected.Name;
            }

            if (prevSignature != selected.Signature)
            {
                SyncArgumentsToMethod(argsProp, selected.Method);
            }
        }

        private static void SyncArgumentsToMethod(SerializedProperty argsProp, MethodInfo method)
        {
            if (method == null)
            {
                argsProp.arraySize = 0;
                return;
            }

            var parameters = method.GetParameters();
            argsProp.arraySize = parameters.Length;
            for (int i = 0; i < parameters.Length; i++)
            {
                var argProp = argsProp.GetArrayElementAtIndex(i);
                var kindProp = argProp.FindPropertyRelative("kind");
                var enumTypeProp = argProp.FindPropertyRelative("enumTypeName");

                kindProp.enumValueIndex = (int)GetArgumentKind(parameters[i].ParameterType);
                if (parameters[i].ParameterType.IsEnum)
                {
                    enumTypeProp.stringValue = parameters[i].ParameterType.AssemblyQualifiedName;
                }

                ApplyDefaultValue(argProp, parameters[i]);
            }
        }

        private static void ApplyDefaultValue(SerializedProperty argProp, ParameterInfo parameter)
        {
            if (argProp == null || parameter == null)
            {
                return;
            }

            object value = null;
            if (parameter.HasDefaultValue)
            {
                value = parameter.DefaultValue;
                if (value == DBNull.Value || value == Type.Missing)
                {
                    value = null;
                }
            }

            if (value == null)
            {
                value = GetReasonableDefault(parameter.ParameterType);
            }

            SetArgumentValue(argProp, parameter.ParameterType, value);
        }

        private static object GetReasonableDefault(Type type)
        {
            if (type == typeof(int))
            {
                return 0;
            }

            if (type == typeof(float))
            {
                return 1f;
            }

            if (type == typeof(bool))
            {
                return false;
            }

            if (type == typeof(string))
            {
                return string.Empty;
            }

            if (type == typeof(Vector2))
            {
                return Vector2.zero;
            }

            if (type == typeof(Vector3))
            {
                return Vector3.zero;
            }

            if (type == typeof(Color))
            {
                return Color.white;
            }

            if (type != null && type.IsEnum)
            {
                Array values = Enum.GetValues(type);
                return values.Length > 0 ? values.GetValue(0) : 0;
            }

            if (type != null && typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                return null;
            }

            return null;
        }

        private static void SetArgumentValue(SerializedProperty argProp, Type type, object value)
        {
            if (type == typeof(int))
            {
                argProp.FindPropertyRelative("intValue").intValue = value == null ? 0 : (value is int v ? v : Convert.ToInt32(value));
                return;
            }

            if (type == typeof(float))
            {
                argProp.FindPropertyRelative("floatValue").floatValue = value == null ? 0f : (value is float v ? v : Convert.ToSingle(value));
                return;
            }

            if (type == typeof(bool))
            {
                argProp.FindPropertyRelative("boolValue").boolValue = value != null && (value is bool v ? v : Convert.ToBoolean(value));
                return;
            }

            if (type == typeof(string))
            {
                argProp.FindPropertyRelative("stringValue").stringValue = value as string ?? string.Empty;
                return;
            }

            if (type == typeof(Vector2))
            {
                argProp.FindPropertyRelative("vector2Value").vector2Value = value is Vector2 v ? v : Vector2.zero;
                return;
            }

            if (type == typeof(Vector3))
            {
                argProp.FindPropertyRelative("vector3Value").vector3Value = value is Vector3 v ? v : Vector3.zero;
                return;
            }

            if (type == typeof(Color))
            {
                argProp.FindPropertyRelative("colorValue").colorValue = value is Color v ? v : Color.white;
                return;
            }

            if (type != null && type.IsEnum)
            {
                int enumValue = 0;
                if (value != null)
                {
                    try
                    {
                        enumValue = Convert.ToInt32(value);
                    }
                    catch
                    {
                        enumValue = 0;
                    }
                }

                argProp.FindPropertyRelative("enumValue").intValue = enumValue;
                argProp.FindPropertyRelative("enumTypeName").stringValue = type.AssemblyQualifiedName;
                return;
            }

            if (type != null && typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                argProp.FindPropertyRelative("objectValue").objectReferenceValue = value as UnityEngine.Object;
                return;
            }
        }

        private static void DrawArgumentField(Rect rect, ParameterInfo parameter, SerializedProperty argProp)
        {
            var type = parameter.ParameterType;
            string label = parameter.Name;
            if (type == typeof(int))
            {
                EditorGUI.PropertyField(rect, argProp.FindPropertyRelative("intValue"), new GUIContent(label));
            }
            else if (type == typeof(float))
            {
                EditorGUI.PropertyField(rect, argProp.FindPropertyRelative("floatValue"), new GUIContent(label));
            }
            else if (type == typeof(bool))
            {
                EditorGUI.PropertyField(rect, argProp.FindPropertyRelative("boolValue"), new GUIContent(label));
            }
            else if (type == typeof(string))
            {
                EditorGUI.PropertyField(rect, argProp.FindPropertyRelative("stringValue"), new GUIContent(label));
            }
            else if (type == typeof(Vector2))
            {
                EditorGUI.PropertyField(rect, argProp.FindPropertyRelative("vector2Value"), new GUIContent(label));
            }
            else if (type == typeof(Vector3))
            {
                EditorGUI.PropertyField(rect, argProp.FindPropertyRelative("vector3Value"), new GUIContent(label));
            }
            else if (type == typeof(Color))
            {
                EditorGUI.PropertyField(rect, argProp.FindPropertyRelative("colorValue"), new GUIContent(label));
            }
            else if (type.IsEnum)
            {
                var enumValueProp = argProp.FindPropertyRelative("enumValue");
                var enumTypeProp = argProp.FindPropertyRelative("enumTypeName");
                enumTypeProp.stringValue = type.AssemblyQualifiedName;

                var values = Enum.GetValues(type);
                var names = Enum.GetNames(type);
                int currentIndex = 0;
                int currentValue = enumValueProp.intValue;
                for (int i = 0; i < values.Length; i++)
                {
                    if (Convert.ToInt32(values.GetValue(i)) == currentValue)
                    {
                        currentIndex = i;
                        break;
                    }
                }

                int newIndex = EditorGUI.Popup(rect, label, currentIndex, names);
                enumValueProp.intValue = Convert.ToInt32(values.GetValue(newIndex));
            }
            else if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                var objProp = argProp.FindPropertyRelative("objectValue");
                objProp.objectReferenceValue = EditorGUI.ObjectField(rect, label, objProp.objectReferenceValue, type, true);
            }
            else
            {
                EditorGUI.LabelField(rect, label, $"Unsupported type: {type.Name}");
            }
        }

        private static FXPlayer.FXArgumentKind GetArgumentKind(Type type)
        {
            if (type == typeof(int)) return FXPlayer.FXArgumentKind.Int;
            if (type == typeof(float)) return FXPlayer.FXArgumentKind.Float;
            if (type == typeof(bool)) return FXPlayer.FXArgumentKind.Bool;
            if (type == typeof(string)) return FXPlayer.FXArgumentKind.String;
            if (type == typeof(Vector2)) return FXPlayer.FXArgumentKind.Vector2;
            if (type == typeof(Vector3)) return FXPlayer.FXArgumentKind.Vector3;
            if (type == typeof(Color)) return FXPlayer.FXArgumentKind.Color;
            if (type.IsEnum) return FXPlayer.FXArgumentKind.Enum;
            if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return FXPlayer.FXArgumentKind.Object;
            return FXPlayer.FXArgumentKind.String;
        }

        private static void ClearMethodSelection(SerializedProperty groupProp)
        {
            var bindingsProp = groupProp.FindPropertyRelative("fxTriggerMethods");
            for (int i = 0; i < bindingsProp.arraySize; i++)
            {
                var bindingProp = bindingsProp.GetArrayElementAtIndex(i);
                bindingProp.FindPropertyRelative("name").stringValue = string.Empty;
                bindingProp.FindPropertyRelative("methodName").stringValue = string.Empty;
                bindingProp.FindPropertyRelative("methodSignature").stringValue = string.Empty;
                bindingProp.FindPropertyRelative("methodComponent").objectReferenceValue = null;
                var argsProp = bindingProp.FindPropertyRelative("arguments");
                argsProp.arraySize = 0;
            }
        }

        private static MethodInfo GetMethodInfo(SerializedProperty groupProp, SerializedProperty bindingProp)
        {
            var objectProp = groupProp.FindPropertyRelative("object");
            var targetObj = objectProp.objectReferenceValue;
            if (targetObj == null)
            {
                return null;
            }

            string methodName = bindingProp.FindPropertyRelative("methodName").stringValue;
            string methodSignature = bindingProp.FindPropertyRelative("methodSignature").stringValue;
            var methodComponentProp = bindingProp.FindPropertyRelative("methodComponent");
            var cachedComponent = methodComponentProp.objectReferenceValue as Component;

            if (cachedComponent != null)
            {
                return FXPlayer.FindTriggerMethod(cachedComponent.GetType(), methodName, methodSignature);
            }

            if (targetObj is Component component)
            {
                return FXPlayer.FindTriggerMethod(component.GetType(), methodName, methodSignature);
            }

            var go = targetObj as GameObject;
            if (go == null)
            {
                return null;
            }

            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null)
                {
                    continue;
                }

                var method = FXPlayer.FindTriggerMethod(comp.GetType(), methodName, methodSignature);
                if (method != null)
                {
                    return method;
                }
            }

            return null;
        }

        private static List<MethodCandidate> GetMethodCandidates(UnityEngine.Object target)
        {
            var results = new List<MethodCandidate>();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            if (target is Component component)
            {
                CollectMethods(component, results, flags);
                return results;
            }

            var go = target as GameObject;
            if (go == null)
            {
                return results;
            }

            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null)
                {
                    continue;
                }

                CollectMethods(comp, results, flags);
            }

            return results;
        }

        private static void CollectMethods(Component source, List<MethodCandidate> results, BindingFlags flags)
        {
            var type = source.GetType();
            foreach (var method in type.GetMethods(flags))
            {
                if (method == null || method.IsGenericMethod || method.ReturnType != typeof(void))
                {
                    continue;
                }

                if (!FXPlayer.IsTriggerActionVisible(method))
                {
                    continue;
                }

                if (!FXPlayer.IsSupportedParameters(method))
                {
                    continue;
                }

                var signature = FXPlayer.BuildMethodSignature(method);
                var display = $"{type.Name}.{method.Name}{FormatSignature(method)}";
                results.Add(new MethodCandidate(source, method, method.Name, signature, display));
            }
        }

        private static string FormatSignature(MethodInfo method)
        {
            if (method == null)
            {
                return "()";
            }

            var parameters = method.GetParameters();
            if (parameters.Length == 0)
            {
                return "()";
            }

            var parts = new string[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                parts[i] = parameters[i].ParameterType.Name;
            }

            return "(" + string.Join(", ", parts) + ")";
        }

        private static List<EventCandidate> GetEventCandidates(UnityEngine.Object source)
        {
            var results = new List<EventCandidate>();
            // 仅显示 public 事件/UnityEvent，隐藏私有事件
            var flags = BindingFlags.Instance | BindingFlags.Public;
            if (source is Component component)
            {
                CollectEventCandidates(component, results, flags);
                return results;
            }

            var go = source as GameObject;
            if (go == null)
            {
                return results;
            }

            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null)
                {
                    continue;
                }

                CollectEventCandidates(comp, results, flags);
            }

            return results;
        }

        private static void CollectEventCandidates(Component source, List<EventCandidate> results, BindingFlags flags)
        {
            var seen = new HashSet<string>();
            var type = source.GetType();

            foreach (var evt in type.GetEvents(flags))
            {
                if (seen.Contains(evt.Name))
                {
                    continue;
                }

                var display = $"{type.Name}.{evt.Name} (event{FormatEventSignature(evt.EventHandlerType)})";
                results.Add(new EventCandidate(source, evt.Name, display));
                seen.Add(evt.Name);
            }

            foreach (var field in type.GetFields(flags))
            {
                if (seen.Contains(field.Name))
                {
                    continue;
                }

                // 仅显示 public 字段
                if (!field.IsPublic)
                {
                    continue;
                }

                if (!typeof(UnityEventBase).IsAssignableFrom(field.FieldType))
                {
                    continue;
                }

                var display = $"{type.Name}.{field.Name} (UnityEvent{FormatUnityEventSignature(field.FieldType)})";
                results.Add(new EventCandidate(source, field.Name, display));
                seen.Add(field.Name);
            }

            foreach (var property in type.GetProperties(flags))
            {
                if (seen.Contains(property.Name))
                {
                    continue;
                }

                // 仅显示 public Getter
                var getter = property.GetMethod;
                if (getter == null || !getter.IsPublic)
                {
                    continue;
                }

                if (!property.CanRead || !typeof(UnityEventBase).IsAssignableFrom(property.PropertyType))
                {
                    continue;
                }

                var display = $"{type.Name}.{property.Name} (UnityEvent{FormatUnityEventSignature(property.PropertyType)})";
                results.Add(new EventCandidate(source, property.Name, display));
                seen.Add(property.Name);
            }
        }

        private static string FormatEventSignature(Type delegateType)
        {
            if (delegateType == null)
            {
                return "()";
            }

            var invoke = delegateType.GetMethod("Invoke");
            if (invoke == null)
            {
                return "()";
            }

            var parameters = invoke.GetParameters();
            if (parameters.Length == 0)
            {
                return "()";
            }

            var parts = new string[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                parts[i] = parameters[i].ParameterType.Name;
            }

            return "(" + string.Join(", ", parts) + ")";
        }

        private static string FormatUnityEventSignature(Type unityEventType)
        {
            if (unityEventType == null)
            {
                return "()";
            }

            if (!unityEventType.IsGenericType)
            {
                return "()";
            }

            var args = unityEventType.GetGenericArguments();
            if (args.Length == 0)
            {
                return "()";
            }

            var parts = new string[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                parts[i] = args[i].Name;
            }

            return "(" + string.Join(", ", parts) + ")";
        }

        private readonly struct MethodCandidate
        {
            public readonly Component Source;
            public readonly MethodInfo Method;
            public readonly string Name;
            public readonly string Signature;
            public readonly string Display;

            public MethodCandidate(Component source, MethodInfo method, string name, string signature, string display)
            {
                Source = source;
                Method = method;
                Name = name;
                Signature = signature;
                Display = display;
            }
        }

        private readonly struct EventCandidate
        {
            public readonly Component Source;
            public readonly string Name;
            public readonly string Display;

            public EventCandidate(Component source, string name, string display)
            {
                Source = source;
                Name = name;
                Display = display;
            }
        }
    }
}
