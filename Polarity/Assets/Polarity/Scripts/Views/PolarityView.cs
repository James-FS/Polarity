using Polarity.Entities;
using UnityEngine;
using Pole = Polarity.Model.Polarity;

namespace Polarity.Views
{
    /// <summary>Only colors explicitly assigned body faces and toggles the polarity symbol.</summary>
    [DisallowMultipleComponent]
    public sealed class PolarityView : MonoBehaviour
    {
        [SerializeField] private PolarityBody body;
        [SerializeField] private SpriteRenderer[] bodySprites = new SpriteRenderer[0];
        [SerializeField] private GameObject positiveSymbol;
        [SerializeField] private GameObject negativeSymbol;
        [SerializeField] private Color neutralColor = new Color(0.55f, 0.58f, 0.62f);
        [SerializeField] private Color positiveColor = new Color(1f, 0.45f, 0.12f);
        [SerializeField] private Color negativeColor = new Color(0.18f, 0.55f, 1f);

        private Color[] faceTints;
        private PolarityBody subscribedBody;

        private void OnEnable()
        {
            if (body == null)
                body = GetComponentInParent<PolarityBody>();
            if (body == null)
                return;
            // Preserve the whitebox's distinct top/front/side lighting and alpha.
            if (faceTints == null)
            {
                faceTints = new Color[bodySprites.Length];
                for (int i = 0; i < bodySprites.Length; i++)
                    faceTints[i] = bodySprites[i] != null ? bodySprites[i].color : Color.white;
            }
            subscribedBody = body;
            subscribedBody.PolarityChanged += Refresh;
            Refresh(subscribedBody.CurrentPolarity);
        }

        private void OnDisable()
        {
            if (subscribedBody != null)
                subscribedBody.PolarityChanged -= Refresh;
            subscribedBody = null;
        }

        private void Refresh(Pole polarity)
        {
            Color color = polarity == Pole.Positive ? positiveColor :
                polarity == Pole.Negative ? negativeColor : neutralColor;
            for (int i = 0; i < bodySprites.Length; i++)
            {
                if (bodySprites[i] == null)
                    continue;
                Color tint = faceTints[i];
                bodySprites[i].color = new Color(color.r * tint.r, color.g * tint.g, color.b * tint.b, tint.a);
            }
            if (positiveSymbol != null)
                positiveSymbol.SetActive(polarity == Pole.Positive);
            if (negativeSymbol != null)
                negativeSymbol.SetActive(polarity == Pole.Negative);
        }
    }
}
