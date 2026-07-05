using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Events;

namespace Kaki
{
    // AudioPlayer 的自定义 Inspector：
    // 1) 按 AudioGroup 分组展示 AudioEntry（每个 Group 绑定一个 AudioType）
    // 2) 提供“事件下拉选择”，自动列出事件源上的 C# event 和 UnityEvent
    [CustomEditor(typeof(AudioPlayer))]
    public class AudioEventPlayerEditor : Editor
    {
        private SerializedProperty groupsProp;
        private ReorderableList groupsList;
        private readonly Dictionary<string, ReorderableList> entriesLists = new Dictionary<string, ReorderableList>();
        private readonly Dictionary<string, ReorderableList> additionalEventLists = new Dictionary<string, ReorderableList>();

        private void OnEnable()
        {
            groupsProp = serializedObject.FindProperty("audioGroups");
            groupsList = new ReorderableList(serializedObject, groupsProp, true, true, true, true);
            groupsList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Audio Groups");
            groupsList.elementHeightCallback = GetGroupHeight;
            groupsList.drawElementCallback = DrawGroupElement;
            groupsList.onAddCallback = _ =>
            {
                int newIndex = groupsProp.arraySize;
                groupsProp.InsertArrayElementAtIndex(newIndex);
                var groupProp = groupsProp.GetArrayElementAtIndex(newIndex);
                groupProp.FindPropertyRelative("groupName").stringValue = $"Audio Group {newIndex + 1}";
                groupProp.FindPropertyRelative("audioType").enumValueIndex = (int)AudioType.SFX;
                groupProp.FindPropertyRelative("triggerMode").enumValueIndex = (int)AudioPlayer.AudioGroupTriggerMode.Single;
                groupProp.FindPropertyRelative("playMode").enumValueIndex = (int)AudioPlayMode.Default;
                groupProp.FindPropertyRelative("eventSource").objectReferenceValue = null;
                groupProp.FindPropertyRelative("eventName").stringValue = string.Empty;
                groupProp.FindPropertyRelative("eventComponent").objectReferenceValue = null;
                var additionalEventsProp = groupProp.FindPropertyRelative("additionalEvents");
                if (additionalEventsProp != null)
                {
                    additionalEventsProp.arraySize = 0;
                }
                groupProp.FindPropertyRelative("stopEventSource").objectReferenceValue = null;
                groupProp.FindPropertyRelative("stopEventName").stringValue = string.Empty;
                groupProp.FindPropertyRelative("stopEventComponent").objectReferenceValue = null;
                var entriesProp = groupProp.FindPropertyRelative("entries");
                if (entriesProp != null)
                {
                    entriesProp.arraySize = 0;
                }
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            groupsList.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
        }

        private float GetGroupHeight(int index)
        {
            var groupProp = groupsProp.GetArrayElementAtIndex(index);
            var list = GetEntriesList(groupProp);
            var additionalEventsList = GetAdditionalEventsList(groupProp);
            float line = EditorGUIUtility.singleLineHeight + 4f;
            int groupLines = GetGroupTriggerMode(groupProp) == AudioPlayer.AudioGroupTriggerMode.Single ? 3 : 6;
            if (GetGroupTriggerMode(groupProp) != AudioPlayer.AudioGroupTriggerMode.Single &&
                RequiresStopEvent(GetPlayMode(groupProp.FindPropertyRelative("playMode"))))
            {
                groupLines += 2;
            }

            float additionalEventsHeight = GetGroupTriggerMode(groupProp) == AudioPlayer.AudioGroupTriggerMode.Single
                ? 0f
                : additionalEventsList.GetHeight() + 4f;
            return line * groupLines + additionalEventsHeight + list.GetHeight() + 6f;
        }

        private void DrawGroupElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var groupProp = groupsProp.GetArrayElementAtIndex(index);
            var nameProp = groupProp.FindPropertyRelative("groupName");
            var typeProp = groupProp.FindPropertyRelative("audioType");
            var triggerModeProp = groupProp.FindPropertyRelative("triggerMode");
            var groupPlayModeProp = groupProp.FindPropertyRelative("playMode");
            var eventSourceProp = groupProp.FindPropertyRelative("eventSource");
            var eventNameProp = groupProp.FindPropertyRelative("eventName");
            var eventComponentProp = groupProp.FindPropertyRelative("eventComponent");
            var stopEventSourceProp = groupProp.FindPropertyRelative("stopEventSource");
            var stopEventNameProp = groupProp.FindPropertyRelative("stopEventName");
            var stopEventComponentProp = groupProp.FindPropertyRelative("stopEventComponent");

            float lineHeight = EditorGUIUtility.singleLineHeight;
            rect.y += 2f;

            var line = new Rect(rect.x, rect.y, rect.width, lineHeight);
            EditorGUI.PropertyField(line, nameProp, new GUIContent("Group Name"));

            line.y += lineHeight + 4f;
            EditorGUI.PropertyField(line, typeProp, new GUIContent("Audio Type"));

            line.y += lineHeight + 4f;
            EditorGUI.PropertyField(line, triggerModeProp, new GUIContent("Trigger Mode"));

            if (GetGroupTriggerMode(groupProp) != AudioPlayer.AudioGroupTriggerMode.Single)
            {
                line.y += lineHeight + 4f;
                EditorGUI.PropertyField(line, groupPlayModeProp, new GUIContent("Group Play Mode"));

                line.y += lineHeight + 4f;
                DrawEventSourceField(line, eventSourceProp, eventNameProp, eventComponentProp, "Group Event Source");

                line.y += lineHeight + 4f;
                DrawEventPopup(line, eventSourceProp, eventNameProp, eventComponentProp, "Group Event");

                line.y += lineHeight + 4f;
                var additionalEventsList = GetAdditionalEventsList(groupProp);
                var additionalEventsRect = new Rect(rect.x, line.y, rect.width, additionalEventsList.GetHeight());
                additionalEventsList.DoList(additionalEventsRect);
                line.y += additionalEventsList.GetHeight();

                if (RequiresStopEvent(GetPlayMode(groupPlayModeProp)))
                {
                    line.y += lineHeight + 4f;
                    DrawEventSourceField(line, stopEventSourceProp, stopEventNameProp, stopEventComponentProp, "Group Stop Event Source");

                    line.y += lineHeight + 4f;
                    DrawEventPopup(line, stopEventSourceProp, stopEventNameProp, stopEventComponentProp, "Group Stop Event");
                }
            }

            line.y += lineHeight + 4f;
            var listRect = new Rect(rect.x, line.y, rect.width, rect.height - (lineHeight + 4f));
            var list = GetEntriesList(groupProp);
            list.DoList(listRect);
        }

