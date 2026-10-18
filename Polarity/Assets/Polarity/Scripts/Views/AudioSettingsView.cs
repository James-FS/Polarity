using Polarity.Config;
using UnityEngine;
using UnityEngine.UI;

namespace Polarity.Views
{
    /// <summary>Menu controls for persistent master, button, and gameplay volume.</summary>
    public sealed class AudioSettingsView : MonoBehaviour
    {
        [SerializeField] private Slider masterSlider, buttonSlider, gameplaySlider;
        [SerializeField] private Text masterValue, buttonValue, gameplayValue;
        [SerializeField] private Button defaultsButton;

        private void OnEnable()
        {
            Refresh();
            masterSlider.onValueChanged.AddListener(AudioVolumeSettings.SetMaster);
            buttonSlider.onValueChanged.AddListener(AudioVolumeSettings.SetButton);
            gameplaySlider.onValueChanged.AddListener(AudioVolumeSettings.SetGameplay);
            defaultsButton.onClick.AddListener(AudioVolumeSettings.RestoreDefaults);
            AudioVolumeSettings.Changed += Refresh;
        }

        private void Refresh()
        {
            Set(masterSlider, masterValue, AudioVolumeSettings.Master);
            Set(buttonSlider, buttonValue, AudioVolumeSettings.Button);
            Set(gameplaySlider, gameplayValue, AudioVolumeSettings.Gameplay);
        }

        private static void Set(Slider slider, Text label, float value)
        {
            slider.SetValueWithoutNotify(value);
            label.text = Mathf.RoundToInt(value * 100f) + "%";
        }

        private void OnDisable()
        {
            masterSlider.onValueChanged.RemoveListener(AudioVolumeSettings.SetMaster);
            buttonSlider.onValueChanged.RemoveListener(AudioVolumeSettings.SetButton);
            gameplaySlider.onValueChanged.RemoveListener(AudioVolumeSettings.SetGameplay);
            defaultsButton.onClick.RemoveListener(AudioVolumeSettings.RestoreDefaults);
            AudioVolumeSettings.Changed -= Refresh;
            AudioVolumeSettings.Save();
        }

        private void OnApplicationPause(bool paused) { if (paused) AudioVolumeSettings.Save(); }
        private void OnApplicationQuit() => AudioVolumeSettings.Save();
    }
}
