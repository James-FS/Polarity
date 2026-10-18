using UnityEngine;
using UnityEngine.UI;

namespace Polarity.Views
{
    /// <summary>Attach to a UGUI Button. All buttons use the shared ButtonAudioConfig asset.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Button))]
    public sealed class ButtonClickAudioView : MonoBehaviour
    {
        private Button button;

        private void OnEnable()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(OnClick);
        }

        private void OnClick()
        {
            // Earlier listeners can already have disabled the panel or begun a scene load.
            // The Button itself validated the click before dispatching its listeners.
            ButtonAudioPlayer.PlayClick();
        }

        private void OnDisable()
        {
            if (button != null) button.onClick.RemoveListener(OnClick);
        }

    }
}
