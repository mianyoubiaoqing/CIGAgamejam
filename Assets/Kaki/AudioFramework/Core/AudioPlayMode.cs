namespace Kaki
{
    /// <summary>
    /// 音频播放类型
    /// </summary>
    public enum AudioPlayMode
    {
        /// <summary>
        /// 默认播放
        /// </summary>
        Default = 4,
        /// <summary>
        /// 叠加播放（可与其他音频叠加，不打断其他模式）
        /// </summary>
        Overlay = 1,
        /// <summary>
        /// 播放一次（Trigger同类型会互斥）
        /// </summary>
        Trigger = 0,
        /// <summary>
        /// 循环播放
        /// </summary>
        Loop = 2,
        /// <summary>
        /// 先播放Intro，再循环播放Loop部分
        /// </summary>
        LoopWithIntro = 3
    }
}