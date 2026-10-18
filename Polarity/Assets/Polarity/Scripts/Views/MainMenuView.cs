using Polarity.Controllers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Polarity.Views
{
    /// <summary>Standalone title screen. Gameplay is created only after Start is pressed.</summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuView : MonoBehaviour
    {
        [SerializeField] private string gameScenePath = "Assets/Polarity/Scenes/Game.scene";
        [SerializeField] private Button startButton, quitButton;
        [SerializeField, FormerlySerializedAs("controlsButton")] private Button settingsButton;
        [SerializeField, FormerlySerializedAs("closeControlsButton")] private Button closeSettingsButton;
        [SerializeField, FormerlySerializedAs("controlsPanel")] private GameObject settingsPanel;
        [Tooltip("Optional illustration shown on the main menu.")]
        [SerializeField] private Sprite illustration;
        [SerializeField] private Image illustrationImage;
        private bool loading;

        private void OnEnable()
        {
            startButton.onClick.AddListener(StartGame);
            settingsButton.onClick.AddListener(ShowSettings);
            quitButton.onClick.AddListener(QuitGame);
            closeSettingsButton.onClick.AddListener(HideSettings);
            settingsPanel.SetActive(false);
            ApplyIllustration();
        }

        private void Start()
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(startButton.gameObject);
        }

        private void Update()
        {
            if (settingsPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape)) HideSettings();
        }

        private void OnValidate() => ApplyIllustration();

        private void ApplyIllustration()
        {
            if (illustrationImage != null)
            {
                illustrationImage.sprite = illustration;
                illustrationImage.enabled = illustration != null;
            }
        }

        public void StartGame()
        {
            if (loading) return;
            if (!Application.CanStreamedLevelBeLoaded(gameScenePath))
            {
                Debug.LogError("Main menu requires the Game scene in Build Settings.", this);
                return;
            }
            loading = true;
            startButton.interactable = settingsButton.interactable = quitButton.interactable = false;
            LevelController.ResetForNewGame();
            SceneManager.LoadSceneAsync(gameScenePath, LoadSceneMode.Single);
        }

        public void ShowSettings()
        {
            if (loading) return;
            settingsPanel.SetActive(true);
            startButton.interactable = settingsButton.interactable = quitButton.interactable = false;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(closeSettingsButton.gameObject);
        }

        public void HideSettings()
        {
            settingsPanel.SetActive(false);
            if (!loading) startButton.interactable = settingsButton.interactable = quitButton.interactable = true;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(settingsButton.gameObject);
        }

        public void QuitGame()
        {
            if (loading) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnDisable()
        {
            startButton.onClick.RemoveListener(StartGame);
            settingsButton.onClick.RemoveListener(ShowSettings);
            quitButton.onClick.RemoveListener(QuitGame);
            closeSettingsButton.onClick.RemoveListener(HideSettings);
        }
    }
}
