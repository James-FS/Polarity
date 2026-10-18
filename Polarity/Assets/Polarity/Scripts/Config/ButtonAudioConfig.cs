using UnityEngine;

namespace Polarity.Config
{
    /// <summary>One shared asset controls button feedback in every scene.</summary>
    public sealed class ButtonAudioConfig : ScriptableObject
    {
        public const string ResourcePath = "Audio/ButtonAudioConfig";

        [Header("所有按钮共用的点击音效")]
        [SerializeField] private AudioClip clickClip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.7f;

        public AudioClip ClickClip => clickClip;
        public float Volume => volume;
    }
}
