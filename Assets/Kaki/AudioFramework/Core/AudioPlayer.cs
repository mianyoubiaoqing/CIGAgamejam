using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

namespace Kaki
{


    /// <summary>
    /// 音频播放器
    /// （在 Inspector 里配置 AudioEntry，拖入事件源与事件名，为任意脚本的事件驱动为音频播放）
    /// </summary>
    [DisallowMultipleComponent]
    public class AudioPlayer : MonoBehaviour
    {
        public static AudioPlayer Instance { get; private set; }

        [Serializable]
        public enum AudioGroupTriggerMode
        {
            Single = 0,
            Sequential = 1,
            RandomNoRepeat = 2
        }

        [Serializable]
        public class Binding
        {
            [Tooltip("配置项")]
            [SerializeField] private string name;
            [Tooltip("要播放的音频剪辑")]
            public AudioClip clip;
            [Tooltip("LoopWithIntro的循环主体")]
            public AudioClip loopClip;
            // 播放方式
            public AudioPlayMode playMode = AudioPlayMode.Default;
            // 单条音量（0~1）
            [Range(0f, 1f)] public float volume = 1f;
            // 事件来源（可拖拽 GameObject 或任意组件）
            [Tooltip("事件来源（可拖拽 GameObject 或任意组件）")]
            public UnityEngine.Object eventSource;
            // 事件名（C# event 或 UnityEvent 成员名）
            [Tooltip("事件名（C# event 或 UnityEvent 成员名）")]
            public string eventName;
            [Tooltip("停止事件来源（用于 Loop / LoopWithIntro）")]
            public UnityEngine.Object stopEventSource;
            [Tooltip("停止事件名（用于 Loop / LoopWithIntro）")]
            public string stopEventName;

            // 运行时缓存（用于解绑）
            [NonSerialized] public Delegate handler;
            [NonSerialized] public EventInfo eventInfo;
            [NonSerialized] public UnityEventBase unityEvent;
            [NonSerialized] public Delegate stopHandler;
            [NonSerialized] public EventInfo stopEventInfo;
            [NonSerialized] public UnityEventBase stopUnityEvent;
            // 选择的具体组件（用于GameObject来源时的精确绑定）
            [HideInInspector] public Component eventComponent;
            [HideInInspector] public Component stopEventComponent;
            // 运行时音量缓存（用于检测实时变化）
            [NonSerialized] public float lastVolume = -1f;

            public string Name => name;
            public void SyncName()
            {
                name = clip != null ? clip.name : string.Empty;
            }
        }

        [Serializable]
        public class AudioGroup
        {
            [Tooltip("AudioGroup 名称")]
            public string groupName = "Audio Group";
            // 该组统一的音频类型（BGM/SFX）
            [Tooltip("该组统一的音频类型（BGM/SFX）")]
            public AudioType audioType = AudioType.SFX;
            [Tooltip("该组的事件触发方式")]
            public AudioGroupTriggerMode triggerMode = AudioGroupTriggerMode.Single;
            [Tooltip("该组统一的播放方式")]
            public AudioPlayMode playMode = AudioPlayMode.Default;
            [Tooltip("Group 级事件来源（可拖拽 GameObject 或任意组件）")]
            public UnityEngine.Object eventSource;
            [Tooltip("Group 级事件名（C# event 或 UnityEvent 成员名）")]
            public string eventName;
            [Tooltip("Group 级停止事件来源（用于 Loop / LoopWithIntro）")]
            public UnityEngine.Object stopEventSource;
            [Tooltip("Group 级停止事件名（用于 Loop / LoopWithIntro）")]
            public string stopEventName;
            // 该组内的所有音频条目
            public List<Binding> entries = new List<Binding>();

