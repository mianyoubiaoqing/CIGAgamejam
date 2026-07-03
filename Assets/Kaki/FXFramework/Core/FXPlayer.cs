using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace Kaki
{
    // FXPlayer：
    // 一个“事件 -> 触发器方法”的通用绑定器。
    // 使用方式：
    // 1) 在场景中挂载 FXPlayer 组件（可放在单例对象上）。
    // 2) 在目标脚本的触发器方法上添加 [TriggerAction] 特性。
    // 3) 在 FXPlayer 的 Inspector 里新增一个 Group：
    //    - Object 指向“包含触发器方法”的对象（Component 或 GameObject）。
    // 4) 在 Group 的 Trigger Methods 中添加一条绑定：
    //    - Event Source：事件来源对象（Component 或 GameObject）
    //    - Event：选择要监听的事件（C# event 或 UnityEvent）
    //    - Method：选择要触发的方法（只显示带 [TriggerAction] 的方法）
    //    - Args：填写方法需要的参数
    // 5) 运行时，当事件触发时，FXPlayer 会调用对应的方法。
    //
    // 说明：
    // - 事件参数会被忽略（只要事件返回 void 即可）。
    // - 支持方法参数类型：int/float/bool/string/Vector2/Vector3/Color/Enum/UnityEngine.Object。
    // - 若 Object 是 GameObject，会在其所有组件中查找 [TriggerAction] 方法。
    // - 若同名方法有多个重载，FXPlayer 通过“签名”来区分。
    [DisallowMultipleComponent]
    public class FXPlayer : MonoBehaviour
    {
        // 单例入口（全局 FX 播放器）
        public static FXPlayer Instance { get; private set; }
        // 可序列化的参数类型
        public enum FXArgumentKind
        {
            Int,
            Float,
            Bool,
            String,
            Vector2,
            Vector3,
            Color,
            Enum,
            Object
        }

        [Serializable]
        public class FXArgument
        {
            [Tooltip("配置项：kind")]
            public FXArgumentKind kind;
            [Tooltip("配置项：intValue")]
            public int intValue;
            [Tooltip("配置项：floatValue")]
            public float floatValue;
            [Tooltip("配置项：boolValue")]
            public bool boolValue;
            [Tooltip("配置项：stringValue")]
            public string stringValue;
            [Tooltip("配置项：vector2Value")]
            public Vector2 vector2Value;
            [Tooltip("配置项：vector3Value")]
            public Vector3 vector3Value;
            [Tooltip("配置项：white")]
            public Color colorValue = Color.white;
            [Tooltip("配置项：objectValue")]
            public UnityEngine.Object objectValue;

            // 用于枚举参数
            [Tooltip("用于枚举参数")]
            public string enumTypeName;
            [Tooltip("配置项：enumValue")]
            public int enumValue;
        }

        [Serializable]
        public class FXBinding
        {
            [Tooltip("配置项")]
            [SerializeField] private string name;
            // 事件来源（可拖拽 GameObject 或任意组件）
            public UnityEngine.Object eventSource;
            // 事件名（C# event 或 UnityEvent 成员名）
            [Tooltip("事件名（C# event 或 UnityEvent 成员名）")]
            public string eventName;
            // 触发的方法名（只允许 [TriggerAction] 标记的方法）
            [Tooltip("触发的方法名（只允许 [TriggerAction] 标记的方法）")]
            public string methodName;
            // 方法签名（用于区分重载）
            [Tooltip("方法签名（用于区分重载）")]
            public string methodSignature;
            // 方法参数
            public List<FXArgument> arguments = new List<FXArgument>();

            // 运行时缓存（用于解绑）
            [NonSerialized] public Delegate handler;
            [NonSerialized] public EventInfo eventInfo;
            [NonSerialized] public UnityEventBase unityEvent;
            [NonSerialized] public MethodInfo methodInfo;

            // 选择的具体组件（用于GameObject来源时的精确绑定）
            [HideInInspector] public Component eventComponent;
            [HideInInspector] public Component methodComponent;

            public string Name => name;

            public void SyncName()
            {
                name = string.IsNullOrWhiteSpace(methodName) ? string.Empty : methodName;
            }
        }

        [Serializable]
        public class FXGroup
        {
            // 目标对象（包含触发器方法）
            [FormerlySerializedAs("target")]
            [Tooltip("目标对象（包含触发器方法）")]
            public UnityEngine.Object @object;
            // 该目标对象对应的绑定列表
            [FormerlySerializedAs("bindings")]
            public List<FXBinding> fxTriggerMethods = new List<FXBinding>();
        }

        public readonly struct TriggerableBinding
        {
            public readonly int GroupIndex;
            public readonly int BindingIndex;
            public readonly string Label;
            public readonly string TargetName;

            public TriggerableBinding(int groupIndex, int bindingIndex, string label, string targetName)
            {
                GroupIndex = groupIndex;
                BindingIndex = bindingIndex;
                Label = label;
                TargetName = targetName;
            }
        }

        [Tooltip("配置项")]
        [SerializeField] private List<FXGroup> groups = new List<FXGroup>();

        // 查询是否已绑定某个事件名（用于判断是否已由 FXPlayer 接管）
        public bool HasBinding(string eventName)
        {
            if (string.IsNullOrWhiteSpace(eventName)) return false;

            for (int i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                if (group == null || group.fxTriggerMethods == null) continue;

                for (int j = 0; j < group.fxTriggerMethods.Count; j++)
                {
                    var binding = group.fxTriggerMethods[j];
                    if (binding == null) continue;
                    if (string.Equals(binding.eventName, eventName, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void Awake()
        {
            // 单例模式，避免场景重复创建
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            SyncAllNames();
            BindAll();
        }

        private void OnDisable()
        {
            UnbindAll();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnValidate()
        {
            SyncAllNames();
        }

        public List<TriggerableBinding> GetTriggerableBindings()
        {
            SyncAllNames();

            var results = new List<TriggerableBinding>();
            if (groups == null)
            {
                return results;
            }

            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                var group = groups[groupIndex];
                if (group == null || group.fxTriggerMethods == null)
                {
                    continue;
                }

                string targetName = group.@object != null ? group.@object.name : "Unassigned";
                for (int bindingIndex = 0; bindingIndex < group.fxTriggerMethods.Count; bindingIndex++)
                {
                    var binding = group.fxTriggerMethods[bindingIndex];
                    if (binding == null || string.IsNullOrWhiteSpace(binding.methodName))
                    {
                        continue;
                    }

                    string label = string.IsNullOrWhiteSpace(binding.Name) ? binding.methodName : binding.Name;
                    if (!string.IsNullOrWhiteSpace(binding.eventName))
                    {
                        label = $"{label} <- {binding.eventName}";
                    }

                    results.Add(new TriggerableBinding(groupIndex, bindingIndex, label, targetName));
                }
            }

            return results;
        }

        public bool InvokeBindingAt(int groupIndex, int bindingIndex)
        {
            if (groups == null || groupIndex < 0 || groupIndex >= groups.Count)
            {
                return false;
            }

            var group = groups[groupIndex];
            if (group == null || group.fxTriggerMethods == null || bindingIndex < 0 || bindingIndex >= group.fxTriggerMethods.Count)
            {
                return false;
            }

            var binding = group.fxTriggerMethods[bindingIndex];
            if (binding == null || string.IsNullOrWhiteSpace(binding.methodName))
            {
                return false;
            }

            InvokeBinding(group, binding);
            return true;
        }

        // 批量绑定
        private void BindAll()
        {
            SyncAllNames();
            foreach (var group in groups)
            {
                if (group == null)
                {
                    continue;
                }

                foreach (var binding in group.fxTriggerMethods)
                {
                    Bind(group, binding);
                }
            }
        }

        // 批量解绑
        private void UnbindAll()
        {
            foreach (var group in groups)
            {
                if (group == null)
                {
                    continue;
                }

                foreach (var binding in group.fxTriggerMethods)
                {
                    Unbind(binding);
                }
            }
        }

        // 绑定单个事件
        private void Bind(FXGroup group, FXBinding binding)
        {
            if (group == null || binding == null)
            {
                return;
            }

            if (binding.eventSource == null || string.IsNullOrWhiteSpace(binding.eventName))
            {
                return;
            }

            var source = ResolveEventComponent(binding);
            if (source == null)
            {
                return;
            }

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var sourceType = source.GetType();

            // 预先解析方法，便于在绑定阶段发现配置错误
            var methodTarget = ResolveMethodComponent(group, binding, out var method);
            if (methodTarget == null || method == null)
            {
                Debug.LogWarning($"FXPlayer: method '{binding.methodName}' not found on object.", this);
            }
            else
            {
                binding.methodComponent = methodTarget;
                binding.methodInfo = method;
            }

            // 优先绑定 C# event
            var eventInfo = sourceType.GetEvent(binding.eventName, flags);
            if (eventInfo != null)
            {
                var del = CreateDelegate(eventInfo.EventHandlerType, () => InvokeBinding(group, binding));
                if (del == null)
                {
                    Debug.LogWarning($"FXPlayer: event '{binding.eventName}' has unsupported signature on {sourceType.Name}.", this);
                    return;
                }

                eventInfo.AddEventHandler(source, del);
                binding.eventInfo = eventInfo;
                binding.handler = del;
                binding.eventComponent = source;
                return;
            }

            // 其次绑定 UnityEvent（字段或属性）
            if (TryGetUnityEvent(source, binding.eventName, flags, out var unityEvent))
            {
                if (TryAddUnityEventListener(unityEvent, () => InvokeBinding(group, binding), out var del))
                {
                    binding.unityEvent = unityEvent;
                    binding.handler = del;
                    binding.eventComponent = source;
                    return;
                }

                Debug.LogWarning($"FXPlayer: UnityEvent '{binding.eventName}' has unsupported signature on {sourceType.Name}.", this);
                return;
            }

            Debug.LogWarning($"FXPlayer: event '{binding.eventName}' not found on {sourceType.Name}.", this);
        }

        // 解绑单个事件
        private void Unbind(FXBinding binding)
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
        }

        // 触发调用
        private void InvokeBinding(FXGroup group, FXBinding binding)
        {
            if (group == null || binding == null)
            {
                return;
            }

            var target = ResolveMethodComponent(group, binding, out var method);
            if (target == null || method == null)
            {
                Debug.LogWarning($"FXPlayer: method '{binding.methodName}' not found on object.", this);
                return;
            }

            if (!TryBuildArguments(method, binding, out var args))
            {
                Debug.LogWarning($"FXPlayer: argument mismatch on '{method.Name}'.", this);
                return;
            }

            method.Invoke(target, args);
        }

        // 从绑定中解析事件组件：
        // 1) 若拖入的是组件，直接使用
        // 2) 若拖入的是GameObject，则在其所有组件中查找包含该事件名的组件
        private static Component ResolveEventComponent(FXBinding binding)
        {
            if (binding == null)
            {
                return null;
            }

            if (binding.eventComponent != null)
            {
                return binding.eventComponent;
            }

            if (binding.eventSource is Component component)
            {
                return component;
            }

            var go = binding.eventSource as GameObject;
            if (go == null || string.IsNullOrWhiteSpace(binding.eventName))
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
                bool hasEvent = type.GetEvent(binding.eventName, flags) != null;
                if (!hasEvent)
                {
                    if (TryGetUnityEvent(comp, binding.eventName, flags, out _))
                    {
                        hasEvent = true;
                    }
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
                Debug.LogWarning($"FXPlayer: multiple components on '{go.name}' contain event '{binding.eventName}', using the first match.");
            }

            return firstMatch;
        }

        // 从绑定中解析方法组件：
        // 1) 若已有缓存的组件且能找到方法，直接使用
        // 2) 若 Target 是组件，直接查找
        // 3) 若 Target 是 GameObject，在其所有组件中查找
        private static Component ResolveMethodComponent(FXGroup group, FXBinding binding, out MethodInfo method)
        {
            method = null;
            if (group == null || binding == null || group.@object == null || string.IsNullOrWhiteSpace(binding.methodName))
            {
                return null;
            }

            if (binding.methodComponent != null)
            {
                method = FindTriggerMethod(binding.methodComponent.GetType(), binding.methodName, binding.methodSignature);
                if (method != null)
                {
                    return binding.methodComponent;
                }
            }

            if (group.@object is Component component)
            {
                method = FindTriggerMethod(component.GetType(), binding.methodName, binding.methodSignature);
                if (method != null)
                {
                    binding.methodComponent = component;
                    return component;
                }
            }

            var go = group.@object as GameObject;
            if (go == null)
            {
                return null;
            }

            Component firstMatch = null;
            MethodInfo firstMethod = null;
            int matchCount = 0;

            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null)
                {
                    continue;
                }

                var candidate = FindTriggerMethod(comp.GetType(), binding.methodName, binding.methodSignature);
                if (candidate == null)
                {
                    continue;
                }

                matchCount++;
                if (firstMatch == null)
                {
                    firstMatch = comp;
                    firstMethod = candidate;
                }
            }

            if (matchCount > 1)
            {
                Debug.LogWarning($"FXPlayer: multiple components on '{go.name}' contain method '{binding.methodName}', using the first match.");
            }

            method = firstMethod;
            binding.methodComponent = firstMatch;
            return firstMatch;
        }

        // 构建参数并做类型检查
        private static bool TryBuildArguments(MethodInfo method, FXBinding binding, out object[] args)
        {
            args = null;
            if (method == null)
            {
                return false;
            }

            var parameters = method.GetParameters();
            if (parameters.Length == 0)
            {
                args = Array.Empty<object>();
                return true;
            }

            if (binding.arguments == null || binding.arguments.Count < parameters.Length)
            {
                return false;
            }

            args = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var paramType = parameters[i].ParameterType;
                var arg = binding.arguments[i];

                if (!TryConvertArgument(paramType, arg, out var value))
                {
                    return false;
                }

                args[i] = value;
            }

            return true;
        }

        private static bool TryConvertArgument(Type paramType, FXArgument arg, out object value)
        {
            value = null;

            if (paramType == typeof(int))
            {
                value = arg.intValue;
                return true;
            }

            if (paramType == typeof(float))
            {
                value = arg.floatValue;
                return true;
            }

            if (paramType == typeof(bool))
            {
                value = arg.boolValue;
                return true;
            }

            if (paramType == typeof(string))
            {
                value = arg.stringValue ?? string.Empty;
                return true;
            }

            if (paramType == typeof(Vector2))
            {
                value = arg.vector2Value;
                return true;
            }

            if (paramType == typeof(Vector3))
            {
                value = arg.vector3Value;
                return true;
            }

            if (paramType == typeof(Color))
            {
                value = arg.colorValue;
                return true;
            }

            if (paramType.IsEnum)
            {
                value = Enum.ToObject(paramType, arg.enumValue);
                return true;
            }

            if (typeof(UnityEngine.Object).IsAssignableFrom(paramType))
            {
                if (arg.objectValue == null)
                {
                    value = null;
                    return true;
                }

                if (!paramType.IsAssignableFrom(arg.objectValue.GetType()))
                {
                    return false;
                }

                value = arg.objectValue;
                return true;
            }

            return false;
        }

        // 生成方法签名（用于区分重载）
        public static string BuildMethodSignature(MethodInfo method)
        {
            if (method == null)
            {
                return string.Empty;
            }

            var parameters = method.GetParameters();
            if (parameters.Length == 0)
            {
                return method.Name + "()";
            }

            var parts = new string[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                parts[i] = parameters[i].ParameterType.FullName;
            }

            return method.Name + "(" + string.Join(",", parts) + ")";
        }

        // 查找带 [TriggerAction] 的方法
        public static MethodInfo FindTriggerMethod(Type type, string methodName, string methodSignature)
        {
            if (type == null || string.IsNullOrWhiteSpace(methodName))
            {
                return null;
            }

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (var method in type.GetMethods(flags))
            {
                if (method == null || method.IsGenericMethod || method.ReturnType != typeof(void))
                {
                    continue;
                }

                if (method.Name != methodName)
                {
                    continue;
                }

                if (!IsTriggerActionMethod(method))
                {
                    continue;
                }

                if (!IsSupportedParameters(method))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(methodSignature))
                {
                    if (BuildMethodSignature(method) != methodSignature)
                    {
                        continue;
                    }
                }

                return method;
            }

            return null;
        }

        public static bool IsTriggerActionMethod(MethodInfo method)
        {
            if (method == null)
            {
                return false;
            }

            return GetTriggerActionAttribute(method) != null;
        }

        public static bool IsTriggerActionVisible(MethodInfo method)
        {
            var attr = GetTriggerActionAttribute(method);
            return attr != null && !attr.Hidden;
        }

        private static TriggerActionAttribute GetTriggerActionAttribute(MethodInfo method)
        {
            if (method == null)
            {
                return null;
            }

            return method.GetCustomAttribute<TriggerActionAttribute>(true);
        }

        public static bool IsSupportedParameters(MethodInfo method)
        {
            if (method == null)
            {
                return false;
            }

            var parameters = method.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
            {
                if (!IsSupportedParameterType(parameters[i].ParameterType))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsSupportedParameterType(Type type)
        {
            if (type == typeof(int) ||
                type == typeof(float) ||
                type == typeof(bool) ||
                type == typeof(string) ||
                type == typeof(Vector2) ||
                type == typeof(Vector3) ||
                type == typeof(Color))
            {
                return true;
            }

            if (type != null && type.IsEnum)
            {
                return true;
            }

            if (type != null && typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                return true;
            }

            return false;
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

        private void SyncAllNames()
        {
            if (groups == null)
            {
                return;
            }

            foreach (var group in groups)
            {
                if (group == null || group.fxTriggerMethods == null)
                {
                    continue;
                }

                foreach (var binding in group.fxTriggerMethods)
                {
                    if (binding == null)
                    {
                        continue;
                    }

                    binding.SyncName();
                }
            }
        }
    }
}