        private ReorderableList GetEntriesList(SerializedProperty groupProp)
        {
            var entriesProp = groupProp.FindPropertyRelative("entries");
            var key = entriesProp.propertyPath;
            if (entriesLists.TryGetValue(key, out var list))
            {
                return list;
            }

            list = new ReorderableList(serializedObject, entriesProp, true, true, true, true);
            list.drawHeaderCallback = rect =>
            {
                string label = GetGroupTriggerMode(groupProp) == AudioPlayer.AudioGroupTriggerMode.Single
                    ? "Audio Entries"
                    : "Audio Entries (Playback Pool)";
                EditorGUI.LabelField(rect, label);
            };
            list.elementHeightCallback = index => GetEntryHeight(entriesProp.GetArrayElementAtIndex(index), groupProp);
            list.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                var element = entriesProp.GetArrayElementAtIndex(index);
                DrawEntryElement(rect, element, groupProp);
            };

            list.onAddCallback = _ =>
            {
                int newIndex = entriesProp.arraySize;
                entriesProp.InsertArrayElementAtIndex(newIndex);
                var element = entriesProp.GetArrayElementAtIndex(newIndex);
                element.FindPropertyRelative("name").stringValue = string.Empty;
                element.FindPropertyRelative("clip").objectReferenceValue = null;
                element.FindPropertyRelative("loopClip").objectReferenceValue = null;
                element.FindPropertyRelative("playMode").enumValueIndex = groupProp.FindPropertyRelative("playMode").enumValueIndex;
                element.FindPropertyRelative("volume").floatValue = 1f;
                element.FindPropertyRelative("eventSource").objectReferenceValue = null;
                element.FindPropertyRelative("eventName").stringValue = string.Empty;
                element.FindPropertyRelative("eventComponent").objectReferenceValue = null;
                element.FindPropertyRelative("stopEventSource").objectReferenceValue = null;
                element.FindPropertyRelative("stopEventName").stringValue = string.Empty;
                element.FindPropertyRelative("stopEventComponent").objectReferenceValue = null;

            };

            list.onRemoveCallback = _ =>
            {
                if (list.index < 0 || list.index >= entriesProp.arraySize)
                {
                    return;
                }

                entriesProp.DeleteArrayElementAtIndex(list.index);
            };

            entriesLists[key] = list;
            return list;
        }