            [NonSerialized] public Delegate handler;
            [NonSerialized] public EventInfo eventInfo;
            [NonSerialized] public UnityEventBase unityEvent;
            [NonSerialized] public Delegate stopHandler;
            [NonSerialized] public EventInfo stopEventInfo;
            [NonSerialized] public UnityEventBase stopUnityEvent;
            [HideInInspector] public Component eventComponent;
            [HideInInspector] public Component stopEventComponent;
            [NonSerialized] public int nextSequentialIndex;
            [NonSerialized] public List<int> randomOrder = new List<int>();
            [NonSerialized] public int randomCursor;

            public string DisplayName => string.IsNullOrWhiteSpace(groupName) ? "Audio Group" : groupName;
        }

        public readonly struct PlayableEntry
        {
            public readonly int GroupIndex;
            public readonly int EntryIndex;
            public readonly string GroupName;
            public readonly string Label;
            public readonly AudioType AudioType;
            public readonly AudioPlayMode PlayMode;
            public readonly AudioGroupTriggerMode TriggerMode;

            public PlayableEntry(int groupIndex, int entryIndex, string groupName, string label, AudioType audioType, AudioPlayMode playMode, AudioGroupTriggerMode triggerMode)
            {
                GroupIndex = groupIndex;
                EntryIndex = entryIndex;
                GroupName = groupName;
                Label = label;
                AudioType = audioType;
                PlayMode = playMode;
                TriggerMode = triggerMode;
            }
        }

        // 所有分组
        [Tooltip("所有分组")]
        [SerializeField] private List<AudioGroup> audioGroups = new List<AudioGroup>();

        private Binding currentBgmEntry;
        private Binding currentSfxLoopEntry;
        private Binding currentDefaultBgmEntry;
        private Binding currentDefaultSfxEntry;

