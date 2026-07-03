using System;

namespace Kaki
{
    // TriggerActionAttribute：
    // 标记“允许被触发器系统触发”的方法。
    // 若 Hidden=true，则不会出现在 FXPlayer 的方法下拉菜单里，但仍可被反射调用。
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class TriggerActionAttribute : Attribute
    {
        public bool Hidden { get; }

        public TriggerActionAttribute()
        {
            Hidden = false;
        }

        public TriggerActionAttribute(bool hidden)
        {
            Hidden = hidden;
        }
    }
}