        private ReorderableList GetAdditionalEventsList(SerializedProperty groupProp)
        {
            var eventsProp = groupProp.FindPropertyRelative("additionalEvents");
            var key = eventsProp.propertyPath;
            if (additionalEventLists.TryGetValue(key, out var list))
            {
                return list;
            }

            list = new ReorderableList(serializedObject, eventsProp, true, true, true, true);
            list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Additional Group Events");
            list.elementHeightCallback = _ => (EditorGUIUtility.singleLineHeight + 4f) * 2 + 4f;
            list.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                var element = eventsProp.GetArrayElementAtIndex(index);
                var sourceProp = element.FindPropertyRelative("eventSource");
                var nameProp = element.FindPropertyRelative("eventName");
                var componentProp = element.FindPropertyRelative("eventComponent");

                float lineHeight = EditorGUIUtility.singleLineHeight;
                rect.y += 2f;

                var line = new Rect(rect.x, rect.y, rect.width, lineHeight);
                DrawEventSourceField(line, sourceProp, nameProp, componentProp, "Event Source");

                line.y += lineHeight + 4f;
                DrawEventPopup(line, sourceProp, nameProp, componentProp, "Event");
            };
            list.onAddCallback = _ =>
            {
                int newIndex = eventsProp.arraySize;
                eventsProp.InsertArrayElementAtIndex(newIndex);
                var element = eventsProp.GetArrayElementAtIndex(newIndex);
                element.FindPropertyRelative("eventSource").objectReferenceValue = null;
                element.FindPropertyRelative("eventName").stringValue = string.Empty;
                element.FindPropertyRelative("eventComponent").objectReferenceValue = null;
            };
            list.onRemoveCallback = _ =>
            {
                if (list.index < 0 || list.index >= eventsProp.arraySize)
                {
                    return;
                }

                eventsProp.DeleteArrayElementAtIndex(list.index);
            };