        // 单例初始化，避免场景重复创建
        private void Awake()
        {
            EnsureGroups();
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // 启用时绑定全部事件
        private void OnEnable()
        {
            BindAll();
        }

        // 禁用时解绑全部事件
        private void OnDisable()
        {
            UnbindAll();
        }

        // 运行时检测 AudioEntry 音量变化并实时应用
        private void Update()
        {
            var manager = AudioManager.Instance;
            if (manager == null)
            {
                return;
            }

            if (currentBgmEntry != null && !Mathf.Approximately(currentBgmEntry.volume, currentBgmEntry.lastVolume))
            {
                manager.SetCurrentBGMEntryVolume(currentBgmEntry.volume);
                currentBgmEntry.lastVolume = currentBgmEntry.volume;
            }

            if (currentDefaultBgmEntry != null && !Mathf.Approximately(currentDefaultBgmEntry.volume, currentDefaultBgmEntry.lastVolume))
            {
                manager.SetDefaultBgmVolume(currentDefaultBgmEntry.volume);
                currentDefaultBgmEntry.lastVolume = currentDefaultBgmEntry.volume;
            }

            if (currentSfxLoopEntry != null && !Mathf.Approximately(currentSfxLoopEntry.volume, currentSfxLoopEntry.lastVolume))
            {
                manager.SetSfxLoopVolume(currentSfxLoopEntry.volume);
                currentSfxLoopEntry.lastVolume = currentSfxLoopEntry.volume;
            }

            if (currentDefaultSfxEntry != null && !Mathf.Approximately(currentDefaultSfxEntry.volume, currentDefaultSfxEntry.lastVolume))
            {
                manager.SetDefaultSfxVolume(currentDefaultSfxEntry.volume);
                currentDefaultSfxEntry.lastVolume = currentDefaultSfxEntry.volume;
            }
        }

        private void OnValidate()
        {
            EnsureGroups();
            SyncAllNames();
        }

        // 清理单例引用
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void ReplaceGroups(List<AudioGroup> groups)
        {
            bool shouldRebind = isActiveAndEnabled;
            if (shouldRebind)
            {
                UnbindAll();
            }

            audioGroups = groups ?? new List<AudioGroup>();
            currentBgmEntry = null;
            currentSfxLoopEntry = null;
            currentDefaultBgmEntry = null;
            currentDefaultSfxEntry = null;

            EnsureGroups();
            SyncAllNames();

            if (shouldRebind)
            {
                BindAll();
            }
        }

        public List<PlayableEntry> GetPlayableEntries()
        {
            EnsureGroups();
            SyncAllNames();

            var results = new List<PlayableEntry>();
            if (audioGroups == null)
            {
                return results;
            }

            for (int groupIndex = 0; groupIndex < audioGroups.Count; groupIndex++)
            {
                var group = audioGroups[groupIndex];
                if (group == null || group.entries == null)
                {
                    continue;
                }

                if (group.triggerMode != AudioGroupTriggerMode.Single)
                {
                    var representativeBinding = GetFirstPlayableBinding(group);
                    if (representativeBinding == null)
                    {
                        continue;
                    }

                    string label = $"{group.triggerMode} - {group.DisplayName}";
                    results.Add(new PlayableEntry(
                        groupIndex,
                        -1,
                        group.DisplayName,
                        label,
                        group.audioType,
                        GetEffectivePlayMode(group, representativeBinding),
                        group.triggerMode));
                    continue;
                }

                for (int entryIndex = 0; entryIndex < group.entries.Count; entryIndex++)
                {
                    var binding = group.entries[entryIndex];
                    if (binding == null || binding.clip == null)
                    {
                        continue;
                    }

                    var label = string.IsNullOrWhiteSpace(binding.Name) ? binding.clip.name : binding.Name;
                    results.Add(new PlayableEntry(groupIndex, entryIndex, group.DisplayName, label, group.audioType, GetEffectivePlayMode(group, binding), group.triggerMode));
                }
            }

            return results;
        }

        public bool HasPlayableEntries()
        {
            if (audioGroups == null)
            {
                return false;
            }

            foreach (var group in audioGroups)
            {
                if (group == null || group.entries == null)
                {
                    continue;
                }

                foreach (var binding in group.entries)
                {
                    if (binding != null && binding.clip != null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool PlayEntryAt(int groupIndex, int entryIndex)
        {
            EnsureGroups();
            if (audioGroups == null || groupIndex < 0 || groupIndex >= audioGroups.Count)
            {
                return false;
            }

            var group = audioGroups[groupIndex];
            if (group == null || group.entries == null)
            {
                return false;
            }

            if (group.triggerMode != AudioGroupTriggerMode.Single)
            {
                int selectedIndex = GetNextPlayableEntryIndex(group);
                if (selectedIndex < 0 || selectedIndex >= group.entries.Count)
                {
                    return false;
                }

                var selectedBinding = group.entries[selectedIndex];
                if (selectedBinding == null || selectedBinding.clip == null)
                {
                    return false;
                }

                PlayEntry(selectedBinding, group.audioType, group.playMode);
                return true;
            }

            if (entryIndex < 0 || entryIndex >= group.entries.Count)
            {
                return false;
            }

            var binding = group.entries[entryIndex];
            if (binding == null || binding.clip == null)
            {
                return false;
            }

            PlayEntry(binding, group.audioType);
            return true;
        }

        // 批量绑定
        private void BindAll()
        {
            EnsureGroups();
            SyncAllNames();
            foreach (var group in audioGroups)
            {
                if (group == null)
                {
                    continue;
                }

                ResetGroupRuntimeState(group);
                if (group.triggerMode == AudioGroupTriggerMode.Single)
                {
                    continue;
                }

                BindGroup(group);
            }

            foreach (var item in EnumerateEntries())
            {
                if (item.group.triggerMode != AudioGroupTriggerMode.Single)
                {
                    continue;
                }

                Bind(item.binding, item.audioType);
            }
        }

        // 批量解绑
        private void UnbindAll()
        {
            if (audioGroups != null)
            {
                foreach (var group in audioGroups)
                {
                    UnbindGroup(group);
                }
            }

            foreach (var item in EnumerateEntries())
            {
                Unbind(item.binding);
            }
        }

        private void SyncAllNames()
        {
            if (audioGroups == null)
            {
                return;
            }

            foreach (var group in audioGroups)
            {
                if (group == null || group.entries == null)
                {
                    continue;
                }

                foreach (var binding in group.entries)
                {
                    if (binding == null)
                    {
                        continue;
                    }

                    binding.SyncName();
                    if (group.triggerMode != AudioGroupTriggerMode.Single)
                    {
                        binding.playMode = group.playMode;
                    }
                }

                if (string.IsNullOrWhiteSpace(group.groupName))
                {
                    group.groupName = $"Audio Group {audioGroups.IndexOf(group) + 1}";
                }
            }
        }

        private void BindGroup(AudioGroup group)
        {
            if (group == null || group.eventSource == null || string.IsNullOrWhiteSpace(group.eventName))
            {
                return;
            }

            TryBindEvent(
                group.eventSource,
                group.eventName,
                group.eventComponent,
                () => PlayNextEntryFromGroup(group),
                ref group.eventInfo,
                ref group.unityEvent,
                ref group.handler,
                ref group.eventComponent);

            if (!RequiresStopEvent(group.playMode))
            {
                return;
            }

            TryBindEvent(
                group.stopEventSource,
                group.stopEventName,
                group.stopEventComponent,
                () => StopLoopByType(group.audioType),
                ref group.stopEventInfo,
                ref group.stopUnityEvent,
                ref group.stopHandler,
                ref group.stopEventComponent);
        }

        private void UnbindGroup(AudioGroup group)
        {
            if (group == null)
            {
                return;
            }

            if (group.eventInfo != null && group.handler != null && group.eventComponent != null)
            {
                group.eventInfo.RemoveEventHandler(group.eventComponent, group.handler);
            }
            else if (group.unityEvent != null && group.handler != null)
            {
                TryRemoveUnityEventListener(group.unityEvent, group.handler);
            }

            group.eventInfo = null;
            group.unityEvent = null;
            group.handler = null;
            group.eventComponent = null;

            if (group.stopEventInfo != null && group.stopHandler != null && group.stopEventComponent != null)
            {
                group.stopEventInfo.RemoveEventHandler(group.stopEventComponent, group.stopHandler);
            }
            else if (group.stopUnityEvent != null && group.stopHandler != null)
            {
                TryRemoveUnityEventListener(group.stopUnityEvent, group.stopHandler);
            }

            group.stopEventInfo = null;
            group.stopUnityEvent = null;
            group.stopHandler = null;
            group.stopEventComponent = null;
        }

        // 绑定单个事件
        private void Bind(Binding binding, AudioType audioType)
        {
            if (binding == null || binding.eventSource == null || string.IsNullOrWhiteSpace(binding.eventName))
            {
                return;
            }

            TryBindEvent(
                binding.eventSource,
                binding.eventName,
                binding.eventComponent,
                () => PlayEntry(binding, audioType),
                ref binding.eventInfo,
                ref binding.unityEvent,
                ref binding.handler,
                ref binding.eventComponent);

            if (!RequiresStopEvent(binding.playMode))
            {
                return;
            }

            TryBindEvent(
                binding.stopEventSource,
                binding.stopEventName,
                binding.stopEventComponent,
                () => StopLoopByType(audioType),
                ref binding.stopEventInfo,
                ref binding.stopUnityEvent,
                ref binding.stopHandler,
                ref binding.stopEventComponent);
        }

        // 解绑单个事件
        private void Unbind(Binding binding)
        {
            if (binding == null)
            {
                return;
            }

            if (binding.eventInfo != null && binding.handler != null && binding.eventComponent != null)
            {
                binding.eventInfo.RemoveEventHandler(binding.eventComponent, binding.handler);
            }
            else if (binding.unityEvent != null && binding.handler != null)
            {
                TryRemoveUnityEventListener(binding.unityEvent, binding.handler);
            }

            binding.eventInfo = null;
            binding.unityEvent = null;
            binding.handler = null;
            binding.eventComponent = null;

            if (binding.stopEventInfo != null && binding.stopHandler != null && binding.stopEventComponent != null)
            {
                binding.stopEventInfo.RemoveEventHandler(binding.stopEventComponent, binding.stopHandler);
            }
            else if (binding.stopUnityEvent != null && binding.stopHandler != null)
            {
                TryRemoveUnityEventListener(binding.stopUnityEvent, binding.stopHandler);
            }

            binding.stopEventInfo = null;
            binding.stopUnityEvent = null;
            binding.stopHandler = null;
            binding.stopEventComponent = null;
        }

        // 实际播放逻辑：根据类型走不同通道
        private void PlayEntry(Binding binding, AudioType audioType, AudioPlayMode? overridePlayMode = null)
        {
            if (binding == null || binding.clip == null)
            {
                return;
            }

            var manager = AudioManager.Instance;
            if (manager == null)
            {
                return;
            }

            var playMode = overridePlayMode ?? binding.playMode;
            switch (playMode)
            {
                case AudioPlayMode.Default:
                    PlayDefaultByType(binding, audioType, manager);
                    break;
                case AudioPlayMode.Overlay:
                    PlayOverlayByType(binding, audioType, manager);
                    break;
                case AudioPlayMode.Trigger:
                    PlayTrigger(binding, audioType, manager);
                    break;
                case AudioPlayMode.Loop:
                    PlayLoopByType(binding, audioType, manager);
                    break;
                case AudioPlayMode.LoopWithIntro:
                    PlayLoopWithIntroByType(binding, audioType, manager);
                    break;
            }

            UpdateCurrentEntry(binding, audioType, playMode);
        }

        private void PlayTrigger(Binding binding, AudioType audioType, AudioManager manager)
        {
            manager.PlayTrigger(binding.clip, audioType, binding.volume);
        }

        private void PlayDefaultByType(Binding binding, AudioType audioType, AudioManager manager)
        {
            manager.PlayDefault(binding.clip, audioType, binding.volume);
        }

        private void PlayOverlayByType(Binding binding, AudioType audioType, AudioManager manager)
        {
            manager.PlayOverlay(binding.clip, audioType, binding.volume);
        }

        private void PlayLoopByType(Binding binding, AudioType audioType, AudioManager manager)
        {
            if (audioType == AudioType.BGM)
            {
                manager.PlayBGMClip(binding.clip, binding.volume);
            }
            else
            {
                manager.PlaySFXLoop(binding.clip, binding.volume);
            }
        }

        private void PlayLoopWithIntroByType(Binding binding, AudioType audioType, AudioManager manager)
        {
            if (binding.loopClip == null)
            {
                Debug.LogWarning("AudioPlayer: LoopWithIntro requires loopClip, fallback to Loop.");
                PlayLoopByType(binding, audioType, manager);
                return;
            }

            if (audioType == AudioType.BGM)
            {
                manager.PlayBgmOnceThen(binding.clip, binding.loopClip, binding.volume, binding.volume);
            }
            else
            {
                manager.PlaySFXOnceThenLoop(binding.clip, binding.loopClip, binding.volume);
            }
        }

        private void UpdateCurrentEntry(Binding binding, AudioType audioType, AudioPlayMode playMode)
        {
            if (binding == null)
            {
                return;
            }

            binding.lastVolume = binding.volume;

            if (playMode == AudioPlayMode.Default)
            {
                if (audioType == AudioType.BGM)
                {
                    currentDefaultBgmEntry = binding;
                }
                else
                {
                    currentDefaultSfxEntry = binding;
                }
                return;
            }

            if (audioType == AudioType.BGM &&
                (playMode == AudioPlayMode.Loop || playMode == AudioPlayMode.LoopWithIntro))
            {
                currentBgmEntry = binding;
                currentDefaultBgmEntry = null;
                return;
            }

            if (audioType == AudioType.SFX &&
                (playMode == AudioPlayMode.Loop || playMode == AudioPlayMode.LoopWithIntro))
            {
                currentSfxLoopEntry = binding;
                currentDefaultSfxEntry = null;
            }
        }

        private void EnsureGroups()
        {
            if (audioGroups == null)
            {
                audioGroups = new List<AudioGroup>();
            }

            if (audioGroups.Count > 0)
            {
                for (int i = 0; i < audioGroups.Count; i++)
                {
                    var group = audioGroups[i];
                    if (group == null)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(group.groupName))
                    {
                        group.groupName = $"Audio Group {i + 1}";
                    }

                    if (group.entries == null)
                    {
                        group.entries = new List<Binding>();
                    }

                    if (group.randomOrder == null)
                    {
                        group.randomOrder = new List<int>();
                    }
                }

                return;
            }
        }

        private void PlayNextEntryFromGroup(AudioGroup group)
        {
            if (group == null || group.triggerMode == AudioGroupTriggerMode.Single)
            {
                return;
            }

            int entryIndex = GetNextPlayableEntryIndex(group);
            if (entryIndex < 0 || group.entries == null || entryIndex >= group.entries.Count)
            {
                return;
            }

            var binding = group.entries[entryIndex];
            if (binding == null || binding.clip == null)
            {
                return;
            }

            PlayEntry(binding, group.audioType, group.playMode);
        }

        private int GetNextPlayableEntryIndex(AudioGroup group)
        {
            if (group == null || group.entries == null || group.entries.Count == 0)
            {
                return -1;
            }

            var playableIndices = new List<int>();
            for (int i = 0; i < group.entries.Count; i++)
            {
                var binding = group.entries[i];
                if (binding != null && binding.clip != null)
                {
                    playableIndices.Add(i);
                }
            }

            if (playableIndices.Count == 0)
            {
                return -1;
            }

            if (group.triggerMode == AudioGroupTriggerMode.Sequential)
            {
                int selected = playableIndices[group.nextSequentialIndex % playableIndices.Count];
                group.nextSequentialIndex = (group.nextSequentialIndex + 1) % playableIndices.Count;
                return selected;
            }

            if (group.triggerMode == AudioGroupTriggerMode.RandomNoRepeat)
            {
                if (group.randomOrder == null)
                {
                    group.randomOrder = new List<int>();
                }

                bool needsRefresh = group.randomOrder.Count != playableIndices.Count || group.randomCursor >= group.randomOrder.Count;
                if (!needsRefresh)
                {
                    for (int i = 0; i < playableIndices.Count; i++)
                    {
                        if (!group.randomOrder.Contains(playableIndices[i]))
                        {
                            needsRefresh = true;
                            break;
                        }
                    }
                }

                if (needsRefresh)
                {
                    group.randomOrder.Clear();
                    group.randomOrder.AddRange(playableIndices);
                    Shuffle(group.randomOrder);
                    group.randomCursor = 0;
                }

                int selected = group.randomOrder[group.randomCursor];
                group.randomCursor++;
                if (group.randomCursor >= group.randomOrder.Count)
                {
                    group.randomCursor = group.randomOrder.Count;
                }

                return selected;
            }

            return playableIndices[0];
        }

        private static void Shuffle(List<int> values)
        {
            if (values == null)
            {
                return;
            }

            for (int i = values.Count - 1; i > 0; i--)
            {
                int swapIndex = UnityEngine.Random.Range(0, i + 1);
                (values[i], values[swapIndex]) = (values[swapIndex], values[i]);
            }
        }

        private static void ResetGroupRuntimeState(AudioGroup group)
        {
            if (group == null)
            {
                return;
            }

            group.nextSequentialIndex = 0;
            if (group.randomOrder == null)
            {
                group.randomOrder = new List<int>();
            }
            else
            {
                group.randomOrder.Clear();
            }

            group.randomCursor = 0;
        }

        private IEnumerable<(AudioGroup group, Binding binding, AudioType audioType)> EnumerateEntries()
        {
            if (audioGroups == null)
            {
                yield break;
            }

            foreach (var group in audioGroups)
            {
                if (group == null || group.entries == null)
                {
                    continue;
                }

                var type = group.audioType;
                foreach (var binding in group.entries)
                {
                    if (binding == null)
                    {
                        continue;
                    }

                    yield return (group, binding, type);
                }
            }
        }

        // 从组件上读取 UnityEvent（字段或属性）
        private static bool TryGetUnityEvent(Component source, string eventName, BindingFlags flags, out UnityEventBase unityEvent)
        {
            unityEvent = null;

            var sourceType = source.GetType();
            var field = sourceType.GetField(eventName, flags);
            if (field != null && typeof(UnityEventBase).IsAssignableFrom(field.FieldType))
            {
                unityEvent = field.GetValue(source) as UnityEventBase;
                return unityEvent != null;
            }

            var property = sourceType.GetProperty(eventName, flags);
            if (property != null && property.CanRead && typeof(UnityEventBase).IsAssignableFrom(property.PropertyType))
            {
                unityEvent = property.GetValue(source, null) as UnityEventBase;
                return unityEvent != null;
            }

            return false;
        }

        private static Binding GetFirstPlayableBinding(AudioGroup group)
        {
            if (group == null || group.entries == null)
            {
                return null;
            }

            for (int i = 0; i < group.entries.Count; i++)
            {
                var binding = group.entries[i];
                if (binding != null && binding.clip != null)
                {
                    return binding;
                }
            }

            return null;
        }

        private static AudioPlayMode GetEffectivePlayMode(AudioGroup group, Binding binding)
        {
            if (group != null && group.triggerMode != AudioGroupTriggerMode.Single)
            {
                return group.playMode;
            }

            return binding != null ? binding.playMode : AudioPlayMode.Default;
        }

        private static bool RequiresStopEvent(AudioPlayMode playMode)
        {
            return playMode == AudioPlayMode.Loop || playMode == AudioPlayMode.LoopWithIntro;
        }

        private void StopLoopByType(AudioType audioType)
        {
            var manager = AudioManager.Instance;
            if (manager == null)
            {
                return;
            }

            if (audioType == AudioType.BGM)
            {
                manager.StopBGM();
            }
            else
            {
                manager.StopSFX();
            }
        }

        // 从绑定中解析实际的事件组件：
        // 1) 若拖入的是组件，直接使用
        // 2) 若拖入的是GameObject，则在其所有组件中查找包含该事件名的组件
        private static Component ResolveEventComponent(Binding binding)
        {
            if (binding == null)
            {
                return null;
            }

            return ResolveEventComponent(binding.eventSource, binding.eventName, binding.eventComponent);
        }

        private static Component ResolveEventComponent(UnityEngine.Object eventSource, string eventName, Component cachedComponent)
        {
            if (cachedComponent != null)
            {
                return cachedComponent;
            }

            if (eventSource is Component component)
            {
                return component;
            }

            var go = eventSource as GameObject;
            if (go == null || string.IsNullOrWhiteSpace(eventName))
            {
                return null;
            }

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Component[] components = go.GetComponents<Component>();
            Component firstMatch = null;
            int matchCount = 0;

            foreach (var comp in components)
            {
                if (comp == null)
                {
                    continue;
                }

                var type = comp.GetType();
                bool hasEvent = type.GetEvent(eventName, flags) != null;
                if (!hasEvent && TryGetUnityEvent(comp, eventName, flags, out _))
                {
                    hasEvent = true;
                }

                if (!hasEvent)
                {
                    continue;
                }

                matchCount++;
                if (firstMatch == null)
                {
                    firstMatch = comp;
                }
            }

            if (matchCount > 1)
            {
                Debug.LogWarning($"AudioPlayer: multiple components on '{go.name}' contain event '{eventName}', using the first match.");
            }

            return firstMatch;
        }

        private void TryBindEvent(
            UnityEngine.Object eventSource,
            string eventName,
            Component cachedComponent,
            Action callback,
            ref EventInfo eventInfo,
            ref UnityEventBase unityEvent,
            ref Delegate handler,
            ref Component eventComponent)
        {
            if (eventSource == null || string.IsNullOrWhiteSpace(eventName) || callback == null)
            {
                return;
            }

            var source = ResolveEventComponent(eventSource, eventName, cachedComponent);
            if (source == null)
            {
                return;
            }

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var sourceType = source.GetType();
            eventInfo = sourceType.GetEvent(eventName, flags);
            if (eventInfo != null)
            {
                var del = CreateDelegate(eventInfo.EventHandlerType, callback);
                if (del == null)
                {
                    Debug.LogWarning($"AudioPlayer: event '{eventName}' has unsupported signature on {sourceType.Name}.", this);
                    eventInfo = null;
                    return;
                }

                eventInfo.AddEventHandler(source, del);
                handler = del;
                eventComponent = source;
                return;
            }

            if (TryGetUnityEvent(source, eventName, flags, out unityEvent))
            {
                if (TryAddUnityEventListener(unityEvent, callback, out var del))
                {
                    handler = del;
                    eventComponent = source;
                    return;
                }

                Debug.LogWarning($"AudioPlayer: UnityEvent '{eventName}' has unsupported signature on {sourceType.Name}.", this);
                unityEvent = null;
                return;
            }

            Debug.LogWarning($"AudioPlayer: event '{eventName}' not found on {sourceType.Name}.", this);
        }

        // 使用反射为 UnityEvent 添加监听
        private static bool TryAddUnityEventListener(UnityEventBase unityEvent, Action callback, out Delegate handler)
        {
            handler = null;
            if (unityEvent == null)
            {
                return false;
            }

            var eventType = unityEvent.GetType();
            var addMethod = eventType.GetMethod("AddListener", BindingFlags.Instance | BindingFlags.Public);
            if (addMethod == null)
            {
                return false;
            }

            var paramType = addMethod.GetParameters()[0].ParameterType;
            var del = CreateDelegate(paramType, callback);
            if (del == null)
            {
                return false;
            }

            addMethod.Invoke(unityEvent, new object[] { del });
            handler = del;
            return true;
        }

        // 使用反射从 UnityEvent 移除监听
        private static void TryRemoveUnityEventListener(UnityEventBase unityEvent, Delegate handler)
        {
            if (unityEvent == null || handler == null)
            {
                return;
            }

            var eventType = unityEvent.GetType();
            var removeMethod = eventType.GetMethod("RemoveListener", BindingFlags.Instance | BindingFlags.Public);
            if (removeMethod == null)
            {
                return;
            }

            removeMethod.Invoke(unityEvent, new object[] { handler });
        }

        // 用 Expression 构建一个“忽略参数”的委托（只要事件返回 void 即可）
        private static Delegate CreateDelegate(Type delegateType, Action callback)
        {
            if (delegateType == null || callback == null)
            {
                return null;
            }

            var invoke = delegateType.GetMethod("Invoke");
            if (invoke == null || invoke.ReturnType != typeof(void))
            {
                return null;
            }

            var parameters = invoke.GetParameters();
            var parameterExpressions = new ParameterExpression[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                parameterExpressions[i] = Expression.Parameter(parameters[i].ParameterType, parameters[i].Name);
            }

            var callbackConst = Expression.Constant(callback);
            var body = Expression.Call(callbackConst, typeof(Action).GetMethod("Invoke"));
            var lambda = Expression.Lambda(delegateType, body, parameterExpressions);
            return lambda.Compile();
        }
    }
}
