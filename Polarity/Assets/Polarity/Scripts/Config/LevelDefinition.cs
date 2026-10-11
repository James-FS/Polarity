using UnityEngine;
using Polarity.Entities;
namespace Polarity.Config
{
    [CreateAssetMenu(fileName = "Level", menuName = "Polarity/Level Definition")]
    public sealed class LevelDefinition : ScriptableObject
    {
        [SerializeField] private string title;
        [SerializeField, TextArea(2, 5)] private string teachingHint;
        [SerializeField, Min(0)] private int markBudget = 3;
        [SerializeField] private LevelRoom roomPrefab;
        [SerializeField] private GameplayConfig gameplayConfig;
        public string Title => title;
        public string TeachingHint => teachingHint;
        public int MarkBudget => Mathf.Max(0, markBudget);
        public LevelRoom RoomPrefab => roomPrefab;
        public GameplayConfig GameplayConfig => gameplayConfig;
        public bool IsValid => roomPrefab != null && gameplayConfig != null;
    }
}