            additionalEventLists[key] = list;
            return list;
        }

        private float GetEntryHeight(SerializedProperty element, SerializedProperty groupProp)
        {
            var playModeProp = element.FindPropertyRelative("playMode");
            bool showEntryBinding = GetGroupTriggerMode(groupProp) == AudioPlayer.AudioGroupTriggerMode.Single;
            bool showEntryPlayMode = showEntryBinding;
            var playMode = GetEffectiveEntryPlayMode(groupProp, playModeProp);

            int lines = showEntryBinding ? 6 : 3; // Name + Clip + PlayMode + Volume (+ EventSource + Event)
            if (playMode == AudioPlayMode.LoopWithIntro)
            {
                lines += 1; // LoopClip
            }
            if (showEntryBinding && RequiresStopEvent(playMode))
            {
                lines += 2;
            }

            return (EditorGUIUtility.singleLineHeight + 4f) * lines + 4f;
        }

        private void DrawEntryElement(Rect rect, SerializedProperty element, SerializedProperty groupProp)
        {
            var nameProp = element.FindPropertyRelative("name");
            var clipProp = element.FindPropertyRelative("clip");
            var loopClipProp = element.FindPropertyRelative("loopClip");
            var playModeProp = element.FindPropertyRelative("playMode");
            var volumeProp = element.FindPropertyRelative("volume");
            var eventSourceProp = element.FindPropertyRelative("eventSource");
            var eventNameProp = element.FindPropertyRelative("eventName");
            var eventComponentProp = element.FindPropertyRelative("eventComponent");
            var stopEventSourceProp = element.FindPropertyRelative("stopEventSource");
            var stopEventNameProp = element.FindPropertyRelative("stopEventName");
            var stopEventComponentProp = element.FindPropertyRelative("stopEventComponent");
            bool showEntryBinding = GetGroupTriggerMode(groupProp) == AudioPlayer.AudioGroupTriggerMode.Single;
            bool showEntryPlayMode = showEntryBinding;

            float lineHeight = EditorGUIUtility.singleLineHeight;
            rect.y += 2f;

            var line = new Rect(rect.x, rect.y, rect.width, lineHeight);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUI.PropertyField(line, nameProp, new GUIContent("Name"));
            EditorGUI.EndDisabledGroup();

            line.y += lineHeight + 4f;
            var playMode = GetEffectiveEntryPlayMode(groupProp, playModeProp);
            string clipLabel = playMode == AudioPlayMode.LoopWithIntro ? "Intro Clip" : "Clip";

            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(line, clipProp, new GUIContent(clipLabel));
            if (EditorGUI.EndChangeCheck())
            {
                var clip = clipProp.objectReferenceValue as AudioClip;
                nameProp.stringValue = clip != null ? clip.name : string.Empty;
            }

            line.y += lineHeight + 4f;
            if (playMode == AudioPlayMode.LoopWithIntro)
            {
                EditorGUI.PropertyField(line, loopClipProp, new GUIContent("Loop Clip"));
                line.y += lineHeight + 4f;
            }

            if (showEntryPlayMode)
            {
                EditorGUI.PropertyField(line, playModeProp, new GUIContent("Play Mode"));
                line.y += lineHeight + 4f;
            }

            EditorGUI.PropertyField(line, volumeProp, new GUIContent("Volume"));

            if (showEntryBinding)
            {
                line.y += lineHeight + 4f;
                DrawEventSourceField(line, eventSourceProp, eventNameProp, eventComponentProp, "Event Source");
                line.y += lineHeight + 4f;
                DrawEventPopup(line, eventSourceProp, eventNameProp, eventComponentProp, "Event");

                if (RequiresStopEvent(playMode))
                {
                    line.y += lineHeight + 4f;
                    DrawEventSourceField(line, stopEventSourceProp, stopEventNameProp, stopEventComponentProp, "Stop Event Source");
                    line.y += lineHeight + 4f;
                    DrawEventPopup(line, stopEventSourceProp, stopEventNameProp, stopEventComponentProp, "Stop Event");
                }
            }

        }

        private static AudioPlayMode GetPlayMode(SerializedProperty playModeProp)
        {
            var names = playModeProp.enumNames;
            if (names == null || names.Length == 0)
            {
                return AudioPlayMode.Default;
            }

            int index = Mathf.Clamp(playModeProp.enumValueIndex, 0, names.Length - 1);
            return (AudioPlayMode)Enum.Parse(typeof(AudioPlayMode), names[index]);
        }

        private static AudioPlayMode GetEffectiveEntryPlayMode(SerializedProperty groupProp, SerializedProperty playModeProp)
        {
            if (GetGroupTriggerMode(groupProp) != AudioPlayer.AudioGroupTriggerMode.Single)
            {
                return GetPlayMode(groupProp.FindPropertyRelative("playMode"));
            }

            return GetPlayMode(playModeProp);
        }

        private static bool RequiresStopEvent(AudioPlayMode playMode)
        {
            return playMode == AudioPlayMode.Loop || playMode == AudioPlayMode.LoopWithIntro;
        }

        private static AudioPlayer.AudioGroupTriggerMode GetGroupTriggerMode(SerializedProperty groupProp)
        {
            var triggerModeProp = groupProp.FindPropertyRelative("triggerMode");
            var names = triggerModeProp.enumNames;
            if (names == null || names.Length == 0)
            {
                return AudioPlayer.AudioGroupTriggerMode.Single;
            }

            int index = Mathf.Clamp(triggerModeProp.enumValueIndex, 0, names.Length - 1);
            return (AudioPlayer.AudioGroupTriggerMode)Enum.Parse(typeof(AudioPlayer.AudioGroupTriggerMode), names[index]);
        }

        private static void DrawEventSourceField(Rect rect, SerializedProperty sourceProp, SerializedProperty eventNameProp, SerializedProperty eventComponentProp, string label)
        {
            EditorGUI.BeginChangeCheck();
            var newSource = EditorGUI.ObjectField(rect, label, sourceProp.objectReferenceValue, typeof(UnityEngine.Object), true);
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

        private void DrawEventPopup(Rect rect, SerializedProperty sourceProp, SerializedProperty eventNameProp, SerializedProperty eventComponentProp, string label)
        {
            var sourceObj = sourceProp.objectReferenceValue;
            if (sourceObj == null)
            {
                EditorGUI.PropertyField(rect, eventNameProp, new GUIContent(label));
                return;
            }

            var candidates = GetEventCandidates(sourceObj);
            if (candidates.Count == 0)
            {
                EditorGUI.PropertyField(rect, eventNameProp, new GUIContent(label));
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

            int newIndex = EditorGUI.Popup(rect, label, selectedIndex, display);
            eventNameProp.stringValue = candidates[newIndex].Name;
            eventComponentProp.objectReferenceValue = candidates[newIndex].Source;
        }

        private static List<EventCandidate> GetEventCandidates(UnityEngine.Object source)
        {
            var results = new List<EventCandidate>();
            // 仅显示 public 事件/UnityEvent，隐藏私有事件
            var flags = BindingFlags.Instance | BindingFlags.Public;
            if (source is Component component)
            {
                CollectCandidates(component, results, flags);
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

                CollectCandidates(comp, results, flags);
            }

            return results;
        }

        private static void CollectCandidates(Component source, List<EventCandidate> results, BindingFlags flags)
        {
            var seen = new HashSet<string>();
            var type = source.GetType();

            foreach (var evt in type.GetEvents(flags))
            {
                if (seen.Contains(evt.Name))
                {
                    continue;
                }

                var display = $"{type.Name}.{evt.Name} (event{FormatSignature(evt.EventHandlerType)})";
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

        private static string FormatSignature(Type delegateType)
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
